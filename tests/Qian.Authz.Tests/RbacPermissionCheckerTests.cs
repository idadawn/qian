using System.Text.Json;
using Kun.Authorization;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;

namespace Qian.Authz.Tests;

public sealed partial class PermissionCatalogPersistenceTests
{
    [Fact]
    public async Task 真正的Check和Explain从角色授权求值_未知权限缺省拒绝_批量与逐条一致()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var catalog = Catalog(); await Store(host).PutAsync(catalog, Ct);
        var data = host.Services.GetRequiredService<IDataService>(); var roles = new PostgresTenantRoleStore(data, tenant);
        var registry = new PermissionRegistry(); using var declarations = registry.Register(catalog.Version, catalog.Permissions);
        var checker = new RbacPermissionChecker(data, tenant, registry);
        var owner = new AuthorizationSubject("owner", "a"); var reader = new AuthorizationSubject("reader", "a");
        using var current = tenant.BeginScope("a");
        await roles.EnsureBuiltinsAsync("operator", Ct); await roles.AssignAsync("tenant.owner", "user", "owner", "operator", Ct);
        await roles.AssignAsync("tenant.user", "user", "reader", "operator", Ct); await roles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);
        var allowed = await checker.ExplainDetailedAsync(new PermissionRequest(owner, "qian.sample.crm:customer:write"), Ct);
        Assert.True(allowed.Decision.Allow); Assert.Equal("role_grant", allowed.Decision.Reason); Assert.Equal(["tenant.owner"], allowed.MatchedRoles);
        Assert.Single(allowed.MatchedGrants); Assert.StartsWith("grant:", allowed.MatchedGrants[0], StringComparison.Ordinal); Assert.Empty(allowed.Missing);
        var denied = await checker.ExplainDetailedAsync(new PermissionRequest(reader, "qian.sample.crm:customer:write"), Ct);
        Assert.False(denied.Decision.Allow); Assert.Equal(["tenant.user"], denied.MatchedRoles); Assert.Equal(["qian.sample.crm:customer:write"], denied.Missing);
        var requests = new[]
        {
            new PermissionRequest(owner, "qian.sample.crm:customer:read"), new PermissionRequest(reader, "qian.sample.crm:customer:write"),
            new PermissionRequest(owner, "qian.sample.crm:customer:delete"), new PermissionRequest(new AuthorizationSubject("owner", "b"), "qian.sample.crm:customer:read")
        };
        var batch = await checker.BatchCheckAsync(requests, Ct);
        for (var index = 0; index < requests.Length; index++) Assert.Equal(JsonSerializer.Serialize(await checker.CheckAsync(requests[index], Ct)), JsonSerializer.Serialize(batch[index]));
        Assert.True(batch[0].Allow); Assert.All(batch.Skip(1), decision => Assert.False(decision.Allow));
        Assert.Equal(["qian.sample.crm:customer:read"], await checker.ListPermissionsAsync(reader, Ct));
        Assert.Equal(["read"], await checker.ListAllowedActionsAsync(reader, "qian.sample.crm:customer", cancellationToken: Ct));
    }

    [Fact]
    public async Task 活动声明释放后数据库授权仍在但不再放行_缺上下文与跨租户不泄露命中信息()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var catalog = Catalog(); await Store(host).PutAsync(catalog, Ct); var data = host.Services.GetRequiredService<IDataService>();
        var roles = new PostgresTenantRoleStore(data, tenant); var registry = new PermissionRegistry(); using var lease = registry.Register(catalog.Version, catalog.Permissions);
        var checker = new RbacPermissionChecker(data, tenant, registry); var request = new PermissionRequest(new AuthorizationSubject("owner", "a"), "qian.sample.crm:customer:read");
        var missing = await checker.ExplainDetailedAsync(request, Ct); Assert.False(missing.Decision.Allow); Assert.Empty(missing.MatchedRoles);
        using (tenant.BeginScope("a"))
        {
            await roles.EnsureBuiltinsAsync("operator", Ct); await roles.AssignAsync("tenant.owner", "user", "owner", "operator", Ct); await roles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);
            Assert.True((await checker.CheckAsync(request, Ct)).Allow);
            lease.Dispose();
            var revoked = await checker.ExplainDetailedAsync(request, Ct); Assert.False(revoked.Decision.Allow); Assert.Equal("permission_not_registered", revoked.Decision.Reason);
            await using var session = await data.OpenAsync<AuthzDbContext>(Ct); Assert.Equal(4, await session.Context.Grants.CountAsync(Ct));
        }
        using (tenant.BeginScope("b"))
        {
            var foreign = await checker.ExplainDetailedAsync(request, Ct); Assert.False(foreign.Decision.Allow); Assert.Empty(foreign.MatchedRoles); Assert.Empty(foreign.MatchedGrants);
        }
    }

    [Fact]
    public async Task RBAC已命中也不会在缺少stepup或终端证明时直接放行()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var catalog = Catalog(); await Store(host).PutAsync(catalog, Ct); var data = host.Services.GetRequiredService<IDataService>();
        var roles = new PostgresTenantRoleStore(data, tenant); var registry = new PermissionRegistry(); using var lease = registry.Register(catalog.Version, catalog.Permissions);
        using var scope = tenant.BeginScope("a"); await roles.EnsureBuiltinsAsync("operator", Ct); await roles.AssignAsync("tenant.owner", "user", "owner", "operator", Ct);
        await roles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);
        var checker = new RbacPermissionChecker(data, tenant, registry);
        var result = await checker.ExplainDetailedAsync(new PermissionRequest(new AuthorizationSubject("owner", "a"), "qian.sample.crm:customer:export"), Ct);
        Assert.False(result.Decision.Allow); Assert.Equal("step_up_required", result.Decision.Reason);
        Assert.Equal(["tenant.owner"], result.MatchedRoles); Assert.Single(result.MatchedGrants); Assert.Empty(result.Missing);
    }
}
