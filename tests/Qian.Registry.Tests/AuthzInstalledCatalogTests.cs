using System.Text.Json;
using Kun.Authorization;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;
using Qian.Tenancy;

namespace Qian.Registry.Tests;

// 平台模式：qian.authz 从注册表的预装组件读取 metadata/permissions.json 与 roles.json，持久化目录并登记活动声明；真实 qian.tenancy 生产方派发
// tenant/provisioned 后为该租户实例化各组件的默认策略。卸载 Provider 撤销活动声明，持久目录与租户授予保留。
public sealed partial class PatchPersistenceTests
{
    [Fact]
    public async Task 平台模式Authz从预装组件读取权限目录_开通后按默认角色授权_卸载撤销活动声明保留目录()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant, services: services =>
        {
            AddAuthz(services, checker: false);
            services.AddSingleton<ISurfaceReader, SurfaceValidator>();
        });
        await PrepareAuthz(running.Host);
        var data = running.Host.Services.GetRequiredService<IDataService>(); var registry = running.Host.Services.GetRequiredService<IPermissionRegistry>();
        await running.Runtime.MountAsync([
            new PluginDefinition(new PluginEntry("tenancy", "qian.tenancy", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new TenancyPlugin(), [TenancyDefinition.Key]),
            new PluginDefinition(new PluginEntry("authz", "qian.authz", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
                AuthzPlugin.FromInstalledComponents(), [AuthzDefinition.Key])], Ct);
        Assert.True(running.Runtime.GetState("authz", ScopeKey.Global) == PluginState.Ready, running.Runtime.GetFailure("authz", ScopeKey.Global)?.ToString());
        Assert.Equal([DataDefinition.Key, RegistryDefinition.Key], AuthzPlugin.FromInstalledComponents().Inject);

        // 带 permissions.json 的预装组件逐个登记（缺 roles.json 视为没有默认角色）；没有声明的 qian.data 不登记。
        Assert.Equal([RegistryDefinition.ScopeReadPermission], Codes("qian.registry"));
        Assert.Equal(["test.shared:record:read"], Codes("test.shared")); Assert.Equal(["test.private:record:read"], Codes("test.private"));
        Assert.Empty(Codes("qian.data"));
        var expected = new PermissionCatalogCompiler(new SurfaceValidator()).FromInstalled(Assert.Single(running.Registry.GetInstalledComponents(), component => component.PluginId == "qian.registry"));
        var stored = await new PostgresPermissionCatalogStore(data).GetAsync("qian.registry", "1.0.0", Ct);
        Assert.NotNull(stored);
        Assert.Equal(Roles(expected), Roles(stored)); Assert.Equal(["tenant.admin=qian.registry:scope:read", "tenant.owner=qian.registry:scope:read"], Roles(stored));

        var source = running.Runtime.GetContext("tenancy", ScopeKey.Global).Scoped(new ScopeKey("tenant", HttpTenantA));
        await source.DispatchAsync(SurfaceHookEvents.TenantProvisioned,
            new HookNotification(source, HttpTenantA, JsonSerializer.SerializeToElement(new { tenantId = HttpTenantA, plan = "standard" }), Ct), Ct);
        using (tenant.BeginScope(HttpTenantA))
        {
            await using (var session = await data.OpenAsync<AuthzDbContext>(Ct))
                Assert.Equal(["qian.registry@1.0.0", "test.private@1.0.0", "test.shared@1.0.0"],
                    (await session.Context.PolicyInstalls.AsNoTracking().Select(row => row.PluginId + "@" + row.Version).ToListAsync(Ct)).Order(StringComparer.Ordinal));
            var roles = new PostgresTenantRoleStore(data, tenant);
            await roles.AssignAsync("tenant.admin", "service", Holder, "test-bootstrap", Ct);
            await roles.AssignAsync("tenant.user", "service", Reader, "test-bootstrap", Ct);
            var authz = running.Runtime.GetContext("authz", ScopeKey.Global).Authz();
            var admin = Assert.Single(await authz.ListEffectivePermissionsAsync(new AuthorizationSubject(Holder, HttpTenantA, "service"), Ct));
            Assert.Equal((RegistryDefinition.ScopeReadPermission, true, "role_grant"), (admin.Definition.Code, admin.Allowed, admin.Reason));
            Assert.Empty(await authz.ListEffectivePermissionsAsync(new AuthorizationSubject(Reader, HttpTenantA, "service"), Ct));
        }

        await running.Runtime.UnmountAsync("authz", ScopeKey.Global, Ct);
        Assert.Empty(Codes("qian.registry")); Assert.Empty(Codes("test.shared")); Assert.Empty(Codes("test.private"));
        Assert.NotNull(await new PostgresPermissionCatalogStore(data).GetAsync("qian.registry", "1.0.0", Ct));
        using (tenant.BeginScope(HttpTenantA)) Assert.Equal([RegistryDefinition.ScopeReadPermission], await GrantedCodes(Holder));

        string[] Codes(string plugin) => [.. registry.List(plugin, "1.0.0").Select(permission => permission.Code)];
        static string[] Roles(PermissionCatalog catalog) => [.. catalog.Roles.SelectMany(role => role.Grants.Select(grant => role.Role + "=" + grant)).Order(StringComparer.Ordinal)];
        async Task<string[]> GrantedCodes(string subject)
        {
            await using var session = await data.OpenAsync<AuthzDbContext>(Ct);
            var db = session.Context;
            return [.. (await (from assignment in db.RoleAssignments.AsNoTracking()
                               join grant in db.Grants.AsNoTracking() on assignment.RoleId equals grant.RoleId
                               where assignment.SubjectType == "service" && assignment.SubjectId == subject
                               select grant.PermissionCode).ToListAsync(Ct)).Distinct().Order(StringComparer.Ordinal)];
        }
    }
}
