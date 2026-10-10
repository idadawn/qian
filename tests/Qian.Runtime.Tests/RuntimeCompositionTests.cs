using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Surfaces;

namespace Qian.Runtime.Tests;

public sealed class RuntimeCompositionTests
{
    [Fact]
    public async Task Shared_patch_scenarios_validate_before_applying_real_providers()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        using var scenarios = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "tck/runtime/assembly.json")));
        foreach (var item in scenarios.RootElement.EnumerateArray())
        {
            var directory = Path.Combine(Path.GetTempPath(), "qian-runtime-assembly-" + Guid.NewGuid().ToString("N"));
            try
            {
                var template = Path.Combine(root.FullName, "tck/profiles/base");
                foreach (var source in Directory.EnumerateFiles(template, "*", SearchOption.AllDirectories))
                    Write(Path.GetRelativePath(template, source), File.ReadAllText(source));
                foreach (var file in item.GetProperty("files").EnumerateObject()) Write(file.Name, file.Value.GetString()!);
                var result = new ProfileLoader(new SurfaceValidator()).Load(new ProfileLoadOptions
                {
                    WorkingDirectory = directory, Profile = "test", IncludeHomePatch = false,
                    Patches = item.GetProperty("patches").EnumerateArray().Select(p => p.GetString()!).ToArray()
                });
                Assert.Equal(item.GetProperty("valid").GetBoolean(), result.IsValid);
                using var services = new ServiceCollection().BuildServiceProvider();
                await using var runtime = new PluginLoader(services);
                List<string> applied = [];
                // 只有经过实际装配器的配置可以跨过启动边界；非法树不会制造一个假的 Ready/Failed 节点。
                if (result.IsValid)
                {
                    var definitions = result.Snapshot!.Entries.Select(entry => new PluginDefinition(
                        new PluginEntry(entry.Id, entry.Plugin.Split('@')[0], entry.Plugin.Split('@')[1], ScopeKey.Global, entry.Layer, entry.Config),
                        new FixtureProvider(entry, applied), entry.Provides.Select(k => new ServiceKey(k)).ToArray())).ToArray();
                    await runtime.MountAsync(definitions, TestContext.Current.CancellationToken);
                    var settings = Assert.Single(runtime.DumpConfig(), e => e.Id == "settings");
                    Assert.Equal(item.GetProperty("plugin").GetString(), settings.Plugin + "@" + settings.Version);
                    Assert.True(JsonElement.DeepEquals(item.GetProperty("config"), settings.Config));
                    Assert.Equal(item.GetProperty("layer").GetString(), result.Snapshot.Layers[settings.Layer].Kind);
                    Assert.Equal(["settings", "consumer"], applied);
                    Assert.Equal(item.GetProperty("consumer").GetString(), runtime.GetContext("consumer", ScopeKey.Global).Inject<string>(new ServiceKey("Settings")));
                    await runtime.DisposeAsync();
                    Assert.Empty(runtime.DumpConfig());
                }
                else
                {
                    Assert.Contains(result.Diagnostics, d => d.Error.Pointer == item.GetProperty("pointer").GetString());
                    Assert.Empty(applied);
                    Assert.Empty(runtime.DumpConfig());
                }
                void Write(string relative, string content)
                {
                    var target = Path.Combine(directory, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllText(target, content);
                }
            }
            finally { Directory.Delete(directory, true); }
        }
    }

    private sealed class FixtureProvider(ResolvedProfileEntry entry, List<string> applied) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => entry.Inject.Select(k => new ServiceKey(k)).ToArray();
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            applied.Add(entry.Id);
            if (entry.Provides.Contains("Settings", StringComparer.Ordinal)) ctx.Provide(new ServiceKey("Settings"), config.GetProperty("marker").GetString()!);
            else Assert.NotNull(ctx.Inject<string>(new ServiceKey("Settings")));
            return ValueTask.CompletedTask;
        }
    }
}
