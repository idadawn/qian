using System.Text.Json;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qian.Data;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;

namespace Qian.Registry.Tests;

public sealed partial class PatchPersistenceTests
{
    [Fact]
    public async Task 真库通知驱动真实Registry插件和装载器_新增重载禁用撤销均在五秒内生效且B不变()
    {
        var readerTenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(readerTenant);
        var writerTenant = new AsyncLocalTenantContext();
        using var writerHost = Host(writerTenant); await writerHost.StartAsync(Ct);
        var repository = Repository(writerHost, writerTenant);
        var writer = new RegistryPatchWriter(repository, new RegistryProfileComposer(repository, new ProfileLoader(new SurfaceValidator()), running.Profile.Options));
        var globalContext = running.Runtime.GetContext("shared", ScopeKey.Global);
        using (readerTenant.BeginScope("a")) await running.Registry.SynchronizeAsync(TenantA, Ct);
        using (readerTenant.BeginScope("b")) await running.Registry.SynchronizeAsync(TenantB, Ct);
        Assert.Equal(["test.shared/hello"], running.Commands(TenantA)); Assert.Equal(["test.shared/hello"], running.Commands(TenantB));
        using var writing = writerTenant.BeginScope("a");
        await writer.PutAsync(TenantA, "private", Patch("""{"id":"private","plugin":"test.private","config":{"label":"A-only"}}"""), 0, "operator-a", Ct);
        await WithinFiveSeconds(() => running.Commands(TenantA).Contains("test.private/hello", StringComparer.Ordinal));
        Assert.Equal(["test.shared/hello"], running.Commands(TenantB));

        await writer.PutAsync(TenantA, "shared", Patch("""{"id":"shared","config":{"label":"A"}}"""), 0, "operator-a", Ct);
        await WithinFiveSeconds(() => running.Label("shared", TenantA) == "A");
        var original = running.Runtime.GetContext("shared", TenantA);
        await writer.PutAsync(TenantA, "shared", Patch("""{"id":"shared","config":{"label":"A-updated"}}"""), 1, "operator-a", Ct);
        await WithinFiveSeconds(() => running.Label("shared", TenantA) == "A-updated");
        Assert.NotSame(original, running.Runtime.GetContext("shared", TenantA));
        Assert.Throws<ObjectDisposedException>(() => original.List(SurfaceKeys.Commands));
        Assert.Equal("global", globalContext.Entry.Config.GetProperty("label").GetString());

        await writer.PutAsync(TenantA, "shared", Patch("""{"id":"shared","disabled":true}"""), 2, "operator-a", Ct);
        await WithinFiveSeconds(() => !running.Commands(TenantA).Contains("test.shared/hello", StringComparer.Ordinal));
        Assert.Equal(["test.private/hello"], running.Commands(TenantA));
        var hidden = Assert.Throws<QianErrorException>(() => running.Root.Scoped(TenantA).Inject<SurfaceDocument>(SurfaceKeys.Commands, "test.shared/hello"));
        Assert.Equal("NOT_FOUND", hidden.Error.Code.Code);
        Assert.Equal(["test.shared/hello"], running.Commands(TenantB));

        await writer.RemoveAsync(TenantA, "private", 1, "operator-a", Ct);
        await WithinFiveSeconds(() => running.Commands(TenantA).Length == 0);
        await writer.RemoveAsync(TenantA, "shared", 3, "operator-a", Ct);
        await WithinFiveSeconds(() => running.Commands(TenantA).SequenceEqual(["test.shared/hello"], StringComparer.Ordinal));
        Assert.DoesNotContain(running.Runtime.DumpConfig(), entry => entry.Scope == TenantA);
        Assert.Same(globalContext, running.Runtime.GetContext("shared", ScopeKey.Global));
        Assert.Equal(["test.shared/hello"], running.Commands(TenantB));
    }

