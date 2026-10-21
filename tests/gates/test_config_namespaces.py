"""config.schema 推导的 Apollo namespace 骨架与线上键比对，缺键 / 多键必须非零退出且不回显配置值（S1-CFG-003）。"""
import json
import shutil
import socket
import subprocess
import sys
import tempfile
import time
import unittest
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TOOL = ROOT / "tools/Qian.Config.Tool/bin/Release/net10.0/Qian.Config.Tool.dll"
MOCK = ROOT / "tests/Qian.Testing.Containers/ApolloMock/apollo_mock.py"
NAMESPACE = "qian.plugin.test.sample"
LITERAL = "synthetic-online-value"
# 本机可能配置了系统代理；回环地址的协议替身不经代理。
OPENER = urllib.request.build_opener(urllib.request.ProxyHandler({}))


def run_tool(*args):
    return subprocess.run(["dotnet", str(TOOL), *map(str, args)], capture_output=True, text=True, cwd=ROOT)


class ConfigNamespaceGateTests(unittest.TestCase):
    def test_repository_profiles_derive_skeletons(self):
        """CI 对仓库 profile 推导骨架：Schema 可解释、只写默认值与注释，敏感键不给值。"""
        self.assertTrue(TOOL.exists(), "Build Qian.slnx in Release before running gates")
        with tempfile.TemporaryDirectory() as temporary:
            result = run_tool(ROOT, "--out", temporary)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            report = json.loads(result.stdout)
            self.assertFalse([item for item in report["diagnostics"] if item["level"] == "error"], result.stdout)
            platform = next(item for item in report["profiles"] if item["profile"] == "platform")
            self.assertEqual(platform["appId"], "qian-platform")
            files = {item["name"]: item for item in platform["namespaces"]}
            self.assertEqual(files["qian.plugin.qian.files"]["required"], 1)
            skeleton = (Path(temporary) / "platform/qian.plugin.qian.files.properties").read_text(encoding="utf-8")
            self.assertIn("qian.plugin.qian.files:quota:perTenantBytes = 10737418240", skeleton)
            self.assertIn("# qian.plugin.qian.files:storage:secretKey = secretRef://platform/…", skeleton)
            self.assertFalse(any(item["profile"] == "standalone" and item["namespaces"] for item in report["profiles"]))

    def test_schema_change_against_protocol_double_fails_on_missing_and_extra_keys(self):
        self.assertTrue(TOOL.exists(), "Build Qian.slnx in Release before running gates")
        with tempfile.TemporaryDirectory() as temporary, ApolloMock() as apollo:
            repo = Path(temporary)
            for name in (".qian-plugin/plugin.json", "schemas/config.schema.json"):
                target = repo / "plugins/qian.config.apollo" / name
                target.parent.mkdir(parents=True)
                shutil.copyfile(ROOT / "plugins/qian.config.apollo" / name, target)
            component = repo / "plugins/test.sample"
            (component / ".qian-plugin").mkdir(parents=True)
            (component / "schemas").mkdir()
            (component / ".qian-plugin/plugin.json").write_text(json.dumps({
                "id": "test.sample", "version": "1.0.0", "description": "namespace 骨架门禁样例", "author": {"name": "Qian"},
                "license": "MIT", "interface": {"displayName": "sample", "category": "test"}, "inject": [], "provides": [],
                "policy": {"tier": "private", "sandbox": "none", "authentication": "ON_INSTALL", "required": False},
                "scopes": ["global"]}), encoding="utf-8")
            (repo / "profiles/gate").mkdir(parents=True)
            (repo / "profiles/gate/qian.profile.yml").write_text(json.dumps({
                "name": "gate", "bundles": [], "patch": "./qian.patch.yml", "hmr": {"surfaces": True, "code": False},
                "build": {"aot": False}, "hooks": {"allowCommand": False}}), encoding="utf-8")
            (repo / "profiles/gate/qian.patch.yml").write_text(json.dumps([
                {"id": "config", "plugin": "qian.config.apollo",
                 "config": {"meta": "http://apollo-config.apollo:8080", "appId": "qian-gate", "namespaces": ["application", NAMESPACE]}},
                {"id": "sample", "plugin": "test.sample", "config": {}}]), encoding="utf-8")

            def schema(*names, required=()):
                properties = {name: {"type": "string"} for name in names}
                (component / "schemas/config.schema.json").write_text(json.dumps({
                    "$schema": "https://json-schema.org/draft/2020-12/schema", "type": "object", "additionalProperties": False,
                    "properties": properties, "required": list(required)}), encoding="utf-8")

            def compare():
                result = run_tool(repo, "--profile", "gate", "--compare", apollo.url)
                self.assertNotIn(LITERAL, result.stdout + result.stderr)
                return result

            schema("title", "region")
            apollo.publish("qian-gate", NAMESPACE, {f"{NAMESPACE}:title": LITERAL})
            result = compare()
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

            # Schema 新增必填键 region（profile 条目随之补上）：线上缺键。
            schema("title", "region", required=["region"])
            (repo / "profiles/gate/qian.patch.yml").write_text((repo / "profiles/gate/qian.patch.yml").read_text(encoding="utf-8")
                                                               .replace('"config": {}', '"config": {"region": "cn"}'), encoding="utf-8")
            result = compare()
            self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
            self.assertEqual([(item["kind"], item["key"]) for item in json.loads(result.stdout)["findings"]], [("missing", f"{NAMESPACE}:region")])

            # Schema 删除 title：线上旧键成为多键。
            schema("region")
            apollo.publish("qian-gate", NAMESPACE, {f"{NAMESPACE}:title": LITERAL, f"{NAMESPACE}:region": "cn"})
            result = compare()
            self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
            self.assertEqual([(item["kind"], item["key"]) for item in json.loads(result.stdout)["findings"]], [("extra", f"{NAMESPACE}:title")])


class ApolloMock:
    """在本机端口上真实启动 Apollo 配置服务协议替身（与 ApolloFixture 容器里运行的是同一个脚本）。"""

    def __enter__(self):
        with socket.socket() as probe:
            probe.bind(("127.0.0.1", 0))
            port = probe.getsockname()[1]
        self.url = f"http://127.0.0.1:{port}/"
        self.process = subprocess.Popen([sys.executable, str(MOCK), "--port", str(port), "--hold-seconds", "1"],
                                        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        deadline = time.monotonic() + 15
        while True:
            try:
                with OPENER.open(self.url + "__mock/health", timeout=1):
                    return self
            except OSError:
                if time.monotonic() > deadline or self.process.poll() is not None:
                    self.__exit__(None, None, None)
                    raise
                time.sleep(0.1)

    def publish(self, app_id, namespace, configurations):
        request = urllib.request.Request(f"{self.url}__mock/configs/{app_id}/default/{namespace}", method="PUT",
                                         data=json.dumps(configurations).encode(), headers={"Content-Type": "application/json"})
        with OPENER.open(request, timeout=5):
            pass

    def __exit__(self, *_):
        self.process.terminate()
        self.process.wait(timeout=10)


if __name__ == "__main__":
    unittest.main()
