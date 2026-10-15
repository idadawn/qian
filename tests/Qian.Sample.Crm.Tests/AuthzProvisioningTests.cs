using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Sample.Crm.Tests;

// 参考应用 metadata 声明的权限与默认角色模板经完整 Authz Provider 持久化并登记；新租户由真实 qian.tenancy 生产方上下文派发 tenant/provisioned 后，
// qian.authz 在该租户实例化内置角色与默认策略：Owner 拥有 customer 的读、写、导出，使用者只有 read；me/permissions 读出风险、step-up 与终端元数据。
// 真实开户编排（何时派发、谁写初始授权）不在这里；本测试以同一角色存储把两个服务主体绑定到角色，不提供开户 API。
public sealed partial class CustomerCommandTests
{
    private const string Provisioned = "01900000-0000-7000-8000-000000000003";
    private const string ProvisionedOwner = "crm-provisioned-owner";
    private const string ProvisionedReader = "crm-provisioned-reader";
    private static readonly string[] CustomerPermissions = ["qian.sample.crm:customer:export", "qian.sample.crm:customer:read", "qian.sample.crm:customer:write"];
    private static readonly string[] BuiltinRoles = ["tenant.admin", "tenant.developer", "tenant.owner", "tenant.user"];
    private static readonly string[] DefaultGrants = ["tenant.owner=qian.sample.crm:customer:export", "tenant.owner=qian.sample.crm:customer:read",
        "tenant.owner=qian.sample.crm:customer:write", "tenant.user=qian.sample.crm:customer:read"];

    [Fact]
    public async Task 参考应用清单经tenant_provisioned实例化默认角色_Owner拥有customer读写导出_使用者只有read_me_permissions读出风险与step_up()
    {
        await using var host = await Start();
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var data = host.App.Services.GetRequiredService<IDataService>(); var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        // 清单声明的三个动作登记为全名；宿主端点的判定器与 Provider 共用同一份活动登记表。
        var registry = runtime.GetContext("authz", ScopeKey.Global).Inject<IPermissionRegistry>(AuthzDefinition.PermissionsKey);
        Assert.Same(host.App.Services.GetRequiredService<IPermissionRegistry>(), registry);
        Assert.Equal(CustomerPermissions, registry.List("qian.sample.crm", "1.0.0").Select(permission => permission.Code));

        await Admin($"INSERT INTO plugin_qian_tenancy.tenants(id, code, name, isolation, status, created_at) VALUES ('{Provisioned}'::uuid, 'c', 'provisioned tenant', 'row', 'active', now());");
        // 不是 qian.tenancy 发出的、或主体与生产方 scope 不一致的通知一律拒绝，不留下任何角色。
        var tenancy = runtime.GetContext("tenancy", ScopeKey.Global).Scoped(new ScopeKey("tenant", Provisioned));
        await AssertInvalid(runtime.GetContext("data", ScopeKey.Global).Scoped(new ScopeKey("tenant", Provisioned)), Provisioned);
        await AssertInvalid(tenancy, A);
        Assert.Equal(("", "", "", 0L), Flatten(await State()));

        Assert.Equal(JsonValueKind.Null, Assert.Single(await Provision(tenancy, Provisioned)).ValueKind);
        var provisioned = await State();
        Assert.Equal(BuiltinRoles, provisioned.Roles); Assert.Equal(DefaultGrants, provisioned.Grants);
        Assert.Equal(["qian.sample.crm@1.0.0"], provisioned.Installs); Assert.Equal(1L, provisioned.Revision);
        using (tenant.BeginScope(Provisioned))
        {
            var roles = new PostgresTenantRoleStore(data, tenant);
            await roles.AssignAsync("tenant.owner", "service", ProvisionedOwner, "tck", Ct);
            await roles.AssignAsync("tenant.user", "service", ProvisionedReader, "tck", Ct);
        }
        // 重复投递幂等：角色、默认授权、安装记录与授权修订都不变。
        var assigned = await State();
        Assert.Equal(JsonValueKind.Null, Assert.Single(await Provision(tenancy, Provisioned)).ValueKind);
        Assert.Equal(Flatten(assigned), Flatten(await State()));

        var owner = await MePermissions(host, Provisioned, ProvisionedOwner);
        Assert.Equal(CustomerPermissions, owner.Select(item => item.GetProperty("permission").GetString()));
        Assert.Equal(("qian.sample.crm:customer", "export", "high", true, false, "step_up_required"), View(owner[0]));
        Assert.Equal(["web"], owner[0].GetProperty("terminals").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(("qian.sample.crm:customer", "read", "low", false, true, "role_grant"), View(owner[1]));
        Assert.Equal(("qian.sample.crm:customer", "write", "low", false, true, "role_grant"), View(owner[2]));
        Assert.All(owner[1..], item => Assert.Empty(item.GetProperty("terminals").EnumerateArray()));
        var reader = Assert.Single(await MePermissions(host, Provisioned, ProvisionedReader));
        Assert.Equal(("qian.sample.crm:customer", "read", "low", false, true, "role_grant"), View(reader));
        // 列表与同一上下文下的 Check 一致：导出需要 step-up，使用者没有写和导出的授权。
        using (tenant.BeginScope(Provisioned))
        {
            var authz = runtime.GetContext("authz", ScopeKey.Global).Authz();
            foreach (var (subject, permission, allow, reason) in new[]
            {
                (ProvisionedOwner, "qian.sample.crm:customer:write", true, "role_grant"), (ProvisionedOwner, "qian.sample.crm:customer:export", false, "step_up_required"),
                (ProvisionedReader, "qian.sample.crm:customer:read", true, "role_grant"), (ProvisionedReader, "qian.sample.crm:customer:write", false, "no_matching_grant"),
                (ProvisionedReader, "qian.sample.crm:customer:export", false, "no_matching_grant")
            })
            {
                var decision = await authz.CheckAsync(new PermissionRequest(new AuthorizationSubject(subject, Provisioned, "service"), permission), Ct);
                Assert.Equal((allow, reason), (decision.Allow, decision.Reason));
            }
        }
        // 租户隔离：C 的 Owner 在 A 不是成员；A 既有的 Owner 不受 C 开通影响。
        Assert.Empty(await MePermissions(host, A, ProvisionedOwner));
        Assert.Equal(CustomerPermissions, (await MePermissions(host, A, Writer)).Select(item => item.GetProperty("permission").GetString()));
        // 端点 fail-closed：未认证 401；S1 不接受 Agent 主体。
        using (var anonymous = await host.Client.GetAsync("/api/authz/me/permissions", Ct)) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using (var agent = await Me(host, Provisioned, ProvisionedOwner, "agent"))
            Assert.Equal((HttpStatusCode.Forbidden, "AUTHZ_DENIED"), (agent.StatusCode, await Code(agent)));

        // 卸载 Provider：端点与不存在同为 404；持久目录与租户授予保留。宿主自己持有的同内容登记不随 Provider 的租约释放而消失。
        await runtime.UnmountAsync("authz", ScopeKey.Global, Ct);
        using (var gone = await Me(host, Provisioned, ProvisionedOwner)) Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), (gone.StatusCode, await Code(gone)));
        Assert.Equal(Flatten(assigned), Flatten(await State()));
        Assert.NotNull(await new PostgresPermissionCatalogStore(data).GetAsync("qian.sample.crm", "1.0.0", Ct));
        Assert.Equal(CustomerPermissions, registry.List("qian.sample.crm", "1.0.0").Select(permission => permission.Code));

