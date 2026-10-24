using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Qian.Profiles.Abstractions;
using Qian.Runtime;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;

namespace Qian.Profiles.Tests;

public sealed class ProfileTests
{
    private static readonly string Root = FindRoot();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    [Fact]
    public void Shared_composition_tck_checks_results_and_emits_parity_report()
    {
        var report = new JsonObject();
        using var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tck/profiles/cases.json")));
        foreach (var item in cases.RootElement.EnumerateArray())
        {
            using var fixture = new Fixture(item);
            var result = new ProfileLoader(new SurfaceValidator()).Load(fixture.Options);
            var name = item.GetProperty("name").GetString()!;
            Assert.True(result.IsValid == item.GetProperty("expect").GetBoolean(), name + ": " + JsonSerializer.Serialize(result, Json));
            if (item.TryGetProperty("pointer", out var expectedPointer)) Assert.True(result.Diagnostics.Any(d => d.Error.Pointer == expectedPointer.GetString()), name + ": " + JsonSerializer.Serialize(result.Diagnostics, Json));
            if (item.TryGetProperty("reason", out var reason))
                Assert.True(result.Diagnostics.Any(d => d.Error.Errors?.Any(e => e.Pointer == item.GetProperty("pointer").GetString() && e.Message == reason.GetString()) == true), name + ": " + JsonSerializer.Serialize(result.Diagnostics, Json));
            if (result.IsValid)
            {
                var entries = result.Snapshot!.Entries;
                if (item.TryGetProperty("count", out var count)) Assert.Equal(count.GetInt32(), entries.Count);
                var settings = entries.FirstOrDefault(e => e.Id == "settings" && e.Scope == "global");
                if (item.TryGetProperty("port", out var port)) Assert.Equal(port.GetInt32(), settings!.Config.GetProperty("port").GetInt32());
                if (item.TryGetProperty("marker", out var marker)) Assert.Equal(marker.GetString(), settings!.Config.GetProperty("marker").GetString());
                if (item.TryGetProperty("noObsolete", out _)) Assert.False(settings!.Config.TryGetProperty("obsolete", out _));
                if (item.TryGetProperty("version", out var version)) Assert.Equal("test.settings@" + version.GetString(), settings!.Plugin);
                if (entries.Any(e => e.Id == "consumer")) Assert.True(result.Snapshot.MountOrder.ToList().IndexOf("global/settings") < result.Snapshot.MountOrder.ToList().IndexOf("global/consumer"));
                // optionalInject 的 Provider 在树里时先于消费方装载；缺席时消费方照常装载。
                if (entries.Any(e => e.Id == "observer")) Assert.True(result.Snapshot.MountOrder.ToList().IndexOf("global/settings") < result.Snapshot.MountOrder.ToList().IndexOf("global/observer"), name);
            }
            if (item.TryGetProperty("warnings", out var warnings)) Assert.Equal(warnings.GetInt32(), result.Diagnostics.Count(d => d.Severity == "warning"));
            Assert.DoesNotContain("DO-NOT-LEAK", JsonSerializer.Serialize(result, Json), StringComparison.Ordinal);
            if (item.TryGetProperty("disabledCount", out var disabledCount)) Assert.Equal(disabledCount.GetInt32(), result.Snapshot!.DisabledEntries.Count);
            var normalized = JsonSerializer.SerializeToNode(result, Json)!;
            if (item.TryGetProperty("scopePatches", out _)) normalized["disabledEntries"] = JsonSerializer.SerializeToNode(result.Snapshot?.DisabledEntries, Json);
            foreach (var diagnostic in normalized["diagnostics"]!.AsArray())
            {
                var error = diagnostic!["error"]!.AsObject(); error["trace_id"] = "TRACE";
                foreach (var key in new[] { "instance", "allowed" }) if (error[key] is null) error.Remove(key);
                foreach (var field in error["errors"]!.AsArray()) if (field!["allowed"] is null) field.AsObject().Remove("allowed");
            }
            report[name] = normalized;
        }
        WriteReport(report);
    }

