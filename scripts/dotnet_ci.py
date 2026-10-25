#!/usr/bin/env python3
"""dotnet 工作流的测试分片、镜像预拉、结果汇总与触发过滤自检（S1-QA-018）。只依赖标准库。

.github/workflows/dotnet.yml 依次调用：

  python3 scripts/dotnet_ci.py shard <分片> --out <文件.slnf>
      写出分片的解决方案筛选文件，交给 dotnet test --solution。分片清单见 SHARDS；
      其余分片是 Qian.slnx 里没登记到别的分片的全部项目（含非测试项目，由 dotnet test 自己识别测试模块），
      新加的测试项目不用登记也不会漏跑。
  python3 scripts/dotnet_ci.py images [<分片>]
      列出分片里的测试项目会拉的容器镜像，不给分片则列出全部。镜像名取 tests/Qian.Testing.Containers 里
      QIAN_TEST_IMAGE_* 变量的缺省值；环境变量已覆盖的取覆盖值，与测试实际拉的一致。
  python3 scripts/dotnet_ci.py prefetch [<分片>] --state <目录>
      顺序 docker pull 上面列出的镜像，逐个记录结果与耗时，结束时写 done 标记。工作流在后台运行它；
      拉取失败只记录，Testcontainers 启动容器前会自己补拉。
  python3 scripts/dotnet_ci.py wait-prefetch --state <目录> [--timeout 秒]
      有界等待后台预拉结束并打印记录；超时或预拉进程已退出都照常返回 0。
  python3 scripts/dotnet_ci.py verify <结果目录>
      汇总各分片上传的 TRX（每个分片一个子目录 test-results-<分片>）：Qian.slnx 里每个测试项目恰好在所属分片跑过一次、
      没有失败，少一个模块即不通过；分片、模块、用例数与耗时写进作业摘要。跳过的用例只告警；tests/ 下引用 xunit.v3
      却不在 Qian.slnx 里的测试项目（哪个分片都不跑）也告警，门禁样例 tests/gates 与专属作业运行的项目除外。
  python3 scripts/dotnet_ci.py paths-check
      dotnet.yml 的 pull_request 路径过滤不看 pm/、docs/ 与根目录 Markdown。工作流读到的这些文件必须被过滤重新纳入，
      否则只改它们的 PR 会漏跑读取它们的测试。核对 .NET 源码的字符串、MSBuild 项的路径与工作流运行的 Python
      写成的仓库路径，识别的写法见 tests/README.md §5.1。

退出码：0 通过；1 核对不通过；2 工具出错。
"""
from __future__ import annotations

import argparse
import ast
import bisect
import fnmatch
import json
import os
import posixpath
import re
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from collections import Counter
from datetime import datetime
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Set, Tuple

ROOT = Path(__file__).resolve().parents[1]
SOLUTION = ROOT / "Qian.slnx"
WORKFLOW = ROOT / ".github" / "workflows" / "dotnet.yml"
CONTAINERS = ROOT / "tests" / "Qian.Testing.Containers"

# 按基线运行 40034799307 的模块壁钟配平。每个分片同时跑 4 个模块，模块启动顺序由 dotnet test 决定、每次不同，
# 所以重模块（Config、Scheduler、Sample.Crm、Identity.Server，各 250–400 秒）所在分片的轻模块越少，重模块越早开跑。
# 值为 None 的分片收下其余全部项目。改分片只改这里；工作流的矩阵只列分片名。
SHARDS: Dict[str, Optional[List[str]]] = {
    # 真实 Apollo 与调度两个重模块都以等待为主，另配一批不起容器或只起 PostgreSQL 的轻模块。
    "a": [
        "Qian.Config.Tests", "Qian.Scheduler.Tests", "Qian.Lifecycle.Tests", "Qian.Plugin.Sdk.Tests",
        "Qian.Profiles.Tests", "Qian.Gateway.Tests", "Qian.Common.Tests", "Qian.Docs.Tests", "Qian.Runtime.Tests",
        "Qian.Capabilities.Tests", "Qian.Tenancy.Tests", "Qian.Runtime.Errors.Tests", "Qian.Surfaces.Tests",
        "Qian.Config.Secrets.Tests",
    ],
    # 恰好 4 个模块，全部一开始就跑。Sample.Crm 与 Registry 的用例调真实 qian CLI，工作流只在这个分片构建它。
    "b": ["Qian.Sample.Crm.Tests", "Qian.Registry.Tests", "Qian.Events.Tests", "Qian.Files.Tests"],
    # 身份、鉴权、审计、数据、容器夹具自测，以及今后新增的测试项目。
    "c": None,
}

TRX = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
FAILED = {"Failed", "Error", "Timeout", "Aborted"}
IMAGE_DEFAULT = re.compile(r'"(QIAN_TEST_IMAGE_[A-Z0-9_]+)"\s*,\s*"([^"]+)"')
IMAGE_VARIABLE = re.compile(r'"(QIAN_TEST_IMAGE_[A-Z0-9_]+)"')
IMAGE_PROPERTY = re.compile(r'\bstatic\s+string\s+(\w+)\s*=>\s*\w+\(\s*"(QIAN_TEST_IMAGE_[A-Z0-9_]+)"')
IMAGE_USE = re.compile(r"\bContainerImages\.(\w+)")
TOP_LEVEL_TYPE = re.compile(r"^(?:(?:public|internal|sealed|abstract|static|partial)\s+)*(?:class|record|struct)\s+([A-Z]\w*)", re.MULTILINE)
XUNIT = re.compile(r'<PackageReference\b[^>]*\bInclude="xunit\.v3"')
TEST_PROJECT = re.compile(r"<IsTestProject>\s*true\s*</IsTestProject>", re.IGNORECASE)
DEDICATED_TEST = re.compile(r"\bdotnet\s+test\b[^\n]*?\s--project\s+([^\s\"']+)")
# 触发过滤自检
CSHARP_STRING = re.compile(r'(\$+@|@\$+|\$+|@)?("{3,}|")')
PATH_CALL = re.compile(r"\bPath\s*\.\s*(?:Combine|Join)\s*\(")
ROOT_LOCATOR = re.compile(r'"Qian\.slnx"|Repo(?:sitory)?Root')
ESCAPES = {"n": "\n", "r": "\r", "t": "\t", "0": "\0", "a": "\a", "b": "\b", "f": "\f", "v": "\v"}
MSBUILD_PATH = re.compile(r'\b(?:Include|Update|Project)\s*=\s*"([^"]*)"')
PYTHON_SCRIPT = re.compile(r"\bpython3?\s+(?:-[A-Za-z]\S*\s+)*([\w./-]+\.py)\b")
PYTHON_UNITTEST = re.compile(r"\bpython3?\s+-m\s+unittest\s+discover\b([^\n]*)")
PATH_TYPES = {"Path", "PurePath", "PosixPath", "PurePosixPath"}


