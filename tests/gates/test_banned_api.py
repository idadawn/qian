"""旧 Kun API 必须产生 RS0030，新存储接口应正常编译（S1-KUN-006）。"""
from pathlib import Path
import os
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]

class BannedApiTests(unittest.TestCase):
    def test_old_and_current_interfaces(self):
        for name, succeeds, diagnostic in [("LegacyRepository", False, "RS0030"), ("LegacyStorage", False, "RS0030"), ("LegacyDeclaration", False, "QIAN0002"), ("CurrentStorage", True, None), ("LegacyIdentityIssuer", False, "RS0030"), ("LegacyIdentityFactory", False, "RS0030"), ("LegacySecurityIssuer", False, "RS0030"), ("CurrentS2SIssuer", True, None)]:
            with self.subTest(sample=name):
                project = ROOT / "tests/gates/bannedapi" / name / (name + ".csproj")
                result = subprocess.run(["dotnet", "build", str(project), "-c", "Release"], cwd=ROOT,
                    env=dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en"), text=True, capture_output=True)
                output = result.stdout + result.stderr
                self.assertEqual(result.returncode == 0, succeeds, output[-4000:])
                if not succeeds:
                    self.assertIn("error " + diagnostic, output)
