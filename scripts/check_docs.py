#!/usr/bin/env python3
"""文档门禁：禁用关键字、Markdown 表格列数、相对链接、空链接；文档站页面另查 frontmatter（docs-site.md §4）。只依赖标准库。

用法：python3 scripts/check_docs.py [路径...]（默认见 DEFAULT_TARGETS）

文档站（website/docs，.md 与 .mdx）同样受检，链接按站内路由解析：
- 「/start/hosts」这类绝对路由对应 website/docs 下的页面（x.md、x.mdx 或 x/index.md(x)）或 public/ 下的文件；
- 「/design/…」对应仓库 docs/ 的镜像页（小写短横线，README → index）；
- 「/cli/…」「/components/…」对应构建前生成的页面，页面清单取自 website/scripts 的生成器；
- 「/sdk/kun/…」「/sdk/api/…」是 Kun 小节与 API 参考（docs-site.md §2b），已生成时按磁盘上的页面解析，
  未生成时（本检查不跑生成器）交给 Rspress 构建时的断链检查。
生成目录 website/docs/{components,cli,design,sdk/kun,sdk/api} 与生成文件 start/versions.md 不进 Git（sdk/kun/with-qian.mdx 是手写页，照常检查），
默认不在这里检查；它们的源（docs/ 等）照常检查。生成目录里出现进了 Git 的文件即失败（生成目录不接受手改）。
Kun 小节是 kun 仓库文档的镜像，源不在本仓库：CI 生成后显式检查它，如 python3 scripts/check_docs.py website/docs/sdk/kun。
"""
from __future__ import annotations

import re
import json
import subprocess
import sys
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
# 已作废项目的名字按整词匹配：前后不能紧挨英文字母，避免误伤 prefixing、mixing 这类英文单词
BANNED = re.compile(r"jnpf|引迈|(?<![a-z])xing(?![a-z])", re.I)
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
# 生成目录（相对 website/docs），与 website/scripts/repo.ts 的 GENERATED_PARTS 一致；手写例外单独列出
SITE_GENERATED = ("components", "cli", "design", "sdk/kun", "sdk/api")
SITE_HANDWRITTEN = {"sdk/kun/with-qian.mdx"}
# 单个生成文件（相对 website/docs）：版本矩阵由 website/scripts/build-versions.ts 从钉版文件产出（docs-site.md §2b.4）
SITE_GENERATED_FILES = {"start/versions.md"}
PAGE_SUFFIXES = (".md", ".mdx")


def generated_part(path: Path) -> str | None:
    """path 所在的生成目录（相对 website/docs）；不在生成目录里或是手写例外时返回 None。"""
    try:
        rel = path.relative_to(SITE_DOCS).as_posix()
    except ValueError:
        return None
    if rel in SITE_HANDWRITTEN:
        return None
    if rel in SITE_GENERATED_FILES:
        return rel
    return next((part for part in SITE_GENERATED if rel.startswith(part + "/")), None)


def is_generated(path: Path) -> bool:
    return generated_part(path) is not None


def in_generated_dir(path: Path) -> bool:
    """path 本身是生成目录或在生成目录里（显式点名生成目录时用）。"""
    try:
        rel = path.relative_to(SITE_DOCS).as_posix()
    except ValueError:
        return False
    return rel in SITE_GENERATED_FILES or any(rel == part or rel.startswith(part + "/") for part in SITE_GENERATED)


def is_site_page(path: Path) -> bool:
    return SITE_DOCS in path.parents


def md_files(args: list[str]) -> list[Path]:
    """默认目标跳过生成目录；显式给出的路径连同其中的生成页一起检查（CI 用它检查 Kun 镜像页）。"""
    targets = args or DEFAULT_TARGETS
    out: list[Path] = []
    for t in targets:
        p = ROOT / t
        explicit_generated = bool(args) and in_generated_dir(p)
        if p.is_dir():
            out += sorted(
                f for f in p.rglob("*")
                if f.suffix in PAGE_SUFFIXES and "node_modules" not in f.parts
                and (explicit_generated or not is_generated(f))
            )
        elif p.exists():
            out.append(p)
    return out


