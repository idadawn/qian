using System.Text.Json;
using Kun.MultiTenancy;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Registry.Tests;

public sealed partial class PatchPersistenceTests
{
    [Fact]
    public async Task 实际scope视图区分预装与可用_真库禁用即时隐藏且不影响B()
    {
        var tenant = new AsyncLocalTenantContext(); await using var running = await CreateRunningRegistry(tenant);
        Assert.Contains(running.Registry.GetInstalledComponents(), component => component.PluginId == "test.private");
        using (tenant.BeginScope("a"))
        {
            var initial = Assert.Single(await running.Registry.GetScopeEntriesAsync(TenantA, SurfaceKeys.Commands, Ct));
            Assert.Equal(new RegistryScopeEntry("shared", "test.shared", "1.0.0", ScopeKey.Global, SurfaceKeys.Commands, "test.shared/hello"), initial);
            await Repository(running.Host, tenant).PutAsync(TenantA, "shared", Patch("""{"id":"shared","disabled":true}"""), 0, "operator-a", Ct);
            Assert.Empty(await running.Registry.GetScopeEntriesAsync(TenantA, SurfaceKeys.Commands, Ct));
            Assert.Contains(running.Registry.GetInstalledComponents(), component => component.PluginId == "test.shared");
            await Repository(running.Host, tenant).PutAsync(TenantA, "private", Patch("""{"id":"private","plugin":"test.private","config":{}}"""), 0, "operator-a", Ct);
            var local = Assert.Single(await running.Registry.GetScopeEntriesAsync(TenantA, SurfaceKeys.Commands, Ct));
            Assert.Equal("test.private", local.PluginId); Assert.Equal(TenantA, local.Scope); Assert.Equal("private", local.EntryId);
        }
        using (tenant.BeginScope("b")) Assert.Equal("test.shared", Assert.Single(await running.Registry.GetScopeEntriesAsync(TenantB, SurfaceKeys.Commands, Ct)).PluginId);
    }

    [Fact]
    public async Task 业务视图受实际restrict交集影响_坏scope不暴露管理原因并可恢复()
    {
        var tenant = new AsyncLocalTenantContext(); await using var running = await CreateRunningRegistry(tenant);
        using var scope = tenant.BeginScope("a");
        using (running.Root.Restrict(TenantA, registration => registration.Name != "test.shared/hello"))
            Assert.Empty(await running.Registry.GetScopeEntriesAsync(TenantA, SurfaceKeys.Commands, Ct));
        Assert.Single(await running.Registry.GetScopeEntriesAsync(TenantA, SurfaceKeys.Commands, Ct));
        var repository = Repository(running.Host, tenant);
        await repository.PutAsync(TenantA, "shared", Patch("""{"id":"shared","config":{"label":42}}"""), 0, "operator-a", Ct);
        Assert.Empty(await running.Registry.GetScopeEntriesAsync(TenantA, SurfaceKeys.Commands, Ct));
        await repository.PutAsync(TenantA, "shared", Patch("""{"id":"shared","config":{"label":"repaired"}}"""), 1, "operator-a", Ct);
        var repaired = Assert.Single(await running.Registry.GetScopeEntriesAsync(TenantA, SurfaceKeys.Commands, Ct)); Assert.Equal(TenantA, repaired.Scope);
    }

    [Fact]
    public async Task scope业务视图拒绝匿名跨租户和未经证明的应用会话_停止后失效()
    {
        var tenant = new AsyncLocalTenantContext(); await using var running = await CreateRunningRegistry(tenant);
        await AssertCode(PlatformErrors.Unauthenticated, () => running.Registry.GetScopeEntriesAsync(TenantA, SurfaceKeys.Commands, Ct).AsTask());
        using (tenant.BeginScope("a"))
        {
            await AssertCode(PlatformErrors.NotFound, () => running.Registry.GetScopeEntriesAsync(TenantB, SurfaceKeys.Commands, Ct).AsTask());
            await AssertCode(PlatformErrors.NotFound, () => running.Registry.GetScopeEntriesAsync(new ScopeKey("app", "unproven"), SurfaceKeys.Commands, Ct).AsTask());
            await AssertCode(PlatformErrors.NotFound, () => running.Registry.GetScopeEntriesAsync(new ScopeKey("agent-session", "unproven"), SurfaceKeys.Commands, Ct).AsTask());
            await running.Registry.DisposeAsync();
            await Assert.ThrowsAsync<ServiceNotReadyException>(() => running.Registry.GetScopeEntriesAsync(TenantA, SurfaceKeys.Commands, Ct).AsTask());
        }
    }

    // S1-YUE-018：平台控制台的来源。全局视图只给无租户上下文（平台身份的请求）读，只列 Runtime 里实际登记、Ready 且属于预装制品的全局条目；
    // 租户的停用与局部条目不改变它，租户上下文里读一律 404，不能借它绕过本租户的遮蔽与 restrict。
    [Fact]
    public async Task 全局视图只给无租户上下文_只列Ready且属于预装制品的全局条目_不受租户停用与局部条目影响_租户上下文404_停止后失效()
    {
        var tenant = new AsyncLocalTenantContext(); await using var running = await CreateRunningRegistry(tenant);
        var shared = new RegistryScopeEntry("shared", "test.shared", "1.0.0", ScopeKey.Global, SurfaceKeys.Commands, "test.shared/hello");
        Assert.Equal(shared, Assert.Single(await running.Registry.GetGlobalEntriesAsync(SurfaceKeys.Commands, Ct)));
        using (tenant.BeginScope("a"))
        {
            var repository = Repository(running.Host, tenant);
            await repository.PutAsync(TenantA, "shared", Patch("""{"id":"shared","disabled":true}"""), 0, "operator-a", Ct);
            await repository.PutAsync(TenantA, "private", Patch("""{"id":"private","plugin":"test.private","config":{}}"""), 0, "operator-a", Ct);
            Assert.Equal("test.private", Assert.Single(await running.Registry.GetScopeEntriesAsync(TenantA, SurfaceKeys.Commands, Ct)).PluginId);
            await AssertCode(PlatformErrors.NotFound, () => running.Registry.GetGlobalEntriesAsync(SurfaceKeys.Commands, Ct).AsTask());
        }
        Assert.Equal(shared, Assert.Single(await running.Registry.GetGlobalEntriesAsync(SurfaceKeys.Commands, Ct)));

        // 宿主另挂一个不属于预装制品的全局条目：Runtime 里确有它的登记，全局视图照样不交出。
        await running.Runtime.MountAsync([new PluginDefinition(new PluginEntry("rogue", "test.rogue", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
            new NamedCommand("test.rogue/hello"), [new ServiceKey(SurfaceKeys.Commands.Name + ":test.rogue/hello")])], Ct);
        Assert.Contains("test.rogue/hello", running.Commands(ScopeKey.Global));
        Assert.Equal(shared, Assert.Single(await running.Registry.GetGlobalEntriesAsync(SurfaceKeys.Commands, Ct)));
        // 全局条目卸载后不再交出。
        await running.Runtime.UnmountAsync("shared", ScopeKey.Global, Ct);
        Assert.Empty(await running.Registry.GetGlobalEntriesAsync(SurfaceKeys.Commands, Ct));
        await running.Registry.DisposeAsync();
        await Assert.ThrowsAsync<ServiceNotReadyException>(() => running.Registry.GetGlobalEntriesAsync(SurfaceKeys.Commands, Ct).AsTask());
    }

    private sealed class NamedCommand(string name) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.Provide<object>(SurfaceKeys.Commands, name, new object());
            return ValueTask.CompletedTask;
        }
    }
}
