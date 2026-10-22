using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Qian.Plugin.Sdk.Generators;
using Qian.Plugin.Tool;

namespace Qian.Plugin.Sdk.Tests;

public sealed partial class CodeDerivedManifestTests
{
    private static string WithJob => Source.Replace("[Capability(\"note.create\"", "[QianJob(\"create-note\")]\n[Capability(\"note.create\"", StringComparison.Ordinal);

    [Fact]
    public async Task QianJob关键字标识符的能力与作业生成均能真实编译和同步清单()
    {
        using var work = new WorkDirectory();
        var source = WithJob.Replace("CreateNote", "@event", StringComparison.Ordinal)
            .Replace("JsonSerializable(typeof(@event))", "JsonSerializable(typeof(@event), TypeInfoPropertyName = \"EventRequest\")", StringComparison.Ordinal);
        var assembly = work.Compile("keyword-job", source); var component = work.Component();
        var sync = await Tool("sync", assembly, component); Assert.True(sync.ExitCode == 0, sync.Error);
        using var jobs = JsonDocument.Parse(File.ReadAllText(Path.Combine(component, "metadata/jobs.json")));
        var job = Assert.Single(jobs.RootElement.EnumerateArray());
        Assert.Equal("create-note", job.GetProperty("code").GetString());
        Assert.Equal("qian.sdk-manifest/note.create", job.GetProperty("target").GetString());
        Assert.Equal(0, (await Tool("check", assembly, component)).ExitCode);
    }

    [Fact]
    public async Task QianJob真实编译派生jobs表面_check幂等_拒绝手改_删除最后声明只移除受管文件()
    {
        using var work = new WorkDirectory(); var assembly = work.Compile("job", WithJob); var component = work.Component();
        var sync = await Tool("sync", assembly, component); Assert.True(sync.ExitCode == 0, sync.Error);
        var file = Path.Combine(component, "metadata/jobs.json"); var text = File.ReadAllText(file);
        using (var document = JsonDocument.Parse(text))
        {
            var job = Assert.Single(document.RootElement.EnumerateArray());
            Assert.Equal("create-note", job.GetProperty("code").GetString()); Assert.Equal("qian.sdk-manifest/note.create", job.GetProperty("target").GetString());
            Assert.Equal("local", job.GetProperty("type").GetString()); Assert.True(job.GetProperty("inputSchema").TryGetProperty("properties", out _));
        }
        var before = WorkDirectory.Snapshot(component);
        Assert.Equal(0, (await Tool("check", assembly, component)).ExitCode); Assert.Equal(0, (await Tool("sync", assembly, component)).ExitCode);
        Assert.Equal(before, WorkDirectory.Snapshot(component));
        File.WriteAllText(file, text.Replace("create-note", "manual-change", StringComparison.Ordinal));
        var edited = WorkDirectory.Snapshot(component); var refused = await Tool("sync", assembly, component);
        Assert.Equal("SDK_HANDWRITTEN_CONFLICT", refused.Json.GetProperty("code").GetString()); Assert.Equal(edited, WorkDirectory.Snapshot(component));
        File.WriteAllText(file, text);
        var without = work.Compile("no-job", Source);
        Assert.NotEqual(0, (await Tool("check", without, component)).ExitCode);
        Assert.Equal(0, (await Tool("sync", without, component)).ExitCode); Assert.False(File.Exists(file));
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(component, ManifestPlan.ManifestFile)))!;
        Assert.Null(manifest["x-qian-generated"]!["metadata"]!["metadata/jobs.json"]);
        Assert.Equal(0, (await Tool("check", without, component)).ExitCode);
    }

    [Fact]
    public async Task 同程序集两组件的相同jobCode各归其能力_清单按ID隔离()
    {
        var source = WithJob.Replace("Description = ", "ComponentId = \"qian.sdk-manifest\", Description = ", StringComparison.Ordinal)
            .Replace("[JsonSerializable(typeof(NoteResponse))]", "[JsonSerializable(typeof(NoteResponse))]\n[JsonSerializable(typeof(OtherJob))]", StringComparison.Ordinal) + """

            [QianPlugin("qian.other")]
            public sealed class OtherPlugin : IQianPlugin {
                public IReadOnlyList<ServiceKey> Inject => [];
                public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
            }
            [QianJob("create-note")]
            [Capability("other", typeof(NoteResponse), typeof(NotesJson), ComponentId = "qian.other", Description = "第二组件的独立作业声明，不能混入第一个组件。", SideEffect = "write", Idempotent = true)]
            public sealed partial record OtherJob(Guid IdempotencyKey);
            """;
        using var work = new WorkDirectory(); using var other = new WorkDirectory();
        var assembly = work.Compile("two", source); var one = work.Component(); var two = other.Component();
        var manifest = Path.Combine(two, ManifestPlan.ManifestFile); File.WriteAllText(manifest, File.ReadAllText(manifest).Replace(Component, "qian.other", StringComparison.Ordinal));
        foreach (var (directory, target) in new[] { (one, "qian.sdk-manifest/note.create"), (two, "qian.other/other") })
        {
            var sync = await Tool("sync", assembly, directory); Assert.True(sync.ExitCode == 0, sync.Error);
            using var jobs = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "metadata/jobs.json")));
            Assert.Equal(target, Assert.Single(jobs.RootElement.EnumerateArray()).GetProperty("target").GetString());
            Assert.Equal(0, (await Tool("check", assembly, directory)).ExitCode);
        }
    }

    [Theory]
    [InlineData("invalid-code", "QIANSDK003")]
    [InlineData("not-partial", "QIANSDK003")]
    [InlineData("no-capability", "QIANSDK003")]
    [InlineData("duplicate-code", "QIANSDK002")]
    [InlineData("reserved-member", "QIANSDK003")]
    public void 非法作业声明与重复代码明确诊断(string scenario, string expected)
    {
        var source = scenario switch
        {
            "invalid-code" => WithJob.Replace("QianJob(\"create-note\")", "QianJob(\"../job\")", StringComparison.Ordinal),
            "not-partial" => WithJob.Replace("partial record CreateNote", "record CreateNote", StringComparison.Ordinal),
            "no-capability" => "using Qian.Plugin.Sdk; [QianJob(\"job\")] public partial class NoCapability { }",
            "duplicate-code" => WithJob.Replace("[Capability(\"note.read\"", "[QianJob(\"create-note\")] [Capability(\"note.read\"", StringComparison.Ordinal),
            "reserved-member" => WithJob.Replace("record CreateNote(Guid IdempotencyKey, string Title);", "record CreateNote(Guid IdempotencyKey, string Title) { public static string QianJobCode => \"own\"; }", StringComparison.Ordinal),
            _ => throw new ArgumentException("未知测试场景")
        };
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create("JobDiagnostics", [CSharpSyntaxTree.ParseText(source, parse, cancellationToken: Ct)], References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(ImmutableArray.Create(new CapabilityGenerator().AsSourceGenerator(), new ComponentMetadataGenerator().AsSourceGenerator(), new JobGenerator().AsSourceGenerator()), parseOptions: parse);
        Assert.Contains(driver.RunGenerators(compilation, Ct).GetRunResult().Diagnostics, diagnostic => diagnostic.Id == expected && diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