class CheckFailed(Exception):
    """核对不通过（退出码 1）。"""


class ToolError(Exception):
    """输入或环境不对，工具无法给出结论（退出码 2）。"""


# ---------- 解决方案与分片 ----------

def solution_projects() -> List[str]:
    """Qian.slnx 里全部项目的相对路径，保持文件里的顺序。"""
    projects = [node.attrib["Path"] for node in ET.parse(SOLUTION).getroot().iter("Project")]
    if not projects:
        raise ToolError("Qian.slnx 里没有项目。")
    return projects


def assembly_name(project: str) -> str:
    return ET.parse(ROOT / project).getroot().findtext(".//AssemblyName") or Path(project).stem


def is_test_project(project: str) -> bool:
    """本仓库的测试项目都引用 xunit.v3。"""
    text = (ROOT / project).read_text(encoding="utf-8")
    return bool(XUNIT.search(text) or TEST_PROJECT.search(text))


def test_projects() -> Dict[str, str]:
    """Qian.slnx 里的测试项目：程序集名 → 项目路径。"""
    found = {assembly_name(project): project for project in solution_projects() if is_test_project(project)}
    if not found:
        raise ToolError("Qian.slnx 里没有识别出测试项目。")
    return found


def unlisted_test_projects() -> List[str]:
    """tests/ 下不在 Qian.slnx 里的测试项目：哪个分片都不跑，verify 也核对不到。门禁样例（tests/gates）与
    dotnet.yml 里专属作业用 dotnet test --project 运行的项目（目前是三个 *.AotTests）除外。"""
    listed = set(solution_projects())
    dedicated = {path.rstrip("/") for path in DEDICATED_TEST.findall(WORKFLOW.read_text(encoding="utf-8"))}
    return sorted(Path(project).stem for project in tracked("tests/*.csproj")
                  if project not in listed and not project.startswith("tests/gates/")
                  and not {project, posixpath.dirname(project)} & dedicated and is_test_project(project))


def assignment() -> Dict[str, str]:
    """项目路径 → 分片。登记的名字必须是 Qian.slnx 里的项目，且只能登记一次。"""
    projects = solution_projects()
    by_name = {Path(project).stem: project for project in projects}
    rest = [shard for shard, names in SHARDS.items() if names is None]
    if len(rest) != 1:
        raise ToolError("SHARDS 必须恰好有一个收下其余项目的分片（值为 None）。")
    result: Dict[str, str] = {}
    for shard, names in SHARDS.items():
        for name in names or []:
            if name not in by_name:
                raise ToolError(f"分片 {shard} 登记的 {name} 不在 Qian.slnx 里。")
            if by_name[name] in result:
                raise ToolError(f"{name} 同时登记在分片 {result[by_name[name]]} 与 {shard}。")
            result[by_name[name]] = shard
    for project in projects:
        result.setdefault(project, rest[0])
    return result


def shard_projects(shard: str) -> List[str]:
    if shard not in SHARDS:
        raise ToolError(f"没有分片 {shard}，可选：{'、'.join(SHARDS)}。")
    owners = assignment()
    return [project for project in solution_projects() if owners[project] == shard]


def write_filter(shard: str, out: Path) -> None:
    projects = shard_projects(shard)
    tests = set(test_projects().values())
    out.parent.mkdir(parents=True, exist_ok=True)
    document = {"solution": {"path": str(SOLUTION), "projects": projects}}
    out.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    names = [Path(project).stem for project in projects if project in tests]
    print(f"分片 {shard}：{len(names)} 个测试项目（筛选文件共 {len(projects)} 个项目）→ {out}")
    for name in names:
        print("  " + name)


# ---------- 测试镜像 ----------

def sources(directory: Path) -> List[Path]:
    return [path for path in sorted(directory.rglob("*.cs"))
            if not {"bin", "obj"} & set(path.relative_to(directory).parts)]


class Fixtures:
    """tests/Qian.Testing.Containers 的镜像声明与夹具类型，以及夹具之间的引用（如 InfrastructureFixture 组合四个夹具）。"""

    def __init__(self) -> None:
        self.defaults: Dict[str, str] = {}
        self.properties: Dict[str, str] = {}
        texts: Dict[str, str] = {}
        for path in sources(CONTAINERS):
            text = path.read_text(encoding="utf-8")
            for variable, image in IMAGE_DEFAULT.findall(text):
                self.defaults.setdefault(variable, image)
            self.properties.update(IMAGE_PROPERTY.findall(text))
            # 只认顶层类型（文件范围命名空间里顶格声明）：夹具里的嵌套类型（如 User）名字太泛，不单独算。
            # ContainerImages 的属性逐个按 ContainerImages.X 解析，不能整类算作「用了全部镜像」。
            for name in TOP_LEVEL_TYPE.findall(text):
                if name != "ContainerImages":
                    texts[name] = texts.get(name, "") + text
        if not self.defaults:
            raise ToolError(f"{CONTAINERS.relative_to(ROOT)} 里没有解析出 QIAN_TEST_IMAGE_* 缺省镜像。")
        self.direct = {name: self.variables(text) for name, text in texts.items()}
        self.references = {name: {other for other in texts if other != name and mentions(text, other)}
                           for name, text in texts.items()}

    def variables(self, text: str) -> Set[str]:
        found = set(IMAGE_VARIABLE.findall(text))
        found.update(self.properties[name] for name in IMAGE_USE.findall(text) if name in self.properties)
        return found

    def used_by(self, text: str) -> Set[str]:
        """一段源码会拉的镜像变量：直接写到的，加上引用的夹具（连同夹具再引用的夹具）的。"""
        found = self.variables(text)
        pending = [name for name in self.direct if mentions(text, name)]
        seen: Set[str] = set()
        while pending:
            name = pending.pop()
            if name not in seen:
                seen.add(name)
                found |= self.direct[name]
                pending.extend(self.references[name])
        return found

    def image(self, variable: str) -> str:
        override = os.environ.get(variable, "").strip()
        return override or self.defaults[variable]


def mentions(text: str, name: str) -> bool:
    return re.search(r"\b" + re.escape(name) + r"\b", text) is not None


def project_sources(project: str, seen: Optional[Set[Path]] = None) -> List[Path]:
    """项目目录下的源码、Compile Include 链进来的共享源码，以及 tests/ 下被引用的测试宿主项目的源码。"""
    path = (ROOT / project).resolve()
    seen = seen if seen is not None else set()
    if path in seen:
        return []
    seen.add(path)
    files = sources(path.parent)
    document = ET.parse(path).getroot()
    for item in document.iter("Compile"):
        include = (item.get("Include") or "").replace("\\", "/")
        if include:
            files.extend(sorted(match.resolve() for match in path.parent.joinpath(include).parent.glob(Path(include).name)))
    tests = ROOT / "tests"
    for item in document.iter("ProjectReference"):
        reference = path.parent.joinpath((item.get("Include") or "").replace("\\", "/")).resolve()
        if tests in reference.parents and CONTAINERS not in reference.parents:
            files.extend(project_sources(str(reference.relative_to(ROOT)), seen))
    return files


