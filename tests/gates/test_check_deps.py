"""依赖图门禁 scripts/check_deps.py 的自测（S1-QA-001）。

运行：python3 -m unittest discover -s tests/gates -v
- 规则与路径计算用合成的 project.assets.json 验证，不需要 dotnet；
- 端到端用 tests/gates/depgraph 下的样例项目真跑一遍（需要 dotnet；CI 上缺 dotnet 直接失败而不是跳过）。
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "check_deps.py"
sys.path.insert(0, str(SCRIPT.parent))

import check_deps  # noqa: E402

SUBJECT_DIR = ROOT / "packages" / "Subject"
NEEDS_DOTNET = unittest.skipUnless(shutil.which("dotnet") or os.environ.get("CI") == "true", "本机没有 dotnet")


def make_assets(direct, edges, projects=(), framework="net10.0", rid_copy=False):
    """合成一份 project.assets.json。edges: 名称 -> 依赖名称列表；projects 中的名称按 ProjectReference 处理。"""
    projects = set(projects)
    targets = {}
    libraries = {}
    for name, deps in edges.items():
        kind = "project" if name in projects else "package"
        targets[f"{name}/1.0.0"] = {"type": kind, "dependencies": {d: "1.0.0" for d in deps}}
        libraries[f"{name}/1.0.0"] = (
            {"type": "project", "path": f"../{name}/{name}.csproj", "msbuildProject": f"../{name}/{name}.csproj"}
            if kind == "project" else {"type": "package", "path": f"{name.lower()}/1.0.0"}
        )
    project_refs = {
        str((SUBJECT_DIR / f"../{n}/{n}.csproj").resolve()): {"projectPath": str((SUBJECT_DIR / f"../{n}/{n}.csproj").resolve())}
        for n in direct if n in projects
    }
    package_refs = {n: {"target": "Package", "version": "[1.0.0, )"} for n in direct if n not in projects}
    all_targets = {framework: targets}
    if rid_copy:
        all_targets[f"{framework}/linux-x64"] = targets
    return {
        "version": 3,
        "targets": all_targets,
        "libraries": libraries,
        "project": {
            "version": "1.0.0",
            "restore": {"frameworks": {framework: {"targetAlias": framework, "projectReferences": project_refs}}},
            "frameworks": {framework: {"targetAlias": framework, "dependencies": package_refs}},
        },
    }


def violations(subject, assets):
    package = check_deps.Package(subject, SUBJECT_DIR / f"{subject}.csproj", Path("unused"))
    return check_deps.evaluate([package], lambda _: assets)


class RoleTests(unittest.TestCase):
    def test_roles_follow_package_naming(self):
        cases = {
            "Qian.Runtime": "runtime",
            "Qian.Runtime.Validation": "runtime",
            "Qian.Plugin.Sdk": "sdk",
            "Qian.Plugin.Sdk.Generators": "sdk",
            "Qian.Files.Abstractions": "definition",
            "Qian.Registry.Abstractions": "definition",
            "Qian.Registry": "platform-plugin",
            "Qian.Lifecycle": "platform-plugin",
            "qian.capabilities": "platform-plugin",
            "Qian.Bundle.Base": "bundle",
            "Qian.Files": "provider",
            "Qian.Identity.Connector.Oidc": "provider",
            "Kun.Core": "external",
            "Npgsql": "external",
        }
        for name, role in cases.items():
            with self.subTest(name=name):
                self.assertEqual(role, check_deps.role_of(name))


class RuleTests(unittest.TestCase):
    def test_runtime_sdk_definitions_and_third_party_are_allowed(self):
        assets = make_assets(
            direct=["Qian.Authz.Abstractions", "Qian.Plugin.Sdk", "Kun.Storage.Abstractions"],
            edges={
                "Qian.Authz.Abstractions": ["Qian.Runtime", "Qian.Registry.Abstractions"],
                "Qian.Registry.Abstractions": ["Qian.Runtime"],
                "Qian.Plugin.Sdk": ["Qian.Runtime"],
                "Qian.Runtime": ["Kun.Core"],
                "Kun.Core": [],
                "Kun.Storage.Abstractions": ["Newtonsoft.Json"],
                "Newtonsoft.Json": [],
            },
            projects={"Qian.Authz.Abstractions", "Qian.Registry.Abstractions", "Qian.Plugin.Sdk", "Qian.Runtime"},
        )
        self.assertEqual([], violations("Qian.Files", assets))

    def test_platform_plugin_via_project_reference_is_reported(self):
        assets = make_assets(direct=["Qian.Registry"], edges={"Qian.Registry": []}, projects={"Qian.Registry"})
        [v] = violations("Qian.Files", assets)
        self.assertEqual(("platform-plugin", ["Qian.Files", "Qian.Registry"]), (v.role, v.path))

    def test_provider_via_package_reference_is_reported(self):
        assets = make_assets(direct=["Qian.Authz"], edges={"Qian.Authz": ["Qian.Authz.Abstractions"], "Qian.Authz.Abstractions": []})
        [v] = violations("Qian.Files.Abstractions", assets)
        self.assertEqual(("provider", ["Qian.Files.Abstractions", "Qian.Authz"]), (v.role, v.path))

    def test_transitive_violation_reports_the_shortest_path(self):
        assets = make_assets(
            direct=["Qian.Files.Abstractions", "Qian.Notify.Abstractions"],
            edges={
                "Qian.Files.Abstractions": ["Qian.Authz.Abstractions"],
                "Qian.Notify.Abstractions": ["Qian.Lifecycle"],
                "Qian.Authz.Abstractions": ["Qian.Lifecycle"],
                "Qian.Lifecycle": [],
            },
            projects={"Qian.Files.Abstractions", "Qian.Notify.Abstractions", "Qian.Authz.Abstractions", "Qian.Lifecycle"},
        )
        [v] = violations("Qian.Reports", assets)
        self.assertEqual(["Qian.Reports", "Qian.Notify.Abstractions", "Qian.Lifecycle"], v.path)

    def test_connector_depending_on_identity_server_is_reported(self):
        # S1-IDP-007：连接器只依赖身份 Definition；经任何路径带进 Qian.Identity.Server 实现包都是违规。
        assets = make_assets(["Qian.Identity.Abstractions", "Helper"], {
            "Qian.Identity.Abstractions": ["Qian.Runtime"], "Qian.Runtime": [], "Helper": ["Qian.Identity.Server"], "Qian.Identity.Server": []},
            projects={"Qian.Identity.Abstractions", "Qian.Runtime", "Helper", "Qian.Identity.Server"})
        [violation] = violations("Qian.Identity.Connector.Oidc", assets)
        self.assertEqual(("Qian.Identity.Server", "provider"), (violation.dependency, violation.role))
        self.assertEqual(["Qian.Identity.Connector.Oidc", "Helper", "Qian.Identity.Server"], violation.path)

    def test_package_ids_are_case_insensitive(self):
        assets = make_assets(direct=["qian.capabilities"], edges={"qian.capabilities": []})
        [v] = violations("Qian.Files", assets)
        self.assertEqual("platform-plugin", v.role)

    def test_rid_specific_graphs_are_not_double_counted(self):
        assets = make_assets(direct=["Qian.Registry"], edges={"Qian.Registry": []}, projects={"Qian.Registry"}, rid_copy=True)
        self.assertEqual(1, len(violations("Qian.Files", assets)))

    def test_bundles_are_not_checked(self):
        assets = make_assets(direct=["Qian.Files", "Qian.Registry"], edges={"Qian.Files": [], "Qian.Registry": []},
                             projects={"Qian.Files", "Qian.Registry"})
        self.assertEqual([], violations("Qian.Bundle.Base", assets))


class SdkBoundaryTests(unittest.TestCase):
    """S1-RT-014：SDK 的传递依赖不得出现 Provider 包与 Qian.Registry / Qian.Lifecycle / Qian.Capabilities。"""

    def test_sdk_transitive_provider_and_platform_plugins_are_reported(self):
        # 经 Definition、第三方包或另一个 SDK 包间接带入也算：门禁按完整依赖闭包检查，不只看直接引用。
        assets = make_assets(
            direct=["Qian.Data.Abstractions", "Qian.Plugin.Sdk.Extras", "Some.Vendor"],
            edges={
                "Qian.Data.Abstractions": ["Qian.Data"],
                "Qian.Data": [],
                "Qian.Plugin.Sdk.Extras": ["Qian.Registry"],
                "Qian.Registry": [],
                "Some.Vendor": ["Qian.Lifecycle", "Qian.Capabilities"],
                "Qian.Lifecycle": [],
                "Qian.Capabilities": [],
            },
            projects={"Qian.Data.Abstractions", "Qian.Data", "Qian.Plugin.Sdk.Extras", "Qian.Registry"},
        )
        found = {(v.dependency, v.role, " → ".join(v.path)) for v in violations("Qian.Plugin.Sdk", assets)}
        self.assertEqual({
            ("Qian.Data", "provider", "Qian.Plugin.Sdk → Qian.Data.Abstractions → Qian.Data"),
            ("Qian.Registry", "platform-plugin", "Qian.Plugin.Sdk → Qian.Plugin.Sdk.Extras → Qian.Registry"),
            ("Qian.Lifecycle", "platform-plugin", "Qian.Plugin.Sdk → Some.Vendor → Qian.Lifecycle"),
            ("Qian.Capabilities", "platform-plugin", "Qian.Plugin.Sdk → Some.Vendor → Qian.Capabilities"),
        }, found)


class DefinitionKunRuleTests(unittest.TestCase):
    def test_definition_rejects_kun_implementation_package(self):
        assets = make_assets(["Kun.EventBus"], {"Kun.EventBus": ["StackExchange.Redis"], "StackExchange.Redis": []})
        [violation] = violations("Qian.Identity.Abstractions", assets)
        self.assertEqual("kun-implementation", violation.role)
        self.assertEqual(["Qian.Identity.Abstractions", "Kun.EventBus"], violation.path)

    def test_definition_allows_abstraction_and_its_transitive_core(self):
        assets = make_assets(["Kun.EventBus.Abstractions"], {"Kun.EventBus.Abstractions": ["Kun.Core"], "Kun.Core": []})
        self.assertEqual([], violations("Qian.Identity.Abstractions", assets))

    def test_kun_project_reference_cannot_bypass_gate(self):
        assets = make_assets(["Kun.EventBus"], {"Kun.EventBus": []}, projects={"Kun.EventBus"})
        self.assertEqual("kun-implementation", violations("Qian.Identity.Abstractions", assets)[0].role)

    def test_exceptions_are_exact_edges_with_reasons_and_case_insensitive(self):
        for (subject, dependency), reason in check_deps.DEFINITION_KUN_EXCEPTIONS.items():
            with self.subTest(subject=subject, dependency=dependency):
                self.assertTrue(reason.strip())
                assets = make_assets([dependency.upper()], {dependency.upper(): []})
                self.assertEqual([], violations(subject.upper(), assets))
                self.assertEqual(1, len(violations("Qian.Other.Abstractions", assets)))
        assets = make_assets(["Kun.EventBus"], {"Kun.EventBus": []})
        self.assertEqual(1, len(violations("Qian.Data.Abstractions", assets)))

    def test_provider_may_use_kun_implementation(self):
        assets = make_assets(["Kun.EventBus"], {"Kun.EventBus": []})
        self.assertEqual([], violations("Qian.Events", assets))

    def test_rid_copy_does_not_duplicate_kun_violation(self):
        assets = make_assets(["Kun.EventBus"], {"Kun.EventBus": []}, rid_copy=True)
        self.assertEqual(1, len(violations("Qian.Identity.Abstractions", assets)))


@NEEDS_DOTNET
class SampleProjectTests(unittest.TestCase):
    """端到端：真实还原 tests/gates/depgraph 下的样例项目再检查。"""

    def run_gate(self, solution: str):
        result = subprocess.run(
            [sys.executable, str(SCRIPT), "--solution", str(ROOT / "tests/gates/depgraph" / solution), "--json"],
            capture_output=True, text=True, cwd=ROOT)
        self.assertNotEqual(2, result.returncode, result.stderr)
        return result.returncode, json.loads(result.stdout)

    def test_compliant_samples_pass(self):
        code, report = self.run_gate("good.slnx")
        self.assertEqual(0, code, report["violations"])
        packages = {p["packageId"] for p in report["packages"]}
        self.assertIn("Qian.Registry", packages)       # 平台插件自身也是被检查方
        self.assertIn("Qian.Bundle.GateSample", packages)
        self.assertNotIn("GateSample.Host", packages)  # 宿主不打包，不在检查范围

    def test_real_kun_packages_respect_definition_boundary(self):
        code, report = self.run_gate("kun.slnx")
        self.assertEqual(1, code)
        self.assertEqual([("Qian.GateSample.KunBad.Abstractions", "Kun.EventBus", "kun-implementation")],
                         [(v["package"], v["dependency"], v["role"]) for v in report["violations"]])

    def test_violating_samples_fail_with_paths(self):
        code, report = self.run_gate("bad.slnx")
        self.assertEqual(1, code)
        found = {(" → ".join(v["path"]), v["role"]) for v in report["violations"]}
        self.assertEqual({
            ("Qian.GateSample.Files.Abstractions → Qian.GateSample.Authz", "provider"),
            ("Qian.GateSample.Notify → Qian.Registry", "platform-plugin"),
            ("Qian.GateSample.Notify → Qian.GateSample.Authz", "provider"),
            ("Qian.GateSample.Reports → Qian.GateSample.Files.Abstractions → Qian.GateSample.Authz", "provider"),
        }, found)


@NEEDS_DOTNET
class RealSdkClosureTests(unittest.TestCase):
    """S1-RT-014：仓库里真实的 SDK 包族在 CI 的依赖图门禁范围内，其完整传递依赖只有 Runtime / SDK / Definition 与第三方包。

    读已还原的 project.assets.json（CI 在本步之前已构建解决方案）；包被移出解决方案或改名时本测试失败，门禁不会静默跳过它。
    """

    SDK_PROJECTS = {
        "Qian.Plugin.Sdk": "packages/Qian.Plugin.Sdk/Qian.Plugin.Sdk.csproj",
        "Qian.Plugin.Sdk.AspNetCore": "packages/Qian.Plugin.Sdk.AspNetCore/Qian.Plugin.Sdk.AspNetCore.csproj",
        "Qian.Plugin.Sdk.Generators": "packages/Qian.Plugin.Sdk.Generators/Qian.Plugin.Sdk.Generators.csproj",
    }

    def test_real_sdk_closure_has_no_provider_or_platform_plugin(self):
        solution = set(check_deps.solution_projects(ROOT / "Qian.slnx"))
        for package_id, relative in self.SDK_PROJECTS.items():
            with self.subTest(package=package_id):
                project = (ROOT / relative).resolve()
                self.assertIn(project, solution, "SDK 包必须在 Qian.slnx 里，CI 的依赖图门禁才会检查它")
                props = check_deps._properties(project)
                self.assertEqual(("true", package_id), (props.get("QianIsPackage"), props.get("QianPackageId")))
                assets_file = Path(props["ProjectAssetsFile"])
                self.assertTrue(assets_file.exists(), f"缺少 {assets_file}：先 dotnet restore Qian.slnx")
                assets = check_deps._load(assets_file)
                closure = {key.split("/", 1)[0] for framework, entries in assets["targets"].items() if "/" not in framework for key in entries}
                roles = {name: check_deps.role_of(name) for name in closure}
                self.assertEqual([], sorted(name for name, role in roles.items() if role not in check_deps.ALLOWED_ROLES))
                self.assertFalse({"qian.registry", "qian.lifecycle", "qian.capabilities"} & {name.lower() for name in closure})
                self.assertEqual([], check_deps.evaluate([check_deps.Package(package_id, project, assets_file)], check_deps._load))
                if package_id != "Qian.Plugin.Sdk.Generators":
                    # 读到的是真实依赖图而不是空图：运行时包确实在闭包里。
                    self.assertIn("Qian.Runtime", closure)


@NEEDS_DOTNET
class RealConnectorClosureTests(unittest.TestCase):
    """S1-IDP-007 验收：通用 OIDC 连接器包的完整传递依赖里没有 Qian.Identity.Server（也没有任何别的 Provider），只经身份 Definition 接入。

    与 SDK 包族同样读已还原的 project.assets.json；包被移出解决方案或改名时本测试失败，门禁不会静默跳过它。
    """

    def test_oidc_connector_does_not_depend_on_identity_server(self):
        project = (ROOT / "packages/Qian.Identity.Connector.Oidc/Qian.Identity.Connector.Oidc.csproj").resolve()
        self.assertIn(project, set(check_deps.solution_projects(ROOT / "Qian.slnx")), "连接器包必须在 Qian.slnx 里，CI 的依赖图门禁才会检查它")
        props = check_deps._properties(project)
        self.assertEqual(("true", "Qian.Identity.Connector.Oidc"), (props.get("QianIsPackage"), props.get("QianPackageId")))
        assets_file = Path(props["ProjectAssetsFile"])
        self.assertTrue(assets_file.exists(), f"缺少 {assets_file}：先 dotnet restore Qian.slnx")
        assets = check_deps._load(assets_file)
        closure = {key.split("/", 1)[0] for framework, entries in assets["targets"].items() if "/" not in framework for key in entries}
        self.assertNotIn("qian.identity.server", {name.lower() for name in closure})
        self.assertEqual([], sorted(name for name in closure if check_deps.role_of(name) not in check_deps.ALLOWED_ROLES))
        self.assertEqual([], check_deps.evaluate([check_deps.Package("Qian.Identity.Connector.Oidc", project, assets_file)], check_deps._load))
        # 读到的是真实依赖图：身份 Definition 与运行时确实在闭包里。
        self.assertTrue({"Qian.Identity.Abstractions", "Qian.Runtime"} <= closure)


if __name__ == "__main__":
    unittest.main()
