"""PublicAPI 门禁（Directory.Build.targets）的自测（S1-QA-001）。

运行：python3 -m unittest discover -s tests/gates -v
逐个构建 tests/gates/publicapi 下的样例项目，断言构建结果与诊断号。需要 dotnet；CI 上缺 dotnet 直接失败而不是跳过。
"""
from __future__ import annotations

import os
import shutil
import subprocess
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SAMPLES = ROOT / "tests" / "gates" / "publicapi"

# 样例项目 -> 期望：(构建是否成功, 必须出现的诊断号)
EXPECTED = {
    "Qian.GateSample.Declared": (True, None),       # 公共 API 已登记
    "Qian.GateSample.Undeclared": (False, "RS0016"),  # 新增公共 API 未登记
    "Qian.GateSample.Stale": (False, "RS0017"),       # 登记了已删除的公共 API
    "Qian.GateSample.NoApiFiles": (False, "QIAN0001"),  # Qian 包缺 PublicAPI 文件
    "GateSample.NotPackable": (True, None),         # 不打包的项目不受约束
}


def build(name: str):
    project = SAMPLES / name / f"{name}.csproj"
    env = dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en", DOTNET_NOLOGO="1")
    result = subprocess.run(["dotnet", "build", str(project), "-c", "Release", "-nologo"],
                            capture_output=True, text=True, cwd=ROOT, env=env)
    return result.returncode, result.stdout + result.stderr


@unittest.skipUnless(shutil.which("dotnet") or os.environ.get("CI") == "true", "本机没有 dotnet")
class PublicApiGateTests(unittest.TestCase):
    def test_samples_build_as_expected(self):
        names = sorted(EXPECTED)
        with ThreadPoolExecutor(max_workers=len(names)) as pool:
            results = dict(zip(names, pool.map(build, names)))
        for name in names:
            succeeds, diagnostic = EXPECTED[name]
            code, output = results[name]
            with self.subTest(sample=name):
                self.assertEqual(succeeds, code == 0, output[-3000:])
                if diagnostic:
                    self.assertIn(f"error {diagnostic}", output)
                else:
                    self.assertNotIn("error RS00", output)


if __name__ == "__main__":
    unittest.main()
