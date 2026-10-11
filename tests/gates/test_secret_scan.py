"""CI must reject literal Apollo credentials without echoing their values."""
import json
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


if __name__ == "__main__":
    unittest.main()