    [Fact]
    public async Task 已活动scope读到坏配置会停止可见项_只阻断A并在修复后恢复_重复核对不重装()
    {
        var readerTenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(readerTenant);
        var writerTenant = new AsyncLocalTenantContext();
        using var writerHost = Host(writerTenant); await writerHost.StartAsync(Ct);
        var repository = Repository(writerHost, writerTenant);
        using (readerTenant.BeginScope("a")) await running.Registry.SynchronizeAsync(TenantA, Ct);
        using (readerTenant.BeginScope("b")) await running.Registry.SynchronizeAsync(TenantB, Ct);
        using var writing = writerTenant.BeginScope("a");
        await repository.PutAsync(TenantA, "shared", Patch("""{"id":"shared","config":{"label":"initial"}}"""), 0, "operator-a", Ct);
        await WithinFiveSeconds(() => running.Label("shared", TenantA) == "initial");
        var original = running.Runtime.GetContext("shared", TenantA);
        using (readerTenant.BeginScope("a")) await running.Registry.SynchronizeAsync(TenantA, Ct);
        Assert.Same(original, running.Runtime.GetContext("shared", TenantA));
        // 模拟管理面之外的坏数据；正式写入入口已在事务内拒绝此配置。
        await repository.PutAsync(TenantA, "shared", Patch("""{"id":"shared","config":{"label":42}}"""), 1, "operator-a", Ct);
        await WithinFiveSeconds(() => running.Commands(TenantA).Length == 0);
        Assert.Equal(["test.shared/hello"], running.Commands(TenantB));
        Assert.DoesNotContain(running.Runtime.DumpConfig(), entry => entry.Scope == TenantA);
        await repository.PutAsync(TenantA, "shared", Patch("""{"id":"shared","config":{"label":"recovered"}}"""), 2, "operator-a", Ct);
        await WithinFiveSeconds(() => running.Label("shared", TenantA) == "recovered");
        Assert.Equal(["test.shared/hello"], running.Commands(TenantA)); Assert.Equal(["test.shared/hello"], running.Commands(TenantB));
    }