def tracked_generated() -> list[str]:
    """生成目录里进了 Git 的文件（手写例外除外）。不在 git 仓库里时返回空。"""
    dirs = [f"website/docs/{part}" for part in SITE_GENERATED] + [f"website/docs/{f}" for f in SITE_GENERATED_FILES]
    try:
        out = subprocess.run(["git", "-C", str(ROOT), "ls-files", "--", *dirs],
                             capture_output=True, text=True, check=True).stdout
    except (OSError, subprocess.CalledProcessError):
        return []
    return [f for f in out.splitlines() if f and f.removeprefix("website/docs/") not in SITE_HANDWRITTEN]


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
    # 非 core 包的文档源由收集器显式登记；预生成检查也只接受这些已声明的路由。
    routes |= {f"/components/{slug}" for slug in re.findall(r"\bpath: '(yue/[a-z0-9-]+)\.(?:md|mdx)'", collect)}
    catalog = ROOT / "packages/yue/core/docs/catalog.json"
    if catalog.is_file():
        names = json.loads(catalog.read_text(encoding="utf-8"))
        routes |= {f"/components/yue/core/{name.lower()}" for name in names}
        routes |= {"/components/yue/tokens", "/components/yue/core/shared-api"}
    return frozenset(routes)


def site_page_exists(base: Path) -> bool:
    if base.is_file():
        return True
    candidates = [base.with_name(base.name + s) for s in PAGE_SUFFIXES] + [base / f"index{s}" for s in PAGE_SUFFIXES]
    return any(c.is_file() for c in candidates)


def mirror_generated(part: str) -> bool:
    """Kun 小节 / API 参考是否已在本机生成：生成器总会写出侧栏或落地页。"""
    marker = {"sdk/kun": "_meta.json", "sdk/api": "index.md"}[part]
    return (SITE_DOCS / part / marker).is_file()


def route_exists(route: str) -> bool:
    if route.endswith(".html"):
        route = route[: -len(".html")]
    if route.startswith("/design/"):
        return route in design_routes()
    # TypeScript 实际声明内容决定的去重属性页只接受构建后确实存在的文件。
    if re.fullmatch(r"/components/yue/api/native-[0-9a-f]{12}", route):
        return site_page_exists(SITE_DOCS / route.lstrip("/"))
    if route.startswith(("/cli/", "/components/")):
        return route in generated_routes()
    for part in ("sdk/kun", "sdk/api"):
        if route.startswith(f"/{part}/") and not mirror_generated(part):
            rel = route.strip("/")
            if any(rel == h.rsplit(".", 1)[0] for h in SITE_HANDWRITTEN):
                return site_page_exists(SITE_DOCS / rel)
            return True  # 还没生成：交给 Rspress 构建时的断链检查
    rel = route.strip("/")
    if rel == "":
        return site_page_exists(SITE_DOCS / "index")
    if f"{rel}.md" in SITE_GENERATED_FILES and not (SITE_DOCS / f"{rel}.md").is_file():
        return True  # 还没生成：交给 Rspress 构建时的断链检查
    return site_page_exists(SITE_DOCS / rel) or (SITE_DOCS / "public" / rel).is_file()


def link_exists(path: Path, target: str) -> bool:
    if not is_site_page(path):
        return (path.parent / target).resolve().exists()
    if target.startswith("/"):
        return route_exists(target)
    return site_page_exists((path.parent / target).resolve())


# ---------------------------------------------------------------- frontmatter（docs-site.md §4，S1-DOCS-003）
# 文档站每页的 frontmatter 按规范校验：必填字段、枚举、长度与日期；镜像页另要 source（Kun 小节还要 version）。
# 只解析生成器与手写页实际用到的 YAML 子集：`key: 标量`、`key: [a, b]`、`key:` + 缩进的 `- 项` 列表、双引号（JSON）与单引号字符串、# 注释。
# 其它写法（嵌套映射、多行标量、锚点）一律报「无法解析」，提示改成上述形式。
FM_PARTS = ("start", "components", "sdk", "cli", "ops", "marketplace", "design")
FM_PROJECTS = ("qian", "kun", "yue")
FM_AUDIENCES = ("developer", "operator", "isv", "agent", "decision-maker")
FM_SURFACES = ("skill", "agent", "command", "hook", "mcp", "tool", "ui")
FM_STAGES = ("S1", "S2", "S3", "S4")
FM_REQUIRED = ("title", "description", "part", "project", "audience", "updated")
FM_DESCRIPTION_MAX = 120
FM_KEY = re.compile(r"^([A-Za-z_][\w-]*):(?:\s+(.*))?$")
FM_DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
# SemVer（含预发布）与 Kun 的 CalVer（2026.10.8）都接受
FM_VERSION = re.compile(r"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$")
# <owner>/<repo>@<ref>:<path>，如 idadawn/kun@v2026.10.8:docs/guides/x.md、idadawn/qian@main:docs/adr/ADR-001-kun-relationship.md
FM_SOURCE = re.compile(r"^[\w.-]+/[\w.-]+@[^\s:@]+:\S+$")


