"""dotnet 工作流脚本 scripts/dotnet_ci.py 的自测（S1-QA-021）：触发过滤自检认得的各类写法，与 verify 对 Qian.slnx 外测试项目的告警。

运行：python3 -m unittest discover -s tests/gates -v
在临时目录里搭一个 git 跟踪的最小仓库（Qian.slnx、dotnet.yml、pm/、docs/、根目录 Markdown 与样例源码），把脚本的仓库根换成它再跑。
每类写法都有一条会被拦下的样例（去掉对应的识别规则，用例即失败），并配不该拦的对照：过滤重新纳入了该文件、路径不在仓库根上、
或者根本不是读取。只依赖标准库与 git。
"""
from __future__ import annotations

import contextlib
import io
import shutil
import subprocess
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))

import dotnet_ci  # noqa: E402

WORKFLOW = """\
name: dotnet
on:
  push:
    branches: [main]
  pull_request:
    paths:
      - '**'
      - '!pm/**'
      - '!docs/**'
      - '!*.md'
{extra}
jobs:
  build-gates:
    runs-on: ubuntu-latest
    steps:
      - run: python3 scripts/gate.py
      - run: python3 -m unittest discover -s tests/gates -v
  native-aot:
    runs-on: ubuntu-latest
    steps:
      - run: dotnet test --project tests/Sample.AotTests -c Release --report-xunit-trx
"""

# 照仓库里测试的写法从 AppContext.BaseDirectory 向上找 Qian.slnx：根目录文件名只在这样定位了仓库根的文件里算引用。
LOCATOR = """\
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
"""

XUNIT_PROJECT = '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="xunit.v3" /></ItemGroup></Project>\n'


def csharp(*statements: str, locator: bool = True) -> str:
    """一个 C# 类，Read 方法体依次是 statements：第 i 条（从 0 数）在第 7 + i 行。"""
    body = "\n".join("        " + statement for statement in statements)
    return ("namespace Sample;\n\npublic sealed class Probe\n{\n    public void Read(string root, string name, string id)\n    {\n"
            f"{body}\n    }}\n{LOCATOR if locator else ''}}}\n")


class Sandbox:
    """临时仓库：scripts/dotnet_ci.py 的 ROOT、SOLUTION、WORKFLOW 换成它；写进去的文件在核对前 git add。"""

    def __init__(self, case: unittest.TestCase) -> None:
        self.case = case
        self.root = Path(tempfile.mkdtemp(prefix="qian-dotnet-ci-"))
        case.addCleanup(shutil.rmtree, self.root, True)
        subprocess.run(["git", "init", "-q", str(self.root)], check=True)
        self.write("Qian.slnx", '<Solution>\n  <Project Path="tests/Sample.Tests/Sample.Tests.csproj" />\n</Solution>\n')
        self.write("tests/Sample.Tests/Sample.Tests.csproj", XUNIT_PROJECT)
        self.write("AGENTS.md", "# 约定\n")
        self.write("README.md", "# 仓库\n")
        self.write("docs/prd/14-quality.md", "# 质量\n")
        self.write("docs/prd/15-delivery.md", "# 交付\n")
        self.write("pm/tasks/S1-QA-001.md", "---\nid: S1-QA-001\n---\n")
        self.write("contracts/v0/sample.schema.json", "{}\n")
        self.write("scripts/gate.py", "print('门禁')\n")
        self.workflow()

    def write(self, path: str, text: str) -> None:
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8")

    def workflow(self, *reincluded: str) -> None:
        self.write(".github/workflows/dotnet.yml", WORKFLOW.format(extra="\n".join(f"      - '{path}'" for path in reincluded)))

    def patched(self) -> contextlib.AbstractContextManager:
        subprocess.run(["git", "-C", str(self.root), "add", "-A"], check=True)
        return mock.patch.multiple(dotnet_ci, ROOT=self.root, SOLUTION=self.root / "Qian.slnx",
                                   WORKFLOW=self.root / ".github" / "workflows" / "dotnet.yml")

    def passes(self):
        with self.patched():
            return dotnet_ci.paths_check()

    def blocked(self) -> str:
        with self.patched(), self.case.assertRaises(dotnet_ci.CheckFailed) as caught:
            dotnet_ci.paths_check()
        return str(caught.exception)


class CsharpReferenceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.box = Sandbox(self)

    def source(self, *statements: str, locator: bool = True, path: str = "tests/Sample.Tests/Sample.cs") -> None:
        self.box.write(path, csharp(*statements, locator=locator))

    def test_single_literal_is_blocked_until_the_filter_reincludes_the_file(self) -> None:
        self.source('var text = File.ReadAllText(Path.Combine(root, "docs/prd/14-quality.md"));')
        self.assertIn("tests/Sample.Tests/Sample.cs:7 读取 docs/prd/14-quality.md，但只改它的 PR 不触发 dotnet 工作流", self.box.blocked())
        self.box.workflow("docs/prd/14-quality.md")
        counts = self.box.passes()
        self.assertEqual(1, counts["重新纳入"])

    def test_path_combine_segments_starting_with_docs_or_pm(self) -> None:
        self.source('var text = File.ReadAllText(Path.Combine(root, "docs", "prd", "14-quality.md"));',
                    'var tasks = Directory.GetFiles(Path.Join(root, "pm", "tasks"));',
                    'var task = Path.Combine(root, "pm", "tasks", name);')
        message = self.box.blocked()
        self.assertIn("Sample.cs:7 读取 docs/prd/14-quality.md，", message)
        self.assertIn("Sample.cs:8 读取 pm/tasks/S1-QA-001.md，", message)
        self.assertIn("Sample.cs:9 读取 pm/tasks/S1-QA-001.md，", message)  # 后面跟着变量：按 pm/tasks/ 前缀算
        self.assertNotIn("15-delivery", message)  # 拼完整的路径只算那一个文件，不算整个目录
        self.box.workflow("docs/prd/14-quality.md", "pm/tasks/**")
        self.box.passes()

    def test_relative_paths_climbing_back_to_the_repository_root(self) -> None:
        self.source('var a = File.ReadAllText("../docs/prd/14-quality.md");',
                    r'var b = File.ReadAllText(@"..\..\..\pm\tasks\S1-QA-001.md");',
                    'var c = Path.Combine(AppContext.BaseDirectory, "..", "..", "docs", "prd", "15-delivery.md");')
        message = self.box.blocked()
        self.assertIn("Sample.cs:7 读取 docs/prd/14-quality.md，", message)
        self.assertIn("Sample.cs:8 读取 pm/tasks/S1-QA-001.md，", message)
        self.assertIn("Sample.cs:9 读取 docs/prd/15-delivery.md，", message)

    def test_interpolated_prefix_and_segments_after_a_hole(self) -> None:
        self.source('var a = File.ReadAllText(Path.Combine(root, $"docs/prd/{name}.md"));',
                    'var b = File.ReadAllText($"{root}/docs/prd/14-quality.md");',
                    'var c = File.ReadAllText(root + "/pm/tasks/" + name + ".md");',
                    'var d = File.ReadAllText($@"{root}\\docs\\prd\\{name}");',
                    'var e = $"docs/prd/15-{name}";',
                    'var f = File.ReadAllText(Path.Combine(root, "docs/prd/14-" + name + ".md"));')
        message = self.box.blocked()
        self.assertIn("Sample.cs:7 读取 docs/prd/…，其中 2 个文件只改它们的 PR 不触发 dotnet 工作流", message)
        self.assertIn("Sample.cs:8 读取 docs/prd/14-quality.md，", message)
        self.assertIn("Sample.cs:9 读取 pm/tasks/S1-QA-001.md，", message)
        self.assertIn("Sample.cs:10 读取 docs/prd/…，", message)
        self.assertIn("Sample.cs:11 读取 docs/prd/15-delivery.md，", message)  # 常量前缀按字符串前缀对文件
        self.assertIn("Sample.cs:12 读取 docs/prd/14-quality.md，", message)  # 后面紧跟 + 也只是前缀

    def test_root_markdown_counts_where_the_file_locates_the_repository_root(self) -> None:
        self.source('var a = File.ReadAllText(Path.Combine(RepositoryRoot(), "AGENTS.md"));')
        self.assertIn("Sample.cs:7 读取 AGENTS.md，", self.box.blocked())
        self.box.workflow("AGENTS.md")
        self.box.passes()

    def test_bare_root_file_names_elsewhere_are_not_repository_reads(self) -> None:
        # 照 Lifecycle 测试的写法：临时部署仓库里的 README.md、签名清单里的文件名，本文件不定位仓库根
        self.source('File.WriteAllText(Path.Combine(root, "README.md"), "部署仓库");',
                    'var files = new Dictionary<string, string> { ["README.md"] = "x" };', locator=False)
        self.box.passes()
        self.source('var a = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../README.md"));', locator=False)
        self.assertIn("Sample.cs:7 读取 README.md，", self.box.blocked())  # 带 ../ 往上爬的照样算

    def test_comments_and_later_segments_are_not_repository_reads(self) -> None:
        self.source('// 读取 "docs/prd/14-quality.md" 的用例见别处',
                    '/* "pm/tasks/S1-QA-001.md" */ var x = 1;',
                    'var plugin = Path.Combine(root, "plugins", id, "docs", "README.md");',
                    'var guide = Path.Combine(root, id, "docs");',
                    'var note = "见 docs/prd/14-quality.md";',
                    'var schema = File.ReadAllText(Path.Combine(root, "contracts/v0/sample.schema.json"));')
        counts = self.box.passes()
        self.assertEqual(0, counts["重新纳入"])
        self.assertEqual(2, counts[".NET 源码"])  # Qian.slnx 与 contracts 下的 schema

    def test_strings_and_characters_before_the_literal_do_not_derail_the_lexer(self) -> None:
        read = 'File.ReadAllText(Path.Combine(root, "{}"));'.format
        self.source('var a = @"x ""docs/prd/14-quality.md"" y";',  # 逐字字符串里的 "" 是引号本身，整串一个字面量
                    'var b = $"{(id == "a" ? "{" : "x")}"; var c = ' + read("docs/prd/15-delivery.md"),  # 洞里字符串的括号不算
                    "var d = '\"'; var e = " + read("pm/tasks/S1-QA-001.md"),  # 字符字面量里的引号
                    'var f = """x"y"""; var g = ' + read("docs/prd/14-quality.md"),  # 原始字面量里的单个引号
                    'var h = $$"""{{name}} "docs/prd/15-delivery.md" """;',  # 原始插值字符串的内容
                    'var i = $$"""{{root}}/pm/tasks/S1-QA-001.md""";')  # 原始插值字符串的洞后段
        message = self.box.blocked()
        self.assertIn("Sample.cs:8 读取 docs/prd/15-delivery.md，", message)
        self.assertIn("Sample.cs:9 读取 pm/tasks/S1-QA-001.md，", message)
        self.assertIn("Sample.cs:10 读取 docs/prd/14-quality.md，", message)
        self.assertIn("Sample.cs:12 读取 pm/tasks/S1-QA-001.md，", message)
        self.assertEqual(4, message.count("读取"), message)


