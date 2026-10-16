#!/usr/bin/env python3
"""下载镜像供应链工具（syft / grype / cosign）的锁定版本，校验摘要后只提取工具本身，不执行远程安装脚本。

  python3 scripts/prepare_image_tools.py --destination <目录>

版本与各平台归档的 sha256 见同目录 image-tools.lock.json；升级时三处一起改：版本、归档名、摘要。
"""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import platform
import tarfile
import urllib.request

LIMIT = 200 * 1024 * 1024


def target():
    system, machine = platform.system().lower(), platform.machine().lower()
    arch = "arm64" if machine in ("arm64", "aarch64") else "amd64" if machine in ("amd64", "x86_64") else None
    if system not in ("linux", "darwin") or arch is None:
        raise SystemExit(f"未支持的镜像工具平台：{system}/{machine}")
    return f"{system}_{arch}"


def install(destination, name, spec, platform_key):
    if platform_key not in spec["assets"]:
        raise SystemExit(f"{name} 没有 {platform_key} 的锁定归档。")
    asset = spec["assets"][platform_key]
    url = spec["url"].format(version=spec["version"], name=asset["name"])
    with urllib.request.urlopen(url, timeout=120) as response:
        content = response.read(LIMIT + 1)
    if len(content) > LIMIT or hashlib.sha256(content).hexdigest() != asset["sha256"]:
        raise SystemExit(f"{name} 归档大小或摘要不符合锁文件。")
    binary = spec["binary"]
    if asset["name"].endswith(".tar.gz"):
        with tarfile.open(fileobj=io.BytesIO(content), mode="r:gz") as archive:
            member = archive.getmember(binary)
            if not member.isfile():
                raise SystemExit(f"{name} 归档里的 {binary} 必须是普通文件。")
            content = archive.extractfile(member).read()
    temporary = destination / (binary + ".tmp")
    temporary.write_bytes(content)
    temporary.chmod(0o755)
    os.replace(temporary, destination / binary)
    print(destination.resolve() / binary)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--destination", type=Path, required=True)
    args = parser.parse_args()
    lock = json.loads(Path(__file__).with_name("image-tools.lock.json").read_text(encoding="utf-8"))
    args.destination.mkdir(parents=True, exist_ok=True)
    platform_key = target()
    for name, spec in lock.items():
        install(args.destination, name, spec, platform_key)


if __name__ == "__main__":
    main()
