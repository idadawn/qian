"""builder.py 的自测（S1-PM-005），只用标准库，docs-and-pm 工作流也会跑：

    python3 -m unittest discover -s deploy/pm-board -p 'test_*.py' -v

每个用例在临时目录里搭出 git-sync 的布局（/git/.worktrees/<提交> + /git/current 链接），
检出里放一个假的 scripts/pm.py，按检出里的 mode 文件模拟三种情形：正常生成、任务文件校验不过、
生成途中 git-sync 同步到新提交（先改指 current，再删旧检出）。
"""
from __future__ import annotations

import contextlib
import io
import os
import shutil
import tempfile
import unittest
from unittest import mock

import builder

FAKE_PM = '''\
import os, pathlib, shutil, sys

checkout = pathlib.Path(__file__).resolve().parents[1]
out = pathlib.Path(sys.argv[sys.argv.index("--out") + 1])
mode = (checkout / "mode").read_text(encoding="utf-8").strip()
if mode == "invalid":
    sys.exit("校验失败：S1-XX-001 缺少「验收」小节")
if mode == "switch":
    # 与 git-sync 相同的顺序：先把 current 改指新提交，再删掉旧检出；本次生成随后读不到任务文件
    link = os.environ["BOARD_TEST_LINK"]
    os.symlink(os.environ["BOARD_TEST_NEXT"], link + ".tmp")
    os.replace(link + ".tmp", link)
    shutil.rmtree(checkout)
    list((checkout / "pm" / "tasks").iterdir())
out.mkdir(parents=True)
for name in ("index.html", "tasks.json", "STATUS.md"):
    (out / name).write_text(checkout.name, encoding="utf-8")
print(f"已生成 tasks.json, index.html, STATUS.md 到 {out}（3 个任务）")
'''

A, B, C = "a" * 40, "b" * 40, "c" * 40


class BuilderTest(unittest.TestCase):
    def setUp(self) -> None:
        # macOS 的临时目录经过 /var → /private/var 链接，builder 比较的是 realpath
        root = os.path.realpath(tempfile.mkdtemp(prefix="pm-board-builder-"))
        self.addCleanup(shutil.rmtree, root, True)
        self.git = os.path.join(root, "git")
        self.site = os.path.join(root, "site")
        self.link = os.path.join(self.git, "current")
        os.makedirs(os.path.join(self.git, ".worktrees"))
        os.makedirs(self.site)
        for patcher in (
            mock.patch.object(builder, "GIT_LINK", self.link),
            mock.patch.object(builder, "SITE", self.site),
            mock.patch.dict(os.environ, {"BOARD_TEST_LINK": self.link}),
        ):
            patcher.start()
            self.addCleanup(patcher.stop)

    def checkout(self, rev: str, mode: str = "ok") -> str:
        path = os.path.join(self.git, ".worktrees", rev)
        os.makedirs(os.path.join(path, "scripts"))
        os.makedirs(os.path.join(path, "pm", "tasks"))
        with open(os.path.join(path, "scripts", "pm.py"), "w", encoding="utf-8") as f:
            f.write(FAKE_PM)
        with open(os.path.join(path, "mode"), "w", encoding="utf-8") as f:
            f.write(mode)
        return path

    def point(self, checkout: str) -> None:
        """像 git-sync 一样原子地把 /git/current 改指某个检出。"""
        os.symlink(checkout, self.link + ".tmp")
        os.replace(self.link + ".tmp", self.link)

    def check(self, seen: str | None) -> tuple[str | None, str]:
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            seen = builder.check(seen)
        return seen, out.getvalue()

    def served(self) -> str:
        return os.readlink(os.path.join(self.site, "current"))

    def published(self) -> str | None:
        a = self.checkout(A)
        self.point(a)
        seen, log = self.check(None)
        self.assertIn(f"已上线 {A[:9]}（3 个任务）", log)
        return seen

    def test_does_nothing_before_first_sync(self) -> None:
        """git-sync 还没同步完第一次时不生成。"""
        self.assertEqual(self.check(None), (None, ""))
        self.assertEqual(os.listdir(self.site), [])

    def test_new_commit_is_published_and_old_build_removed(self) -> None:
        """新提交生成后原子切换 /site/current，删掉旧版；同一提交不再生成。"""
        seen = self.published()
        self.assertEqual(self.served(), A)
        with open(os.path.join(self.site, "current", "tasks.json"), encoding="utf-8") as f:
            self.assertEqual(f.read(), A)

        self.point(self.checkout(B))
        seen, log = self.check(seen)
        self.assertIn(f"已上线 {B[:9]}", log)
        self.assertEqual(self.served(), B)
        self.assertEqual(sorted(os.listdir(self.site)), sorted([B, "current"]))

        # 同一提交不再生成
        self.assertEqual(self.check(seen), (seen, ""))

    def test_invalid_tasks_log_failure_and_keep_previous_board(self) -> None:
        """任务文件校验不过：记「生成失败」，继续提供上一版，同一提交不再重试。"""
        seen = self.published()
        b = self.checkout(B, "invalid")
        self.point(b)
        seen, log = self.check(seen)
        self.assertIn(f"生成失败 {B[:9]}，继续提供上一版：校验失败：S1-XX-001 缺少「验收」小节", log)
        self.assertEqual(self.served(), A)
        self.assertEqual(sorted(os.listdir(self.site)), sorted([A, "current"]))
        # 同一提交不再重试
        self.assertEqual(self.check(seen), (b, ""))

    def test_commit_switch_during_build_is_skipped_not_failed(self) -> None:
        """生成途中 git-sync 切到新提交并删掉旧检出：只记跳过，不记失败，下一轮生成新提交。"""
        seen = self.published()
        c = self.checkout(C)
        b = self.checkout(B, "switch")
        self.point(b)
        with mock.patch.dict(os.environ, {"BOARD_TEST_NEXT": c}):
            seen, log = self.check(seen)
        self.assertFalse(os.path.exists(b), "假 pm.py 应像 git-sync 一样删掉旧检出")
        self.assertIn(f"生成 {B[:9]} 途中 git-sync 已切到 {C[:9]}，跳过这一版", log)
        self.assertNotIn("生成失败", log)
        self.assertEqual(self.served(), A)
        self.assertEqual(sorted(os.listdir(self.site)), sorted([A, "current"]))

        seen, log = self.check(seen)
        self.assertEqual(seen, c)
        self.assertIn(f"已上线 {C[:9]}", log)
        self.assertEqual(self.served(), C)


if __name__ == "__main__":
    unittest.main()