        async Task<(string[] Roles, string[] Grants, string[] Installs, long Revision)> State()
        {
            using var scope = tenant.BeginScope(Provisioned);
            await using var session = await data.OpenAsync<AuthzDbContext>(Ct);
            var db = session.Context;
            var roles = await db.Roles.AsNoTracking().Select(row => row.Code).ToListAsync(Ct);
            var grants = await (from grant in db.Grants.AsNoTracking() join role in db.Roles.AsNoTracking() on grant.RoleId equals role.Id
                                select role.Code + "=" + grant.PermissionCode).ToListAsync(Ct);
            var installs = await db.PolicyInstalls.AsNoTracking().Select(row => row.PluginId + "@" + row.Version).ToListAsync(Ct);
            var revision = await db.Revisions.AsNoTracking().Select(row => (long?)row.Revision).SingleOrDefaultAsync(Ct) ?? 0;
            return ([.. roles.Order(StringComparer.Ordinal)], [.. grants.Order(StringComparer.Ordinal)], [.. installs.Order(StringComparer.Ordinal)], revision);
        }
    }

    private static async Task<IReadOnlyList<JsonElement>> Provision(IPluginContext source, string subject) =>
        await source.DispatchAsync(SurfaceHookEvents.TenantProvisioned,
            new HookNotification(source, subject, JsonSerializer.SerializeToElement(new { tenantId = subject, plan = "standard" }), Ct), Ct);

    private static async Task AssertInvalid(IPluginContext source, string subject)
    {
        var error = await Assert.ThrowsAsync<QianErrorException>(async () => await Provision(source, subject));
        Assert.Equal(PlatformErrors.InputInvalid, error.Error.Code);
    }

    // 元组里的数组按值比较。
    private static (string Roles, string Grants, string Installs, long Revision) Flatten((string[] Roles, string[] Grants, string[] Installs, long Revision) state) =>
        (string.Join(',', state.Roles), string.Join(',', state.Grants), string.Join(',', state.Installs), state.Revision);

    private static (string?, string?, string?, bool, bool, string?) View(JsonElement item) => (item.GetProperty("resource").GetString(), item.GetProperty("action").GetString(),
        item.GetProperty("risk").GetString(), item.GetProperty("stepUp").GetBoolean(), item.GetProperty("allowed").GetBoolean(), item.GetProperty("reason").GetString());

    private static async Task<JsonElement[]> MePermissions(RunningApp host, string tenantId, string subject)
    {
        using var response = await Me(host, tenantId, subject);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, host.FailureEvidence + body);
        Assert.True(response.Headers.CacheControl is { Private: true, NoStore: true }, response.Headers.CacheControl?.ToString());
        using var document = JsonDocument.Parse(body);
        return [.. document.RootElement.GetProperty("items").EnumerateArray().Select(item => item.Clone())];
    }

    private static Task<HttpResponseMessage> Me(RunningApp host, string tenantId, string subject, string kind = "service")
    {
        var message = new HttpRequestMessage(HttpMethod.Get, "/api/authz/me/permissions");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(tenantId, "crm.read", subject, kind));
        return host.Send(message);
    }

    private static async Task<string?> Code(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.GetProperty("code").GetString();
    }
}
