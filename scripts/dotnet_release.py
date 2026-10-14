#!/usr/bin/env python3
"""Qian .NET 包族锁步发布的核对工具（S1-RT-014，ADR-019 D19.7、ADR-021）。只依赖标准库。

包族 = Qian.slnx 里 QianIsPackage=true 的全部项目（判定见 Directory.Build.targets，与 scripts/check_deps.py 同一口径）。
发布工作流 .github/workflows/dotnet-release.yml 依次调用：

  python3 scripts/dotnet_release.py check-version --version 0.1.0 [--feed URL --token-env NAME]
      SemVer 2.0 版本号；给了私有源时，包族里任何一个包在源里已有这个版本即失败（锁步版本只发一次）。
  python3 scripts/dotnet_release.py verify-packages --version 0.1.0 --dir <nupkg 目录>
      制品恰好是包族全部包、各一个且版本都是 0.1.0；包族内依赖的下限都钉在同一版本；
      源生成器包只带 analyzers/dotnet/cs 下的分析器且是开发依赖；许可证与仓库地址齐全。
  python3 scripts/dotnet_release.py verify-consumer --version 0.1.0 --source <URL 或目录> [--token-env NAME]
      在临时目录建一个干净的消费者项目，用独立的包缓存、按 packageSourceMapping 只从给定源还原 Qian.*，
      引用 Qian.Plugin.Sdk / Qian.Plugin.Sdk.Generators / Qian.Data 的精确版本；构建时包里的源生成器派生组件元数据，
      运行后核对派生结果，并核对还原出的每个 Qian.* 包都是同一版本、都来自给定源。

退出码：0 通过；1 核对不通过；2 工具出错。
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import re
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Dict, List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

import check_deps  # noqa: E402

# SemVer 2.0（semver.org 给出的正则），不带 v 前缀与构建元数据：NuGet 归一化时会丢掉 +metadata，同一版本只能发一次。
SEMVER = re.compile(
    r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)"
    r"(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?$")
GENERATORS = "Qian.Plugin.Sdk.Generators"
LICENSE = "Apache-2.0"
REPOSITORY = "https://github.com/idadawn/qian.git"
CONSUMER_PACKAGES = ("Qian.Plugin.Sdk", GENERATORS, "Qian.Data")


class Failure(Exception):
    """核对不通过。"""


def family(solution: Path) -> List[str]:
    """包族的包名，按名称排序。读项目属性，不需要已还原。"""
    projects = check_deps.solution_projects(solution)
    with ThreadPoolExecutor(max_workers=min(8, os.cpu_count() or 4)) as pool:
        props = list(pool.map(check_deps._properties, projects))
    return sorted((p["QianPackageId"] for p in props if p.get("QianIsPackage") == "true"), key=str.lower)


# ---------- 版本 ----------

def check_version(version: str, feed: Optional[str], token: Optional[str], packages: List[str]) -> List[str]:
    if not SEMVER.match(version):
        raise Failure(f"版本号不是 SemVer 2.0（不带 v 前缀与 +构建元数据）：{version}")
    if not feed:
        return []
    base = _flat_container(feed, token)
    existing = []
    for package in packages:
        versions = _json(f"{base.rstrip('/')}/{package.lower()}/index.json", token, missing_ok=True)
        if versions and version.lower() in [v.lower() for v in versions.get("versions", [])]:
            existing.append(package)
    if existing:
        raise Failure(f"私有源里已有 {version} 的包：{', '.join(existing)}。锁步版本只发一次，请换新版本号。")
    return packages


def _user() -> str:
    # GitHub Packages 以令牌认证；用户名取触发发布的账号，本机核对时用占位名。
    return os.environ.get("GITHUB_ACTOR") or "qian-release"


def _flat_container(feed: str, token: Optional[str]) -> str:
    index = _json(feed, token)
    for resource in index.get("resources", []):
        if str(resource.get("@type", "")).startswith("PackageBaseAddress/3.0.0"):
            return resource["@id"]
    raise Failure(f"包源索引里没有 PackageBaseAddress：{feed}")


def _json(url: str, token: Optional[str], missing_ok: bool = False):
    request = urllib.request.Request(url)
    if token:
        request.add_header("Authorization", "Basic " + base64.b64encode(f"{_user()}:{token}".encode()).decode())
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        if missing_ok and error.code == 404:
            return None
        raise Failure(f"读取包源失败：{url} → HTTP {error.code}") from error


# ---------- 制品 ----------

def read_nuspec(nupkg: Path) -> Tuple[dict, List[str]]:
    with zipfile.ZipFile(nupkg) as archive:
        names = archive.namelist()
        specs = [n for n in names if "/" not in n and n.endswith(".nuspec")]
        if len(specs) != 1:
            raise Failure(f"{nupkg.name} 里应恰有一个 .nuspec")
        root = ET.fromstring(archive.read(specs[0]))
    metadata = next(e for e in root if _local(e.tag) == "metadata")

    def text(name: str) -> Optional[str]:
        node = next((e for e in metadata if _local(e.tag) == name), None)
        return node.text.strip() if node is not None and node.text else None

    license_node = next((e for e in metadata if _local(e.tag) == "license"), None)
    repository = next((e for e in metadata if _local(e.tag) == "repository"), None)
    dependencies: Dict[str, str] = {}
    for node in metadata.iter():
        if _local(node.tag) == "dependency":
            dependencies[node.get("id")] = node.get("version", "")
    return {
        "id": text("id"),
        "version": text("version"),
        "developmentDependency": text("developmentDependency") == "true",
        "license": (license_node.get("type"), license_node.text) if license_node is not None else None,
        "repository": repository.get("url") if repository is not None else None,
        "dependencies": dependencies,
    }, names


def _local(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def lower_bound(range_text: str) -> str:
    """NuGet 版本范围的下限：1.0.0、[1.0.0, )、[1.0.0] 都给出 1.0.0。"""
    text = range_text.strip()
    if text[:1] in "[(":
        return text[1:].split(",", 1)[0].rstrip("])").strip()
    return text


def verify_packages(version: str, directory: Path, packages: List[str]) -> List[str]:
    problems: List[str] = []
    found: Dict[str, dict] = {}
    for nupkg in sorted(directory.glob("*.nupkg")):
        spec, names = read_nuspec(nupkg)
        package = spec["id"]
        if package in found:
            problems.append(f"{package} 出现了不止一个制品")
            continue
        found[package] = spec
        if spec["version"] != version:
            problems.append(f"{package} 的版本是 {spec['version']}，不是 {version}")
        if nupkg.name.lower() != f"{package}.{version}.nupkg".lower():
            problems.append(f"{nupkg.name} 与包名或版本不符")
        if spec["license"] != ("expression", LICENSE):
            problems.append(f"{package} 的许可证不是 {LICENSE} 表达式")
        if spec["repository"] != REPOSITORY:
            problems.append(f"{package} 的仓库地址不是 {REPOSITORY}，GitHub Packages 无法关联到仓库")
        for dependency, range_text in sorted(spec["dependencies"].items()):
            if dependency.lower().startswith("qian."):
                if dependency not in packages:
                    problems.append(f"{package} 依赖的 {dependency} 不在包族里（还原必然失败）")
                elif lower_bound(range_text) != version:
                    problems.append(f"{package} 对 {dependency} 的依赖是 {range_text}，下限没有钉在 {version}")
        if package == GENERATORS:
            analyzers = [n for n in names if n.startswith("analyzers/dotnet/cs/") and n.endswith(".dll")]
            if analyzers != [f"analyzers/dotnet/cs/{GENERATORS}.dll"]:
                problems.append(f"{GENERATORS} 应只在 analyzers/dotnet/cs 下带自身分析器，实际：{analyzers}")
            if any(n.startswith("lib/") for n in names) or spec["dependencies"]:
                problems.append(f"{GENERATORS} 不能带 lib/ 程序集或传递依赖")
            if not spec["developmentDependency"]:
                problems.append(f"{GENERATORS} 应标记为开发依赖")
    missing = sorted(set(packages) - set(found), key=str.lower)
    extra = sorted(set(found) - set(packages), key=str.lower)
    if missing:
        problems.append(f"缺少包族里的包：{', '.join(missing)}")
    if extra:
        problems.append(f"多出包族以外的包：{', '.join(extra)}")
    if problems:
        raise Failure("制品核对不通过：\n  " + "\n  ".join(problems))
    return sorted(found, key=str.lower)


# ---------- 消费者 ----------

NUGET_CONFIG = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="qian" value="{source}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="qian">
      <package pattern="Qian.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>{credentials}
</configuration>
"""