class MsbuildItemTests(unittest.TestCase):
    def setUp(self) -> None:
        self.box = Sandbox(self)

    def project(self, *items: str) -> None:
        self.box.write("tests/Sample.Tests/Sample.Tests.csproj",
                       '<Project Sdk="Microsoft.NET.Sdk">\n  <ItemGroup>\n    <PackageReference Include="xunit.v3" />\n'
                       + "".join(f"    {item}\n" for item in items) + "  </ItemGroup>\n</Project>\n")

    def test_content_and_none_items_pointing_into_docs_pm_or_root_markdown(self) -> None:
        self.project('<None Include="../../docs/prd/14-quality.md" CopyToOutputDirectory="PreserveNewest" />',
                     '<Content Include="$(MSBuildThisFileDirectory)../../pm/tasks/*.md" />',
                     r'<None Update="..\..\README.md" Pack="true" PackagePath="/" />')
        message = self.box.blocked()
        self.assertIn("tests/Sample.Tests/Sample.Tests.csproj:4 读取 docs/prd/14-quality.md，", message)
        self.assertIn("Sample.Tests.csproj:5 读取 pm/tasks/S1-QA-001.md，", message)
        self.assertIn("Sample.Tests.csproj:6 读取 README.md，", message)
        self.box.workflow("docs/prd/14-quality.md", "pm/tasks/*.md", "README.md")
        self.assertEqual(3, self.box.passes()["重新纳入"])

    def test_items_elsewhere_in_the_repository_and_unresolved_properties_pass(self) -> None:
        self.project('<EmbeddedResource Include="../../contracts/v0/*.json" />',
                     '<None Include="$(OutputPath)docs/prd/14-quality.md" />',
                     '<ProjectReference Include="../Sample.Host/Sample.Host.csproj" />')
        self.assertEqual(0, self.box.passes()["重新纳入"])


class PythonReferenceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.box = Sandbox(self)

    def test_script_run_by_the_workflow(self) -> None:
        self.box.write("scripts/gate.py", textwrap.dedent("""\
            import os
            from pathlib import Path

            ROOT = Path(__file__).resolve().parents[1]


            def main(name):
                quality = (ROOT / "docs" / "prd" / "14-quality.md").read_text()
                with open(f"pm/tasks/{name}.md") as task:
                    delivery = ROOT.joinpath("docs", "prd", "15-delivery.md").read_text()
                agents = (Path.cwd() / "AGENTS.md").read_text()
                readme = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "README.md")
                return quality, task.read(), delivery, agents, readme
            """))
        message = self.box.blocked()
        self.assertIn("scripts/gate.py:8 读取 docs/prd/14-quality.md，", message)
        self.assertIn("scripts/gate.py:9 读取 pm/tasks/S1-QA-001.md，", message)  # open() 的相对路径相对仓库根（工作流在仓库根运行）
        self.assertIn("scripts/gate.py:10 读取 docs/prd/15-delivery.md，", message)
        self.assertIn("scripts/gate.py:11 读取 AGENTS.md，", message)
        self.assertIn("scripts/gate.py:12 读取 README.md，", message)
        self.box.workflow("docs/prd/*.md", "pm/tasks/**", "*.md")
        self.assertEqual(5, self.box.passes()["重新纳入"])

    def test_unittest_module_and_the_helpers_it_imports(self) -> None:
        self.box.write("tests/gates/test_sample.py", textwrap.dedent("""\
            import sys
            import unittest
            from pathlib import Path

            ROOT = Path(__file__).resolve().parents[2]
            TASKS = ROOT / "pm" / "tasks"
            sys.path.insert(0, str(ROOT / "scripts"))

            import helper


            class SampleTests(unittest.TestCase):
                def test_tasks(self):
                    self.assertTrue(sorted(TASKS.glob("*.md")))
            """))
        self.box.write("scripts/helper.py", "from pathlib import Path\n\nAGENTS = Path(__file__).parent.parent / 'AGENTS.md'\n"
                                            "\n\ndef rules():\n    return open(AGENTS).read()\n")
        message = self.box.blocked()
        self.assertIn("tests/gates/test_sample.py:14 读取 pm/tasks/S1-QA-001.md，", message)  # 模块顶层的名字在用到它的地方算
        self.assertIn("scripts/helper.py:7 读取 AGENTS.md，", message)

    def test_paths_on_other_anchors_and_scripts_the_workflow_does_not_run_pass(self) -> None:
        self.box.write("tests/gates/test_sample.py", textwrap.dedent("""\
            import tempfile
            import unittest
            from pathlib import Path


            class SampleTests(unittest.TestCase):
                def setUp(self):
                    self.tmp = Path(tempfile.mkdtemp())
                    (self.tmp / "docs/prd").mkdir(parents=True)
                    (self.tmp / "docs/prd/14-quality.md").write_text("# 质量")
                    site = self.tmp / "pm"
                    (site / "README.md").write_text("# 任务")
            """))
        self.box.write("scripts/other.py", "from pathlib import Path\n\nprint((Path(__file__).parents[1] / 'docs/prd/14-quality.md').read_text())\n")
        self.assertEqual(0, self.box.passes()["重新纳入"])

    def test_module_whose_root_is_replaced_by_the_loading_test(self) -> None:
        # 照 test_check_docs.py：按路径加载被测脚本，再把它的 ROOT 换成临时目录；没换的名字照样算
        self.box.write("scripts/docs_gate.py", textwrap.dedent("""\
            from pathlib import Path

            ROOT = Path(__file__).resolve().parents[1]
            INDEX = ROOT / "docs" / "prd"


            def routes():
                return sorted((ROOT / "docs").rglob("*.md"))


            def index():
                return sorted(INDEX.glob("*.md"))
            """))
        loader = textwrap.dedent("""\
            import importlib.util
            import tempfile
            import unittest
            from pathlib import Path

            ROOT = Path(__file__).resolve().parents[2]
            _spec = importlib.util.spec_from_file_location("docs_gate", ROOT / "scripts" / "docs_gate.py")
            docs_gate = importlib.util.module_from_spec(_spec)
            _spec.loader.exec_module(docs_gate)


            class DocsGateTests(unittest.TestCase):
                def setUp(self):
                    self.saved = docs_gate.ROOT
                    docs_gate.ROOT{replaced} = Path(tempfile.mkdtemp()){extra}
            """)
        self.box.write("tests/gates/test_docs_gate.py", loader.format(replaced=", docs_gate.INDEX", extra=", Path(tempfile.mkdtemp())"))
        self.assertEqual(0, self.box.passes()["重新纳入"])
        self.box.write("tests/gates/test_docs_gate.py", loader.format(replaced="", extra=""))
        message = self.box.blocked()
        self.assertIn("scripts/docs_gate.py:12 读取 docs/prd，", message)  # INDEX 没被换掉
        self.assertNotIn("docs_gate.py:8", message)


