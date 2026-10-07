#!/usr/bin/env python3
"""文档门禁：禁用关键字、Markdown 表格列数、相对链接、空链接。只依赖标准库。

用法：python3 scripts/check_docs.py [路径...]（默认见 DEFAULT_TARGETS）

文档站（website/docs，.md 与 .mdx）同样受检，链接按站内路由解析：
- 「/start/hosts」这类绝对路由对应 website/docs 下的页面（x.md、x.mdx 或 x/index.md(x)）或 public/ 下的文件；
- 「/design/…」对应仓库 docs/ 的镜像页（小写短横线，README → index）；
- 「/cli/…」「/components/…」对应构建前生成的页面，页面清单取自 website/scripts 的生成器。
生成目录 website/docs/{components,cli,design} 不进 Git，不在这里检查；它们的源（docs/ 等）照常检查，
生成后的链接由 Rspress 构建时的断链检查把关。
"""
from __future__ import annotations

import re
import sys
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BANNED = re.compile(r"jnpf|引迈|xing", re.I)
# 对标项目只能写「对标项目」，不写名字（ADR-020 §8）
REFERENCE_NAMES = re.compile(r"claude-code-templates|deepseek|openai/plugins|aitmpl|davila7|codex-plugin", re.I)

DEFAULT_TARGETS = [
    "docs",
    "contracts",
    "README.md",
    "AGENTS.md",
    "pm/README.md",
    "pm/tasks",
    "tests/README.md",
    "deploy",
    "website/README.md",
    "website/AGENTS.md",
    "website/docs",
]
# 文档站的界面文字与脚本注释也属于仓库文本（PRD §14 C5），只查禁用词
SITE_SOURCE_TARGETS = ["website/theme", "website/scripts", "website/rspress.config.ts"]
SITE_SOURCE_SUFFIXES = {".ts", ".tsx", ".css", ".json"}

SITE_DOCS = ROOT / "website" / "docs"
SITE_GENERATED = {"components", "cli", "design"}
PAGE_SUFFIXES = (".md", ".mdx")


def is_generated(path: Path) -> bool:
    try:
        rel = path.relative_to(SITE_DOCS)
    except ValueError:
        return False
    return len(rel.parts) > 1 and rel.parts[0] in SITE_GENERATED


def is_site_page(path: Path) -> bool:
    return SITE_DOCS in path.parents


def md_files(args: list[str]) -> list[Path]:
    targets = args or DEFAULT_TARGETS
    out: list[Path] = []
    for t in targets:
        p = ROOT / t
        if p.is_dir():
            out += sorted(
                f for f in p.rglob("*")
                if f.suffix in PAGE_SUFFIXES and "node_modules" not in f.parts and not is_generated(f)
            )
        elif p.exists():
            out.append(p)
    return out


def source_files() -> list[Path]:
    out: list[Path] = []
    for t in SITE_SOURCE_TARGETS:
        p = ROOT / t
        if p.is_dir():
            out += sorted(f for f in p.rglob("*") if f.suffix in SITE_SOURCE_SUFFIXES and "node_modules" not in f.parts)
        elif p.exists():
            out.append(p)
    return out


@lru_cache(maxsize=1)
def design_routes() -> frozenset[str]:
    """docs/ 下每个 .md 在文档站「设计与路线」的路由，规则同 website/scripts/sync-specs.ts 的 designRoute。"""
    routes = set()
    for f in (ROOT / "docs").rglob("*.md"):
        parts = [p.lower() for p in f.relative_to(ROOT / "docs").parts]
        name = parts.pop()
        page = "/".join(parts + ["index" if name == "readme.md" else name[: -len(".md")]])
        routes.add("/design/" + page)
        if page == "index" or page.endswith("/index"):
            routes.add("/design/" + page[: -len("index")])
    return frozenset(routes)


@lru_cache(maxsize=1)
def generated_routes() -> frozenset[str]:
    """生成器源码里登记的 /cli/ 与 /components/ 页面（CLI_PAGES 的 slug、PLATFORM_COMPONENTS 的 id）。"""
    scripts = ROOT / "website" / "scripts"
    routes = {"/cli/", "/cli/index", "/components/", "/components/index", "/components/platform/",
              "/components/platform/index", "/components/yue/", "/components/yue/index"}
    sync = (scripts / "sync-specs.ts").read_text(encoding="utf-8")
    routes |= {f"/cli/{slug}" for slug in re.findall(r"\bslug: '([a-z0-9-]+)'", sync)}
    collect = (scripts / "collect-components.ts").read_text(encoding="utf-8")
    routes |= {f"/components/platform/{cid}" for cid in re.findall(r"\bid: '(qian\.[a-z0-9.-]+)'", collect)}
    return frozenset(routes)


def site_page_exists(base: Path) -> bool:
    if base.is_file():
        return True
    candidates = [base.with_name(base.name + s) for s in PAGE_SUFFIXES] + [base / f"index{s}" for s in PAGE_SUFFIXES]
    return any(c.is_file() for c in candidates)


def route_exists(route: str) -> bool:
    if route.endswith(".html"):
        route = route[: -len(".html")]
    if route.startswith("/design/"):
        return route in design_routes()
    if route.startswith(("/cli/", "/components/")):
        return route in generated_routes()
    rel = route.strip("/")
    if rel == "":
        return site_page_exists(SITE_DOCS / "index")
    return site_page_exists(SITE_DOCS / rel) or (SITE_DOCS / "public" / rel).is_file()


def link_exists(path: Path, target: str) -> bool:
    if not is_site_page(path):
        return (path.parent / target).resolve().exists()
    if target.startswith("/"):
        return route_exists(target)
    return site_page_exists((path.parent / target).resolve())


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
    # 相对链接与站内路由（代码块外）
    no_fence = re.sub(r"```.*?```", "", text, flags=re.S)
    for m in re.finditer(r"\]\(([^)\s]+)\)", no_fence):
        href = m.group(1)
        if href.startswith(("http://", "https://", "mailto:", "#")):
            continue
        target = href.split("#")[0]
        if target and not link_exists(path, target):
            errs.append(f"{rel}: 链接指向不存在的文件 {href}")
    return errs


def check_source(path: Path) -> list[str]:
    rel = path.relative_to(ROOT)
    errs: list[str] = []
    for i, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if BANNED.search(line):
            errs.append(f"{rel}:{i}: 含禁用关键字")
        if REFERENCE_NAMES.search(line):
            errs.append(f"{rel}:{i}: 对标项目请写「对标项目」，不写名字")
    return errs


def main() -> None:
    args = sys.argv[1:]
    files = md_files(args)
    sources = [] if args else source_files()
    errs: list[str] = []
    for f in files:
        errs += check(f)
    for f in sources:
        errs += check_source(f)
    if errs:
        print("\n".join(errs))
        sys.exit(1)
    extra = f"，另查文档站源码 {len(sources)} 个" if sources else ""
    print(f"文档门禁通过（{len(files)} 个文件{extra}）")


if __name__ == "__main__":
    main()
