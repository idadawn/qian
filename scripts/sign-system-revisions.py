#!/usr/bin/env python3
"""系统组件修订的签名清单（S1-DEP-011，R151 ①）。

  python3 scripts/sign-system-revisions.py sign --key <私钥.pem> (--image <镜像引用> | --plugins <目录>) [--out <文件>]
  python3 scripts/sign-system-revisions.py verify --public-key <公钥.pem> --signatures <文件> (--image <镜像引用> | --plugins <目录>)

开户编排第 03 步要求宿主预装的每个 system 级组件在 Lifecycle 里有已激活的修订：Ed25519 签名、
摘要等于镜像里组件文件夹的制品。staging 没有发布链（S1-DEP-005），由本脚本在构建机上签名：

sign：从镜像（docker create + docker cp 取出 /app/qian/plugins）或本地目录读组件文件夹，
  选 policy.tier 为 system、runtime.form 为 module、不带 x-skeleton 的组件，按 D4 目录摘要算法
  （与平台宿主的预装绑定、Lifecycle 核对制品同一算法）算摘要，用 Ed25519 私钥签摘要的 32 字节
  原始值，输出签名清单 JSON。私钥只在构建机，不进集群；签名清单是公开数据，进部署仓库的 ConfigMap，
  由平台宿主的 --install-system-revisions Job 读取。
verify：用公钥重算并核对签名清单，部署前自查用；有任何一项对不上即退出码 1。

私钥生成（只做一次，权限 0600，保存在构建机）：
  openssl genpkey -algorithm ed25519 -out ~/.qian/staging-system-revisions.pem
公钥（写进 Lifecycle 信任根的 Secret）：
  openssl pkey -in ~/.qian/staging-system-revisions.pem -pubout
"""
from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

FORMAT = "qian.system-revisions.signatures/v1"
MANIFEST = ".qian-plugin/plugin.json"
# 不属于制品的构建产物与依赖目录：与平台宿主 PlatformRuntime.Artifact.Read 的排除口径相同。
EXCLUDED = {"bin", "obj", "node_modules", "dist"}
IMAGE_PLUGINS = "/app/qian/plugins"


def fail(message: str) -> None:
    print("sign-system-revisions: " + message, file=sys.stderr)
    sys.exit(1)


def crypto():
    try:
        from cryptography.hazmat.primitives import serialization
        from cryptography.hazmat.primitives.asymmetric import ed25519
    except ImportError:
        fail("需要 Python 包 cryptography（pip install cryptography）。")
    return serialization, ed25519


def digest(folder: Path) -> str:
    """D4 目录摘要：按序数路径排序的「路径\\t字节数\\tSHA-256」清单（UTF-8、LF）再取 SHA-256。"""
    files = {}
    for root, dirs, names in os.walk(folder):
        dirs[:] = [name for name in dirs if name not in EXCLUDED]
        for name in names:
            path = Path(root, name)
            relative = path.relative_to(folder).as_posix()
            if any(part in EXCLUDED for part in relative.split("/")):
                continue
            files[relative] = path.read_bytes()
    # .NET 的 StringComparer.Ordinal 按 UTF-16 码元比较；用 UTF-16BE 字节序排序与之一致（含 BMP 之外的字符）。
    inventory = "".join(
        f"{key}\t{len(files[key])}\t{hashlib.sha256(files[key]).hexdigest()}\n"
        for key in sorted(files, key=lambda item: item.encode("utf-16-be")))
    return "sha256:" + hashlib.sha256(inventory.encode("utf-8")).hexdigest()


def components(plugins: Path) -> list[tuple[str, str, str]]:
    """返回 (组件标识, 版本, 摘要)：system 级、module 形态、非骨架的组件。"""
    result = []
    for folder in sorted(path for path in plugins.iterdir() if path.is_dir()):
        manifest_path = folder / MANIFEST
        if not manifest_path.is_file():
            continue
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        policy = manifest.get("policy") or {}
        runtime = manifest.get("runtime") or {}
        if policy.get("tier") != "system" or runtime.get("form") != "module" or manifest.get("x-skeleton") is True:
            continue
        result.append((manifest["id"], manifest["version"], digest(folder)))
    if not result:
        fail(f"{plugins} 里没有 system 级、module 形态的非骨架组件。")
    return sorted(result)


