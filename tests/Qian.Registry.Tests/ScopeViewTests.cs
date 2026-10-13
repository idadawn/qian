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
}
