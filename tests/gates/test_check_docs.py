"""文档门禁（scripts/check_docs.py）对文档站内容的自测（S1-DOCS-001）。

运行：python3 -m unittest discover -s tests/gates -v
在临时目录里搭一个最小仓库（docs/、website/docs、生成器源码），断言站内路由、镜像路由、生成页路由的解析，
以及生成目录被跳过、禁用词在 .mdx 与站点源码里同样生效。只依赖标准库。
"""
from __future__ import annotations

import importlib.util
import shutil
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
        return check_docs.check(page)

    def test_existing_routes_pass(self) -> None:
        good = [
            "/", "/start/", "/start/hosts", "/start/hosts.html", "/start/hosts#anchor", "./hosts", "hosts.md",
            "../index.mdx", "/logo.svg", "/design/", "/design/adr/", "/design/adr/adr-001-kun",
            "/cli/", "/cli/commands", "/components/platform/qian.files", "/components/yue/",
            "https://example.com/x", "#local",
        ]
        self.assertEqual(self.errors_for(good), [])

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


if __name__ == "__main__":
    unittest.main()
