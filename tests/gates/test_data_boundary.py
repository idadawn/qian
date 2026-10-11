"""真实 EF 类型的编译正反例，验证在线上下文与查询过滤器边界。"""
from pathlib import Path
import os
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]


class DataBoundaryTests(unittest.TestCase):
    def test_context_and_filter_boundaries(self):
        cases = [
            ("DirectContext", "QIAN0003"),
            ("IndirectContext", "QIAN0003"),
            ("FilterBypass", "QIAN0004"),
            ("SafeContext", None),
            ("UnrelatedMethod", None),
        ]
        for name, diagnostic in cases:
            with self.subTest(sample=name):
                project = ROOT / "tests/gates/data" / name / (name + ".csproj")
                result = subprocess.run(
                    ["dotnet", "build", str(project), "-c", "Release"], cwd=ROOT,
                    env=dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en"),
                    text=True, capture_output=True)
                output = result.stdout + result.stderr
                self.assertEqual(result.returncode == 0, diagnostic is None, output[-5000:])
                if diagnostic:
                    self.assertIn("error " + diagnostic, output)