CREDENTIALS = """
  <packageSourceCredentials>
    <qian>
      <add key="Username" value="{user}" />
      <add key="ClearTextPassword" value="%{variable}%" />
    </qian>
  </packageSourceCredentials>"""

CONSUMER_PROJECT = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Qian.Plugin.Sdk" Version="[{version}]" />
    <PackageReference Include="Qian.Plugin.Sdk.Generators" Version="[{version}]" PrivateAssets="all" />
    <PackageReference Include="Qian.Data" Version="[{version}]" />
  </ItemGroup>
</Project>
"""

# 一个最小组件：源生成器从 [QianPlugin] / [Capability] 与 Apply 里的 Provide 派生入口、服务键与能力契约；
# 运行时读同一套生成工厂，并读包里 qian.data 的生成元数据，证明组件代码字段确实来自源生成器。
CONSUMER_SOURCE = """using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qian.Plugin.Sdk;
using Qian.Runtime;

namespace ReleaseProbe;

[QianPlugin("qian.release-probe")]
public sealed class ProbePlugin : IQianPlugin
{
    public static readonly ServiceKey Probe = new("ReleaseProbe");
    public IReadOnlyList<ServiceKey> Inject => [];
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ctx.Provide(Probe, new object());
        return ValueTask.CompletedTask;
    }
}

