#!/usr/bin/env python3
"""依赖图门禁（PRD §14.4 C19、specs/component-model.md §10.3 G3、ADR-019 D19.8）。只依赖标准库。

被检查方：解决方案里的每个 Qian 包，即 IsPackable=true 且包名以 Qian. 开头的项目（判定写在 Directory.Build.targets，
属性 QianIsPackage / QianPackageId）。组合包 Qian.Bundle.* 的职责就是把一组 Provider 一起装（component-model §7.3），不检查。

规则：被检查方的传递依赖里只允许出现这些 Qian 包，其余一律违规——
  Qian.Runtime、Qian.Runtime.*          运行时：组件库唯一必需的 Qian 依赖（ADR-020 D20.10）
  Qian.Plugin.Sdk、Qian.Plugin.Sdk.*    SDK：自身只依赖运行时与 Definition（PRD 7.2.3）
  *.Abstractions                        Service Definition：组件之间只经 Abstractions 引用（ADR-019 D19.5）
违规的典型是平台插件包 Qian.Registry / Qian.Lifecycle / Qian.Capabilities（只进 platform profile，PRD 7.2.1）
与任何 Provider 包。另对 Definition 的 Kun 直接依赖只允许 Kun.*.Abstractions 与逐条注明理由的白名单；
该附加规则不检查 Kun 传递依赖，Provider 使用 Kun 实现包不受影响。

为什么读 obj/project.assets.json：`dotnet list package --include-transitive` 只列 NuGet 包，
看不到 ProjectReference 带进来的项目；仓库内组件之间大多是项目引用，所以直接读 NuGet 还原产物
（list package 读的也是它），包与项目两类依赖一并检查，并报出从被检查方到违规依赖的完整路径。

用法（仓库根目录）：
  python3 scripts/check_deps.py                         还原 Qian.slnx 后检查
  python3 scripts/check_deps.py --no-restore            CI 已还原 / 构建时直接检查
  python3 scripts/check_deps.py --solution x.slnx --json
退出码：0 通过；1 有违规；2 工具出错（还原失败、缺 assets 文件等）。
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections import deque
from concurrent.futures import ThreadPoolExecutor
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Callable, Dict, List, Optional, Set, Tuple

ROOT = Path(__file__).resolve().parents[1]
PLATFORM_PLUGINS = {"qian.registry", "qian.lifecycle", "qian.capabilities"}
ALLOWED_ROLES = {"external", "runtime", "sdk", "definition"}
# 仅放行已有公开契约所需的具体边；不得以 Kun.* 或所有 Definition 为范围豁免。
DEFINITION_KUN_EXCEPTIONS = {
    ("qian.tenancy.abstractions", "kun.core"): "ITenantContext 与 IMultiTenant 是 Core 的 BCL 契约，无实现依赖。",
    ("qian.authz.abstractions", "kun.authorization"): "复用已发布 IPermissionChecker / IDataPermissionFilter / IFieldPolicy 框架端口，避免复制类型。",
    ("qian.data.abstractions", "kun.entityframeworkcore"): "已发布 QianDbContext 继承 KunDbContext，IDataSession 暴露同上下文工作单元；拆分需保持公开 API 兼容。",
    ("qian.data.abstractions", "kun.multitenancy.entityframeworkcore"): "QianDbContext 启动时校验 Kun RLS 双拦截器与模型缓存键；安全装配依赖目前必须保留。",
}
REASONS = {
    "kun-implementation": "Definition 的 Kun 直接依赖只允许 Kun.*.Abstractions 或具名例外（specs/component-model.md §6.1、S1-KUN-010）",
    "platform-plugin": "平台插件包只进 platform profile，组件库不得依赖（PRD 7.2.1、ADR-019 D19.8）",
    "provider": "Provider 包；组件之间只经 Abstractions 引用（ADR-019 D19.5、specs/component-model.md §6.1）",
    "bundle": "组合包只由 profile 宿主引用，组件库不得依赖（specs/component-model.md §7.3）",
}


class GateError(Exception):
    """工具层面的失败（不是规则违规）。"""


def role_of(package_id: str) -> str:
    """按包名归类：external / runtime / sdk / definition / platform-plugin / bundle / provider。"""
    name = package_id.lower()
    if not name.startswith("qian."):
        return "external"
    if name.endswith(".abstractions"):
        return "definition"
    if name in PLATFORM_PLUGINS:
        return "platform-plugin"
    if name == "qian.runtime" or name.startswith("qian.runtime."):
        return "runtime"
    if name == "qian.plugin.sdk" or name.startswith("qian.plugin.sdk."):
        return "sdk"
    if name.startswith("qian.bundle."):
        return "bundle"
    return "provider"


@dataclass(frozen=True)
class Package:
    package_id: str
    project: Path
    assets_file: Path


@dataclass
class Violation:
    package: str
    project: str
    framework: str
    dependency: str
    role: str
    path: List[str]

    @property
    def reason(self) -> str:
        return REASONS[self.role]


def _direct_dependencies(assets: dict, framework: str, project_dir: Path) -> Set[str]:
    """被检查方的直接依赖（小写名）：PackageReference 与 ProjectReference 两类。"""
    project = assets.get("project", {})
    names: Set[str] = set()
    frameworks = project.get("frameworks", {})
    matched = [f for k, f in frameworks.items() if k == framework or f.get("targetAlias") == framework] or list(frameworks.values())
    for f in matched:
        names.update(d.lower() for d in (f.get("dependencies") or {}))

    by_path: Dict[str, str] = {}
    for key, lib in (assets.get("libraries") or {}).items():
        if lib.get("type") == "project" and lib.get("path"):
            by_path[os.path.normcase(str((project_dir / lib["path"]).resolve()))] = key.split("/", 1)[0].lower()
    restore = (project.get("restore") or {}).get("frameworks") or {}
    matched_restore = [f for k, f in restore.items() if k == framework or f.get("targetAlias") == framework] or list(restore.values())
    for f in matched_restore:
        for ref in (f.get("projectReferences") or {}):
            name = by_path.get(os.path.normcase(str(Path(ref).resolve())))
            if name:
                names.add(name)
    return names


def find_violations(package: Package, assets: dict) -> List[Violation]:
    """在一份 project.assets.json 里找出违规依赖，并给出从被检查方出发的最短路径。"""
    out: List[Violation] = []
    project_dir = package.project.parent
    for framework, entries in (assets.get("targets") or {}).items():
        if "/" in framework:
            continue  # 带 RID 的依赖图与不带 RID 的重复
        graph: Dict[str, Tuple[str, List[str]]] = {}
        for key, entry in entries.items():
            name = key.split("/", 1)[0]
            graph[name.lower()] = (name, [d.lower() for d in (entry.get("dependencies") or {})])

        direct = _direct_dependencies(assets, framework, project_dir)
        if role_of(package.package_id) == "definition":
            for low in sorted(direct):
                if (low.startswith("kun.") and not low.endswith(".abstractions")
                        and (package.package_id.lower(), low) not in DEFINITION_KUN_EXCEPTIONS):
                    name = graph.get(low, (low, []))[0]
                    out.append(Violation(package.package_id, _rel(package.project), framework, name,
                                         "kun-implementation", [package.package_id, name]))

        parents: Dict[str, Optional[str]] = {}
        queue = deque()
        for name in sorted(direct):
            if name in graph and name not in parents:
                parents[name] = None
                queue.append(name)
        while queue:
            node = queue.popleft()
            for child in graph[node][1]:
                if child in graph and child not in parents:
                    parents[child] = node
                    queue.append(child)

        for low, (name, _) in sorted(graph.items()):
            role = role_of(name)
            if role in ALLOWED_ROLES:
                continue
            chain: List[str] = []
            node: Optional[str] = low
            while node is not None:
                chain.append(graph[node][0])
                node = parents.get(node)
            out.append(Violation(package.package_id, _rel(package.project), framework, name, role,
                                 [package.package_id] + list(reversed(chain))))
    return out


def evaluate(packages: List[Package], load_assets: Callable[[Path], dict]) -> List[Violation]:
    violations: List[Violation] = []
    for package in packages:
        if role_of(package.package_id) == "bundle":
            continue
        violations.extend(find_violations(package, load_assets(package.assets_file)))
    return violations


# ---------- 与 dotnet 交互 ----------

def _env() -> Dict[str, str]:
    env = dict(os.environ)
    env.update({"DOTNET_CLI_UI_LANGUAGE": "en", "DOTNET_NOLOGO": "1", "DOTNET_CLI_TELEMETRY_OPTOUT": "1"})
    return env


def _rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def solution_projects(solution: Path) -> List[Path]:
    text = solution.read_text(encoding="utf-8-sig")
    if solution.suffix.lower() == ".slnx":
        paths = [e.get("Path") for e in ET.fromstring(text).iter("Project") if e.get("Path")]
    else:
        paths = re.findall(r'^Project\("\{[^}]+\}"\)\s*=\s*"[^"]*",\s*"([^"]+\.(?:cs|fs|vb)proj)"', text, re.M)
    return [(solution.parent / p.replace("\\", "/")).resolve() for p in paths]


def _properties(project: Path) -> Dict[str, str]:
    cmd = ["dotnet", "msbuild", str(project), "-nologo",
           "-getProperty:QianIsPackage", "-getProperty:QianPackageId", "-getProperty:ProjectAssetsFile"]
    result = subprocess.run(cmd, capture_output=True, text=True, env=_env())
    if result.returncode != 0:
        raise GateError(f"读取项目属性失败：{_rel(project)}\n{result.stdout}{result.stderr}")
    return json.loads(result.stdout)["Properties"]


def discover(solution: Path, restore: bool) -> List[Package]:
    if restore:
        result = subprocess.run(["dotnet", "restore", str(solution), "-nologo", "-v:q"],
                                capture_output=True, text=True, env=_env())
        if result.returncode != 0:
            raise GateError(f"dotnet restore 失败：\n{result.stdout}{result.stderr}")
    projects = solution_projects(solution)
    with ThreadPoolExecutor(max_workers=min(8, os.cpu_count() or 4)) as pool:
        props = list(pool.map(_properties, projects))
    packages = []
    for project, p in zip(projects, props):
        if p.get("QianIsPackage") != "true":
            continue
        assets = Path(p["ProjectAssetsFile"])
        if not assets.exists():
            raise GateError(f"缺少 {_rel(assets)}：先 dotnet restore {_rel(solution)}")
        packages.append(Package(p["QianPackageId"], project, assets))
    return sorted(packages, key=lambda x: x.package_id.lower())


def _load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="Qian 包依赖图门禁")
    parser.add_argument("--solution", default="Qian.slnx", help="解决方案文件（.slnx / .sln），默认 Qian.slnx")
    parser.add_argument("--no-restore", action="store_true", help="跳过 dotnet restore（已还原时使用）")
    parser.add_argument("--json", action="store_true", help="以 JSON 输出检查结果")
    args = parser.parse_args(argv)

    solution = Path(args.solution)
    if not solution.is_absolute():
        solution = (Path.cwd() / solution).resolve()
    try:
        packages = discover(solution, restore=not args.no_restore)
        violations = evaluate(packages, _load)
    except GateError as ex:
        print(f"依赖图门禁出错：{ex}", file=sys.stderr)
        return 2

    if args.json:
        print(json.dumps({
            "solution": _rel(solution),
            "packages": [{"packageId": p.package_id, "project": _rel(p.project)} for p in packages],
            "violations": [dict(asdict(v), reason=v.reason) for v in violations],
        }, ensure_ascii=False, indent=2))
    elif violations:
        print(f"依赖图门禁失败：{len({v.package for v in violations})} 个 Qian 包共 {len(violations)} 处违规依赖")
        for v in violations:
            print(f"  {' → '.join(v.path)}（{v.framework}）")
            print(f"    {v.dependency}：{v.reason}")
        print("修法：改为依赖对方的 .Abstractions 包；做法见 tests/README.md「门禁」一节。")
    elif packages:
        checked = [p.package_id for p in packages if role_of(p.package_id) != "bundle"]
        bundles = [p.package_id for p in packages if role_of(p.package_id) == "bundle"]
        print(f"依赖图门禁通过：检查了 {len(checked)} 个 Qian 包：{', '.join(checked) or '无'}")
        if bundles:
            print(f"  组合包不检查：{', '.join(bundles)}")
    else:
        print("依赖图门禁通过：解决方案里还没有 Qian 包（IsPackable=true 且包名以 Qian. 开头），新增后自动纳入检查。")
    return 1 if violations else 0


if __name__ == "__main__":
    sys.exit(main())
