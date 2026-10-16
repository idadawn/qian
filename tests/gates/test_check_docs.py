"""文档门禁（scripts/check_docs.py）对文档站内容的自测（S1-DOCS-001）。

运行：python3 -m unittest discover -s tests/gates -v
在临时目录里搭一个最小仓库（docs/、website/docs、生成器源码），断言站内路由、镜像路由、生成页路由的解析，
以及生成目录被跳过、禁用词在 .mdx 与站点源码里同样生效；S1-DOCS-005 起另测 Kun 小节（镜像页、延后解析的路由、
进了 Git 的生成文件）与禁用词的整词匹配。只依赖标准库。
"""
from __future__ import annotations

import importlib.util
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
_spec = importlib.util.spec_from_file_location("check_docs", ROOT / "scripts" / "check_docs.py")
assert _spec and _spec.loader
check_docs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(check_docs)

BANNED_WORD = "xi" + "ng"  # 拆开写，免得本文件自己被门禁拦下


def write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


class SiteContentTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp(prefix="qian-check-docs-"))
        write(self.tmp / "docs/README.md", "# 索引\n")
        write(self.tmp / "docs/adr/README.md", "# ADR\n")
        write(self.tmp / "docs/adr/ADR-001-kun.md", "# ADR-001\n")
        write(self.tmp / "website/scripts/sync-specs.ts", "{ section: 9, slug: 'commands' }\n")
        write(self.tmp / "website/scripts/collect-components.ts", "{ id: 'qian.files' }\n")
        site = self.tmp / "website/docs"
        write(site / "index.mdx", "# 首页\n")
        write(site / "start/index.md", "# 开始\n")
        write(site / "start/hosts.md", "# 宿主\n")
        write(site / "public/logo.svg", "<svg/>\n")
        self.saved = (check_docs.ROOT, check_docs.SITE_DOCS)
        check_docs.ROOT, check_docs.SITE_DOCS = self.tmp, site
        check_docs.design_routes.cache_clear()
        check_docs.generated_routes.cache_clear()

    def tearDown(self) -> None:
        check_docs.ROOT, check_docs.SITE_DOCS = self.saved
        check_docs.design_routes.cache_clear()
        check_docs.generated_routes.cache_clear()
        shutil.rmtree(self.tmp)

    def errors_for(self, links: list[str], name: str = "start/page.mdx") -> list[str]:
        page = self.tmp / "website/docs" / name
        write(page, "# 页\n\n" + "\n".join(f"- [x]({href})" for href in links) + "\n")
        return [e for e in check_docs.check(page) if "frontmatter" not in e]

    def test_existing_routes_pass(self) -> None:
        good = [
            "/", "/start/", "/start/hosts", "/start/hosts.html", "/start/hosts#anchor", "./hosts", "hosts.md",
            "../index.mdx", "/logo.svg", "/design/", "/design/adr/", "/design/adr/adr-001-kun",
            "/cli/", "/cli/commands", "/components/platform/qian.files", "/components/yue/",
            "https://example.com/x", "#local",
        ]
        self.assertEqual(self.errors_for(good), [])

    def test_yue_routes_come_from_catalog_and_api_files_must_exist(self) -> None:
        write(self.tmp / "packages/yue/core/docs/catalog.json", '{"Button": {}}')
        write(self.tmp / "website/docs/components/yue/api/native-123456abcdef.mdx", "# 属性")
        self.assertEqual(self.errors_for(["/components/yue/core/button", "/components/yue/tokens", "/components/yue/api/native-123456abcdef"]), [])
        for route in ["/components/yue/core/invented", "/components/yue/api/native-000000000000"]:
            self.assertTrue(self.errors_for([route]), route)

    def test_yue_package_routes_must_be_declared_by_collector(self) -> None:
        write(self.tmp / "website/scripts/collect-components.ts", "{ path: 'yue/form.mdx' }, { path: 'yue/data.md' }\n")
        self.assertEqual(self.errors_for(["/components/yue/form", "/components/yue/data"]), [])
        self.assertTrue(self.errors_for(["/components/yue/undeclared"]))

    def test_missing_routes_fail(self) -> None:
        bad = ["/start/missing", "./nope", "/design/adr/adr-999", "/design/adr/ADR-001-kun", "/cli/nope", "/components/platform/qian.nope"]
        errors = self.errors_for(bad)
        self.assertEqual(len(errors), len(bad), errors)
        for href in bad:
            self.assertTrue(any(href in e for e in errors), href)

    def test_generated_dirs_are_skipped(self) -> None:
        write(self.tmp / "website/docs/design/x.md", f"# {BANNED_WORD}\n")
        write(self.tmp / "website/docs/start/y.mdx", f"# {BANNED_WORD}\n")
        files = check_docs.md_files(["website/docs"])
        rels = {f.relative_to(self.tmp).as_posix() for f in files}
        self.assertNotIn("website/docs/design/x.md", rels)
        self.assertIn("website/docs/start/y.mdx", rels)
        self.assertTrue(any("禁用关键字" in e for e in check_docs.check(self.tmp / "website/docs/start/y.mdx")))

    def test_banned_words_in_site_source(self) -> None:
        source = self.tmp / "website/theme/Home.tsx"
        write(source, f"const title = '{BANNED_WORD}';\n")
        self.assertTrue(any("禁用关键字" in e for e in check_docs.check_source(source)))

    def test_banned_word_matches_whole_word_only(self) -> None:
        # Kun 的英文文档里有 prefixing、Mixing 这类单词，不能误判（S1-DOCS-005）
        for ok in ["prefi" + BANNED_WORD, "Mi" + BANNED_WORD + " it", "fi" + BANNED_WORD + "es"]:
            self.assertIsNone(check_docs.BANNED.search(ok), ok)
        for bad in [BANNED_WORD.capitalize(), BANNED_WORD + "项目", f"({BANNED_WORD})", f"仓库 {BANNED_WORD}/docs"]:
            self.assertIsNotNone(check_docs.BANNED.search(bad), bad)

    def test_kun_mirror_skipped_by_default_but_checked_explicitly(self) -> None:
        kun = self.tmp / "website/docs/sdk/kun"
        write(kun / "guides/x.md", f"# {BANNED_WORD}\n")
        write(kun / "with-qian.mdx", "# 与 Qian 的关系\n")
        write(self.tmp / "website/docs/sdk/api/kun/index.md", "# API\n")
        default = {f.relative_to(self.tmp).as_posix() for f in check_docs.md_files(["website/docs"])}
        self.assertNotIn("website/docs/sdk/kun/guides/x.md", default)
        self.assertNotIn("website/docs/sdk/api/kun/index.md", default)
        self.assertIn("website/docs/sdk/kun/with-qian.mdx", default)
        explicit = {f.relative_to(self.tmp).as_posix() for f in check_docs.md_files(["website/docs/sdk/kun"])}
        self.assertIn("website/docs/sdk/kun/guides/x.md", explicit)

    def test_kun_routes_deferred_until_generated(self) -> None:
        # 还没生成：Kun 小节与 API 参考的路由交给构建时的断链检查，唯一的手写页仍要真实存在
        self.assertEqual(self.errors_for(["/sdk/kun/guides/anything", "/sdk/api/kun/x/y"]), [])
        self.assertEqual(len(self.errors_for(["/sdk/kun/with-qian"])), 1)
        write(self.tmp / "website/docs/sdk/kun/with-qian.mdx", "# 与 Qian 的关系\n")
        self.assertEqual(self.errors_for(["/sdk/kun/with-qian"]), [])
        # 生成之后按磁盘上的页面解析
        write(self.tmp / "website/docs/sdk/kun/_meta.json", "[]\n")
        write(self.tmp / "website/docs/sdk/kun/guides/real.md", "# 指南\n")
        self.assertEqual(self.errors_for(["/sdk/kun/guides/real"]), [])
        self.assertEqual(len(self.errors_for(["/sdk/kun/guides/anything"])), 1)

    def test_generated_single_file_skipped_and_deferred(self) -> None:
        # 版本矩阵 start/versions.md 是生成文件：默认不查、显式点名就查；没生成时指向它的路由延后到构建时检查
        write(self.tmp / "website/docs/start/versions.md", f"# {BANNED_WORD}\n")
        default = {f.relative_to(self.tmp).as_posix() for f in check_docs.md_files(["website/docs"])}
        self.assertNotIn("website/docs/start/versions.md", default)
        explicit = {f.relative_to(self.tmp).as_posix() for f in check_docs.md_files(["website/docs/start/versions.md"])}
        self.assertIn("website/docs/start/versions.md", explicit)
        self.assertEqual(self.errors_for(["/start/versions"]), [])
        (self.tmp / "website/docs/start/versions.md").unlink()
        self.assertEqual(self.errors_for(["/start/versions"]), [])
        self.assertEqual(len(self.errors_for(["/start/version"])), 1)

    def test_tracked_generated_files_fail(self) -> None:
        if shutil.which("git") is None:
            self.skipTest("需要 git")
        write(self.tmp / "website/docs/sdk/kun/with-qian.mdx", "# 与 Qian 的关系\n")
        write(self.tmp / "website/docs/sdk/kun/guides/x.md", "# 手改的镜像页\n")
        write(self.tmp / "website/docs/start/versions.md", "# 手改的版本矩阵\n")
        subprocess.run(["git", "init", "-q", str(self.tmp)], check=True)
        subprocess.run(["git", "-C", str(self.tmp), "add", "-f", "website"], check=True)
        self.assertEqual(check_docs.tracked_generated(), ["website/docs/sdk/kun/guides/x.md", "website/docs/start/versions.md"])


