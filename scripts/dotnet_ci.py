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
      没有失败，少一个模块即不通过；分片、模块、用例数与耗时写进作业摘要。跳过的用例只告警。
  python3 scripts/dotnet_ci.py paths-check
      dotnet.yml 的 pull_request 路径过滤不看 pm/、docs/ 与根目录 Markdown。.NET 源码以字符串字面量引用的
      docs/、pm/ 下的文件必须被过滤重新纳入，否则只改这些文件的 PR 会漏跑读取它们的测试。

退出码：0 通过；1 核对不通过；2 工具出错。
"""
from __future__ import annotations

import argparse
import json
import os
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
REPOSITORY_LITERAL = re.compile(r'"((?:docs|pm)/[^"{}\s]+)"')


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


def test_projects() -> Dict[str, str]:
    """Qian.slnx 里的测试项目：程序集名 → 项目路径。本仓库的测试项目都引用 xunit.v3。"""
    found = {}
    for project in solution_projects():
        text = (ROOT / project).read_text(encoding="utf-8")
        if XUNIT.search(text) or re.search(r"<IsTestProject>\s*true\s*</IsTestProject>", text, re.IGNORECASE):
            found[assembly_name(project)] = project
    if not found:
        raise ToolError("Qian.slnx 里没有识别出测试项目。")
    return found


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


def verify(results: Path) -> Tuple[List[str], List[str]]:
    """返回作业摘要的 Markdown 行与发现的问题；没有问题才算通过。"""
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
    summary += ["<details><summary>各模块</summary>", "", "| 分片 | 模块 | 用例 | 秒 |", "|---|---|---|---|"]
    for shard, entries in rows.items():
        for module, outcomes, seconds in sorted(entries, key=lambda entry: -entry[2]):
            summary.append(f"| {shard} | {module} | {sum(outcomes.values())} | {seconds:.0f} |")
    summary += ["", "</details>"]
    for warning in warnings:
        print("::warning::" + warning)
    return summary, problems


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
    output = subprocess.run(["git", "ls-files", "--", *patterns], cwd=ROOT, capture_output=True, text=True)
    if output.returncode:
        raise ToolError("git ls-files 失败：" + output.stderr.strip())
    return output.stdout.splitlines()


def paths_check() -> int:
    patterns = pull_request_paths()
    files = tracked()
    problems = []
    references = 0
    for source in tracked("*.cs"):
        text = (ROOT / source).read_text(encoding="utf-8", errors="replace")
        for number, line in enumerate(text.splitlines(), 1):
            for literal in REPOSITORY_LITERAL.findall(line):
                covered = [name for name in files if name == literal or name.startswith(literal.rstrip("/") + "/")]
                references += bool(covered)
                for name in covered:
                    if not triggers(patterns, name):
                        problems.append(f"{source}:{number} 读取 {name}，但只改它的 PR 不触发 dotnet 工作流；"
                                        "把它加进 dotnet.yml 的 pull_request.paths。")
    if problems:
        raise CheckFailed("\n".join(problems))
    return references


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
            summary, problems = verify(args.results)
            print("\n".join(summary))
            if args.summary:  # 不通过也写摘要，便于对照
                with Path(args.summary).open("a", encoding="utf-8") as out:
                    out.write("\n".join(summary) + "\n")
            if problems:
                raise CheckFailed("\n".join(problems))
            print("汇总核对通过。")
        elif args.command == "paths-check":
            references = paths_check()
            print(f"触发过滤自检通过：.NET 源码引用 docs/、pm/ 下的路径 {references} 处，全部会触发 dotnet 工作流。")
        return 0
    except CheckFailed as error:
        print(str(error), file=sys.stderr)
        return 1
    except (ToolError, OSError, ET.ParseError, json.JSONDecodeError) as error:
        print(f"工具出错：{error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
