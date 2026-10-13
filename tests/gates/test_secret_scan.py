"""CI must reject literal Apollo credentials without echoing their values."""
import json
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TOOL = ROOT / "tools/Qian.Secrets.Tool/bin/Release/net10.0/Qian.Secrets.Tool.dll"


class SecretScanGateTests(unittest.TestCase):
    def test_schema_and_heuristics_are_blocking(self):
        self.assertTrue(TOOL.exists(), "Build Qian.slnx in Release before running gates")
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            schema = directory / "schema.json"
            schema.write_text(json.dumps({"properties": {"credential": {"x-qian-sensitive": True}}}))
            cases = [
                ({"credential": "synthetic-private-data"}, 1),
                ({"password": "synthetic-private-data"}, 1),
                ({"value": "aZ8pQ1mB9xR2vC7nH5jD3kL6wS4tF0uE"}, 1),
                ({"credential": "secretRef://platform/db/password", "pageSize": 20}, 0),
            ]
            for value, expected in cases:
                config = directory / "config.json"
                config.write_text(json.dumps(value))
                result = subprocess.run(["dotnet", str(TOOL), str(config), str(schema)],
                                        capture_output=True, text=True, cwd=ROOT)
                with self.subTest(expected=expected, keys=list(value)):
                    self.assertEqual(result.returncode, expected, result.stdout + result.stderr)
                    self.assertNotIn("synthetic-private-data", result.stdout + result.stderr)
                    if expected:
                        self.assertIn("CONFIG_SECRET_EXPOSED", result.stdout)
            schema.write_text(json.dumps({"properties": {"Service": {"properties": {"credential": {"x-qian-sensitive": True}}}}}))
            config.write_text(json.dumps({"service:credential": "synthetic-private-data"}))
            result = subprocess.run(["dotnet", str(TOOL), "--apollo", str(config), str(schema)], capture_output=True, text=True, cwd=ROOT)
            self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
            self.assertNotIn("synthetic-private-data", result.stdout + result.stderr)


    def test_repository_profiles_are_clean_and_injected_literals_are_blocking(self):
        """CI 对仓库实际 profile 的生效配置执行同一扫描；注入的明文必须阻断且不回显。"""
        self.assertTrue(TOOL.exists(), "Build Qian.slnx in Release before running gates")
        result = subprocess.run(["dotnet", str(TOOL), "--profiles", str(ROOT)], capture_output=True, text=True, cwd=ROOT)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        cases = [("marker", "aZ8pQ1mB9xR2vC7nH5jD3kL6wS4tF0uE"), ("secret", "synthetic-private-data")]
        with tempfile.TemporaryDirectory() as temporary:
            for key, literal in cases:
                directory = Path(temporary) / key
                shutil.copytree(ROOT / "tck/profiles/base", directory)
                patch = directory / "profiles/test/qian.patch.yml"
                rows = json.loads(patch.read_text(encoding="utf-8"))
                rows[0]["config"][key] = literal
                patch.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")
                result = subprocess.run(["dotnet", str(TOOL), "--profiles", str(directory), "test"],
                                        capture_output=True, text=True, cwd=ROOT)
                with self.subTest(key=key):
                    self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
                    self.assertNotIn(literal, result.stdout + result.stderr)

if __name__ == "__main__":
    unittest.main()
