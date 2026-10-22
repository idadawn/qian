using System.Net;
using System.Text;
using System.Text.Json;
using Kun.EntityFrameworkCore.Extensions;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Registry.Tests;

// S1-AGT-002 验收 3：清单 policy.permissions（platform / data / secrets / ui.connectSrc）与 policy.egress 五类随制品快照落库，经受信端口按摘要、
// 经管理面按组件版本读出（安装时列出供租户管理员批准）。真实 PostgreSQL、真实 RBAC 判定、真实 Kestrel + JWT；批准本身见 Qian.Lifecycle.Tests。
public sealed partial class PatchPersistenceTests
{
    private static readonly string[] DeclaredPlatform = ["tenant:read", "notify:send"];
    private static readonly string[] DeclaredData = ["qian.sample.crm:customer"];
    private static readonly object[] DeclaredSecrets = [new { name = "oss/*", scope = "platform" }, new { name = "db/token", scope = "tenant" }];
    private static readonly string[] DeclaredConnectSrc = ["https://api.example.test"];
    private static readonly object DeclaredPermissions = new { platform = DeclaredPlatform, data = DeclaredData, secrets = DeclaredSecrets, ui = new { connectSrc = DeclaredConnectSrc } };
    private static readonly string[] DeclaredEgress = ["api.example.test", "*.files.example.test:443"];
    private static readonly string[] PermissionScopes = ["tenant"];
    private const string PermissionSigner = "sha256:1111111111111111111111111111111111111111111111111111111111111111";

