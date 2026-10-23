#!/usr/bin/env python3
"""dotnet 工作流的测试镜像预拉与触发过滤自检（S1-QA-018）。只依赖标准库。

.github/workflows/dotnet.yml 调用：

  python3 scripts/dotnet_ci.py images
      列出测试会拉的容器镜像。镜像名取 tests/Qian.Testing.Containers 里 QIAN_TEST_IMAGE_* 变量的缺省值；
      环境变量已覆盖的取覆盖值，与测试实际拉的一致。
  python3 scripts/dotnet_ci.py prefetch --state <目录>
      顺序 docker pull 上面列出的镜像，逐个记录结果与耗时，结束时写 done 标记。工作流在后台运行它；
      拉取失败只记录，Testcontainers 启动容器前会自己补拉。
  python3 scripts/dotnet_ci.py wait-prefetch --state <目录> [--timeout 秒]
      有界等待后台预拉结束并打印记录；超时或预拉进程已退出都照常返回 0。
  python3 scripts/dotnet_ci.py paths-check
      dotnet.yml 的 pull_request 路径过滤不看 pm/、docs/ 与根目录 Markdown。.NET 源码以字符串字面量引用的
      docs/、pm/ 下的文件必须被过滤重新纳入，否则只改这些文件的 PR 会漏跑读取它们的测试。

退出码：0 通过；1 核对不通过；2 工具出错。
"""
from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
import time
from pathlib import Path
from typing import Dict, List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "dotnet.yml"
CONTAINERS = ROOT / "tests" / "Qian.Testing.Containers"

IMAGE_DEFAULT = re.compile(r'"(QIAN_TEST_IMAGE_[A-Z0-9_]+)"\s*,\s*"([^"]+)"')
REPOSITORY_LITERAL = re.compile(r'"((?:docs|pm)/[^"{}\s]+)"')


class CheckFailed(Exception):
    """核对不通过（退出码 1）。"""


class ToolError(Exception):
    """输入或环境不对，工具无法给出结论（退出码 2）。"""


# ---------- 测试镜像 ----------

def sources(directory: Path) -> List[Path]:
    return [path for path in sorted(directory.rglob("*.cs"))
            if not {"bin", "obj"} & set(path.relative_to(directory).parts)]


def image_defaults() -> Dict[str, str]:
    """QIAN_TEST_IMAGE_* 变量 → 缺省镜像，按声明顺序。"""
    defaults: Dict[str, str] = {}
    for path in sources(CONTAINERS):
        for variable, image in IMAGE_DEFAULT.findall(path.read_text(encoding="utf-8")):
            defaults.setdefault(variable, image)
    if not defaults:
        raise ToolError(f"{CONTAINERS.relative_to(ROOT)} 里没有解析出 QIAN_TEST_IMAGE_* 缺省镜像。")
    return defaults


def test_images() -> List[str]:
    images: List[str] = []
    for variable, default in image_defaults().items():
        image = os.environ.get(variable, "").strip() or default
        if image not in images:
            images.append(image)
    return images


def prefetch(state: Path) -> None:
    state.mkdir(parents=True, exist_ok=True)
    (state / "pid").write_text(str(os.getpid()), encoding="utf-8")
    try:
        with (state / "pulls.log").open("a", encoding="utf-8") as log:
            def note(line: str) -> None:
                log.write(line + "\n")
                log.flush()
            try:
                images = test_images()
            except (ToolError, OSError) as error:
                note(f"无法列出测试镜像：{error}")
                return
            note(f"顺序预拉 {len(images)} 个镜像：{' '.join(images)}")
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
    parser = argparse.ArgumentParser(description="dotnet 工作流的测试镜像预拉与触发过滤自检")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("images", help="列出测试会拉的镜像")
    prefetch_parser = commands.add_parser("prefetch", help="顺序拉取测试镜像")
    prefetch_parser.add_argument("--state", type=Path, required=True)
    wait_parser = commands.add_parser("wait-prefetch", help="有界等待后台预拉结束")
    wait_parser.add_argument("--state", type=Path, required=True)
    wait_parser.add_argument("--timeout", type=float, default=180)
    commands.add_parser("paths-check", help="核对 PR 路径过滤没有漏掉 .NET 源码读取的文档")
    args = parser.parse_args(argv)

    try:
        if args.command == "images":
            for image in test_images():
                print(image)
        elif args.command == "prefetch":
            prefetch(args.state)
        elif args.command == "wait-prefetch":
            wait_prefetch(args.state, args.timeout)
        elif args.command == "paths-check":
            references = paths_check()
            print(f"触发过滤自检通过：.NET 源码引用 docs/、pm/ 下的路径 {references} 处，全部会触发 dotnet 工作流。")
        return 0
    except CheckFailed as error:
        print(str(error), file=sys.stderr)
        return 1
    except (ToolError, OSError) as error:
        print(f"工具出错：{error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
