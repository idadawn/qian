using System.Text.Json;
using Kun.Authorization;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime;
using Qian.Surfaces;

namespace Qian.Authz.Tests;

// S1-YUE-018：平台超管的只读判定。与旁路对平台资源同一套条件（平台用户主体、无租户上下文、权限点已登记、持有效角色、审计可用），
// 但不写任何审计、不放行任何操作；外壳的平台控制台目录据此决定给平台身份哪些页面。真实 PostgreSQL、专用平台登录与真实 Audit 插件。
public sealed partial class SuperadminBypassTests
{
    [Fact]
    public async Task 平台超管只读判定与旁路同一套条件_只交出持角色时已登记的平台权限点_租户上下文与非平台用户为空_不写任何审计()
    {
        var role = Login("qian_authz_platform_"); var platform = await CreateLogin(role);
        var auditRole = Login("qian_audit_platform_"); var auditPlatform = await CreateLogin(auditRole);
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant, platform, auditPlatform); await host.StartAsync(Ct); await Prepare(host, role, auditRole);
        var data = host.Services.GetRequiredService<IDataService>();
        var tenancy = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.tenancy", "1.0.0",
            JsonSerializer.Deserialize<JsonElement>("""[{"resource":"tenant","actions":["read","create"],"risk":{"create":"critical"}}]"""), JsonSerializer.Deserialize<JsonElement>("[]"));
        await new PostgresPermissionCatalogStore(data).PutAsync(tenancy, Ct);
        var registry = host.Services.GetRequiredService<IPermissionRegistry>();
        var active = registry.Register(tenancy.Version, tenancy.Permissions);
        await using var runtime = new PluginLoader(host.Services); var consumer = new Consumer();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]),
            Define("audit", new AuditPlugin(), [AuditDefinition.Key], new { chain = new { store = "pg", platformRole = auditRole } }),
            Define("authz", new AuthzPlugin(), AuthzKeys, new { platformRole = role }), Define("consumer", consumer, [])], Ct);
        var ctx = consumer.Context!; var check = ctx.Inject<ISuperadminCheck>(AuthzDefinition.SuperadminCheckKey);
        var roles = new PostgresPlatformRoleStore(data, tenant, role);
        // 两条已登记的平台权限点、一条没有登记的（别的组件）、一条不合法的码与一条重复。
        string[] requested = ["qian.tenancy:tenant:read", "qian.tenancy:tenant:create", "qian.tenancy.signup:application:read", "not a permission", "qian.tenancy:tenant:read"];
        string[] expected = ["qian.tenancy:tenant:create", "qian.tenancy:tenant:read"];

        // 不持角色：空。
        Assert.Empty(await check.AllowedAsync(Root, requested, Ct));
        await roles.AssignSuperadminAsync("root", "platform-operator", Ct);
        // 持角色：只交出已登记的，去重、按 Ordinal 排序。
        Assert.Equal(expected, await check.AllowedAsync(Root, requested, Ct));
        Assert.Empty(await check.AllowedAsync(Root, [], Ct));
        // 主体不是人类平台用户（带租户、服务、Agent、空标识）或当前处在某个租户的上下文里：空，与旁路的 invalid_subject / tenant_context_mismatch 同一结论。
        foreach (var subject in new[] { new AuthorizationSubject("root", A, "user"), new AuthorizationSubject("root", null, "service"), new AuthorizationSubject("root", null, "agent"), new AuthorizationSubject(" ", null, "user") })
            Assert.Empty(await check.AllowedAsync(subject, requested, Ct));
        using (tenant.BeginScope(A)) Assert.Empty(await check.AllowedAsync(Root, requested, Ct));

        // 只读：上面的判定一条审计也没写（平台链与租户链同在一张表）；对照真的经旁路访问一次，平台链才多一条放行记录。
        Assert.Equal(0L, await AdminScalar("SELECT count(*) FROM plugin_qian_audit.audit_entries"));
        Assert.Empty(await ctx.Audit().ReadPlatformAsync(ctx, 0, 10, cancellationToken: Ct));
        var bypass = ctx.Inject<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey);
        Assert.True((await bypass.AuthorizeAsync(new SuperadminAccess(Root, "qian.tenancy:tenant:read", null, "tenancy/tenants", "ui"), Ct)).Allow);
        Assert.Equal(1L, await AdminScalar("SELECT count(*) FROM plugin_qian_audit.audit_entries"));

        // 活动登记释放、角色撤销、审计不可用：旁路都会拒绝，只读判定同样不再交出。
        active.Dispose();
        Assert.Empty(await check.AllowedAsync(Root, requested, Ct));
        using (registry.Register(tenancy.Version, tenancy.Permissions))
        {
            Assert.Equal(expected, await check.AllowedAsync(Root, requested, Ct));
            Assert.True(await roles.RevokeSuperadminAsync("root", "platform-operator", Ct));
            Assert.Empty(await check.AllowedAsync(Root, requested, Ct));
            await roles.AssignSuperadminAsync("root", "platform-operator", Ct);
            Assert.Equal(expected, await check.AllowedAsync(Root, requested, Ct));
            await runtime.UnmountAsync("audit", ScopeKey.Global, Ct);
            Assert.Empty(await check.AllowedAsync(Root, requested, Ct));
        }
        Assert.Equal(1L, await AdminScalar("SELECT count(*) FROM plugin_qian_audit.audit_entries"));
    }
}
