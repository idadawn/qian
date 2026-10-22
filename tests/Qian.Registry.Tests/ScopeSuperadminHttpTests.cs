using System.Net;
using System.Text.Json;
using Kun.EntityFrameworkCore.Extensions;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime;

namespace Qian.Registry.Tests;

public sealed partial class PatchPersistenceTests
{
    [Fact]
    public async Task 平台超管经真实JWT和HTTP读取scope_目标租户留审计_非超管错租户和未配置平台角色均拒绝()
    {
        var role = "qian_authz_platform_" + Guid.NewGuid().ToString("N")[..8]; var platform = await CreatePlatformLogin(role);
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant, services: services =>
        {
            AddAuthz(services, checker: true);
            services.AddQianData<PlatformAuthzDbContext>((_, options) => options.UseNpgsql(platform).UseKunSchema(AuthzDbContext.Schema));
            services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuditDbContext.Schema));
        });
        await PrepareAuthz(running.Host); await PrepareAudit(running.Host);
        await AdminExec("BEGIN;" + AuthzPlatformAccess.CreateMigrationSql(role) + "COMMIT;");
        using var active = await GrantRegistryRoles(running, tenant);
        using (tenant.BeginScope(HttpTenantA))
            await Repository(running.Host, tenant).PutAsync(new ScopeKey("tenant", HttpTenantA), "private",
                Patch("""{"id":"private","plugin":"test.private","config":{}}"""), 0, "operator-a", Ct);
        await new PostgresPlatformRoleStore(running.Host.Services.GetRequiredService<IDataService>(), tenant, role).AssignSuperadminAsync("root", "operator", Ct);
        await running.Runtime.MountAsync([
            new PluginDefinition(new PluginEntry("audit", "qian.audit", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new AuditPlugin(), [AuditDefinition.Key]),
            Authz(new { platformRole = role })], Ct);
        Assert.Equal(PluginState.Ready, running.Runtime.GetState("authz", ScopeKey.Global));
        // 与生产适配器契约一致：宿主先建立已验证目标租户。路径与伪造 X-Tenant-Id 都不能改变它。
        await using var http = await StartManagementHost(running, tenant, platformTargetTenant: HttpTenantA);
        var path = "/api/registry/scopes/tenant:" + HttpTenantA + "/entries";
        using var root = await http.GetPlatform(path, "root"); Assert.True(root.StatusCode == HttpStatusCode.OK, await root.Content.ReadAsStringAsync(Ct));
        Assert.Contains("private", Ids(await Json(root)));
        using var intruder = await http.GetPlatform(path, "intruder"); Assert.Equal(HttpStatusCode.NotFound, intruder.StatusCode);
        using var crossRoot = await http.GetPlatform("/api/registry/scopes/tenant:" + HttpTenantB + "/entries", "root"); Assert.Equal(HttpStatusCode.NotFound, crossRoot.StatusCode);
        using var crossTenant = await http.Get(path, HttpTenantB, Holder); Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
        Assert.Equal(await ErrorShape(intruder), await ErrorShape(crossRoot)); Assert.Equal(await ErrorShape(intruder), await ErrorShape(crossTenant));
        await running.Runtime.UnmountAsync("authz", ScopeKey.Global, Ct); await running.Runtime.MountAsync([Authz(new { })], Ct);
        using var unconfigured = await http.GetPlatform(path, "root"); Assert.Equal(HttpStatusCode.NotFound, unconfigured.StatusCode);
        using (tenant.BeginScope(HttpTenantA))
        {
            var entry = Assert.Single(await running.Root.Audit().ReadAsync(0, 20, cancellationToken: Ct));
            Assert.Equal((HttpTenantA, "root", "user"), (entry.Record.TenantId, entry.Record.ActorId, entry.Record.ActorType));
            Assert.Equal("superadmin", entry.Record.Payload.GetProperty("bypass").GetString());
            Assert.Equal(RegistryDefinition.ScopeReadPermission, entry.Record.Payload.GetProperty("permission").GetString());
            Assert.True((await running.Root.Audit().VerifyAsync(Ct)).Valid);
        }
        using (tenant.BeginScope(HttpTenantB)) Assert.Empty(await running.Root.Audit().ReadAsync(0, 20, cancellationToken: Ct));
        static PluginDefinition Authz(object config) => new(new PluginEntry("authz", "qian.authz", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(config)),
            new AuthzPlugin(), [AuthzDefinition.Key, AuthzDefinition.PermissionsKey, AuthzDefinition.ResourceTypesKey, AuthzDefinition.SuperadminBypassKey]);
    }
}