def shard_images(shard: Optional[str]) -> List[str]:
    fixtures = Fixtures()
    if shard is None:
        variables = set(fixtures.defaults)
    else:
        projects = set(shard_projects(shard)) & set(test_projects().values())
        variables = set()
        for project in sorted(projects):
            text = "\n".join(path.read_text(encoding="utf-8") for path in project_sources(project))
            variables |= fixtures.used_by(text)
    images: List[str] = []
    for variable in fixtures.defaults:  # 按声明顺序，结果稳定
        if variable in variables and fixtures.image(variable) not in images:
            images.append(fixtures.image(variable))
    return images


def prefetch(shard: Optional[str], state: Path) -> None:
    state.mkdir(parents=True, exist_ok=True)
    (state / "pid").write_text(str(os.getpid()), encoding="utf-8")
    try:
        with (state / "pulls.log").open("a", encoding="utf-8") as log:
            def note(line: str) -> None:
                log.write(line + "\n")
                log.flush()
            scope = f"分片 {shard}" if shard else "全部测试"
            try:
                images = shard_images(shard)
            except (ToolError, OSError, ET.ParseError) as error:
                note(f"无法列出{scope}的镜像：{error}")
                return
            note(f"{scope}顺序预拉 {len(images)} 个镜像：{' '.join(images)}")
            for image in images:
                began = time.monotonic()
                result = subprocess.run(["docker", "pull", "--quiet", image], capture_output=True, text=True)
                elapsed = time.monotonic() - began
                if result.returncode == 0:
                    note(f"已拉取 {image}（{elapsed:.0f} 秒）")
                else:
                    reason = (result.stderr.strip().splitlines() or ["没有输出"])[-1]
                    note(f"拉取失败 {image}（{elapsed:.0f} 秒）：{reason}")
    finally:
        (state / "done").write_text("", encoding="utf-8")


def alive(pid: int) -> bool:
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    return True


def wait_prefetch(state: Path, timeout: float) -> None:
    began = time.monotonic()
    pid_file = state / "pid"
    while not (state / "done").exists():
        pid = pid_file.read_text(encoding="utf-8").strip() if pid_file.exists() else ""
        if not pid and time.monotonic() - began > 10:
            print("::warning::没有找到后台预拉进程，测试照常开始，镜像由 Testcontainers 拉取。")
            break
        if pid.isdigit() and not alive(int(pid)) and not (state / "done").exists():
            print("::warning::后台预拉进程已退出但没有写完成标记，测试照常开始。")
            break
        if time.monotonic() - began >= timeout:
            print(f"::warning::镜像预拉 {timeout:.0f} 秒内没有结束，测试照常开始，没拉完的由 Testcontainers 补拉。")
            break
        time.sleep(2)
    print(f"等待 {time.monotonic() - began:.0f} 秒。")
    log = state / "pulls.log"
    if log.exists():
        print(log.read_text(encoding="utf-8"), end="")


# ---------- TRX 汇总 ----------

def timestamp(value: str) -> datetime:
    # Python 3.9 的 fromisoformat 只认 6 位小数与 +00:00 写法。
    value = re.sub(r"(\.\d{6})\d+", r"\1", value.replace("Z", "+00:00"))
    return datetime.fromisoformat(value)


def read_trx(path: Path) -> Tuple[str, Counter, datetime, datetime]:
    root = ET.parse(path).getroot()
    storages = {Path(test.get("storage") or "").name for test in root.iter(TRX + "UnitTest")}
    if len(storages) != 1:
        raise CheckFailed(f"{path.name}：期望恰好一个测试模块，实际 {sorted(storages) or '没有用例'}。")
    module = re.sub(r"\.(dll|exe)$", "", storages.pop(), flags=re.IGNORECASE)
    outcomes = Counter(result.get("outcome") or "?" for result in root.iter(TRX + "UnitTestResult"))
    times = root.find(TRX + "Times")
    if times is None:
        raise CheckFailed(f"{path.name}：缺少 Times。")
    return module, outcomes, timestamp(times.get("start") or ""), timestamp(times.get("finish") or "")


def verify(results: Path) -> Tuple[List[str], List[str], List[str]]:
    """返回作业摘要的 Markdown 行、发现的问题与告警；没有问题才算通过。"""
    expected = test_projects()
    owners = assignment()
    problems: List[str] = []
    warnings: List[str] = []
    seen: Dict[str, str] = {}
    rows: Dict[str, List[Tuple[str, Counter, float]]] = {}
    spans: Dict[str, Tuple[datetime, datetime]] = {}
    for shard in SHARDS:
        directory = results / f"test-results-{shard}"
        if not directory.is_dir():
            problems.append(f"分片 {shard} 没有上传测试结果（{directory.name}）。")
            continue
        rows[shard] = []
        for path in sorted(directory.rglob("*.trx")):
            try:
                module, outcomes, start, finish = read_trx(path)
            except (CheckFailed, ET.ParseError, ValueError) as error:
                problems.append(f"分片 {shard}：{error}")
                continue
            rows[shard].append((module, outcomes, (finish - start).total_seconds()))
            first, last = spans.get(shard, (start, finish))
            spans[shard] = (min(first, start), max(last, finish))
            if module in seen:
                problems.append(f"{module} 跑了不止一次（分片 {seen[module]} 与 {shard}）。")
            seen.setdefault(module, shard)
            if module not in expected:
                problems.append(f"分片 {shard} 跑了 {module}，但它不是本脚本认出的测试项目（核对 test_projects）。")
            elif owners[expected[module]] != shard:
                problems.append(f"{module} 应在分片 {owners[expected[module]]}，实际在分片 {shard}。")
            failed = sum(count for outcome, count in outcomes.items() if outcome in FAILED)
            if failed:
                problems.append(f"{module}：{failed} 条失败。")
            skipped = sum(count for outcome, count in outcomes.items() if outcome not in FAILED and outcome != "Passed")
            if skipped:
                warnings.append(f"{module}：{skipped} 条未执行或跳过。")
    for module in sorted(set(expected) - set(seen)):
        if owners[expected[module]] in rows:
            problems.append(f"{module}（分片 {owners[expected[module]]}）没有测试结果。")
    unlisted = unlisted_test_projects()
    if unlisted:
        warnings.append(f"tests/ 下有 {len(unlisted)} 个测试项目不在 Qian.slnx 里，哪个分片都不跑、也不在本核对范围内：{'、'.join(unlisted)}。"
                        "加进 Qian.slnx，或在 dotnet.yml 里由专属作业 dotnet test --project 运行。")

    summary = ["## 测试分片汇总", "",
               "| 分片 | 模块 | 用例 | 通过 | 未通过 | 跳过 | 模块累计秒 | 首尾跨度秒 |", "|---|---|---|---|---|---|---|---|"]
    grand: Counter = Counter()
    for shard, entries in rows.items():
        totals: Counter = Counter()
        for _, outcomes, _ in entries:
            totals.update(outcomes)
        grand.update(totals)
        failed = sum(count for outcome, count in totals.items() if outcome in FAILED)
        passed = totals.get("Passed", 0)
        span = (spans[shard][1] - spans[shard][0]).total_seconds() if shard in spans else 0.0
        summary.append(f"| {shard} | {len(entries)} | {sum(totals.values())} | {passed} | {failed} | "
                       f"{sum(totals.values()) - passed - failed} | {sum(seconds for _, _, seconds in entries):.0f} | {span:.0f} |")
    summary += ["", f"合计 {sum(len(entries) for entries in rows.values())} 个模块、{sum(grand.values())} 条用例；"
                    f"Qian.slnx 里识别出 {len(expected)} 个测试项目。", ""]
    if unlisted:
        summary += [f"tests/ 下不在 Qian.slnx 里、没有任何分片运行的测试项目：{'、'.join(unlisted)}。", ""]
    summary += ["<details><summary>各模块</summary>", "", "| 分片 | 模块 | 用例 | 秒 |", "|---|---|---|---|"]
    for shard, entries in rows.items():
        for module, outcomes, seconds in sorted(entries, key=lambda entry: -entry[2]):
            summary.append(f"| {shard} | {module} | {sum(outcomes.values())} | {seconds:.0f} |")
    summary += ["", "</details>"]
    return summary, problems, warnings