    [Fact]
    public async Task 宿主同步入口要求真实租户上下文_拒绝跨租户与未经归属验证的scope_停止后无局部残留()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant);
        await AssertCode(PlatformErrors.Unauthenticated, () => running.Registry.SynchronizeAsync(TenantA, Ct).AsTask());
        using (tenant.BeginScope("a"))
        {
            await AssertCode(PlatformErrors.NotFound, () => running.Registry.SynchronizeAsync(TenantB, Ct).AsTask());
            await AssertCode(PlatformErrors.NotFound, () => running.Registry.SynchronizeAsync(new ScopeKey("app", "unproven"), Ct).AsTask());
            var repository = Repository(running.Host, tenant);
            var writer = new RegistryPatchWriter(repository, new RegistryProfileComposer(repository, new ProfileLoader(new SurfaceValidator()), running.Profile.Options));
            await AssertCode(PlatformErrors.InputInvalid, () => writer.PutAsync(TenantA, "registry", Patch("""{"id":"registry","disabled":true}"""), 0, "actor", Ct));
            await writer.PutAsync(TenantA, "private", Patch("""{"id":"private","plugin":"test.private","config":{}}"""), 0, "actor", Ct);
            await running.Registry.SynchronizeAsync(TenantA, Ct);
            Assert.Contains("test.private/hello", running.Commands(TenantA));
        }
        await running.Registry.DisposeAsync();
        Assert.DoesNotContain(running.Runtime.DumpConfig(), entry => entry.Scope == TenantA);
        await Assert.ThrowsAsync<ServiceNotReadyException>(() => running.Registry.SynchronizeAsync(TenantA, Ct).AsTask());
    }

    [Fact]
    public async Task 宿主解析器不能把租户A条目改写到B_配置修复后后台可恢复()
    {
        var tenant = new AsyncLocalTenantContext(); var corrupt = 1;
        await using var running = await CreateRunningRegistry(tenant, definition => Volatile.Read(ref corrupt) == 1 ? definition with { Entry = definition.Entry with { Scope = TenantB } } : definition);
        using (tenant.BeginScope("a"))
        {
            var repository = Repository(running.Host, tenant);
            await repository.PutAsync(TenantA, "private", Patch("""{"id":"private","plugin":"test.private","config":{}}"""), 0, "actor", Ct);
            await Assert.ThrowsAsync<InvalidOperationException>(() => running.Registry.SynchronizeAsync(TenantA, Ct).AsTask());
        }
        Assert.DoesNotContain(running.Runtime.DumpConfig(), entry => entry.Scope == TenantB);
        Assert.Empty(running.Commands(TenantA)); Assert.Equal(["test.shared/hello"], running.Commands(TenantB));
        Volatile.Write(ref corrupt, 0);
        await WithinFiveSeconds(() => running.Commands(TenantA).Contains("test.private/hello", StringComparer.Ordinal));
        Assert.Equal(["test.shared/hello"], running.Commands(TenantB));
    }

    [Fact]
    public async Task 整树释放会等待Registry后台任务退出_再次释放服务不死锁不残留()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant);
        using (tenant.BeginScope("a"))
        {
            await Repository(running.Host, tenant).PutAsync(TenantA, "private", Patch("""{"id":"private","plugin":"test.private","config":{}}"""), 0, "actor", Ct);
            await running.Registry.SynchronizeAsync(TenantA, Ct);
        }
        await running.Runtime.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.Empty(running.Runtime.DumpConfig());
        await running.Registry.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await Assert.ThrowsAsync<ServiceNotReadyException>(() => running.Registry.SynchronizeAsync(TenantA, Ct).AsTask());
    }

    private async Task<RunningRegistry> CreateRunningRegistry(AsyncLocalTenantContext tenant, Func<PluginDefinition, PluginDefinition>? modify = null, bool start = true,
        Action<IServiceCollection>? services = null)
    {
        var host = Host(tenant, services: services); await host.StartAsync(Ct); await Prepare(host);
        var profile = new RuntimeProfile(); var validator = new SurfaceValidator(); var profiles = new ProfileLoader(validator);
        var logger = new RecordingLogger(); var runtime = new PluginLoader(host.Services, logger);
        RegistryPlugin? registry = null;
        PluginDefinition Resolve(ResolvedProfileEntry entry)
        {
            var identity = entry.Plugin.Split('@', 2);
            var scope = entry.Scope == "global" ? ScopeKey.Global : new ScopeKey(entry.Scope.Split(':', 2)[0], entry.Scope.Split(':', 2)[1]);
            IQianPlugin plugin; IReadOnlyList<ServiceKey> provides;
            if (entry.Plugin.StartsWith("qian.data@", StringComparison.Ordinal)) { plugin = new DataPlugin(); provides = [DataDefinition.Key]; }
            else if (entry.Plugin.StartsWith("qian.registry@", StringComparison.Ordinal)) { plugin = registry!; provides = [RegistryDefinition.Key]; }
            else
            {
                var surfaces = new DeclarativeSurfacePlugin(validator.ValidateComponent(entry.ComponentDirectory, new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true }));
                plugin = surfaces; provides = surfaces.Provides;
            }
            var definition = new PluginDefinition(new PluginEntry(entry.Id, identity[0], identity[1], scope, entry.Layer, entry.Config), plugin, provides);
            return scope != ScopeKey.Global && modify is not null ? modify(definition) : definition;
        }
        var installed = new List<RegistryComponentBinding>();
        registry = new RegistryPlugin(new RegistryRuntimeOptions { InstalledComponents = installed, Runtime = runtime, Profiles = profiles, Profile = profile.Options, Surfaces = validator, Resolve = Resolve });
        var snapshot = profiles.Load(profile.Options); Assert.True(snapshot.IsValid, JsonSerializer.Serialize(snapshot.Diagnostics));
        var definitions = snapshot.Snapshot!.Entries.Select(entry =>
        {
            var definition = Resolve(entry);
            installed.Add(new RegistryComponentBinding(definition, validator.ValidateComponent(entry.ComponentDirectory, ArtifactOptions)));
            return definition;
        }).ToArray();
        var privateEntry = profile.PrivateEntry(validator);
        installed.Add(new RegistryComponentBinding(Resolve(privateEntry), validator.ValidateComponent(privateEntry.ComponentDirectory, ArtifactOptions)));
        await runtime.MountAsync(definitions, Ct);
        Assert.All(snapshot.Snapshot.Entries, entry => Assert.Equal(PluginState.Ready, runtime.GetState(entry.Id, ScopeKey.Global)));
        var root = runtime.GetContext("registry", ScopeKey.Global); var service = root.Registry();
        if (start) await service.StartAsync(Ct);
        return new RunningRegistry(host, profile, runtime, root, service, logger);
    }

    private static async Task WithinFiveSeconds(Func<bool> ready)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct); deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!ready()) await Task.Delay(20, deadline.Token);
        Assert.True(elapsed.Elapsed <= TimeSpan.FromSeconds(5), $"运行时生效耗时 {elapsed.Elapsed.TotalMilliseconds:F0} ms，超过 5 秒。");
    }

    private sealed class RunningRegistry(IHost host, RuntimeProfile profile, PluginLoader runtime, IPluginContext root, IRegistryService registry, RecordingLogger logger) : IAsyncDisposable
    {
        internal IHost Host => host;
        internal RuntimeProfile Profile => profile;
        internal PluginLoader Runtime => runtime;
        internal IPluginContext Root => root;
        internal IRegistryService Registry => registry;
        internal string[] Commands(ScopeKey scope) => root.Scoped(scope).List(SurfaceKeys.Commands).Select(registration => registration.Name).Order(StringComparer.Ordinal).ToArray();
        internal string? Label(string id, ScopeKey scope)
        {
            var entry = runtime.DumpConfig().SingleOrDefault(entry => entry.Id == id && entry.Scope == scope);
            if (entry is null) return null;
            try { return runtime.GetState(id, scope) == PluginState.Ready ? entry.Config.GetProperty("label").GetString() : null; }
            // 两个只读快照之间可能正好撤销旧节点；下一次轮询再检查实际 Ready 状态。
            catch (InvalidOperationException) { return null; }
        }
        public async ValueTask DisposeAsync()
        {
            await registry.DisposeAsync(); await runtime.DisposeAsync(); await host.StopAsync(Ct); host.Dispose(); profile.Dispose();
            Assert.DoesNotContain(logger.Messages, message => message.Contains("副作用撤销失败", StringComparison.Ordinal));
        }
    }

    private sealed class RuntimeProfile : IDisposable
    {
        private static readonly string[] AllowedScopes = ["global", "tenant"];
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "qian-registry-runtime-" + Guid.NewGuid().ToString("N"));
        internal ProfileLoadOptions Options => new() { WorkingDirectory = _directory, Profile = "platform", IncludeHomePatch = false };
        internal RuntimeProfile()
        {
            Write("profiles/platform/qian.profile.yml", """{"name":"platform","bundles":[],"patch":"./qian.patch.yml","hmr":{"surfaces":true,"code":false},"build":{"aot":false},"hooks":{"allowCommand":false}}""");
            Write("profiles/platform/qian.patch.yml", """[{"id":"data","plugin":"qian.data","config":{}},{"id":"registry","plugin":"qian.registry","config":{"store":"pg"}},{"id":"shared","plugin":"test.shared","config":{"label":"global"}}]""");
            Component("qian.data", "data", [], ["Data"], true);
            Component("qian.registry", "registry", ["Data"], ["Registry"], true);
            Component("test.shared", "shared", [], [], false);
            Component("test.private", "private", [], [], false);
        }
        internal ResolvedProfileEntry PrivateEntry(SurfaceValidator validator)
        {
            var directory = Path.Combine(_directory, "plugins/private");
            var component = validator.ValidateComponent(directory, ArtifactOptions).Component!;
            return new ResolvedProfileEntry("private", "global", "test.private@1.0.0", Patch("{}"), 0, [], [], null)
                { ComponentDirectory = directory, Manifest = component.Manifest };
        }
        internal void ChangePrivateMetadata() => Write("plugins/private/metadata/permissions.json", "[]");
        internal void AddUnbound() => Component("test.unbound", "unbound", [], [], false);
        private void Component(string id, string directory, string[] inject, string[] provides, bool required)
        {
            Write($"plugins/{directory}/.qian-plugin/plugin.json", JsonSerializer.Serialize(new
            {
                id, version = "1.0.0", description = "注册表真实装载测试用组件，代码 Provider 由宿主静态装配。", author = new { name = "Qian" }, license = "MIT",
                @interface = new { displayName = directory, category = "testing" }, inject, provides,
                policy = new { tier = required ? "system" : "private", sandbox = "none", authentication = "ON_INSTALL", required }, scopes = AllowedScopes
            }));
            if (!required)
            {
                Write($"plugins/{directory}/commands/hello.md", "---\ndescription: 通过真实声明式表面验证租户配置的装载与撤销。\n---\n" + directory + " command\n");
                Write($"plugins/{directory}/schemas/config.schema.json", """{"type":"object","properties":{"label":{"type":"string"}},"additionalProperties":false}""");
                Write($"plugins/{directory}/metadata/permissions.json", """[{"resource":"record","actions":["read"]}]""");
            }
            else Write($"plugins/{directory}/schemas/config.schema.json", """{"type":"object","properties":{"store":{"type":"string","enum":["pg"]}},"additionalProperties":false}""");
            // 注册表组件携带仓库交付的权限声明（管理面 qian.registry:scope:read），Authz 从预装快照编译。
            if (id == "qian.registry")
                foreach (var name in new[] { "permissions.json", "roles.json" })
                    Write($"plugins/{directory}/metadata/{name}", File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins", "qian.registry", "metadata", name)));
        }
        private void Write(string name, string text)
        {
            var file = Path.Combine(_directory, name); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text);
        }
        public void Dispose() => Directory.Delete(_directory, true);
    }
}