FM_OK = (
    "---\ntitle: 宿主\ndescription: 一句话。\npart: start\nproject: qian\naudience: [developer, operator]\nupdated: 2026-10-09\n---\n\n# 宿主\n"
)


class FrontmatterTests(SiteContentTests):
    """文档站页面的 frontmatter 校验（docs-site.md §4，S1-DOCS-003）。"""

    def fm_errors(self, text: str, name: str = "start/page.mdx") -> list[str]:
        page = self.tmp / "website/docs" / name
        write(page, text)
        return check_docs.check_frontmatter(page, text)

    def test_complete_page_passes(self) -> None:
        self.assertEqual(self.fm_errors(FM_OK), [])
        # 首页与站点地图在根目录，part 记作 start；额外字段（pageType、outline）不报错
        root = FM_OK.replace("title: 宿主", "pageType: custom\noutline: false\ntitle: 首页")
        self.assertEqual(self.fm_errors(root, "index.mdx"), [])
        # 非站点页面不检查
        self.assertEqual(check_docs.check_frontmatter(self.tmp / "docs/README.md", "# 索引\n"), [])

    def test_parser_subset(self) -> None:
        data = check_docs.parse_frontmatter(
            "---\ntitle: \"带：冒号\"\ndescription: '单引号 it''s'\naudience:\n  - developer\n  - agent\ntags: []\nempty:\n# 注释\nstage: S2\n---\n正文\n"
        )
        self.assertEqual(data, {"title": "带：冒号", "description": "单引号 it's", "audience": ["developer", "agent"], "tags": [], "empty": "", "stage": "S2"})
        self.assertIsNone(check_docs.parse_frontmatter("# 没有 frontmatter\n"))
        for bad in ["---\ntitle: a\ntitle: b\n---\n", "---\nnested:\n  key: value\n---\n", "---\ntext: |\n  多行\n---\n", "---\n- 列表\n---\n"]:
            with self.assertRaises(check_docs.FrontmatterError, msg=bad):
                check_docs.parse_frontmatter(bad)
        self.assertTrue(any("无法解析" in e for e in self.fm_errors("---\nnested:\n  key: value\n---\n# x\n")))

    def test_required_fields_and_enums(self) -> None:
        self.assertTrue(any("缺少 frontmatter" in e for e in self.fm_errors("# 没有\n")))
        missing = self.fm_errors("---\ntitle: x\n---\n")
        self.assertTrue(any("缺少必填字段 description, part, project, audience, updated" in e for e in missing), missing)
        cases = {
            "part: start": ("part: ops", "但页面在 start/ 目录下"),
            "project: qian": ("project: yue2", "project 取值"),
            "audience: [developer, operator]": ("audience: [developer, boss]", "audience 取值"),
            "updated: 2026-10-09": ("updated: 2026-13-01", "updated 应为"),
            "description: 一句话。": ("description: " + "长" * 121, "超过 120 字"),
        }
        for old, (new, message) in cases.items():
            errors = self.fm_errors(FM_OK.replace(old, new))
            self.assertTrue(any(message in e for e in errors), (new, errors))
        extra = FM_OK.replace("updated: 2026-10-09", "updated: 2026-10-09\nstage: S5\nsurface: widget\nversion: v1\nsource: kun docs\naudience2: x")
        errors = self.fm_errors(extra)
        for message in ["stage 取值", "surface 取值", "version 应为", "source 应为"]:
            self.assertTrue(any(message in e for e in errors), (message, errors))
        self.assertTrue(any("audience 应为列表" in e for e in self.fm_errors(FM_OK.replace("audience: [developer, operator]", "audience: developer"))))
        self.assertTrue(any("audience 不能为空" in e for e in self.fm_errors(FM_OK.replace("audience: [developer, operator]", "audience: []"))))

    def test_mirror_and_component_pages(self) -> None:
        design = FM_OK.replace("part: start", "part: design")
        self.assertTrue(any("镜像页必须写 source" in e for e in self.fm_errors(design, "design/adr/adr-001.md")))
        self.assertEqual(self.fm_errors(design.replace("updated: 2026-10-09", "updated: 2026-10-09\nsource: idadawn/qian@main:docs/adr/ADR-001-kun.md"), "design/adr/adr-001.md"), [])
        kun = FM_OK.replace("part: start", "part: sdk").replace("project: qian", "project: kun")
        errors = self.fm_errors(kun, "sdk/kun/guides/x.md")
        self.assertTrue(any("必须写 source" in e for e in errors) and any("必须写 version" in e for e in errors), errors)
        ok = kun.replace("updated: 2026-10-09", "updated: 2026-10-09\nversion: \"2026.10.8\"\nsource: \"idadawn/kun@v2026.10.8:docs/guides/x.md\"")
        self.assertEqual(self.fm_errors(ok, "sdk/kun/guides/x.md"), [])
        self.assertTrue(any("只在 sdk/" in e for e in self.fm_errors(ok.replace("part: sdk", "part: ops"), "ops/x.md")))
        component = FM_OK.replace("part: start", "part: components")
        self.assertTrue(any("组件页必须写 component" in e for e in self.fm_errors(component, "components/platform/qian.files.md")))
        self.assertEqual(self.fm_errors(component, "components/platform/index.md"), [])
        self.assertEqual(self.fm_errors(component.replace("updated: 2026-10-09", "updated: 2026-10-09\ncomponent: qian.files"), "components/platform/qian.files.md"), [])

    def test_check_includes_frontmatter(self) -> None:
        page = self.tmp / "website/docs/start/bad.md"
        write(page, "# 没有 frontmatter\n")
        self.assertTrue(any("缺少 frontmatter" in e for e in check_docs.check(page)))


if __name__ == "__main__":
    unittest.main()
