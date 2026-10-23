#!/usr/bin/env python3
"""dotnet 工作流的辅助检查（S1-QA-018）。只依赖标准库。

.github/workflows/dotnet.yml 调用：

  python3 scripts/dotnet_ci.py paths-check
      dotnet.yml 的 pull_request 路径过滤不看 pm/、docs/ 与根目录 Markdown。.NET 源码以字符串字面量引用的
      docs/、pm/ 下的文件必须被过滤重新纳入，否则只改这些文件的 PR 会漏跑读取它们的测试。

退出码：0 通过；1 核对不通过；2 工具出错。
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path
from typing import List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[1]
WORKFLOW = ROOT / ".github" / "workflows" / "dotnet.yml"

REPOSITORY_LITERAL = re.compile(r'"((?:docs|pm)/[^"{}\s]+)"')


class CheckFailed(Exception):
    """核对不通过（退出码 1）。"""


class ToolError(Exception):
    """输入或环境不对，工具无法给出结论（退出码 2）。"""


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
    parser = argparse.ArgumentParser(description="dotnet 工作流的辅助检查")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("paths-check", help="核对 PR 路径过滤没有漏掉 .NET 源码读取的文档")
    args = parser.parse_args(argv)

    try:
        if args.command == "paths-check":
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
