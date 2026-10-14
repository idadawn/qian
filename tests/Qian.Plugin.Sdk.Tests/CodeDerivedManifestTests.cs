using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Qian.Plugin.Sdk.Generators;
using Qian.Plugin.Tool;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;

namespace Qian.Plugin.Sdk.Tests;

// S1-RT-014 验收 2、3：组件源码经真实编译器、SDK 的两个源生成器与 System.Text.Json 源生成器编译成程序集，
// 再用 CI 里同一个 Qian.Plugin.Tool 进程（check / sync）比对唯一 plugin.json 与它声明的 tools 表面。
public sealed class CodeDerivedManifestTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Component = "qian.sdk-manifest";
    private const string ExportAttribute = """
        [Capability("note.export", typeof(NoteResponse), typeof(NotesJson), Description = "把当前租户的一条笔记推送到外部归档系统。",
            SideEffect = "external", PolicyHint = "deny", Idempotent = true)]
        """;
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using System.Threading;
        using System.Threading.Tasks;
        using Qian.Plugin.Sdk;
        using Qian.Runtime;
        namespace Fixture;
        [QianPlugin("qian.sdk-manifest")]
        public sealed class NotesPlugin : IQianPlugin
        {
            public static readonly ServiceKey Notes = new("SdkManifestNotes");
            public IReadOnlyList<ServiceKey> Inject => [];
            public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
            {
                ctx.Provide(Notes, new object());
                return ValueTask.CompletedTask;
            }
        }
        [Capability("note.create", typeof(NoteResponse), typeof(NotesJson), Description = "为当前租户新建一条笔记；修改已有笔记请用其它能力。",
            SideEffect = "write", Idempotent = true, RequiredScopes = ["qian.sdk-manifest:write"])]
        public sealed partial record CreateNote(Guid IdempotencyKey, string Title);
        [Capability("note.read", typeof(NoteResponse), typeof(NotesJson), Description = "按编号读取当前租户的一条笔记，不修改任何数据。",
            PolicyHint = "confirm", RequiredScopes = ["qian.sdk-manifest:read"])]
        public sealed partial record ReadNote(Guid Id);
        [Capability("note.purge", typeof(NoteResponse), typeof(NotesJson), Description = "永久删除当前租户的一条笔记，删除后无法恢复。",
            SideEffect = "destructive", Idempotent = true, RequiredScopes = ["qian.sdk-manifest:write", "qian.sdk-manifest:admin"])]
        public sealed partial record PurgeNote(Guid IdempotencyKey, Guid Id);
        /*export*/
        public sealed partial record ExportNote(Guid IdempotencyKey, Guid Id);
        public sealed record NoteResponse(Guid Id, string Title);
        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true)]
        [JsonSerializable(typeof(CreateNote))]
        [JsonSerializable(typeof(ReadNote))]
        [JsonSerializable(typeof(PurgeNote))]
        [JsonSerializable(typeof(ExportNote))]
        [JsonSerializable(typeof(NoteResponse))]
        public sealed partial class NotesJson : JsonSerializerContext;
        """;
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Append(typeof(CapabilityAttribute).Assembly.Location).Append(typeof(Qian.Runtime.IQianPlugin).Assembly.Location).Distinct(StringComparer.Ordinal)
        .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    private static readonly Lazy<ISourceGenerator> JsonGenerator = new(LoadJsonGenerator);

    [Fact]
    public async Task 源生成器把Capability的描述副作用策略提示与所需scope写入清单声明的tools表面_工具文件符合tool契约()
    {
        using var work = new WorkDirectory();
        var assembly = work.Compile("v1", Source.Replace("/*export*/", ExportAttribute, StringComparison.Ordinal));
        var component = work.Component();

        var sync = await Tool("sync", assembly, component);
        Assert.True(sync.ExitCode == 0, sync.Error);
        Assert.True(sync.Json.GetProperty("applied").GetBoolean());

        // 值与源码里 [Capability] 的写法逐项对应；未写 PolicyHint 时 destructive 派生为 confirm、其余派生为 allow，未写 RequiredScopes 时为空数组。
        (string Name, string Description, string SideEffect, string PolicyHint, string[] Scopes)[] expected =
        [
            ("note.create", "为当前租户新建一条笔记；修改已有笔记请用其它能力。", "write", "allow", ["qian.sdk-manifest:write"]),
            ("note.read", "按编号读取当前租户的一条笔记，不修改任何数据。", "read", "confirm", ["qian.sdk-manifest:read"]),
            ("note.purge", "永久删除当前租户的一条笔记，删除后无法恢复。", "destructive", "confirm", ["qian.sdk-manifest:write", "qian.sdk-manifest:admin"]),
            ("note.export", "把当前租户的一条笔记推送到外部归档系统。", "external", "deny", []),
        ];
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(component, ManifestPlan.ManifestFile)))!;
        Assert.Equal("./tools", manifest["surfaces"]!["tools"]!.GetValue<string>());
        Assert.Equal(expected.Select(item => "tools/" + item.Name + ".json").Order(StringComparer.Ordinal),
            manifest["x-qian-generated"]!["tools"]!.AsArray().Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal));
        Assert.Equal(["SdkManifestNotes"], manifest["provides"]!.AsArray().Select(item => item!.GetValue<string>()));
        Assert.Equal("Fixture.NotesPlugin, Qian.SdkManifestFixture", manifest["runtime"]!["entry"]!.GetValue<string>());
        foreach (var item in expected)
        {
            var tool = JsonNode.Parse(File.ReadAllText(Path.Combine(component, "tools", item.Name + ".json")))!;
            Assert.Equal(item.Name, tool["name"]!.GetValue<string>());
            Assert.Equal(item.Description, tool["description"]!.GetValue<string>());
            Assert.Equal(item.SideEffect, tool["sideEffect"]!.GetValue<string>());
            Assert.Equal(item.PolicyHint, tool["policyHint"]!.GetValue<string>());
            Assert.Equal(item.Scopes, tool["requiredScopes"]!.AsArray().Select(scope => scope!.GetValue<string>()));
            Assert.Equal(Component + "/" + item.Name, tool["binding"]!["id"]!.GetValue<string>());
            Assert.True(tool["x-qian-generated"]!["$generated"]!.GetValue<bool>());
        }
        var validation = new SurfaceValidator().ValidateComponent(component, new SurfaceValidationOptions { Capabilities = expected.Select(item => Component + "/" + item.Name).ToArray() });
        Assert.True(validation.IsValid, string.Join('\n', validation.Diagnostics.Select(d => Qian.Runtime.Errors.ErrorDetailJson.Serialize(d.Error))));

        var unchanged = await Tool("check", assembly, component);
        Assert.Equal(0, unchanged.ExitCode);
        Assert.Empty(unchanged.Json.GetProperty("changed").EnumerateArray());
    }

    [Fact]
    public async Task 删除代码中的一个Capability而保留清单派生条目_CI比对命令失败并以文件与JSON_Pointer指出漂移项_比对不改动任何文件()
    {
        using var work = new WorkDirectory();
        var withExport = work.Compile("v1", Source.Replace("/*export*/", ExportAttribute, StringComparison.Ordinal));
        var component = work.Component();
        Assert.Equal(0, (await Tool("sync", withExport, component)).ExitCode);

        // 删掉代码里的 [Capability("note.export", ...)]：请求类型仍在，只是不再声明为能力；清单与 tools/note.export.json 原样保留。
        var withoutExport = work.Compile("v2", Source.Replace("/*export*/", "", StringComparison.Ordinal));
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(component, ManifestPlan.ManifestFile)))!;
        Assert.Contains("tools/note.export.json", manifest["x-qian-generated"]!["tools"]!.AsArray().Select(item => item!.GetValue<string>()));
        var before = WorkDirectory.Snapshot(component);

        var drift = await Tool("check", withoutExport, component);
        Assert.Equal(1, drift.ExitCode);
        Assert.False(drift.Json.GetProperty("applied").GetBoolean());
        Assert.Equal([("tools/note.export.json", ""), (ManifestPlan.ManifestFile, "/x-qian-generated/tools")],
            drift.Json.GetProperty("changed").EnumerateArray().Select(item => (item.GetProperty("file").GetString()!, item.GetProperty("pointer").GetString()!)));
        Assert.Equal(before, WorkDirectory.Snapshot(component));

        // 代码没变时同一比对通过；按新代码 sync 后撤下过期工具，比对恢复通过。
        Assert.Equal(0, (await Tool("check", withExport, component)).ExitCode);
        Assert.Equal(0, (await Tool("sync", withoutExport, component)).ExitCode);
        Assert.False(File.Exists(Path.Combine(component, "tools/note.export.json")));
        var fixedUp = await Tool("check", withoutExport, component);
        Assert.Equal(0, fixedUp.ExitCode);
        Assert.Empty(fixedUp.Json.GetProperty("changed").EnumerateArray());
    }

    private static async Task<(int ExitCode, JsonElement Json, string Error)> Tool(string verb, string assembly, string component)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { Path.Combine(AppContext.BaseDirectory, "Qian.Plugin.Tool.dll"), verb, assembly, component }) start.ArgumentList.Add(value);
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync(Ct); var errors = child.StandardError.ReadToEndAsync(Ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try { await child.WaitForExitAsync(timeout.Token); }
        finally { if (!child.HasExited) child.Kill(entireProcessTree: true); }
        var stdout = await output; var stderr = await errors;
        Assert.True(stdout.Length != 0, verb + " 没有输出：" + stderr);
        using var json = JsonDocument.Parse(stdout);
        return (child.ExitCode, json.RootElement.Clone(), stderr);
    }

    // System.Text.Json 源生成器随 .NET 引用包分发，取与当前运行时同一主版本里最新的一份。
    private static ISourceGenerator LoadJsonGenerator()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "..", "..", ".."));
        var packs = new DirectoryInfo(Path.Combine(root, "packs", "Microsoft.NETCore.App.Ref"));
        var path = packs.Exists ? packs.EnumerateDirectories()
            .Select(directory => (Directory: directory, Version: Version.TryParse(directory.Name, out var version) ? version : null))
            .Where(item => item.Version?.Major == Environment.Version.Major).OrderByDescending(item => item.Version)
            .Select(item => Path.Combine(item.Directory.FullName, "analyzers", "dotnet", "cs", "System.Text.Json.SourceGeneration.dll"))
            .FirstOrDefault(File.Exists) : null;
        var reference = new AnalyzerFileReference(path ?? throw new InvalidOperationException("找不到 .NET 引用包里的 System.Text.Json 源生成器：" + packs.FullName), AnalyzerLoader.Instance);
        return Assert.Single(reference.GetGenerators(LanguageNames.CSharp), generator => generator.GetGeneratorType().Name == "JsonSourceGenerator");
    }

    private sealed class AnalyzerLoader : IAnalyzerAssemblyLoader
    {
        internal static readonly AnalyzerLoader Instance = new();
        public void AddDependencyLocation(string fullPath) { }
        public Assembly LoadFromPath(string fullPath) => AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
    }

    private sealed class WorkDirectory : IDisposable
    {
        private readonly string _root = Path.Combine(AppContext.BaseDirectory, "manifest-tool-tests-" + Guid.NewGuid().ToString("N"));

        internal string Compile(string version, string source)
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview);
            var compilation = CSharpCompilation.Create("Qian.SdkManifestFixture", [CSharpSyntaxTree.ParseText(source, parse)], References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                ImmutableArray.Create(new CapabilityGenerator().AsSourceGenerator(), new ComponentMetadataGenerator().AsSourceGenerator(), JsonGenerator.Value), parseOptions: parse);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics, Ct);
            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            var directory = Path.Combine(_root, version); Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "Qian.SdkManifestFixture.dll");
            var emitted = output.Emit(path, cancellationToken: Ct);
            Assert.True(emitted.Success, string.Join('\n', emitted.Diagnostics));
            return path;
        }

        internal string Component()
        {
            var directory = Path.Combine(_root, "component"); Directory.CreateDirectory(Path.Combine(directory, ".qian-plugin"));
            File.WriteAllText(Path.Combine(directory, ManifestPlan.ManifestFile), """
                {"id":"qian.sdk-manifest","version":"1.0.0","description":"验证代码派生字段与 tools 表面的 SDK 验收组件。",
                 "author":{"name":"Qian"},"license":"Apache-2.0","interface":{"displayName":"SDK 清单验收","category":"testing"},
                 "inject":[],"provides":[],"scopes":["global"],"surfaces":{"tools":"./tools"},
                 "policy":{"tier":"official","sandbox":"none","authentication":"ON_INSTALL"}}
                """);
            return directory;
        }

        internal static string Snapshot(string directory) => string.Join('\n', Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(file => Path.GetRelativePath(directory, file) + "\t" + Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)))));

        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }
}
