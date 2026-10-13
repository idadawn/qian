using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Profiles.Abstractions;
using Qian.Runtime;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;

namespace Qian.Profiles.Tests;

public sealed class SkillOnlyTests
{
    [Fact]
    public async Task 共享无代码技能组件_真实minimal装配进入dump与技能目录_租户隔离并可卸载()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "tck/runtime/skill-only.json")));
        var spec = fixture.RootElement;
        var directory = Directory.CreateTempSubdirectory("qian-skill-only-net-");
        try
        {
            // 复制交付的 profile 与组合包，不能用自造空 profile 绕过真实装配。
            Copy("profiles/minimal"); Copy("plugins/qian.bundle.minimal");
            var component = Path.Combine(directory.FullName, spec.GetProperty("directory").GetString()!);
            foreach (var file in spec.GetProperty("files").EnumerateObject())
            {
                var target = Path.Combine(component, file.Name); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.WriteAllText(target, file.Value.GetString());
            }
            Assert.Equal(2, Directory.GetFiles(component, "*", SearchOption.AllDirectories).Length);
            Assert.False(Directory.Exists(Path.Combine(component, "src")));
            File.WriteAllText(Path.Combine(directory.FullName, "tenant.patch.yml"), spec.GetProperty("patch").GetString());
            var validator = new SurfaceValidator();
            var result = new ProfileLoader(validator).Load(new ProfileLoadOptions { WorkingDirectory = directory.FullName, Profile = spec.GetProperty("profile").GetString()!, IncludeHomePatch = false, Patches = ["tenant.patch.yml"] });
            Assert.True(result.IsValid, JsonSerializer.Serialize(result.Diagnostics)); Assert.Empty(result.Diagnostics);
            Assert.Equal(spec.GetProperty("bundle").GetString(), Assert.Single(result.Snapshot!.Layers, layer => layer.Kind == "bundle").Reference);
            var entry = Assert.Single(result.Snapshot.Entries);
            Assert.False(entry.Manifest.TryGetProperty("runtime", out _));
            var scope = ParseScope(entry.Scope);
            var plugin = new HotReloadSurfacePlugin(entry.ComponentDirectory, validator, new SurfaceValidationOptions { Profile = "minimal", ExecutionPlane = "server" });
            using var services = new ServiceCollection().BuildServiceProvider(); await using var runtime = new PluginLoader(services);
            await runtime.MountAsync([new PluginDefinition(new PluginEntry(entry.Id, spec.GetProperty("plugin").GetString()!, spec.GetProperty("version").GetString()!, scope, entry.Layer, entry.Config), plugin, plugin.SurfaceProvides)], TestContext.Current.CancellationToken);
            Assert.Equal(PluginState.Ready, runtime.GetState(entry.Id, scope));
            var context = runtime.GetContext(entry.Id, scope); var other = context.Scoped(ParseScope(spec.GetProperty("otherScope").GetString()!));
            var name = spec.GetProperty("plugin").GetString() + "/" + spec.GetProperty("skill").GetString();
            Assert.Equal(name, Assert.Single(context.List(SurfaceKeys.Skills)).Name); Assert.Equal(name, Assert.Single(context.List(SurfaceKeys.Tools)).Name);
            Assert.Empty(other.List(SurfaceKeys.Skills)); Assert.Empty(other.List(SurfaceKeys.Tools));
            var dumped = Assert.Single(runtime.DumpConfig()); Assert.Equal(entry.Id, dumped.Id); Assert.Equal(scope, dumped.Scope);
            await runtime.UnmountAsync(entry.Id, scope, TestContext.Current.CancellationToken); Assert.Empty(runtime.DumpConfig());
            if (Environment.GetEnvironmentVariable("QIAN_SKILL_ONLY_REPORT") is { Length: > 0 } report)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
                File.WriteAllText(report, JsonSerializer.Serialize(new { id = spec.GetProperty("id").GetString(), noCode = true, profile = result.Snapshot.Name,
                    entryId = dumped.Id, plugin = dumped.Plugin + "@" + dumped.Version, scope = SurfaceRuntimeDiagnostics.ScopeName(dumped.Scope), skill = name, otherVisible = false, clean = runtime.DumpConfig().Count == 0 }));
            }
            void Copy(string relative)
            {
                foreach (var original in Directory.EnumerateFiles(Path.Combine(root.FullName, relative), "*", SearchOption.AllDirectories))
                {
                    var target = Path.Combine(directory.FullName, Path.GetRelativePath(root.FullName, original)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(original, target);
                }
            }
        }
        finally { directory.Delete(true); }
    }
    private static ScopeKey ParseScope(string scope) { var parts = scope.Split(':', 2); return new ScopeKey(parts[0], parts[1]); }
}