# ---------- 触发过滤自检 ----------

def pull_request_paths() -> List[str]:
    """读 dotnet.yml 里 on.pull_request.paths 的块状列表（本工作流固定这种写法）。"""
    patterns: List[str] = []
    keys: List[Tuple[int, str]] = []  # 当前行所在的键路径与各键的缩进
    for line in WORKFLOW.read_text(encoding="utf-8").splitlines():
        content = line.split(" #", 1)[0].rstrip()
        item = content.lstrip()
        if not item or item.startswith("#"):
            continue
        if item.startswith("- "):
            if [key for _, key in keys] == ["on", "pull_request", "paths"]:
                patterns.append(item[2:].strip().strip("'\""))
            continue
        indent = len(content) - len(item)
        while keys and keys[-1][0] >= indent:
            keys.pop()
        if item.endswith(":"):
            keys.append((indent, item[:-1].strip().strip("'\"")))
    if not patterns or patterns[0] != "**":
        raise ToolError("没能从 dotnet.yml 读出以 '**' 开头的 pull_request.paths 块状列表。")
    return patterns


def glob_regex(pattern: str) -> re.Pattern:
    """GitHub Actions 路径过滤的通配：** 跨目录，* 不跨目录。"""
    parts = []
    index = 0
    while index < len(pattern):
        if pattern.startswith("**/", index):
            parts.append("(?:.*/)?")
            index += 3
        elif pattern.startswith("**", index):
            parts.append(".*")
            index += 2
        elif pattern[index] == "*":
            parts.append("[^/]*")
            index += 1
        else:
            parts.append(re.escape(pattern[index]))
            index += 1
    return re.compile("".join(parts) + r"\Z")


def triggers(patterns: List[str], path: str) -> bool:
    """最后一个匹配的模式说了算：! 开头排除，否则纳入。"""
    result = False
    for pattern in patterns:
        negative = pattern.startswith("!")
        if glob_regex(pattern.lstrip("!")).match(path):
            result = not negative
    return result


def tracked(*patterns: str) -> List[str]:
    output = subprocess.run(["git", "ls-files", "-z", "--", *patterns], cwd=ROOT, capture_output=True, text=True)
    if output.returncode:
        raise ToolError("git ls-files 失败：" + output.stderr.strip())
    return [name for name in output.stdout.split("\0") if name]


class Repository:
    """git 跟踪的文件，按路径或前缀查。"""

    def __init__(self, files: Iterable[str]) -> None:
        self.files = sorted(files)
        self.names = set(self.files)
        self.top = {name for name in self.files if "/" not in name}

    def covered(self, path: str, complete: bool = True) -> List[str]:
        """完整路径对得上文件本身或目录下的全部文件；不完整的路径（前缀）对得上以它起头的全部文件。"""
        if not path:
            return []
        if complete and path in self.names:
            return [path]
        prefix = path + "/" if complete else path
        start = end = bisect.bisect_left(self.files, prefix)
        while end < len(self.files) and self.files[end].startswith(prefix):
            end += 1
        return self.files[start:end]

    def matching(self, pattern: str) -> List[str]:
        """MSBuild 通配：** 跨目录（可以是零层），* 与 ? 不跨目录。"""
        regex = re.compile(re.escape(pattern).replace(r"\*\*/", "(?:.*/)?").replace(r"\*\*", ".*")
                           .replace(r"\*", "[^/]*").replace(r"\?", "[^/]") + r"\Z")
        return [name for name in self.files if regex.match(name)]


def repository_path(raw: str, complete: bool) -> Optional[Tuple[str, bool]]:
    """把字符串当相对路径读：反斜杠也是分隔符，消去 . 与 ..，返回 (路径, 是否往上爬出了起点)。
    绝对路径、盘符与 URL 返回 None。不完整的路径（前缀）最后一段原样保留，为空即以 / 结尾的目录前缀。"""
    path = raw.replace("\\", "/")
    if not path or path.startswith("/") or ":" in path.split("/", 1)[0]:
        return None
    *segments, last = path.split("/")
    if complete:
        segments.append(last)
    stack: List[str] = []
    climbed = False
    for segment in segments:
        if segment == "..":
            climbed |= not stack
            stack = stack[:-1]
        elif segment not in ("", "."):
            stack.append(segment)
    if not complete:
        stack.append(last)
    return "/".join(stack), climbed


# C# 源码：先词法切出字符串字面量（跳过注释与字符字面量），再按调用与运算符的上下文读成路径。

def skip_char(text: str, index: int) -> int:
    """跳过从 index 起的字符字面量（'a'、'\\''、'\\u0041'）。"""
    end = text.find("'", index + (3 if text.startswith("\\", index + 1) else 2))
    return index + 1 if end < 0 or "\n" in text[index:end] else end + 1


def skip_hole(text: str, index: int, depth: int) -> int:
    """跳过插值洞里的表达式（其中可以嵌套字符串），返回洞的右括号之后。"""
    while index < len(text):
        char = text[index]
        match = CSHARP_STRING.match(text, index) if char in '"@$' else None
        if char == "'":
            index = skip_char(text, index)
            continue
        if match:
            index = read_string(text, match)[0]
            continue
        if char in "({[":
            depth += 1
        elif char in ")}]":
            depth -= 1
            if depth == 0:
                return index + 1
        index += 1
    return len(text)