class VerifyTests(unittest.TestCase):
    """verify 对 tests/ 下引用 xunit.v3 却不在 Qian.slnx 里的测试项目告警（门禁样例与专属作业运行的项目除外）。"""

    TRX = ('<?xml version="1.0" encoding="utf-8"?>\n'
           '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">\n'
           '  <Times start="2026-10-25T00:00:00.0000000+00:00" finish="2026-10-25T00:00:05.0000000+00:00" />\n'
           '  <Results><UnitTestResult outcome="Passed" /></Results>\n'
           '  <TestDefinitions><UnitTest storage="/work/Sample.Tests.dll" /></TestDefinitions>\n'
           '</TestRun>\n')

    def setUp(self) -> None:
        self.box = Sandbox(self)
        self.results = self.box.root / "results"
        (self.results / "test-results-a").mkdir(parents=True)
        (self.results / "test-results-a" / "Sample.Tests.trx").write_text(self.TRX, encoding="utf-8")
        self.box.write("tests/gates/depgraph/Sample/Sample.csproj", XUNIT_PROJECT)
        self.box.write("tests/Sample.AotTests/Sample.AotTests.csproj", XUNIT_PROJECT)
        self.box.write("tests/Sample.TestHost/Sample.TestHost.csproj", '<Project Sdk="Microsoft.NET.Sdk" />\n')

    def verify(self):
        with self.box.patched(), mock.patch.object(dotnet_ci, "SHARDS", {"a": None}):
            return dotnet_ci.verify(self.results)

    def test_gates_samples_dedicated_jobs_and_support_projects_are_not_reported(self) -> None:
        summary, problems, warnings = self.verify()
        self.assertEqual(([], []), (problems, warnings))
        self.assertNotIn("不在 Qian.slnx 里", "\n".join(summary))

    def test_test_project_outside_the_solution_is_reported_by_name(self) -> None:
        self.box.write("tests/Extra.Tests/Extra.Tests.csproj", XUNIT_PROJECT)
        summary, problems, warnings = self.verify()
        self.assertEqual([], problems)
        self.assertEqual(1, len(warnings))
        self.assertIn("1 个测试项目不在 Qian.slnx 里", warnings[0])
        self.assertIn("：Extra.Tests。", warnings[0])
        self.assertIn("tests/ 下不在 Qian.slnx 里、没有任何分片运行的测试项目：Extra.Tests。", summary)
        output = io.StringIO()
        with self.box.patched(), mock.patch.object(dotnet_ci, "SHARDS", {"a": None}), contextlib.redirect_stdout(output):
            self.assertEqual(0, dotnet_ci.main(["verify", str(self.results), "--summary", str(self.box.root / "summary.md")]))
        self.assertIn("::warning::tests/ 下有 1 个测试项目不在 Qian.slnx 里", output.getvalue())


if __name__ == "__main__":
    unittest.main()