class FrontmatterError(ValueError):
    pass


def _fm_scalar(raw: str) -> str:
    raw = raw.strip()
    if raw.startswith('"'):
        import json
        try:
            value = json.loads(raw)
        except ValueError as e:
            raise FrontmatterError(f"双引号字符串不合法：{raw}") from e
        if not isinstance(value, str):
            raise FrontmatterError(f"双引号里只能是字符串：{raw}")
        return value
    if raw.startswith("'"):
        if not raw.endswith("'") or len(raw) < 2:
            raise FrontmatterError(f"单引号字符串不合法：{raw}")
        return raw[1:-1].replace("''", "'")
    if raw.startswith(("{", "&", "*", "|", ">")):
        raise FrontmatterError(f"不支持的写法：{raw}")
    return raw


def _fm_inline_list(raw: str) -> list[str]:
    inner = raw.strip()[1:-1].strip()
    if not inner:
        return []
    items: list[str] = []
    buf = ""
    quote: str | None = None
    for ch in inner:
        if quote:
            buf += ch
            if ch == quote:
                quote = None
        elif ch in "\"'":
            quote = ch
            buf += ch
        elif ch == ",":
            items.append(_fm_scalar(buf))
            buf = ""
        else:
            buf += ch
    items.append(_fm_scalar(buf))
    return [i for i in items if i != ""]


def parse_frontmatter(text: str) -> dict[str, str | list[str]] | None:
    """返回 frontmatter 的键值（标量为 str，列表为 list[str]）；页面没有 frontmatter 时返回 None；写法不支持时抛 FrontmatterError。"""
    m = re.match(r"^---\n(.*?)\n---(?:\n|$)", text, re.S)
    if not m:
        return None
    data: dict[str, str | list[str]] = {}
    lines = m.group(1).split("\n")
    i = 0
    while i < len(lines):
        line = lines[i]
        i += 1
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if line[0] in " \t":
            raise FrontmatterError(f"意外的缩进行：{line.strip()}")
        km = FM_KEY.match(line)
        if not km:
            raise FrontmatterError(f"不是 key: value 形式：{line}")
        key, raw = km.group(1), (km.group(2) or "").strip()
        if key in data:
            raise FrontmatterError(f"字段重复：{key}")
        if raw.startswith("#"):
            raw = ""
        if raw == "":
            items: list[str] = []
            while i < len(lines) and lines[i].startswith((" ", "\t")) and lines[i].strip().startswith("- "):
                items.append(_fm_scalar(lines[i].strip()[2:]))
                i += 1
            if items:
                data[key] = items
            else:
                data[key] = ""
            continue
        if raw.startswith("["):
            if not raw.endswith("]"):
                raise FrontmatterError(f"列表要写在一行内：{key}")
            data[key] = _fm_inline_list(raw)
        else:
            data[key] = _fm_scalar(raw)
    return data


def _fm_page_part(rel: str) -> str | None:
    """页面所在的部分（website/docs 下的第一级目录）；根目录下的页面（首页、站点地图）记作 start。"""
    head = rel.split("/", 1)[0] if "/" in rel else ""
    return head if head in FM_PARTS else ("start" if head == "" else None)


def _valid_date(value: str) -> bool:
    if not FM_DATE.match(value):
        return False
    from datetime import date
    try:
        date.fromisoformat(value)
    except ValueError:
        return False
    return True


