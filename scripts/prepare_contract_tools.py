#!/usr/bin/env python3
"""下载锁定版本并校验归档摘要，只提取检查器自身，不执行远程安装脚本。"""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import platform
import tarfile
import urllib.request


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--destination", type=Path, required=True)
    args = parser.parse_args()
    lock = json.loads(Path(__file__).with_name("contract-tools.lock.json").read_text())
    system, machine = platform.system().lower(), platform.machine().lower()
    arch = "arm64" if machine in ("arm64", "aarch64") else "amd64" if machine in ("amd64", "x86_64") else None
    target = "darwin_all" if system == "darwin" and arch else f"{system}_{arch}"
    if target not in lock["assets"]:
        raise SystemExit(f"未支持的契约检查器平台：{system}/{machine}")
    asset = lock["assets"][target]
    url = f"https://github.com/oasdiff/oasdiff/releases/download/v{lock['version']}/{asset['name']}"
    with urllib.request.urlopen(url, timeout=60) as response:
        content = response.read(100 * 1024 * 1024 + 1)
    if len(content) > 100 * 1024 * 1024 or hashlib.sha256(content).hexdigest() != asset["sha256"]:
        raise SystemExit("契约检查器归档大小或摘要不符合锁文件。")
    name = "oasdiff.exe" if system == "windows" else "oasdiff"
    with tarfile.open(fileobj=io.BytesIO(content), mode="r:gz") as archive:
        member = archive.getmember(name)
        if not member.isfile():
            raise SystemExit("契约检查器必须是普通文件。")
        binary = archive.extractfile(member).read()
    args.destination.mkdir(parents=True, exist_ok=True)
    temporary = args.destination / (name + ".tmp")
    temporary.write_bytes(binary)
    temporary.chmod(0o755)
    os.replace(temporary, args.destination / name)
    print(args.destination.resolve() / name)
    install_buf(args.destination, system, arch)


def install_buf(destination, system, arch):
    lock = json.loads(Path(__file__).with_name("proto-tools.lock.json").read_text())
    target = f"{system}_{arch}"
    if target not in lock["assets"]:
        raise SystemExit(f"Unsupported proto checker platform: {target}")
    asset = lock["assets"][target]
    url = f"https://github.com/bufbuild/buf/releases/download/v{lock['version']}/{asset['name']}"
    with urllib.request.urlopen(url, timeout=60) as response:
        binary = response.read(100 * 1024 * 1024 + 1)
    if len(binary) > 100 * 1024 * 1024 or hashlib.sha256(binary).hexdigest() != asset["sha256"]:
        raise SystemExit("Proto checker size or digest does not match the lock file.")
    name = "buf.exe" if system == "windows" else "buf"
    temporary = destination / (name + ".tmp")
    temporary.write_bytes(binary)
    temporary.chmod(0o755)
    os.replace(temporary, destination / name)
    print(destination.resolve() / name)


if __name__ == "__main__":
    main()
