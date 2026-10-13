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


if __name__ == "__main__":
    unittest.main()
