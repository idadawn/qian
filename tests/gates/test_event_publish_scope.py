"""QIAN0006：集成事件只能在写命令处理器或领域事件处理器内发布，用真实 Kun / SDK 类型编译正反例。"""
from pathlib import Path
import os
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]


class EventPublishScopeTests(unittest.TestCase):
    def test_publish_scope(self):
        cases = [
            ("CommandHandler", True),
            ("DomainEventHandler", True),
            ("CapabilityCommand", True),
            ("Unrelated", True),
            ("QueryHandler", False),
            ("CapabilityQuery", False),
            ("BackgroundPublisher", False),
            ("RawBuffer", False),
        ]
        for name, succeeds in cases:
            with self.subTest(sample=name):
                project = ROOT / "tests/gates/events" / name / (name + ".csproj")
                result = subprocess.run(
                    ["dotnet", "build", str(project), "-c", "Release"], cwd=ROOT,
                    env=dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en"),
                    text=True, capture_output=True)
                output = result.stdout + result.stderr
                self.assertEqual(result.returncode == 0, succeeds, output[-5000:])
                if not succeeds:
                    self.assertIn("error QIAN0006", output)
                else:
                    self.assertNotIn("QIAN0006", output)


if __name__ == "__main__":
    unittest.main()
