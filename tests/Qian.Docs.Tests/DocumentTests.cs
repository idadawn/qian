using System.Text.Json;
using Json.Schema;
using Qian.Docs.Abstractions;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Runtime.Errors;
using Qian.Surfaces;

namespace Qian.Docs.Tests;

public sealed class DocumentTests
{
    private const string Definitions = """
        using Qian.Runtime;
        namespace Demo;
        public static class Events
        {
            [QianEvent(Mode = DispatchMode.Emit)]
            public static readonly QianEvent<string, string?> Changed = QianEvent.Define<string, string?>("demo/changed", DispatchMode.Emit);
        }
        """;
    private const string Calls = """
        using Qian.Runtime;
        using E = Demo.Events;
        namespace Demo;
        public static class Feature
        {
            public static void Subscribe(IPluginContext ctx)
            {
                var evt = E.Changed;
                var mode = evt.Mode;
                ctx.On(handler: (Action<string>)(_ => {}), mode: mode, evt: evt);
            }
            public static async Task<string?> Publish(IPluginContext ctx)
                => await ctx.DispatchAsync(E.Changed, "hi", CancellationToken.None);
        }
        """;
    [Fact]
    public void Generator_derives_four_documents_from_schema_overlay_and_bound_event_calls()
    {
        using var fixture = new Fixture(); fixture.Write("src/Events.cs", Definitions); fixture.Write("src/Calls.cs", Calls);
        var result = fixture.Generate();
        Assert.True(result.IsValid, JsonSerializer.Serialize(result.Diagnostics));
        Assert.Equal(4, result.Documents.Count);
        var evt = Assert.Single(result.Events);
        Assert.Equal("demo/changed", evt.Name); Assert.Equal("emit", evt.Mode);
        Assert.Contains("Feature.Publish", Assert.Single(evt.Producers).Symbol, StringComparison.Ordinal);
        Assert.Contains("Feature.Subscribe", Assert.Single(evt.Consumers).Symbol, StringComparison.Ordinal);
        Assert.Contains("新位置", result.Documents["config-catalog.md"], StringComparison.Ordinal);
        Assert.Contains("overlay", result.Documents["config-catalog.md"], StringComparison.Ordinal);
        Assert.DoesNotContain("旧位置", result.Documents["config-catalog.md"], StringComparison.Ordinal);
        Assert.Contains("Settings", result.Documents["seam-graph.md"], StringComparison.Ordinal);
        // optionalInject：seam 表里标「（可选）」、图里是虚线；必需依赖仍是实线，且 provider 先于 observer 装载。
        Assert.Contains("| ` Settings ` | ` global/provider ` | ` global/consumer `、` global/observer `（可选） |", result.Documents["seam-graph.md"], StringComparison.Ordinal);
        Assert.Contains("-.-> n", result.Documents["seam-graph.md"], StringComparison.Ordinal);
        Assert.Contains("-.->|\"Settings\"|", result.Documents["module-graph.md"], StringComparison.Ordinal);
        Assert.Contains("global/provider ` → ` global/observer `", result.Documents["module-graph.md"], StringComparison.Ordinal);
        Assert.Contains("global/provider", result.Documents["module-graph.md"], StringComparison.Ordinal);
        Assert.Contains("src/Calls.cs", result.Documents["event-graph.md"], StringComparison.Ordinal);
        Assert.DoesNotContain("SHOULD-NOT-BE-PUBLISHED", result.Documents["config-catalog.md"], StringComparison.Ordinal);
        var second = fixture.Generate();
        Assert.Equal(result.Documents.OrderBy(p => p.Key, StringComparer.Ordinal), second.Documents.OrderBy(p => p.Key, StringComparer.Ordinal));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Annotation_or_call_mode_mismatch_fails_and_preserves_mapping(bool annotation)
    {
        using var fixture = new Fixture();
        fixture.Write("src/Events.cs", annotation ? Definitions.Replace("Mode = DispatchMode.Emit", "Mode = DispatchMode.Waterfall", StringComparison.Ordinal) : Definitions);
        fixture.Write("src/Calls.cs", annotation ? Calls : Calls.Replace("var mode = evt.Mode;", "var mode = DispatchMode.Waterfall;", StringComparison.Ordinal));
        var result = fixture.Generate();
        Assert.False(result.IsValid);
        Assert.Single(result.Events); Assert.Single(result.Events[0].Consumers); Assert.Single(result.Events[0].Producers);
        var failure = Assert.Single(result.Diagnostics, d => d.Severity == "error");
        Assert.Equal("DOC_EVENT_MODE_MISMATCH", failure.Error.Errors![0].Code);
        Assert.True(failure.Line > 1);
        Assert.EndsWith(".cs#", failure.Error.Pointer, StringComparison.Ordinal);
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(FindRoot(), "contracts/v0/error-detail.schema.json")), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        Assert.True(schema.Evaluate(JsonSerializer.SerializeToElement(failure.Error, ErrorDetailJson.TypeInfo)).IsValid);
    }
    [Fact]
    public void Mutation_of_a_mode_local_cannot_be_mistaken_for_its_initial_value()
    {
        using var fixture = new Fixture(); fixture.Write("src/Events.cs", Definitions);
        fixture.Write("src/Calls.cs", Calls.Replace("var mode = evt.Mode;", "var mode = DispatchMode.Emit; mode = DispatchMode.Waterfall;", StringComparison.Ordinal));
        Assert.False(fixture.Generate().IsValid);
    }
    [Fact]
    public void Unrelated_methods_comments_and_strings_are_not_event_consumers()
    {
        using var fixture = new Fixture(); fixture.Write("src/Events.cs", Definitions);
        fixture.Write("src/Fake.cs", """
            using Qian.Runtime;
            namespace Demo;
            class Fake {
                public void On(QianEvent<string,string?> evt, DispatchMode mode) { }
                public void Method() { On(Events.Changed, DispatchMode.Waterfall); }
                // ctx.On(Events.Changed, DispatchMode.Waterfall, handler);
                public const string Text = "ctx.On(Events.Changed, DispatchMode.Waterfall, handler)";
            }
            """);
        var result = fixture.Generate(); Assert.True(result.IsValid);
        Assert.Empty(Assert.Single(result.Events).Consumers);
    }
    [Fact]
    public void Syntax_errors_duplicate_definitions_and_unsafe_source_paths_are_rejected()
    {
        using var fixture = new Fixture(); fixture.Write("src/Events.cs", Definitions);
        fixture.Write("src/Duplicate.cs", Definitions.Replace("class Events", "class Duplicate", StringComparison.Ordinal));
        Assert.Contains(fixture.Generate().Diagnostics, d => d.Error.Errors![0].Code == "DOC_EVENT_DECLARATION_INVALID");
        fixture.Write("src/Duplicate.cs", "not valid c#;");
        Assert.False(fixture.Generate().IsValid);
        Assert.False(fixture.Generate(["../"]).IsValid);
    }
    [Fact]
    public void Named_constructor_arguments_and_property_aliases_resolve_the_same_definition()
    {
        using var fixture = new Fixture();
        fixture.Write("src/Events.cs", Definitions.Replace("QianEvent.Define<string, string?>(\"demo/changed\", DispatchMode.Emit)", "new(Mode: DispatchMode.Emit, Name: \"demo/changed\")", StringComparison.Ordinal));
        fixture.Write("src/Alias.cs", "using Qian.Runtime; namespace Demo; public static class Alias { public static QianEvent<string,string?> Changed => Events.Changed; }");
        fixture.Write("src/Calls.cs", Calls.Replace("using E = Demo.Events;", "using E = Demo.Alias;", StringComparison.Ordinal));
        var result = fixture.Generate(); Assert.True(result.IsValid, JsonSerializer.Serialize(result.Diagnostics));
        Assert.Single(Assert.Single(result.Events).Consumers);
    }
    [Fact]
    public void Mutable_definitions_cannot_bypass_mode_validation()
    {
        using var fixture = new Fixture();
        fixture.Write("src/Events.cs", Definitions.Replace("static readonly", "static", StringComparison.Ordinal));
        fixture.Write("src/Calls.cs", Calls);
        var result = fixture.Generate();
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d => d.Error.Errors![0].Code == "DOC_EVENT_DECLARATION_INVALID");
    }

    [Fact]
    public void Alias_attributes_must_match_the_original_event_mode()
    {
        using var fixture = new Fixture(); fixture.Write("src/Events.cs", Definitions);
        fixture.Write("src/Alias.cs", "using Qian.Runtime; namespace Demo; public static class Alias { [QianEvent(Mode=DispatchMode.Waterfall)] public static QianEvent<string,string?> Changed => Events.Changed; }");
        fixture.Write("src/Calls.cs", Calls.Replace("using E = Demo.Events;", "using E = Demo.Alias;", StringComparison.Ordinal));
        var result = fixture.Generate();
        Assert.False(result.IsValid);
        Assert.Single(result.Events[0].Consumers);
        Assert.Contains(result.Diagnostics, d => d.File == "src/Alias.cs" && d.Error.Errors![0].Code == "DOC_EVENT_MODE_MISMATCH");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("找不到仓库。");
    }
    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "qian-docs-" + Guid.NewGuid().ToString("N"));
        internal Fixture()
        {
            Write("profiles/minimal/qian.profile.yml", "name: minimal\nbundles: []\npatch: ./qian.patch.yml\n");
            // observer 先于 provider 写进 patch：它只以 optionalInject 依赖 Settings，装载顺序仍须把 provider 排在前面。
            Write("profiles/minimal/qian.patch.yml", "- {id: observer, plugin: test.observer, config: {}}\n- {id: provider, plugin: test.provider, config: {location: 旧位置, token: 'secretRef://fixture/value'}}\n- {id: consumer, plugin: test.consumer, config: {}}\n");
            Write("override.yml", "- {id: provider, config: {location: 新位置, token: 'secretRef://fixture/value'}}\n");
            foreach (var name in new[] { "provider", "consumer", "observer" })
            {
                var manifest = new Dictionary<string, object?>
                {
                    ["id"] = "test." + name, ["version"] = "1.0.0", ["description"] = "用于文档生成器验收的静态组件", ["author"] = new { name = "Qian" }, ["license"] = "Apache-2.0",
                    ["interface"] = new { displayName = name, category = "test" }, ["inject"] = name == "consumer" ? new[] { "Settings" } : [],
                    ["provides"] = name == "provider" ? new[] { "Settings" } : [], ["policy"] = new { tier = "private", sandbox = "none", authentication = "ON_INSTALL" }, ["scopes"] = new[] { "global" }
                };
                if (name == "observer") manifest["optionalInject"] = new[] { "Settings" };
                Write("plugins/" + name + "/.qian-plugin/plugin.json", JsonSerializer.Serialize(manifest));
            }
            Write("plugins/provider/schemas/config.schema.json", """{"type":"object","properties":{"location":{"type":"string"},"token":{"type":"string","x-qian-sensitive":true,"default":"SHOULD-NOT-BE-PUBLISHED"}}} """);
        }
        internal void Write(string file, string content) { var path = Path.Combine(Root, file); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content); }
        internal DocumentResult Generate(IReadOnlyList<string>? sources = null) => new DocumentGenerator(new ProfileLoader(new SurfaceValidator())).Generate(new DocumentOptions
        {
            Profile = new ProfileLoadOptions { WorkingDirectory = Root, Profile = "minimal", IncludeHomePatch = false, Patches = ["override.yml"] },
            SourceDirectories = sources ?? ["src"]
        });
        public void Dispose() => Directory.Delete(Root, true);
    }
}