def plugins_from(args, scratch: Path) -> tuple[Path, str]:
    if args.plugins:
        folder = Path(args.plugins).resolve()
        if not folder.is_dir():
            fail(f"目录不存在：{folder}")
        return folder, "dir:" + str(folder)
    # 从镜像取组件文件夹：docker create 不启动容器（运行镜像没有 shell 也可以），docker cp 复制出来后删除容器。
    created = subprocess.run(["docker", "create", args.image], capture_output=True, text=True)
    if created.returncode != 0:
        fail("docker create 失败：" + created.stderr.strip())
    container = created.stdout.strip()
    try:
        copied = subprocess.run(["docker", "cp", f"{container}:{IMAGE_PLUGINS}", str(scratch / "plugins")], capture_output=True, text=True)
        if copied.returncode != 0:
            fail("docker cp 失败：" + copied.stderr.strip())
    finally:
        subprocess.run(["docker", "rm", container], capture_output=True, text=True)
    return scratch / "plugins", args.image


def key_id(public_key, serialization) -> str:
    spki = public_key.public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)
    return "sha256:" + hashlib.sha256(spki).hexdigest()


def sign(args) -> None:
    serialization, ed25519 = crypto()
    private_key = serialization.load_pem_private_key(Path(args.key).read_bytes(), password=None)
    if not isinstance(private_key, ed25519.Ed25519PrivateKey):
        fail("私钥必须是 Ed25519（openssl genpkey -algorithm ed25519）。")
    with tempfile.TemporaryDirectory(prefix="qian-sign-") as scratch:
        plugins, source = plugins_from(args, Path(scratch))
        artifacts = [
            {"plugin": plugin, "version": version, "digest": value,
             "signature": base64.b64encode(private_key.sign(bytes.fromhex(value[len("sha256:"):]))).decode("ascii")}
            for plugin, version, value in components(plugins)]
    document = {"format": FORMAT, "keyId": key_id(private_key.public_key(), serialization), "source": source, "artifacts": artifacts}
    text = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
    if args.out:
        Path(args.out).write_text(text, encoding="utf-8")
        print(f"已签名 {len(artifacts)} 个组件，写入 {args.out}（keyId {document['keyId']}）。", file=sys.stderr)
    else:
        sys.stdout.write(text)


def verify(args) -> None:
    serialization, ed25519 = crypto()
    public_key = serialization.load_pem_public_key(Path(args.public_key).read_bytes())
    if not isinstance(public_key, ed25519.Ed25519PublicKey):
        fail("公钥必须是 Ed25519。")
    document = json.loads(Path(args.signatures).read_text(encoding="utf-8"))
    if document.get("format") != FORMAT:
        fail(f"签名清单的 format 不是 {FORMAT}。")
    if document.get("keyId") != key_id(public_key, serialization):
        fail("签名清单的 keyId 与公钥指纹不一致。")
    signed = {(item["plugin"], item["version"]): item for item in document.get("artifacts", [])}
    problems = []
    with tempfile.TemporaryDirectory(prefix="qian-verify-") as scratch:
        plugins, _ = plugins_from(args, Path(scratch))
        for plugin, version, value in components(plugins):
            item = signed.get((plugin, version))
            if item is None:
                problems.append(f"{plugin}@{version} 没有签名")
                continue
            if item["digest"] != value:
                problems.append(f"{plugin}@{version} 的摘要不一致（清单 {item['digest']}，实际 {value}）")
                continue
            try:
                public_key.verify(base64.b64decode(item["signature"]), bytes.fromhex(value[len("sha256:"):]))
            except Exception:  # cryptography 的 InvalidSignature
                problems.append(f"{plugin}@{version} 验签失败")
    if problems:
        fail("签名清单与组件文件夹对不上：" + "；".join(problems))
    print(f"签名清单核对通过：{len(signed)} 项。", file=sys.stderr)


def main() -> None:
    parser = argparse.ArgumentParser(description="系统组件修订的签名清单（S1-DEP-011，R151）")
    commands = parser.add_subparsers(dest="command", required=True)
    for name in ("sign", "verify"):
        command = commands.add_parser(name)
        source = command.add_mutually_exclusive_group(required=True)
        source.add_argument("--image", help="平台镜像引用（建议按 digest），从中取出 /app/qian/plugins")
        source.add_argument("--plugins", help="本地组件目录（如仓库的 plugins/），本机自查用")
        if name == "sign":
            command.add_argument("--key", required=True, help="Ed25519 私钥 PEM（只在构建机）")
            command.add_argument("--out", help="输出文件，缺省写到标准输出")
        else:
            command.add_argument("--public-key", required=True, help="Ed25519 公钥 PEM")
            command.add_argument("--signatures", required=True, help="签名清单 JSON")
    args = parser.parse_args()
    sign(args) if args.command == "sign" else verify(args)


if __name__ == "__main__":
    main()