[Capability("probe.read", typeof(ProbeResponse), typeof(ProbeJson), Description = "读取发布核对组件的一条记录，不修改任何数据。", RequiredScopes = ["qian.release-probe:read"])]
public sealed partial record ProbeRequest(Guid Id);
public sealed record ProbeResponse(Guid Id);

[JsonSerializable(typeof(ProbeRequest))]
[JsonSerializable(typeof(ProbeResponse))]
public sealed partial class ProbeJson : JsonSerializerContext;

public static class Program
{
    public static void Main()
    {
        var probe = Read(typeof(ProbePlugin).Assembly);
        var data = Read(typeof(Qian.Data.DataPlugin).Assembly);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            probe = new { probe.ComponentId, probe.RuntimeEntry, probe.Provides, Capabilities = probe.Capabilities.Select(c => new { c.Id, Metadata = c.Metadata }) },
            data = new { data.ComponentId, data.RuntimeEntry, data.Provides },
        }));
    }

    private static ComponentCodeMetadata Read(Assembly assembly)
    {
        var declaration = assembly.GetCustomAttribute<QianCodeMetadataAttribute>() ?? throw new InvalidOperationException(assembly.GetName().Name + " 没有生成元数据");
        return ((IComponentCodeMetadataFactory)Activator.CreateInstance(declaration.FactoryType)!).Create();
    }
}
"""


def verify_consumer(version: str, source: str, token_variable: Optional[str], attempts: int, keep: Optional[Path]) -> dict:
    if token_variable and not os.environ.get(token_variable):
        raise Failure(f"环境变量 {token_variable} 没有值，无法向私有源认证")
    work = Path(tempfile.mkdtemp(prefix="qian-release-consumer-")) if keep is None else keep
    work.mkdir(parents=True, exist_ok=True)
    feed = str(Path(source).resolve()) if Path(source).exists() else source
    (work / "nuget.config").write_text(NUGET_CONFIG.format(
        source=feed, credentials=CREDENTIALS.format(user=_user(), variable=token_variable) if token_variable else ""), encoding="utf-8")
    (work / "ReleaseProbe.csproj").write_text(CONSUMER_PROJECT.format(version=version), encoding="utf-8")
    (work / "Program.cs").write_text(CONSUMER_SOURCE, encoding="utf-8")
    # 不继承仓库的 Directory.Build.* 与中央包管理，也不复用本机已有的包缓存：每个包都要从源里真实下载。
    for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"):
        (work / name).write_text("<Project />\n", encoding="utf-8")
    (work / "global.json").write_text((ROOT / "global.json").read_text(encoding="utf-8"), encoding="utf-8")
    env = dict(check_deps._env(), NUGET_PACKAGES=str(work / ".packages"), NUGET_HTTP_CACHE_PATH=str(work / ".http-cache"),
               NUGET_SCRATCH=str(work / ".scratch"), NUGET_PLUGINS_CACHE_PATH=str(work / ".plugins-cache"))

    for attempt in range(1, attempts + 1):
        restore = subprocess.run(["dotnet", "restore", str(work / "ReleaseProbe.csproj"), "--configfile", str(work / "nuget.config"), "-nologo"],
                                 capture_output=True, text=True, env=env, cwd=work)
        if restore.returncode == 0:
            break
        if attempt == attempts:
            raise Failure(f"消费者从 {source} 还原失败：\n{restore.stdout}{restore.stderr}")
        # GitHub Packages 刚推送的版本可能要几秒后才可还原。
        time.sleep(15)
    run = subprocess.run(["dotnet", "run", "--project", str(work / "ReleaseProbe.csproj"), "-c", "Release", "--no-restore"],
                         capture_output=True, text=True, env=env, cwd=work)
    if run.returncode != 0:
        raise Failure(f"消费者构建或运行失败：\n{run.stdout}{run.stderr}")
    report = json.loads(run.stdout.strip().splitlines()[-1])

    problems: List[str] = []
    probe = report["probe"]
    if (probe["ComponentId"], probe["RuntimeEntry"], probe["Provides"]) != ("qian.release-probe", "ReleaseProbe.ProbePlugin, ReleaseProbe", ["ReleaseProbe"]):
        problems.append(f"消费者组件的派生元数据不符：{probe}")
    capabilities = probe["Capabilities"]
    if [c["Id"] for c in capabilities] != ["qian.release-probe/probe.read"] or \
            capabilities[0]["Metadata"].get("requiredScopes") != ["qian.release-probe:read"]:
        problems.append(f"消费者能力契约不符：{capabilities}")
    data = report["data"]
    if (data["ComponentId"], data["RuntimeEntry"], data["Provides"]) != ("qian.data", "Qian.Data.DataPlugin, Qian.Data", ["Data"]):
        problems.append(f"包里 qian.data 的生成元数据不符：{data}")

    assets = json.loads((work / "obj" / "project.assets.json").read_text(encoding="utf-8"))
    restored = {}
    for key, library in assets["libraries"].items():
        name, resolved = key.split("/", 1)
        if name.lower().startswith("qian.") and library.get("type") == "package":
            restored[name] = resolved
            metadata = work / ".packages" / name.lower() / resolved.lower() / ".nupkg.metadata"
            origin = json.loads(metadata.read_text(encoding="utf-8")).get("source") if metadata.exists() else None
            if origin is None or _normalize(origin) != _normalize(feed):
                problems.append(f"{name} 不是从 {source} 还原的（来源 {origin}）")
    wrong = {name: resolved for name, resolved in restored.items() if resolved != version}
    if wrong:
        problems.append(f"还原出的 Qian.* 版本不一致：{wrong}")
    for package in CONSUMER_PACKAGES:
        if package not in restored:
            problems.append(f"没有还原到 {package}")
    if problems:
        raise Failure("消费者核对不通过：\n  " + "\n  ".join(problems))
    return {"source": source, "version": version, "restored": dict(sorted(restored.items(), key=lambda item: item[0].lower())), "work": str(work)}


def _normalize(location: str) -> str:
    return location.rstrip("/\\").lower()


# ---------- 入口 ----------

def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="Qian .NET 包族锁步发布核对")
    commands = parser.add_subparsers(dest="command", required=True)
    version_parser = commands.add_parser("check-version", help="版本号格式，以及私有源里尚无该版本")
    version_parser.add_argument("--version", required=True)
    version_parser.add_argument("--solution", default="Qian.slnx")
    version_parser.add_argument("--feed", help="NuGet v3 服务索引，例如 https://nuget.pkg.github.com/idadawn/index.json")
    version_parser.add_argument("--token-env", help="保存私有源令牌的环境变量名")
    packages_parser = commands.add_parser("verify-packages", help="核对打出的制品")
    packages_parser.add_argument("--version", required=True)
    packages_parser.add_argument("--dir", required=True, type=Path)
    packages_parser.add_argument("--solution", default="Qian.slnx")
    consumer_parser = commands.add_parser("verify-consumer", help="干净消费者项目从给定源真实还原、构建并运行")
    consumer_parser.add_argument("--version", required=True)
    consumer_parser.add_argument("--source", required=True, help="NuGet v3 服务索引地址或本地目录")
    consumer_parser.add_argument("--token-env", help="保存私有源令牌的环境变量名")
    consumer_parser.add_argument("--attempts", type=int, default=1, help="还原重试次数（刚推送的包可能稍后才可见）")
    consumer_parser.add_argument("--work", type=Path, help="消费者项目目录（默认新建临时目录）")
    args = parser.parse_args(argv)

    try:
        if args.command == "check-version":
            packages = family((ROOT / args.solution).resolve())
            token = os.environ.get(args.token_env) if args.token_env else None
            check_version(args.version, args.feed, token, packages)
            where = f"，私有源 {args.feed} 里尚无该版本" if args.feed else ""
            print(f"版本 {args.version} 可用{where}；包族 {len(packages)} 个包")
        elif args.command == "verify-packages":
            packages = family((ROOT / args.solution).resolve())
            found = verify_packages(args.version, args.dir, packages)
            print(f"制品核对通过：{len(found)} 个包均为 {args.version}，包族内依赖下限钉在同一版本")
        else:
            report = verify_consumer(args.version, args.source, args.token_env, args.attempts, args.work)
            print(json.dumps(report, ensure_ascii=False, indent=2))
            print(f"消费者核对通过：{len(report['restored'])} 个 Qian.* 包均为 {args.version} 且来自 {args.source}")
    except Failure as failure:
        print(str(failure), file=sys.stderr)
        return 1
    except (check_deps.GateError, OSError, subprocess.SubprocessError, json.JSONDecodeError, zipfile.BadZipFile) as error:
        print(f"发布核对出错：{error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
