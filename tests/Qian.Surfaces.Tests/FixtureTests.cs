using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces.Tests;

public sealed class FixtureTests
{
    private static readonly string Root = FindRoot();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };
    [Fact]
    public void SharedFixtures_ValidateWithStructuredErrors()
    {
        using var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tck/surfaces/cases.json")));
        Assert.True(cases.RootElement.EnumerateArray().Count(c => !c.GetProperty("valid").GetBoolean()) >= 20);
        var validator = new SurfaceValidator();
        var errorSchema = JsonSchema.FromText(File.ReadAllText(Path.Combine(Root, "contracts/v0/error-detail.schema.json")));
        var report = new Dictionary<string, object>();
        foreach (var entry in cases.RootElement.EnumerateArray())
        {
            var name = entry.GetProperty("name").GetString()!;
            using var fixture = new ComponentFixture(entry);
            var options = entry.GetProperty("options").Deserialize<SurfaceValidationOptions>(JsonOptions)!;
            var result = validator.ValidateComponent(fixture.Directory, options);
            var normalized = new
            {
                valid = result.IsValid,
                documents = result.Component?.Documents.Select(d => new { kind = d.Kind, name = d.Name, file = d.File, data = d.Data, body = d.Body }).ToArray() ?? [],
                diagnostics = result.Diagnostics.Select(d => new { file = d.File, line = d.Line, severity = d.Severity, pointer = d.Error.Pointer, code = d.Error.Errors![0].Code }).ToArray()
            };
            var details = name + ": " + JsonSerializer.Serialize(normalized);
            Assert.True(result.IsValid == entry.GetProperty("valid").GetBoolean(), details);
            if (name is "hooks-shell-command" or "command-server-shell" or "command-unapproved-shell")
                Assert.Equal(PlatformErrors.InputInvalid, Assert.Throws<QianErrorException>(() => new HotReloadSurfacePlugin(fixture.Directory, validator, options)).Error.Code);
            if (name == "all-surfaces") _ = new HotReloadSurfacePlugin(fixture.Directory, validator, options); // 明确授权的人类 CLI 声明仍可装载。
            foreach (var expected in entry.GetProperty("expected").EnumerateArray())
                Assert.True(result.Diagnostics.Any(d => d.File == expected.GetProperty("file").GetString() &&
                    d.Error.Pointer == expected.GetProperty("pointer").GetString() && d.Error.Errors![0].Code == expected.GetProperty("code").GetString()), details);
            foreach (var diagnostic in result.Diagnostics)
            {
                Assert.True(diagnostic.Line >= 1, details);
                using var error = JsonDocument.Parse(ErrorDetailJson.Serialize(diagnostic.Error));
                Assert.True(errorSchema.Evaluate(error.RootElement).IsValid, details);
            }
            report[name] = normalized;
        }
        if (Environment.GetEnvironmentVariable("QIAN_SURFACE_REPORT") is { } output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, JsonSerializer.Serialize(report, ReportOptions));
        }
    }

    [Fact]
    public void JcsVectors_CoverFloatingPointAndUtf16Ordering()
    {
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tck/surfaces/jcs-vectors.json")));
        foreach (var vector in vectors.RootElement.EnumerateArray())
        {
            using var source = JsonDocument.Parse(vector.GetProperty("source").GetString()!);
            Assert.Equal(vector.GetProperty("sha256").GetString(), ToolContractHash.Compute(source.RootElement));
        }
    }

    [Fact]
    public void YamlCoreScalars_MatchLanguageIndependentValues()
    {
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tck/surfaces/parse-vectors.json")));
        foreach (var vector in vectors.RootElement.EnumerateArray())
        {
            var parsed = new SurfaceValidator().Parse("values.yml", vector.GetProperty("source").GetString()!);
            Assert.Empty(parsed.Diagnostics);
            Assert.NotNull(parsed.Syntax);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(vector.GetProperty("data").GetRawText()), JsonNode.Parse(parsed.Syntax.Data.GetRawText())));
        }
    }

    // 字面量的未成对代理项只可能来自直接传入的字符串（文件按 UTF-8 严格解码）；与 CLI 的「字面量的未成对代理项按值无效处理」同断言（S1-RT-022）。
    [Fact]
    public void LiteralUnpairedSurrogates_AreInvalidValues()
    {
        var lone = ((char)0xD800).ToString();
        foreach (var (file, content, markdown) in new[] { ("values.json", "{\"a\": \"x" + lone + "\"}", false), ("values.yml", "a: x" + lone + "\n", false), ("guide.md", "---\nname: guide\n---\n" + lone + "\n", true) })
        {
            var parsed = new SurfaceValidator().Parse(file, content, markdown);
            Assert.Null(parsed.Syntax);
            var diagnostic = Assert.Single(parsed.Diagnostics);
            Assert.Equal((1, file + "#", "SURFACE_SYNTAX_INVALID"), (diagnostic.Line, diagnostic.Error.Pointer, diagnostic.Error.Errors![0].Code));
        }
    }

    [Fact]
    public async Task SkillOnlyComponent_RegistersAtTenantScope_AndEffectsAreRemoved()
    {
        using var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tck/surfaces/cases.json")));
        using var fixture = new ComponentFixture(cases.RootElement[0]);
        var result = new SurfaceValidator().ValidateComponent(fixture.Directory);
        Assert.True(result.IsValid);
        Assert.Single(result.Component!.Documents);
        var plugin = new DeclarativeSurfacePlugin(result);
        Assert.Equal(2, plugin.Provides.Count);
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var runtime = new PluginLoader(services);
        using var empty = JsonDocument.Parse("{}");
        var a = new ScopeKey("tenant", "a"); var b = new ScopeKey("tenant", "b");
        var surface = new PluginEntry("surfaces", result.Component!.Id, "1.0.0", a, 0, empty.RootElement.Clone());
        var probe = new PluginEntry("probe", "qian.probe", "1.0.0", ScopeKey.Global, 0, empty.RootElement.Clone());
        await runtime.MountAsync([new PluginDefinition(surface, plugin, plugin.Provides), new PluginDefinition(probe, new Probe(), [])], TestContext.Current.CancellationToken);
        Assert.True(runtime.GetState("surfaces", a) == PluginState.Ready, runtime.GetFailure("surfaces", a)?.ToString());
        var context = runtime.GetContext("probe", ScopeKey.Global);
        Assert.Single(context.Scoped(a).List(SurfaceKeys.Skills));
        Assert.Empty(context.Scoped(b).List(SurfaceKeys.Skills));
        Assert.Empty(context.List(SurfaceKeys.Skills));
        await runtime.UnmountAsync("surfaces", a, TestContext.Current.CancellationToken);
        Assert.Empty(context.Scoped(a).List(SurfaceKeys.Skills));
    }

    private sealed class Probe : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
    }
    private sealed class ComponentFixture : IDisposable
    {
        private readonly string _temporary = Path.Combine(Path.GetTempPath(), "qian-surface-" + Guid.NewGuid().ToString("N"));
        internal string Directory { get; }
        internal ComponentFixture(JsonElement entry)
        {
            Directory = Path.Combine(_temporary, "component");
            System.IO.Directory.CreateDirectory(Directory);
            foreach (var file in entry.GetProperty("files").EnumerateObject())
            {
                var target = Path.GetFullPath(Path.Combine(Directory, file.Name));
                Assert.StartsWith(Directory + Path.DirectorySeparatorChar, target, StringComparison.Ordinal);
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, file.Value.GetString());
            }
            if (entry.TryGetProperty("symlinks", out var links))
                foreach (var link in links.EnumerateObject())
                {
                    var outside = Path.Combine(_temporary, "outside");
                    System.IO.Directory.CreateDirectory(outside);
                    System.IO.Directory.CreateSymbolicLink(Path.Combine(Directory, link.Name), outside);
                }
        }
        public void Dispose() => System.IO.Directory.Delete(_temporary, recursive: true);
    }
    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Qian.slnx"))) return dir.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }
}
