"""包族锁步发布核对 scripts/dotnet_release.py 的自测（S1-RT-014）。

运行：python3 -m unittest discover -s tests/gates -v
制品与包源核对用合成数据验证，不需要 dotnet 与网络；真实打包、推送、包源核对与消费者还原在 .github/workflows/dotnet-release.yml 里执行。
"""
from __future__ import annotations

import shutil
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))

import dotnet_release  # noqa: E402

VERSION = "0.1.0"
FAMILY = ["Qian.Runtime", "Qian.Plugin.Sdk", "Qian.Plugin.Sdk.Generators"]


def nuspec(package, version=VERSION, dependencies=None, development=False,
           repository=dotnet_release.REPOSITORY, license_expression=dotnet_release.LICENSE):
    deps = "".join(f'<dependency id="{d}" version="{v}" exclude="Build,Analyzers" />' for d, v in (dependencies or {}).items())
    return (
        '<?xml version="1.0" encoding="utf-8"?>'
        '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata>'
        f"<id>{package}</id><version>{version}</version>"
        + ("<developmentDependency>true</developmentDependency>" if development else "")
        + f'<license type="expression">{license_expression}</license>'
        f'<repository type="git" url="{repository}" />'
        f'<dependencies><group targetFramework="net10.0">{deps}</group></dependencies>'
        "</metadata></package>")


class PackageVerificationTests(unittest.TestCase):
    def setUp(self):
        self.directory = Path(tempfile.mkdtemp(prefix="qian-release-test-"))
        self.addCleanup(shutil.rmtree, self.directory, True)

    def write(self, package, version=VERSION, files=None, **kwargs):
        with zipfile.ZipFile(self.directory / f"{package}.{version}.nupkg", "w") as archive:
            archive.writestr(f"{package}.nuspec", nuspec(package, version, **kwargs))
            for name in files or [f"lib/net10.0/{package}.dll"]:
                archive.writestr(name, b"")

    def write_family(self):
        self.write("Qian.Runtime")
        self.write("Qian.Plugin.Sdk", dependencies={"Qian.Runtime": VERSION, "Kun.Core": "[2026.10.14]"})
        self.write("Qian.Plugin.Sdk.Generators", development=True, files=["analyzers/dotnet/cs/Qian.Plugin.Sdk.Generators.dll", "README.md"])

    def failures(self):
        with self.assertRaises(dotnet_release.Failure) as caught:
            dotnet_release.verify_packages(VERSION, self.directory, FAMILY)
        return str(caught.exception)

    def test_complete_lockstep_family_passes(self):
        self.write_family()
        self.assertEqual(sorted(FAMILY, key=str.lower), dotnet_release.verify_packages(VERSION, self.directory, FAMILY))

    def test_missing_extra_and_wrong_version_are_reported(self):
        self.write_family()
        (self.directory / f"Qian.Runtime.{VERSION}.nupkg").unlink()
        self.write("Qian.Runtime", version="0.1.1")
        self.write("Qian.Unknown")
        message = self.failures()
        self.assertIn("Qian.Runtime 的版本是 0.1.1", message)
        self.assertIn("多出包族以外的包：Qian.Unknown", message)
        (self.directory / "Qian.Runtime.0.1.1.nupkg").unlink()
        self.assertIn("缺少包族里的包：Qian.Runtime", self.failures())

    def test_family_dependencies_must_be_pinned_to_the_same_version_and_stay_inside_the_family(self):
        self.write_family()
        self.write("Qian.Plugin.Sdk", dependencies={"Qian.Runtime": "[0.0.9, )", "Qian.Registry": VERSION})
        message = self.failures()
        self.assertIn("对 Qian.Runtime 的依赖是 [0.0.9, )", message)
        self.assertIn("依赖的 Qian.Registry 不在包族里", message)

    def test_generator_package_carries_only_its_analyzer_and_is_a_development_dependency(self):
        self.write_family()
        self.write("Qian.Plugin.Sdk.Generators", files=["analyzers/dotnet/cs/Qian.Plugin.Sdk.Generators.dll", "lib/netstandard2.0/Qian.Plugin.Sdk.Generators.dll"])
        message = self.failures()
        self.assertIn("不能带 lib/ 程序集", message)
        self.assertIn("应标记为开发依赖", message)

    def test_license_and_repository_link_are_required(self):
        self.write_family()
        self.write("Qian.Runtime", repository="https://example.com/qian.git", license_expression="MIT")
        message = self.failures()
        self.assertIn("Qian.Runtime 的许可证不是 Apache-2.0", message)
        self.assertIn("GitHub Packages 无法关联到仓库", message)


class FeedVerificationTests(unittest.TestCase):
    REPOSITORY = "idadawn/qian"

    def record(self, visibility="public", repository=REPOSITORY, versions=(VERSION,)):
        return {"visibility": visibility, "repository": repository, "versions": list(versions)}

    def test_every_package_has_the_version_is_linked_and_matches_repository_visibility(self):
        records = {package: self.record() for package in FAMILY}
        self.assertEqual(FAMILY, dotnet_release.check_feed(FAMILY, VERSION, records, self.REPOSITORY, "public"))

    def test_missing_package_wrong_link_visibility_or_version_fail(self):
        records = {
            "Qian.Runtime": None,
            "Qian.Plugin.Sdk": self.record(repository="someone/fork", versions=("0.0.9",)),
            "Qian.Plugin.Sdk.Generators": self.record(visibility="private"),
        }
        with self.assertRaises(dotnet_release.Failure) as caught:
            dotnet_release.check_feed(FAMILY, VERSION, records, self.REPOSITORY, "public")
        message = str(caught.exception)
        self.assertIn("GitHub Packages 里没有 Qian.Runtime", message)
        self.assertIn("Qian.Plugin.Sdk 关联的仓库是 someone/fork", message)
        self.assertIn("Qian.Plugin.Sdk 没有 0.1.0 版本", message)
        self.assertIn("Qian.Plugin.Sdk.Generators 的可见性是 private，与仓库的 public 不一致", message)


class VersionTests(unittest.TestCase):
    def test_semver_without_prefix_or_build_metadata(self):
        for good in ["0.1.0", "1.2.3", "1.0.0-s1.1", "2.0.0-rc.1"]:
            with self.subTest(version=good):
                self.assertEqual([], dotnet_release.check_version(good, None, None, FAMILY))
        for bad in ["v0.1.0", "0.1", "01.0.0", "0.1.0+build.5", "0.1.0-", "latest"]:
            with self.subTest(version=bad), self.assertRaises(dotnet_release.Failure):
                dotnet_release.check_version(bad, None, None, FAMILY)

    def test_lower_bound_of_nuget_ranges(self):
        for text in ["0.1.0", "[0.1.0, )", "[0.1.0]", "(0.1.0,1.0.0)"]:
            with self.subTest(range=text):
                self.assertEqual("0.1.0", dotnet_release.lower_bound(text))


if __name__ == "__main__":
    unittest.main()