def read_string(text: str, match: "re.Match[str]") -> Tuple[int, List[str]]:
    """读一个字符串字面量，返回 (结束偏移, 常量段)：插值字符串按洞切段，其余只有一段。"""
    prefix, quotes = match.group(1) or "", match.group(2)
    dollars, verbatim, index = prefix.count("$"), "@" in prefix, match.end()
    segments = [""]
    if len(quotes) >= 3:  # 原始字面量："""…"""，插值时以与 $ 个数相同的 { 开洞
        while index < len(text):
            if text.startswith(quotes, index):
                return index + len(quotes), segments
            if dollars and text.startswith("{" * dollars, index):
                run = dollars
                while text.startswith("{", index + run):
                    run += 1
                segments[-1] += "{" * (run - dollars)  # 多出的 { 是内容
                index = skip_hole(text, index + run, dollars)
                segments.append("")
                continue
            segments[-1] += text[index]
            index += 1
        return len(text), segments
    while index < len(text):
        char = text[index]
        if char == '"':
            if verbatim and text.startswith('"', index + 1):
                segments[-1] += '"'
                index += 2
                continue
            return index + 1, segments
        if char == "\\" and not verbatim:
            escaped = text[index + 1:index + 2]
            segments[-1] += ESCAPES.get(escaped, escaped)
            index += 2
            continue
        if dollars and char in "{}":
            if text.startswith(char * 2, index):
                segments[-1] += char
                index += 2
                continue
            if char == "{":
                index = skip_hole(text, index + 1, 1)
                segments.append("")
                continue
        if char == "\n" and not verbatim:
            return index, segments  # 普通字面量不跨行，没闭合就到行尾为止
        segments[-1] += char
        index += 1
    return len(text), segments


def csharp_strings(text: str) -> Tuple[List[Tuple[int, int, List[str]]], str]:
    """C# 源码里的字符串字面量 (起始偏移, 结束偏移, 常量段)，以及把字面量填成 S、注释填成空格的源码（偏移与换行不变）。"""
    found: List[Tuple[int, int, List[str]]] = []
    masked = list(text)

    def blank(start: int, end: int, fill: str) -> None:
        for position in range(start, end):
            if masked[position] != "\n":
                masked[position] = fill

    index = 0
    while index < len(text):
        char = text[index]
        if text.startswith("//", index):
            end = text.find("\n", index)
            end = len(text) if end < 0 else end
            blank(index, end, " ")
            index = end
        elif text.startswith("/*", index):
            end = text.find("*/", index + 2)
            end = len(text) if end < 0 else end + 2
            blank(index, end, " ")
            index = end
        elif char == "'":
            index = skip_char(text, index)
        elif char in '"@$' and CSHARP_STRING.match(text, index):
            end, segments = read_string(text, CSHARP_STRING.match(text, index))
            found.append((index, end, segments))
            blank(index, end, "S")
            index = end
        else:
            index += 1
    return found, "".join(masked)


def neighbour(masked: str, index: int, step: int) -> str:
    """从 index 起朝 step 方向第一个非空白字符（没有则为空串）。"""
    while 0 <= index < len(masked) and masked[index] in " \t\r\n":
        index += step
    return masked[index] if 0 <= index < len(masked) else ""


def arguments(masked: str, index: int) -> List[Tuple[int, int]]:
    """从左括号之后起，调用的各个顶层实参 (起, 止)，去掉首尾空白。"""
    spans: List[Tuple[int, int]] = []
    depth, start = 0, index
    while index < len(masked):
        char = masked[index]
        if char in "([{":
            depth += 1
        elif char in ")]}" and depth:
            depth -= 1
        elif char == ")" or (char == "," and not depth):
            left = start + len(masked[start:index]) - len(masked[start:index].lstrip())
            right = start + len(masked[start:index].rstrip())
            if right > left:
                spans.append((left, right))
            if char == ")":
                break
            start = index + 1
        index += 1
    return spans


def csharp_references(text: str, top: Set[str]) -> List[Tuple[int, str, bool]]:
    """一个 C# 源文件里写成仓库路径的字符串：(行号, 仓库相对路径, 是否完整)，不完整的是路径前缀。top 是仓库根目录下的文件名。

    - 含 / 或 \\ 的字面量，开头的 ./、../ 去掉（相对某个子目录往上爬回仓库根），如 "docs/x.md"、"../docs/x.md"；
    - Path.Combine / Path.Join 里从第一个实参或紧跟第一个实参起的连续字面量段，拼成一条路径（如 root, "docs", "x.md"）；
      后面还有非字面量实参时只得到前缀，更靠后的字面量段相对未知的子目录，不算；
    - 插值字符串的常量前缀（$"docs/{x}.md"），以及插值洞后以 / 起头的段（$"{root}/docs/x.md"）；字面量后紧跟 + 时也只是前缀，
      前面紧跟 + 时去掉开头的 /（root + "/docs/x.md"）；
    - 单写一个根目录文件名（"AGENTS.md"、Path.Combine(root, "README.md")）只在本文件定位了仓库根（写到 "Qian.slnx"，
      或用到 RepositoryRoot / RepoRoot）时算：README.md 这类常见文件名在别处多半指临时目录或包里的文件。"""
    literals, masked = csharp_strings(text)
    anchored = ROOT_LOCATOR.search(text) is not None
    newlines = [position for position, char in enumerate(text) if char == "\n"]
    found: List[Tuple[int, str, bool]] = []

    def add(offset: int, raw: str, complete: bool, chain: bool) -> None:
        resolved = repository_path(raw, complete)
        if resolved is None or not resolved[0]:
            return
        path, _ = resolved
        if "/" not in raw and "\\" not in raw and (path in top and not anchored or path not in top and not chain):
            return  # 单写一个名字：根目录文件要本文件定位了仓库根；其他名字只在 Path.Combine 的路径段里算
        found.append((bisect.bisect_left(newlines, offset) + 1, path, complete))

    by_span = {(start, end): position for position, (start, end, _) in enumerate(literals)}
    consumed: Set[int] = set()
    for call in PATH_CALL.finditer(masked):
        positions = [by_span.get(span) for span in arguments(masked, call.end())]
        consumed.update(position for position in positions if position is not None)
        first = 0 if positions and positions[0] is not None else 1
        run: List[int] = []
        for position in positions[first:]:
            if position is None:
                break
            run.append(position)
        if not run:
            continue
        values, complete = [], True
        for position in run:
            segments = literals[position][2]
            values.append(segments[0])
            if len(segments) > 1:
                complete = False
                break
        raw = "/".join(values)
        if complete and first + len(run) < len(positions):
            raw, complete = raw + "/", False
        add(literals[run[0]][0], raw, complete, True)

    for position, (start, end, segments) in enumerate(literals):
        if position in consumed:
            continue
        before, after = neighbour(masked, start - 1, -1), neighbour(masked, end, 1)
        head = segments[0][1:] if before == "+" and segments[0][:1] in ("/", "\\") else segments[0]
        add(start, head, len(segments) == 1 and after != "+", False)
        for index, segment in enumerate(segments[1:], 1):
            if segment[:1] in ("/", "\\"):
                add(start, segment[1:], index == len(segments) - 1 and after != "+", False)
    return found