    [Theory]
    // 警告只来自仍带骨架标记的条目；qian.data / qian.events / qian.config / qian.files / qian.identity.client 经真实安装后不再是骨架。
    [InlineData("platform", 21, 17)]
    [InlineData("gateway", 6, 5)]
    [InlineData("identity", 8, 6)]
    [InlineData("standalone", 3, 1)]
    [InlineData("minimal", 0, 0)]
    public void Shipped_profiles_resolve_with_correct_business_boundaries(string profile, int count, int skeletons)
    {
        var result = new ProfileLoader(new SurfaceValidator()).Load(new ProfileLoadOptions { WorkingDirectory = Root, Profile = profile, IncludeHomePatch = false });
        Assert.True(result.IsValid, JsonSerializer.Serialize(result, Json));
        Assert.Equal(count, result.Snapshot!.Entries.Count);
        Assert.Equal(skeletons, result.Diagnostics.Count(d => d.Severity == "warning"));
        if (profile != "platform") Assert.DoesNotContain(result.Snapshot.Entries, e => e.Plugin.StartsWith("qian.registry@", StringComparison.Ordinal) || e.Plugin.StartsWith("qian.lifecycle@", StringComparison.Ordinal) || e.Plugin.StartsWith("qian.capabilities@", StringComparison.Ordinal));
    }

    [Fact]
    public void Gateway原生profile仅装配声明兼容的代码组件()
    {
        var file = Path.Combine(Root, "profiles/gateway/qian.profile.yml");
        var definition = new SurfaceValidator().Parse(file, File.ReadAllText(file));
        Assert.True(definition.Syntax!.Data.GetProperty("build").GetProperty("aot").GetBoolean());
        var shipped = new ProfileLoader(new SurfaceValidator()).Load(new ProfileLoadOptions { WorkingDirectory = Root, Profile = "gateway", IncludeHomePatch = false });
        Assert.True(shipped.IsValid, JsonSerializer.Serialize(shipped, Json));
        Assert.All(shipped.Snapshot!.Entries.Where(entry => entry.Manifest.TryGetProperty("runtime", out _)),
            entry => Assert.True(entry.Manifest.GetProperty("x-aot").GetBoolean()));
        var identity = Assert.Single(shipped.Snapshot!.Entries, e => e.Id == "identity");
        Assert.StartsWith("qian.identity.client@", identity.Plugin, StringComparison.Ordinal);
        Assert.Equal("module", identity.Manifest.GetProperty("runtime").GetProperty("form").GetString());
        Assert.True(identity.Manifest.GetProperty("x-aot").GetBoolean());
    }