    [Fact]
    public async Task 反向权限五类随制品快照落库_受信端口按摘要读出_管理面持权限者按组件版本读出_无权限候选与未知版本同为404_别名同结果()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant, services: services => AddAuthz(services, checker: true));
        await PrepareAuthz(running.Host);
        using var active = await GrantRegistryRoles(running, tenant);
        var installed = await SubmitPermissions(running, "acme.connector", "1.0.0", DeclaredPermissions, DeclaredEgress, install: true);
        var candidate = await SubmitPermissions(running, "acme.connector", "2.0.0", DeclaredPermissions, DeclaredEgress, install: false);
        var plain = await SubmitPermissions(running, "acme.plain", "1.0.0", null, null, install: true);

        // 受信端口：按摘要读出，不论状态，顺序同清单；未登记为 null，摘要格式无效为 INPUT_INVALID。
        var declared = (await running.Registry.GetArtifactPermissionsAsync(installed, Ct))!;
        Assert.Equal(("acme.connector", "1.0.0", installed), (declared.PluginId, declared.Version, declared.ArtifactDigest));
        Assert.Equal(DeclaredPlatform, declared.Platform); Assert.Equal(DeclaredData, declared.Data); Assert.Equal(DeclaredEgress, declared.Egress);
        Assert.Equal([("oss/*", "platform"), ("db/token", "tenant")], declared.Secrets.Select(secret => (secret.Name, secret.Scope)));
        Assert.Equal(DeclaredConnectSrc, declared.Ui.ConnectSrc);
        Assert.Equal("2.0.0", (await running.Registry.GetArtifactPermissionsAsync(candidate, Ct))!.Version);
        Assert.Null(await running.Registry.GetArtifactPermissionsAsync("sha256:" + new string('0', 64), Ct));
        Assert.Equal("INPUT_INVALID", (await Assert.ThrowsAsync<QianErrorException>(() => running.Registry.GetArtifactPermissionsAsync("sha256:ABC", Ct).AsTask())).Error.Code.Code);

        await using var http = await StartManagementHost(running, tenant);
        var path = "/api/registry/plugins/acme.connector/versions/1.0.0/permissions";
        using var read = await http.Get(path, HttpTenantA, Holder);
        Assert.True(read.StatusCode == HttpStatusCode.OK, await read.Content.ReadAsStringAsync(Ct));
        Assert.Equal((true, true), (read.Headers.CacheControl!.Private, read.Headers.CacheControl.NoStore));
        var body = await Json(read);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(new
        {
            pluginId = "acme.connector", version = "1.0.0", artifactDigest = installed, platform = DeclaredPlatform, data = DeclaredData, egress = DeclaredEgress,
            secrets = DeclaredSecrets, ui = new { connectSrc = DeclaredConnectSrc }
        }), body), body.ToString());
        // 声明属于组件版本（平台级目录）：B 的持权限者读到同一份；租户只来自令牌，请求头里的 X-Tenant-Id 不起作用。
        using var readB = await http.Get(path, HttpTenantB, Holder);
        Assert.True(JsonElement.DeepEquals(body, await Json(readB)));

        // 无权限者、只是候选（Submitted）的版本、未知组件、格式不合格的标识同为 404，错误体完全相同。
        using var hidden = await http.Get(path, HttpTenantA, Reader);
        using var submitted = await http.Get("/api/registry/plugins/acme.connector/versions/2.0.0/permissions", HttpTenantA, Holder);
        using var unknown = await http.Get("/api/registry/plugins/acme.absent/versions/1.0.0/permissions", HttpTenantA, Holder);
        using var control = await http.Get("/api/registry/plugins/acme.connector/versions/" + Uri.EscapeDataString("1.0.0\u0007") + "/permissions", HttpTenantA, Holder);
        using var overlong = await http.Get("/api/registry/plugins/acme.connector/versions/" + new string('9', 201) + "/permissions", HttpTenantA, Holder);
        var absent = await ErrorShape(unknown);
        foreach (var response in new[] { hidden, submitted, unknown, control, overlong })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(absent, await ErrorShape(response));
        }

        // v1 别名：同一结果，带 Deprecation 与指向后继路径的 Link；错误响应同样带。
        using var alias = await http.Get("/api/kernel/plugins/acme.connector/versions/1.0.0/permissions", HttpTenantA, Holder);
        Assert.Equal(HttpStatusCode.OK, alias.StatusCode);
        Assert.True(JsonElement.DeepEquals(body, await Json(alias)));
        Assert.Equal(DeprecatedAlias.Deprecation, Assert.Single(alias.Headers.GetValues("Deprecation")));
        Assert.Equal("</api/registry/plugins/acme.connector/versions/1.0.0/permissions>; rel=\"successor-version\"", Assert.Single(alias.Headers.GetValues("Link")));
        using var aliasHidden = await http.Get("/api/kernel/plugins/acme.connector/versions/1.0.0/permissions", HttpTenantA, Reader);
        Assert.Equal(HttpStatusCode.NotFound, aliasHidden.StatusCode); Assert.Equal(absent, await ErrorShape(aliasHidden));
        Assert.Equal(DeprecatedAlias.Deprecation, Assert.Single(aliasHidden.Headers.GetValues("Deprecation")));
        Assert.False(read.Headers.Contains("Deprecation"));

        // 没有声明反向权限的组件：五类都是空数组，不是缺省字段。
        using var none = await http.Get("/api/registry/plugins/acme.plain/versions/1.0.0/permissions", HttpTenantA, Holder);
        Assert.Equal(HttpStatusCode.OK, none.StatusCode);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(new
        {
            pluginId = "acme.plain", version = "1.0.0", artifactDigest = plain, platform = Array.Empty<string>(), data = Array.Empty<string>(), egress = Array.Empty<string>(),
            secrets = Array.Empty<object>(), ui = new { connectSrc = Array.Empty<string>() }
        }), await Json(none)));
    }

    [Fact]
    public async Task 平台超管经真实JWT读取组件反向权限_目标租户审计记下权限点与组件版本资源_非超管与另一目标租户为404()
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
        await SubmitPermissions(running, "acme.connector", "1.0.0", DeclaredPermissions, DeclaredEgress, install: true);
        await new PostgresPlatformRoleStore(running.Host.Services.GetRequiredService<IDataService>(), tenant, role).AssignSuperadminAsync("root", "operator", Ct);
        await running.Runtime.MountAsync([
            new PluginDefinition(new PluginEntry("audit", "qian.audit", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new AuditPlugin(), [AuditDefinition.Key]),
            new PluginDefinition(new PluginEntry("authz", "qian.authz", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { platformRole = role })), new AuthzPlugin(),
                [AuthzDefinition.Key, AuthzDefinition.PermissionsKey, AuthzDefinition.ResourceTypesKey, AuthzDefinition.SuperadminBypassKey])], Ct);
        Assert.Equal(PluginState.Ready, running.Runtime.GetState("authz", ScopeKey.Global));
        // 运营宿主预先选定已验证的目标租户 A；路径与伪造的 X-Tenant-Id 都不能改变它。
        await using var http = await StartManagementHost(running, tenant, platformTargetTenant: HttpTenantA);
        var path = "/api/registry/plugins/acme.connector/versions/1.0.0/permissions";
        using var root = await http.GetPlatform(path, "root");
        Assert.True(root.StatusCode == HttpStatusCode.OK, await root.Content.ReadAsStringAsync(Ct));
        Assert.Equal(DeclaredEgress, (await Json(root)).GetProperty("egress").EnumerateArray().Select(item => item.GetString()));
        using var intruder = await http.GetPlatform(path, "intruder");
        using var absent = await http.GetPlatform("/api/registry/plugins/acme.connector/versions/9.9.9/permissions", "root");
        Assert.Equal(HttpStatusCode.NotFound, intruder.StatusCode); Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        Assert.Equal(await ErrorShape(intruder), await ErrorShape(absent));
        using (tenant.BeginScope(HttpTenantA))
        {
            // 每次放行都记入目标租户的审计链（含判定之后才发现版本不存在的那次），资源写明组件版本。
            var entries = await running.Root.Audit().ReadAsync(0, 20, cancellationToken: Ct);
            Assert.Equal(2, entries.Count);
            Assert.All(entries, entry =>
            {
                Assert.Equal((HttpTenantA, "root", "user"), (entry.Record.TenantId, entry.Record.ActorId, entry.Record.ActorType));
                Assert.Equal("superadmin", entry.Record.Payload.GetProperty("bypass").GetString());
                Assert.Equal(RegistryDefinition.ScopeReadPermission, entry.Record.Payload.GetProperty("permission").GetString());
            });
            Assert.Equal(["registry/artifact:acme.connector@1.0.0", "registry/artifact:acme.connector@9.9.9"], entries.Select(entry => entry.Record.Resource));
            Assert.True((await running.Root.Audit().VerifyAsync(Ct)).Valid);
        }
        using (tenant.BeginScope(HttpTenantB)) Assert.Empty(await running.Root.Audit().ReadAsync(0, 20, cancellationToken: Ct));
    }

    // 登记一个只有清单的声明式制品；install 时再按 Lifecycle 的方式回写 Installed（验签由 Lifecycle 负责，这里只给公钥指纹）。
    private static async Task<string> SubmitPermissions(RunningRegistry running, string pluginId, string version, object? permissions, string[]? egress, bool install)
    {
        var policy = new Dictionary<string, object?> { ["tier"] = "official", ["sandbox"] = "none", ["authentication"] = "ON_INSTALL" };
        if (permissions is not null) policy["permissions"] = permissions;
        if (egress is not null) policy["egress"] = egress;
        var manifest = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = pluginId, ["version"] = version, ["description"] = "反向权限读取验收使用的声明式组件制品。", ["author"] = new { name = "Acme" }, ["license"] = "Apache-2.0",
            ["interface"] = new { displayName = pluginId, category = "testing" }, ["inject"] = Array.Empty<string>(), ["provides"] = Array.Empty<string>(), ["policy"] = policy,
            ["scopes"] = PermissionScopes
        });
        var files = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal) { [".qian-plugin/plugin.json"] = Encoding.UTF8.GetBytes(manifest) };
        var digest = TestDigest(files);
        Assert.Equal("Submitted", (await running.Registry.SubmitArtifactAsync(files, digest, "tck", Ct)).State);
        if (install) Assert.Equal("Installed", (await running.Registry.RecordArtifactStateAsync(digest, "Installed", PermissionSigner, Ct)).State);
        return digest;
    }
}