def msbuild_references(source: str, text: str) -> List[Tuple[int, str]]:
    """MSBuild 文件里 Include、Update、Project 属性指向的仓库路径（可以带通配）：(行号, 路径)。
    $(MSBuildThisFileDirectory) 与 $(MSBuildProjectDirectory) 取本文件所在目录，含其他属性、项或元数据引用的跳过。"""
    directory = posixpath.dirname(source)
    found: List[Tuple[int, str]] = []
    for match in MSBUILD_PATH.finditer(text):
        line = text.count("\n", 0, match.start()) + 1
        for item in match.group(1).split(";"):
            item = item.strip().replace("$(MSBuildThisFileDirectory)", "./").replace("$(MSBuildProjectDirectory)", ".")
            if not item or re.search(r"[$@%]\(", item):
                continue
            resolved = repository_path(posixpath.join(directory, item) if directory else item, True)
            if resolved and resolved[0] and not resolved[1]:
                found.append((line, resolved[0]))
    return found


# 工作流运行的 Python：dotnet.yml 直接运行的脚本与 unittest discover 收集的测试模块，加上它们导入、按路径加载或当脚本运行的
# 本仓库模块。用语法树判断路径以什么为锚，见 PythonSource。

def dotted(node: ast.AST) -> Optional[str]:
    parts: List[str] = []
    while isinstance(node, ast.Attribute):
        parts.append(node.attr)
        node = node.value
    if not isinstance(node, ast.Name):
        return None
    return ".".join([node.id, *reversed(parts)])


def literal(node: ast.AST) -> Optional[Tuple[str, bool]]:
    """字符串常量 (值, True)；f-string 取第一个插值之前的常量前缀 (前缀, 是否没有插值)。"""
    if isinstance(node, ast.Constant) and isinstance(node.value, str):
        return node.value, True
    if isinstance(node, ast.JoinedStr):
        prefix = ""
        for value in node.values:
            if not (isinstance(value, ast.Constant) and isinstance(value.value, str)):
                return prefix, False
            prefix += value.value
        return prefix, True
    return None


def parent(base: Optional[Tuple[str, bool]]) -> Optional[Tuple[str, bool]]:
    if base is None or not base[1] or not base[0]:
        return None  # 前缀的上级不可知；仓库根之上不在仓库里
    return posixpath.dirname(base[0]), True


def join(base: Optional[Tuple[str, bool]], parts: Iterable[ast.AST]) -> Optional[Tuple[str, bool]]:
    """在仓库路径后拼路径段：常量段照拼，f-string 只拼常量前缀，其他表达式只留目录前缀。"""
    if base is None:
        return None
    path, complete = base
    for part in parts:
        if not complete:
            break
        value = literal(part)
        if value is None:
            return (path + "/" if path else ""), False
        resolved = repository_path(f"{path}/{value[0]}" if path else value[0], value[1])
        if resolved is None or resolved[1]:
            return None  # 绝对路径，或爬出了仓库
        path, complete = resolved[0], value[1]
    return path, complete


def local_names(function: ast.AST) -> Set[str]:
    """函数的形参与函数体里赋值的名字（global / nonlocal 声明的除外）：它们遮住同名的模块级名字。"""
    arguments = function.args  # type: ignore[attr-defined]
    names = {argument.arg for argument in [*arguments.posonlyargs, *arguments.args, *arguments.kwonlyargs,
                                           arguments.vararg, arguments.kwarg] if argument}
    declared: Set[str] = set()
    body = function.body if isinstance(function.body, list) else [function.body]  # type: ignore[attr-defined]
    for node in (inner for statement in body for inner in ast.walk(statement)):
        if isinstance(node, ast.Name) and isinstance(node.ctx, ast.Store):
            names.add(node.id)
        elif isinstance(node, (ast.Global, ast.Nonlocal)):
            declared.update(node.names)
    return names - declared


