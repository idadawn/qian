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
            }
            if (item.TryGetProperty("warnings", out var warnings)) Assert.Equal(warnings.GetInt32(), result.Diagnostics.Count(d => d.Severity == "warning"));
            Assert.DoesNotContain("DO-NOT-LEAK", JsonSerializer.Serialize(result, Json), StringComparison.Ordinal);
            var normalized = JsonSerializer.SerializeToNode(result, Json)!;
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
    [InlineData("platform", 21)]
    [InlineData("gateway", 5)]
    [InlineData("identity", 8)]
    [InlineData("standalone", 2)]
    public void Shipped_profiles_resolve_with_explicit_skeleton_warnings(string profile, int count)
    {
        var result = new ProfileLoader(new SurfaceValidator()).Load(new ProfileLoadOptions { WorkingDirectory = Root, Profile = profile, IncludeHomePatch = false });
        Assert.True(result.IsValid, JsonSerializer.Serialize(result, Json));
        Assert.Equal(count, result.Snapshot!.Entries.Count);
        Assert.Equal(count, result.Diagnostics.Count(d => d.Severity == "warning"));
        if (profile != "platform") Assert.DoesNotContain(result.Snapshot.Entries, e => e.Plugin.StartsWith("qian.registry@", StringComparison.Ordinal) || e.Plugin.StartsWith("qian.lifecycle@", StringComparison.Ordinal) || e.Plugin.StartsWith("qian.capabilities@", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Custom_empty_profile_and_declarative_component_start_on_the_real_runtime()
    {
        using var item = JsonDocument.Parse("""{"files":{"profiles/test/qian.profile.yml":"{\"name\":\"test\",\"bundles\":[]}"},"patches":[],"home":false}""");
        using var fixture = new Fixture(item.RootElement);
        var reader = new SurfaceValidator();
        var composer = new ProfileLoader(reader);
        var empty = composer.Load(fixture.Options);
        Assert.True(empty.IsValid);
        Assert.Empty(empty.Snapshot!.Entries);
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var runtime = new PluginLoader(services);
        await runtime.MountAsync([], TestContext.Current.CancellationToken);
        await runtime.WhenIdleAsync(TestContext.Current.CancellationToken);
        // 只在自建测试 profile 中加入真实静态命令，不能替代待裁决的默认 minimal。
        var componentDirectory = Path.Combine(fixture.Directory, "plugins/other");
        Directory.CreateDirectory(Path.Combine(componentDirectory, "commands"));
        File.WriteAllText(Path.Combine(componentDirectory, "commands/hello.md"), "---\ndescription: 这是用于验证真实表面装载与释放的测试命令。\n---\n你好。\n");
        File.WriteAllText(Path.Combine(fixture.Directory, "profiles/test/qian.profile.yml"), "name: test\nbundles: []\npatch: ./qian.patch.yml\n");
        File.WriteAllText(Path.Combine(fixture.Directory, "profiles/test/qian.patch.yml"), "- id: hello\n  plugin: test.other\n  config: {}\n");
        var result = composer.Load(fixture.Options);
        Assert.True(result.IsValid);
        var entry = Assert.Single(result.Snapshot!.Entries);
        var parsed = reader.ValidateComponent(entry.ComponentDirectory, new SurfaceValidationOptions { Profile = "test" });
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
                var target = Path.Combine(Directory, file.Name); System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllText(target, file.Value.GetString());
            }
            if (item.TryGetProperty("missingHome", out var absent) && absent.GetBoolean()) File.Delete(Path.Combine(Directory, "home/qian.patch.yml"));
            Options = new ProfileLoadOptions { WorkingDirectory = Directory, Profile = "test", HomeDirectory = "home", IncludeHomePatch = item.GetProperty("home").GetBoolean(), Patches = item.GetProperty("patches").EnumerateArray().Select(v => v.GetString()!).ToArray() };
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }
}