    [Fact]
    public void Gateway原生数据库绑定与生产宿主默认角色一致()
    {
        var result = new ProfileLoader(new SurfaceValidator()).Load(new ProfileLoadOptions { WorkingDirectory = Root, Profile = "gateway", IncludeHomePatch = false });
        Assert.True(result.IsValid, JsonSerializer.Serialize(result, Json));
        var data = Assert.Single(result.Snapshot!.Entries, entry => entry.Id == "data");
        var audit = Assert.Single(result.Snapshot.Entries, entry => entry.Id == "audit");
        Assert.StartsWith("qian.data.native@", data.Plugin, StringComparison.Ordinal);
        Assert.StartsWith("qian.audit.native@", audit.Plugin, StringComparison.Ordinal);
        var bindings = data.Config.GetProperty("connections").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Contains(audit.Config.GetProperty("tenantBinding").GetString(), bindings);
        Assert.Contains(audit.Config.GetProperty("platformBinding").GetString(), bindings);
        using var host = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "hosts/Qian.Gateway.Host/appsettings.json")));
        foreach (var field in new[] { "TenantRole", "PlatformRole", "FunctionOwner" })
            Assert.Equal(host.RootElement.GetProperty("Audit").GetProperty(field).GetString(),
                audit.Config.GetProperty(char.ToLowerInvariant(field[0]) + field[1..]).GetString());
        Assert.Contains(data.Manifest.GetProperty("provides").EnumerateArray(), value => value.GetString() == "NativeData");
        Assert.DoesNotContain(data.Manifest.GetProperty("provides").EnumerateArray(), value => value.GetString() == "Data");
    }

    [Fact]
    public async Task Shipped_minimal_profile_starts_the_real_runtime_and_mounts_a_declarative_overlay()
    {
        using var item = JsonDocument.Parse("""{"files":{},"patches":[],"home":false}""");
        using var fixture = new Fixture(item.RootElement);
        var reader = new SurfaceValidator();
        var composer = new ProfileLoader(reader);
        // 直接消费仓库交付文件，不能用临时空树替代默认 minimal 验收。
        var empty = composer.Load(new ProfileLoadOptions { WorkingDirectory = Root, Profile = "minimal", IncludeHomePatch = false });
        Assert.True(empty.IsValid, JsonSerializer.Serialize(empty.Diagnostics, Json));
        Assert.Empty(empty.Diagnostics);
        Assert.Empty(empty.Snapshot!.Entries);
        Assert.Empty(empty.Snapshot.MountOrder);
        Assert.Equal("qian.bundle.minimal@1.0.0", Assert.Single(empty.Snapshot.Layers, layer => layer.Kind == "bundle").Reference);
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var runtime = new PluginLoader(services);
        await runtime.MountAsync([], TestContext.Current.CancellationToken);
        await runtime.WhenIdleAsync(TestContext.Current.CancellationToken);
        // 同一 minimal 基准宿主可通过 overlay 装载无代码表面，不需要业务 Provider。
        var componentDirectory = Path.Combine(fixture.Directory, "plugins/other");
        Directory.CreateDirectory(Path.Combine(componentDirectory, "commands"));
        File.WriteAllText(Path.Combine(componentDirectory, "commands/hello.md"), "---\ndescription: 这是用于验证真实表面装载与释放的测试命令。\n---\n你好。\n");
        var patch = Path.Combine(fixture.Directory, "minimal.patch.yml");
        File.WriteAllText(patch, "- id: hello\n  plugin: test.other\n  config: {}\n");
        var result = composer.Load(new ProfileLoadOptions
        {
            WorkingDirectory = Root, Profile = "minimal", IncludeHomePatch = false,
            Patches = [patch], ComponentDirectories = [componentDirectory]
        });
        Assert.True(result.IsValid, JsonSerializer.Serialize(result.Diagnostics, Json));
        Assert.Empty(result.Diagnostics);
        var entry = Assert.Single(result.Snapshot!.Entries);
        var parsed = reader.ValidateComponent(entry.ComponentDirectory, new SurfaceValidationOptions { Profile = "minimal" });
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Diagnostics, Json));
        var plugin = new DeclarativeSurfacePlugin(parsed);
        var identity = entry.Plugin.Split('@');
        var definition = new PluginDefinition(new PluginEntry(entry.Id, identity[0], identity[1], ScopeKey.Global, entry.Layer, entry.Config), plugin, plugin.Provides);
        await runtime.MountAsync([definition], TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Ready, runtime.GetState("hello", ScopeKey.Global));
        var context = runtime.GetContext("hello", ScopeKey.Global);
        Assert.Single(context.List(SurfaceKeys.Commands));
    }

    private static void WriteReport(JsonObject report)
    {
        var file = Environment.GetEnvironmentVariable("QIAN_PROFILE_REPORT");
        if (string.IsNullOrEmpty(file)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        File.WriteAllText(file, report.ToJsonString(Json));
    }
    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Qian.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("找不到仓库。");
    }
    private sealed class Fixture : IDisposable
    {
        internal string Directory { get; } = Path.Combine(Path.GetTempPath(), "qian-profile-" + Guid.NewGuid().ToString("N"));
        internal ProfileLoadOptions Options { get; }
        internal Fixture(JsonElement item)
        {
            var template = Path.Combine(Root, "tck/profiles/base");
            foreach (var source in System.IO.Directory.EnumerateFiles(template, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(Directory, Path.GetRelativePath(template, source));
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target);
            }
            foreach (var file in item.GetProperty("files").EnumerateObject())
            {
                var target = Path.Combine(Directory, file.Name); System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                // 字符串按 UTF-8 写入；{"base64": …} 按原样字节写入，用于 BOM 与其他编码。
                if (file.Value.ValueKind == JsonValueKind.Object) File.WriteAllBytes(target, Convert.FromBase64String(file.Value.GetProperty("base64").GetString()!));
                else File.WriteAllText(target, file.Value.GetString());
            }
            if (item.TryGetProperty("missingHome", out var absent) && absent.GetBoolean()) File.Delete(Path.Combine(Directory, "home/qian.patch.yml"));
            Options = new ProfileLoadOptions { WorkingDirectory = Directory, Profile = "test", HomeDirectory = "home", IncludeHomePatch = item.GetProperty("home").GetBoolean(), Patches = item.GetProperty("patches").EnumerateArray().Select(v => v.GetString()!).ToArray(),
                ScopePatches = item.TryGetProperty("scopePatches", out var scoped) ? scoped.Deserialize<ScopePatchLayer[]>(Json)! : [] };
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