class PythonSource:
    """工作流运行的一个 Python 文件。

    路径以仓库根为锚才算读仓库：__file__ 推出的路径（Path(__file__).resolve().parents[1] 等）、模块顶层赋给名字的这类路径
    （如 ROOT、SCRIPT），以及传给 Path()、os.path.join()、open() 的相对路径字符串（工作流在仓库根运行）；在锚上用 /、joinpath、
    os.path.join 拼常量段，f-string 取常量前缀。临时目录这类别的锚上拼出的路径不算。"""

    def __init__(self, path: str, text: str) -> None:
        self.path = path
        try:
            self.tree = ast.parse(text, filename=path)
        except SyntaxError as error:
            raise ToolError(f"{path} 不是合法的 Python：{error}") from error
        self.names: Dict[str, Tuple[str, bool]] = {}  # 模块顶层赋了仓库路径的名字
        self.definitions: Set[int] = set()  # 这些赋值本身不算引用，用到名字的地方才算
        for node in self.tree.body:
            targets = node.targets if isinstance(node, ast.Assign) else [node.target] if isinstance(node, ast.AnnAssign) else []
            if not targets or not all(isinstance(target, ast.Name) for target in targets) or node.value is None:
                continue
            value = self.value(node.value, self.names)
            for target in targets:
                if value is None:
                    self.names.pop(target.id, None)  # type: ignore[attr-defined]
                else:
                    self.names[target.id] = value  # type: ignore[attr-defined]
                    self.definitions.add(id(node))

    def value(self, node: ast.AST, names: Dict[str, Tuple[str, bool]]) -> Optional[Tuple[str, bool]]:
        """node 求出的仓库相对路径 (路径, 是否完整)；不是以仓库根为锚的路径时返回 None。"""
        if isinstance(node, ast.Name):
            return (self.path, True) if node.id == "__file__" else names.get(node.id)
        if isinstance(node, ast.Attribute) and node.attr == "parent":
            return parent(self.value(node.value, names))
        if isinstance(node, ast.Subscript) and isinstance(node.value, ast.Attribute) and node.value.attr == "parents":
            base, index = self.value(node.value.value, names), node.slice
            if not (isinstance(index, ast.Constant) and isinstance(index.value, int)):
                return None
            for _ in range(index.value + 1):
                base = parent(base)
            return base
        if isinstance(node, ast.BinOp) and isinstance(node.op, ast.Div):
            return join(self.value(node.left, names), [node.right])
        if not isinstance(node, ast.Call):
            return None
        name, args = dotted(node.func) or "", node.args
        if name in ("Path.cwd", "pathlib.Path.cwd", "os.getcwd"):
            return "", True
        if name in ("str", "os.fspath", "os.path.abspath", "os.path.realpath", "os.path.normpath") and args:
            return self.value(args[0], names)
        if name == "os.path.dirname" and args:
            return parent(self.value(args[0], names))
        if name == "os.path.join" or name.split(".")[-1] in PATH_TYPES:
            base = self.value(args[0], names) if args else None
            if base is not None:
                return join(base, args[1:])
            return join(("", True), args) if args and literal(args[0]) else None
        if isinstance(node.func, ast.Attribute):
            base = self.value(node.func.value, names)
            if node.func.attr in ("resolve", "absolute", "expanduser"):
                return base
            if node.func.attr == "joinpath":
                return join(base, args)
        return None

    def references(self, rebound: Iterable[str] = ()) -> List[Tuple[int, str, bool]]:
        """(行号, 仓库相对路径, 是否完整)。rebound 是加载本模块的测试会换掉的模块级名字：函数里用到它们时不算以仓库根为锚。"""
        found: List[Tuple[int, str, bool]] = []
        replaced = set(rebound)

        def visit(node: ast.AST, names: Dict[str, Tuple[str, bool]]) -> None:
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.Lambda)):
                outer = [*getattr(node, "decorator_list", []), *node.args.defaults, *node.args.kw_defaults]
                for item in outer:
                    if item is not None:
                        visit(item, names)
                hidden = local_names(node) | replaced
                inner = {name: value for name, value in names.items() if name not in hidden}
                for child in node.body if isinstance(node.body, list) else [node.body]:
                    visit(child, inner)
                return
            if isinstance(node, ast.expr):
                value = self.value(node, names)
                if value is None and isinstance(node, ast.Call) and dotted(node.func) in ("open", "io.open") and node.args:
                    first = node.args[0]
                    value = self.value(first, names) or (join(("", True), [first]) if literal(first) else None)
                if value is not None:
                    if value[0]:
                        found.append((node.lineno, *value))
                    return
            for child in ast.iter_child_nodes(node):
                visit(child, names)

        for statement in self.tree.body:
            if id(statement) not in self.definitions:
                visit(statement, self.names)
        return found

    def rebound(self, alias: str) -> Set[str]:
        """本文件给 alias 模块换掉的模块级名字：alias.X = …（含解包赋值）、setattr(alias, "X", …)、patch.object(alias, "X", …)。"""
        found: Set[str] = set()
        for node in ast.walk(self.tree):
            targets = node.targets if isinstance(node, ast.Assign) else [node.target] if isinstance(node, (ast.AugAssign, ast.AnnAssign)) else []
            pending = list(targets)
            while pending:
                target = pending.pop()
                if isinstance(target, (ast.Tuple, ast.List)):
                    pending.extend(target.elts)
                elif isinstance(target, ast.Starred):
                    pending.append(target.value)
                elif isinstance(target, ast.Attribute) and isinstance(target.value, ast.Name) and target.value.id == alias:
                    found.add(target.attr)
            if isinstance(node, ast.Call) and (dotted(node.func) or "").split(".")[-1] in ("setattr", "object") and len(node.args) >= 2:
                module, name = node.args[:2]
                if isinstance(module, ast.Name) and module.id == alias and literal(name) and literal(name)[1]:  # type: ignore[index]
                    found.add(literal(name)[0])  # type: ignore[index]
        return found

    def loads(self, repository: Repository) -> List[Tuple[str, Optional[str]]]:
        """本文件导入、按路径加载或引用到的本仓库 .py 文件：(路径, 模块在本文件里的名字)。from 导入、当脚本运行等
        拿不到模块对象的方式，名字为 None。"""
        directories = [posixpath.dirname(self.path)]
        for node in ast.walk(self.tree):
            if isinstance(node, ast.Call) and dotted(node.func) in ("sys.path.insert", "sys.path.append") and node.args:
                value = self.value(node.args[-1], self.names)
                if value and value[1]:
                    directories.append(value[0])

        def module(name: str) -> Optional[str]:
            for directory in directories:
                candidate = posixpath.join(directory, *name.split(".")) + ".py"
                if candidate in repository.names:
                    return candidate
            return None

        found: List[Tuple[str, Optional[str]]] = []
        calls: Dict[int, str] = {}  # spec_from_file_location 调用 → 加载的文件
        by_path: Set[Tuple[int, str]] = set()  # 交给 spec_from_file_location 的路径按加载处理，不再当脚本引用
        for node in ast.walk(self.tree):
            if isinstance(node, ast.Import):
                found += [(path, alias.asname or alias.name) for alias in node.names for path in [module(alias.name)] if path]
            elif isinstance(node, ast.ImportFrom) and node.module and not node.level and module(node.module):
                found.append((module(node.module), None))  # type: ignore[arg-type]
            elif isinstance(node, ast.Call) and (dotted(node.func) or "").endswith("spec_from_file_location") and len(node.args) >= 2:
                value = self.value(node.args[1], self.names)
                if value and value[1] and value[0] in repository.names:
                    calls[id(node)] = value[0]
                    by_path.add((node.args[1].lineno, value[0]))
        specs: Dict[str, int] = {}
        named: Set[int] = set()
        for node in self.tree.body:  # 模块顶层的 spec = spec_from_file_location(…)；alias = module_from_spec(spec)
            if not (isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name)
                    and isinstance(node.value, ast.Call)):
                continue
            target, argument = node.targets[0].id, node.value.args[0] if node.value.args else None
            if id(node.value) in calls:
                specs[target] = id(node.value)
            elif (dotted(node.value.func) or "").endswith("module_from_spec") and isinstance(argument, ast.Name) and argument.id in specs:
                named.add(specs[argument.id])
                found.append((calls[specs[argument.id]], target))
        found += [(path, None) for call, path in calls.items() if call not in named]
        found += [(path, None) for line, path, complete in self.references()
                  if complete and path.endswith(".py") and path in repository.names and path != self.path and (line, path) not in by_path]
        return found


def workflow_python(repository: Repository) -> List[str]:
    """dotnet.yml 直接运行的 Python：python3 <脚本>，以及 python3 -m unittest discover 收集的测试模块。"""
    text = WORKFLOW.read_text(encoding="utf-8")
    found = [path for path in PYTHON_SCRIPT.findall(text) if path in repository.names]
    for options in PYTHON_UNITTEST.findall(text):
        words, values, positional = options.split(), {"-s": ".", "-p": "test*.py"}, []
        aliases = {"--start-directory": "-s", "--pattern": "-p", "-t": "-t", "--top-level-directory": "-t"}
        index = 0
        while index < len(words):
            flag = aliases.get(words[index], words[index])
            if flag in ("-s", "-p", "-t") and index + 1 < len(words):
                values[flag] = words[index + 1]
                index += 2
                continue
            if not words[index].startswith("-"):
                positional.append(words[index])
            index += 1
        start = posixpath.normpath(positional[0] if positional else values["-s"]).strip("/")
        start = "" if start == "." else start
        pattern = positional[1] if len(positional) > 1 else values["-p"]
        for name in repository.files:
            directory = posixpath.dirname(name)
            if not fnmatch.fnmatchcase(posixpath.basename(name), pattern) or start and not (directory + "/").startswith(start + "/"):
                continue
            # unittest 在起始目录之下只进包目录（有 __init__.py）
            parts = directory[len(start):].strip("/").split("/") if directory != start else []
            if all(posixpath.join(start, *parts[:depth], "__init__.py") in repository.names for depth in range(1, len(parts) + 1)):
                found.append(name)
    return list(dict.fromkeys(found))


