using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces.Tests;

public sealed class HotReloadTests
{
    private static readonly string[] Scopes = ["global", "tenant"];
    private static readonly string[] Provides = ["Code"];
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task 共用T19_真实文件监视只替换表面_代码对象与效果保持_卸载无残留()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "tck/runtime/hotreload.json")));
        var spec = fixture.RootElement; var directory = Directory.CreateTempSubdirectory("qian-hot-reload-");
        try
        {
            var pluginId = spec.GetProperty("plugin").GetString()!; var entry = spec.GetProperty("entry").GetString()!;
            var skill = spec.GetProperty("skill").GetString()!;
            Write(".qian-plugin/plugin.json", JsonSerializer.Serialize(new
            {
                id = pluginId, version = "1.0.0", description = "真实表面热重载的共用运行时场景。", author = new { name = "Qian" }, license = "Apache-2.0",
                @interface = new { displayName = "热重载", category = "testing" }, inject = Array.Empty<string>(), provides = Provides,
                policy = new { tier = "official", sandbox = "none", authentication = "ON_INSTALL" }, scopes = Scopes,
                runtime = new { form = "module", entry = typeof(CodePlugin).FullName + ", " + typeof(CodePlugin).Assembly.GetName().Name }
            }));
            Skill(spec.GetProperty("oldDescription").GetString()!); Write(spec.GetProperty("codeFile").GetString()!, "code one");
            var validation = new SurfaceValidator().ValidateComponent(directory.FullName, new SurfaceValidationOptions { Profile = "minimal", ExecutionPlane = "server" });
            Assert.True(validation.IsValid, JsonSerializer.Serialize(validation.Diagnostics));
            var code = new CodePlugin(); var plugin = new HotReloadSurfacePlugin(directory.FullName, new SurfaceValidator(), new SurfaceValidationOptions { Profile = "minimal", ExecutionPlane = "server" }, code);
            using var services = new ServiceCollection().BuildServiceProvider(); await using var runtime = new PluginLoader(services);
            var scope = new ScopeKey("tenant", "a");
            await runtime.MountAsync([new PluginDefinition(new PluginEntry(entry, pluginId, "1.0.0", scope, 0, JsonSerializer.SerializeToElement(new { })), plugin, [new ServiceKey("Code"), .. plugin.SurfaceProvides])], Ct);
            Assert.Equal(PluginState.Ready, runtime.GetState(entry, scope));
            var context = runtime.GetContext(entry, scope); var original = context.Inject<object>(new ServiceKey("Code"));
            Assert.Single(context.List(SurfaceKeys.Skills)); Assert.Single(context.List(SurfaceKeys.Tools));
            Assert.Empty(context.Scoped(new ScopeKey("tenant", "b")).List(SurfaceKeys.Tools));
            var started = Stopwatch.StartNew(); Skill(spec.GetProperty("newDescription").GetString()!);
            await Wait(() => Description(context) == spec.GetProperty("newDescription").GetString(), spec.GetProperty("deadlineMs").GetInt32());
            Assert.True(started.ElapsedMilliseconds <= spec.GetProperty("deadlineMs").GetInt32());
            Assert.Same(original, context.Inject<object>(new ServiceKey("Code")));
            Assert.Equal(spec.GetProperty("expectedCodeApplies").GetInt32(), code.Applies);
            Assert.Equal(spec.GetProperty("expectedCodeDisposalsBeforeUnmount").GetInt32(), code.Disposals);
            Write(spec.GetProperty("codeFile").GetString()!, "code changed");
            await Wait(() => plugin.Status.RestartRequired, 5000);
            Assert.Same(original, context.Inject<object>(new ServiceKey("Code"))); Assert.Equal(1, code.Applies);
            Skill("bad"); await Wait(() => plugin.Status.Diagnostics.Any(item => item.Severity == "error"), 5000);
            Assert.Equal(spec.GetProperty("newDescription").GetString(), Description(context));
            Skill(spec.GetProperty("oldDescription").GetString()!); await Wait(() => Description(context) == spec.GetProperty("oldDescription").GetString(), 5000);
            await runtime.UnmountAsync(entry, scope, Ct);
            Assert.Equal(spec.GetProperty("expectedCodeDisposalsAfterUnmount").GetInt32(), code.Disposals);
            Assert.Empty(runtime.DumpConfig());
            if (Environment.GetEnvironmentVariable("QIAN_HOT_RELOAD_REPORT") is { Length: > 0 } report)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
                File.WriteAllText(report, JsonSerializer.Serialize(new { id = spec.GetProperty("id").GetString(), codeApplies = code.Applies, codeDisposals = code.Disposals, skillUpdated = true, codeStable = true, scoped = true, invalidRetained = true, restartRequired = plugin.Status.RestartRequired, clean = runtime.DumpConfig().Count == 0 }));
            }
            void Skill(string description) => Write("skills/" + skill + "/SKILL.md", "---\nname: " + skill + "\ndescription: " + description + "\n---\n检查当前 scope 的内容。\n");
            void Write(string path, string contents) { var file = Path.Combine(directory.FullName, path); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, contents); }
        }
        finally { directory.Delete(true); }
    }
    private static string? Description(IPluginContext context) => ((SurfaceDocument)Assert.Single(context.List(SurfaceKeys.Tools)).Value).Data.GetProperty("description").GetString();
    private static async Task Wait(Func<bool> predicate, int milliseconds)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct); deadline.CancelAfter(milliseconds);
        while (!predicate()) await Task.Delay(20, deadline.Token);
    }
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Qian.slnx"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("未找到共用 TCK。");
    }
    private sealed class CodePlugin : IQianPlugin
    {
        internal int Applies; internal int Disposals;
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext context, JsonElement config, CancellationToken cancellationToken)
        { Applies++; context.Provide(new ServiceKey("Code"), new object()); context.Effect(() => Disposals++); return ValueTask.CompletedTask; }
    }
}