def check_frontmatter(path: Path, text: str) -> list[str]:
    """文档站页面的 frontmatter 校验（docs-site.md §4）。非站点页面不检查。"""
    if not is_site_page(path):
        return []
    rel = path.relative_to(SITE_DOCS).as_posix()
    label = path.relative_to(ROOT)
    try:
        data = parse_frontmatter(text)
    except FrontmatterError as e:
        return [f"{label}: frontmatter 无法解析（{e}）；只支持 key: 值、key: [a, b]、缩进的 - 列表与引号字符串"]
    if data is None:
        return [f"{label}: 缺少 frontmatter（必填 {', '.join(FM_REQUIRED)}）"]
    errs: list[str] = []

    def scalar(key: str) -> str | None:
        value = data.get(key)
        if value is None:
            return None
        if isinstance(value, list):
            errs.append(f"{label}: frontmatter 字段 {key} 应为单个值，不是列表")
            return None
        return value

    def listing(key: str) -> list[str] | None:
        value = data.get(key)
        if value is None:
            return None
        if not isinstance(value, list):
            errs.append(f"{label}: frontmatter 字段 {key} 应为列表，如 [developer, operator]")
            return None
        return value

    missing = [k for k in FM_REQUIRED if k not in data]
    if missing:
        errs.append(f"{label}: frontmatter 缺少必填字段 {', '.join(missing)}")

    title = scalar("title")
    if title is not None and not title.strip():
        errs.append(f"{label}: frontmatter 的 title 为空")
    description = scalar("description")
    if description is not None:
        if not description.strip():
            errs.append(f"{label}: frontmatter 的 description 为空")
        elif len(description) > FM_DESCRIPTION_MAX:
            errs.append(f"{label}: description 有 {len(description)} 字，超过 {FM_DESCRIPTION_MAX} 字")
    part = scalar("part")
    expected_part = _fm_page_part(rel)
    if part is not None:
        if part not in FM_PARTS:
            errs.append(f"{label}: part 取值 {part!r} 不在 {', '.join(FM_PARTS)} 之中")
        elif expected_part is not None and part != expected_part:
            errs.append(f"{label}: part 是 {part!r}，但页面在 {expected_part}/ 目录下")
    project = scalar("project")
    if project is not None and project not in FM_PROJECTS:
        errs.append(f"{label}: project 取值 {project!r} 不在 {', '.join(FM_PROJECTS)} 之中")
    audience = listing("audience")
    if audience is not None:
        if not audience:
            errs.append(f"{label}: audience 不能为空")
        for a in audience:
            if a not in FM_AUDIENCES:
                errs.append(f"{label}: audience 取值 {a!r} 不在 {', '.join(FM_AUDIENCES)} 之中")
    updated = scalar("updated")
    if updated is not None and not _valid_date(updated):
        errs.append(f"{label}: updated 应为 YYYY-MM-DD 的有效日期，现为 {updated!r}")

    stage = scalar("stage")
    if stage is not None and stage not in FM_STAGES:
        errs.append(f"{label}: stage 取值 {stage!r} 不在 {', '.join(FM_STAGES)} 之中")
    surface = scalar("surface")
    if surface is not None and surface not in FM_SURFACES:
        errs.append(f"{label}: surface 取值 {surface!r} 不在 {', '.join(FM_SURFACES)} 之中")
    version = scalar("version")
    if version is not None and not FM_VERSION.match(version):
        errs.append(f"{label}: version 应为 SemVer 或 CalVer，现为 {version!r}")
    source = scalar("source")
    if source is not None and not FM_SOURCE.match(source):
        errs.append(f"{label}: source 应为 <owner>/<repo>@<ref>:<path>，现为 {source!r}")
    listing("tags")
    component = scalar("component")
    if component is not None and not component.strip():
        errs.append(f"{label}: component 为空")

    # 条件必填：镜像页要 source（第 3 部分 Kun 小节、第 7 部分 docs/ 镜像）；Kun 镜像页还要 version 且只在 sdk/
    mirror = project == "kun" or part == "design"
    if mirror and "source" not in data:
        errs.append(f"{label}: 镜像页必须写 source（<owner>/<repo>@<ref>:<path>）")
    if project == "kun":
        if "version" not in data:
            errs.append(f"{label}: Kun 镜像页必须写 version（等于 KunVersion）")
        if part is not None and part != "sdk":
            errs.append(f"{label}: Kun 镜像页只在 sdk/ 下（part: sdk）")
    # 组件页（components/platform/<id>、components/yue/<name>）要 component
    if re.match(r"^components/(platform|yue)/(?!index\.)[^/]+\.(md|mdx)$", rel) and "component" not in data:
        errs.append(f"{label}: 组件页必须写 component（组件 id）")
    return errs


def check(path: Path) -> list[str]:
    errs: list[str] = []
    text = path.read_text(encoding="utf-8")
    rel = path.relative_to(ROOT)
    errs += check_frontmatter(path, text)
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
    errs: list[str] = [f"{f}: 生成目录里的文件不能进 Git（生成目录不接受手改，改它的源）" for f in tracked_generated()]
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
