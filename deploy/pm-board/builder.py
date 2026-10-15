"""看板 Pod 的构建容器（S1-PM-004）：按 git-sync 同步到的提交生成看板，交给 nginx。

git-sync 把仓库检出到 /git/.worktrees/<提交>，再把 /git/current 指向它。本循环每隔几秒看一次
/git/current：换了提交，就用该提交自己的 scripts/pm.py 把看板生成到 /site/<提交>，
再把 /site/current 原子地改指向它，最后删掉旧的生成目录。nginx 只读 /site/current。

生成失败（任务文件校验不过等）时保留上一版看板，记一条日志，同一提交不再重试。
只用 Python 标准库；加 --once 只检查一次就退出，供本机验证。
"""
from __future__ import annotations

import os
import re
import shutil
import subprocess
import sys
import time

GIT_LINK = os.environ.get("BOARD_GIT_LINK", "/git/current")
SITE = os.environ.get("BOARD_SITE", "/site")
PERIOD = float(os.environ.get("BOARD_PERIOD", "5"))


def log(message: str) -> None:
    print(time.strftime("%Y-%m-%dT%H:%M:%S%z"), message, flush=True)


def publish(checkout: str) -> bool:
    rev = os.path.basename(checkout)
    target = os.path.join(SITE, rev)
    staging = target + ".tmp"
    shutil.rmtree(staging, ignore_errors=True)
    result = subprocess.run(
        [sys.executable, os.path.join(checkout, "scripts", "pm.py"), "build", "--out", staging],
        capture_output=True, text=True)
    if result.returncode != 0:
        shutil.rmtree(staging, ignore_errors=True)
        log(f"生成失败 {rev[:9]}，继续提供上一版：{(result.stdout + result.stderr).strip()}")
        return False
    shutil.rmtree(target, ignore_errors=True)
    os.rename(staging, target)
    # 先建临时链接再 rename 覆盖，nginx 任何时刻看到的都是完整的一版。
    link = os.path.join(SITE, "current.tmp")
    if os.path.lexists(link):
        os.remove(link)
    os.symlink(rev, link)
    os.replace(link, os.path.join(SITE, "current"))
    for name in os.listdir(SITE):
        path = os.path.join(SITE, name)
        if name != rev and not os.path.islink(path) and os.path.isdir(path):
            shutil.rmtree(path, ignore_errors=True)
    count = re.search(r"（(\d+) 个任务）", result.stdout)
    log(f"已上线 {rev[:9]}" + (f"（{count.group(1)} 个任务）" if count else ""))
    return True


def main() -> None:
    once = "--once" in sys.argv[1:]
    seen = None
    log(f"开始：{GIT_LINK} → {SITE}，每 {PERIOD:g} 秒检查一次")
    while True:
        checkout = os.path.realpath(GIT_LINK) if os.path.lexists(GIT_LINK) else None
        if checkout and checkout != seen and os.path.isfile(os.path.join(checkout, "scripts", "pm.py")):
            publish(checkout)
            seen = checkout
        if once:
            return
        time.sleep(PERIOD)


if __name__ == "__main__":
    main()
