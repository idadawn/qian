using System.Security.Cryptography;
using System.Text.Json;
using Kun.EntityFrameworkCore.Extensions;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Registry.Tests;

// S1-AZ-003（自 S1-RT-004 移交）：平台超管跨租户读取 scope 视图只走 Authz 的显式旁路，并记入目标租户审计链（bypass = superadmin）。
// 真实 Registry、真实 Authz / Audit 插件、专用平台授权登录；令牌里的角色与请求头都不授予任何权限。
public sealed partial class PatchPersistenceTests
{
    [Fact]
    public async Task 平台超管经Authz显式旁路读取租户scope视图_审计记入该租户链_未装配旁路或非超管与跨租户同为404()
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
        var scopeA = new ScopeKey("tenant", HttpTenantA);
        using (tenant.BeginScope(HttpTenantA))
            await Repository(running.Host, tenant).PutAsync(scopeA, "private", Patch("""{"id":"private","plugin":"test.private","config":{}}"""), 0, "operator-a", Ct);
        await new PostgresPlatformRoleStore(running.Host.Services.GetRequiredService<IDataService>(), tenant, role).AssignSuperadminAsync("root", "platform-operator", Ct);
        var root = new PlatformUser("root");

        // 宿主还没装 Authz 组件：没有旁路端口，平台主体与不存在同为 404，不回退到租户判定器。
        using (tenant.BeginScope(HttpTenantA))
            await AssertCode(PlatformErrors.NotFound, async () => await running.Registry.GetScopeViewAsync(scopeA, root, Ct));
        await running.Runtime.MountAsync([
            new PluginDefinition(new PluginEntry("audit", "qian.audit", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { chain = new { store = "pg" } })),
                new AuditPlugin(), [AuditDefinition.Key]),
            new PluginDefinition(new PluginEntry("authz", "qian.authz", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { platformRole = role })),
                new AuthzPlugin(), [AuthzDefinition.Key, AuthzDefinition.PermissionsKey, AuthzDefinition.ResourceTypesKey, AuthzDefinition.SuperadminBypassKey])], Ct);
        Assert.Equal(PluginState.Ready, running.Runtime.GetState("authz", ScopeKey.Global));
        using (tenant.BeginScope(HttpTenantA))
        {
            var view = await running.Registry.GetScopeViewAsync(scopeA, root, Ct);
            Assert.Contains(view, entry => entry.EntryId == "private");
            var record = Assert.Single(await running.Root.Audit().ReadAsync(0, 10, cancellationToken: Ct)).Record;
            Assert.Equal((HttpTenantA, "user", "root", "api", "registry/scope:tenant:" + HttpTenantA, "qian.authz/superadmin.bypass"),
                (record.TenantId, record.ActorType, record.ActorId, record.Channel, record.Resource, record.CapabilityId));
            Assert.Equal("superadmin", record.Payload.GetProperty("bypass").GetString());
            Assert.Equal(RegistryDefinition.ScopeReadPermission, record.Payload.GetProperty("permission").GetString());
            // 非超管平台主体、当前上下文之外的租户 scope 都与不存在同为 404，且不写审计。
            await AssertCode(PlatformErrors.NotFound, async () => await running.Registry.GetScopeViewAsync(scopeA, new PlatformUser("intruder"), Ct));
            await AssertCode(PlatformErrors.NotFound, async () => await running.Registry.GetScopeViewAsync(new ScopeKey("tenant", HttpTenantB), root, Ct));
            Assert.Single(await running.Root.Audit().ReadAsync(0, 10, cancellationToken: Ct));
        }
    }

    private async Task PrepareAudit(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var sql = "DROP SCHEMA IF EXISTS " + AuditDbContext.Schema + " CASCADE;\n" + scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.GenerateCreateScript() +
            $"\nGRANT USAGE ON SCHEMA {AuditDbContext.Schema} TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {AuditDbContext.Schema} TO qian_app;";
        await AdminExec(sql);
    }

    private async Task<string> CreatePlatformLogin(string role)
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await AdminExec($"CREATE ROLE \"{role}\" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}';");
        return new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { Username = role, Password = password }.ConnectionString;
    }

    private async Task AdminExec(string sql)
    {
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, admin); await command.ExecuteNonQueryAsync(Ct);
    }

    // 已经由身份层校验过的平台主体（tenant_id 为 null）；本测试只消费主体快照，不从令牌角色推导权限。
    private sealed class PlatformUser(string subject) : ICurrentUser
    {
        private static readonly long Now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public Principal? Principal { get; } = new()
        {
            Issuer = "qian-identity-test", Audience = ["registry-http"], Subject = subject, Kind = PrincipalKind.User, TenantId = null, Roles = ["platform.superadmin"],
            Scope = "registry.read", SessionId = Guid.CreateVersion7().ToString(), IssuedAt = Now, NotBefore = Now, ExpiresAt = Now + 300, TokenId = Guid.CreateVersion7().ToString()
        };
        public string? UserId => subject;
        public IReadOnlyList<string> Roles => Principal!.Roles;
        public bool IsAuthenticated => true;
    }
}