def python_references(repository: Repository) -> List[Tuple[str, List[Tuple[int, str, bool]]]]:
    """工作流运行的每个 Python 文件及其仓库路径引用。只被测试按名字加载、又被该测试换掉模块级名字的模块（如
    test_check_docs.py 把 check_docs.ROOT 换成临时目录），函数里用到这些名字的路径不算读取仓库。"""
    entries = set(workflow_python(repository))
    sources: Dict[str, PythonSource] = {}
    loaders: Dict[str, List[Tuple[str, Optional[str]]]] = {}
    pending = sorted(entries)
    while pending:
        path = pending.pop()
        if path in sources:
            continue
        sources[path] = PythonSource(path, (ROOT / path).read_text(encoding="utf-8", errors="replace"))
        for module, alias in sources[path].loads(repository):
            loaders.setdefault(module, []).append((path, alias))
            pending.append(module)
    result = []
    for path in sorted(sources):
        users = loaders.get(path, [])
        rebound: Set[str] = set()
        if path not in entries and users and all(alias for _, alias in users):
            rebound = set.intersection(*(sources[user].rebound(alias) for user, alias in users))  # type: ignore[arg-type]
        result.append((path, sources[path].references(rebound)))
    return result


def paths_check() -> Counter:
    """返回各类来源里对得上仓库文件的引用数，以及其中落在排除区、已被重新纳入的引用数（键「重新纳入」）。"""
    patterns = pull_request_paths()
    excluded = [glob_regex(pattern[1:]) for pattern in patterns if pattern.startswith("!")]
    repository = Repository(tracked())
    problems: List[str] = []
    counts: Counter = Counter()

    def check(kind: str, place: str, shown: str, covered: List[str]) -> None:
        if not covered:
            return
        counts[kind] += 1
        missed = [name for name in covered if not triggers(patterns, name)]
        if not missed:
            counts["重新纳入"] += any(regex.match(name) for name in covered for regex in excluded)
        elif len(missed) == 1:
            problems.append(f"{place} 读取 {missed[0]}，但只改它的 PR 不触发 dotnet 工作流；把它加进 dotnet.yml 的 pull_request.paths。")
        else:
            problems.append(f"{place} 读取 {shown}，其中 {len(missed)} 个文件只改它们的 PR 不触发 dotnet 工作流（如 "
                            f"{'、'.join(missed[:3])}）；把它们加进 dotnet.yml 的 pull_request.paths。")

    def shown(path: str, complete: bool) -> str:
        return path if complete else path + "…"

    for source in tracked("*.cs"):
        text = (ROOT / source).read_text(encoding="utf-8", errors="replace")
        for line, path, complete in csharp_references(text, repository.top):
            check(".NET 源码", f"{source}:{line}", shown(path, complete), repository.covered(path, complete))
    for source in tracked("*.csproj", "*.props", "*.targets"):
        text = (ROOT / source).read_text(encoding="utf-8", errors="replace")
        for line, path in msbuild_references(source, text):
            covered = repository.matching(path) if re.search(r"[*?]", path) else repository.covered(path)
            check("MSBuild 项", f"{source}:{line}", path, covered)
    for source, references in python_references(repository):
        counts["Python 文件"] += 1
        for line, path, complete in references:
            check("Python", f"{source}:{line}", shown(path, complete), repository.covered(path, complete))
    if problems:
        raise CheckFailed("\n".join(problems))
    return counts


# ---------- 入口 ----------

def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="dotnet 工作流的测试分片、镜像预拉、结果汇总与触发过滤自检")
    commands = parser.add_subparsers(dest="command", required=True)
    shard_parser = commands.add_parser("shard", help="写出分片的解决方案筛选文件")
    shard_parser.add_argument("shard", choices=list(SHARDS))
    shard_parser.add_argument("--out", type=Path, required=True)
    images_parser = commands.add_parser("images", help="列出分片会拉的测试镜像")
    images_parser.add_argument("shard", nargs="?", choices=list(SHARDS))
    prefetch_parser = commands.add_parser("prefetch", help="顺序拉取分片的测试镜像")
    prefetch_parser.add_argument("shard", nargs="?", choices=list(SHARDS))
    prefetch_parser.add_argument("--state", type=Path, required=True)
    wait_parser = commands.add_parser("wait-prefetch", help="有界等待后台预拉结束")
    wait_parser.add_argument("--state", type=Path, required=True)
    wait_parser.add_argument("--timeout", type=float, default=180)
    verify_parser = commands.add_parser("verify", help="汇总核对各分片的 TRX")
    verify_parser.add_argument("results", type=Path)
    verify_parser.add_argument("--summary", type=Path, default=os.environ.get("GITHUB_STEP_SUMMARY") or None)
    commands.add_parser("paths-check", help="核对 PR 路径过滤没有漏掉 .NET 源码读取的文档")
    args = parser.parse_args(argv)

    try:
        if args.command == "shard":
            write_filter(args.shard, args.out)
        elif args.command == "images":
            for image in shard_images(args.shard):
                print(image)
        elif args.command == "prefetch":
            prefetch(args.shard, args.state)
        elif args.command == "wait-prefetch":
            wait_prefetch(args.state, args.timeout)
        elif args.command == "verify":
            summary, problems, warnings = verify(args.results)
            for warning in warnings:
                print("::warning::" + warning)
            print("\n".join(summary))
            if args.summary:  # 不通过也写摘要，便于对照
                with Path(args.summary).open("a", encoding="utf-8") as out:
                    out.write("\n".join(summary) + "\n")
            if problems:
                raise CheckFailed("\n".join(problems))
            print("汇总核对通过。")
        elif args.command == "paths-check":
            counts = paths_check()
            print(f"触发过滤自检通过：对得上仓库文件的路径引用 .NET 源码 {counts['.NET 源码']} 处、MSBuild 项 {counts['MSBuild 项']} 处、"
                  f"工作流运行的 {counts['Python 文件']} 个 Python 文件 {counts['Python']} 处，只改这些文件的 PR 都会触发 dotnet 工作流；"
                  f"其中 {counts['重新纳入']} 处落在过滤排除的 pm/、docs/ 或根目录 Markdown 里、已被重新纳入。")
        return 0
    except CheckFailed as error:
        print(str(error), file=sys.stderr)
        return 1
    except (ToolError, OSError, ET.ParseError, json.JSONDecodeError) as error:
        print(f"工具出错：{error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
