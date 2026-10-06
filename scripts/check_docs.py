#!/usr/bin/env python3
"""文档门禁：禁用关键字、Markdown 表格列数、相对链接、空链接。只依赖标准库。

用法：python3 scripts/check_docs.py [路径...]（默认 docs/ README.md AGENTS.md pm/README.md pm/tasks/）
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BANNED = re.compile(r"jnpf|引迈|xing", re.I)
# 对标项目只能写「对标项目」，不写名字（ADR-020 §8）
REFERENCE_NAMES = re.compile(r"claude-code-templates|deepseek|openai/plugins|aitmpl|davila7|codex-plugin", re.I)


def md_files(args: list[str]) -> list[Path]:
    targets = args or ["docs", "README.md", "AGENTS.md", "pm/README.md", "pm/tasks"]
    out: list[Path] = []
    for t in targets:
        p = ROOT / t
        if p.is_dir():
            out += sorted(p.rglob("*.md"))
        elif p.exists():
            out.append(p)
    return out


def check(path: Path) -> list[str]:
    errs: list[str] = []
    text = path.read_text(encoding="utf-8")
    rel = path.relative_to(ROOT)
    for i, line in enumerate(text.splitlines(), 1):
        if BANNED.search(line):
            errs.append(f"{rel}:{i}: 含禁用关键字")
        if REFERENCE_NAMES.search(line):
            errs.append(f"{rel}:{i}: 对标项目请写「对标项目」，不写名字")
        if "[](" in line:
            errs.append(f"{rel}:{i}: 空链接 [](...)")
    # 表格列数
    lines = text.split("\n")
    i = 0
    fence = False
    while i < len(lines):
        l = lines[i]
        if l.startswith("```"):
            fence = not fence
        if not fence and l.lstrip().startswith("|") and i + 1 < len(lines) and re.match(r"^\s*\|?\s*:?-{2,}", lines[i + 1]):
            h = len(re.split(r"(?<!\\)\|", l.strip().strip("|")))
            j = i + 2
            while j < len(lines) and lines[j].lstrip().startswith("|"):
                c = len(re.split(r"(?<!\\)\|", lines[j].strip().strip("|")))
                if c != h:
                    errs.append(f"{rel}:{j + 1}: 表格列数 {c} 与表头 {h} 不一致（反引号内的竖线请写 \\|）")
                j += 1
            i = j
            continue
        i += 1
    # 相对链接（代码块外）
    no_fence = re.sub(r"```.*?```", "", text, flags=re.S)
    for m in re.finditer(r"\]\(([^)\s]+)\)", no_fence):
        href = m.group(1)
        if href.startswith(("http://", "https://", "mailto:", "#")):
            continue
        target = href.split("#")[0]
        if target and not (path.parent / target).resolve().exists():
            errs.append(f"{rel}: 链接指向不存在的文件 {href}")
    return errs


def main() -> None:
    files = md_files(sys.argv[1:])
    errs: list[str] = []
    for f in files:
        errs += check(f)
    if errs:
        print("\n".join(errs))
        sys.exit(1)
    print(f"文档门禁通过（{len(files)} 个文件）")


if __name__ == "__main__":
    main()
