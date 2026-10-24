using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz;
using Qian.Data;
using Qian.Events;
using Qian.Lifecycle;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Sample.Crm.Tests;

public abstract partial class CustomerCommandTests
{
    private protected const string Connector = "acme.connector";
    private protected static readonly string[] ConnectorScopes = ["tenant"];
    private protected static readonly string[] ConnectorPlatform = ["tenant:read", "notify:send"];
    private protected static readonly string[] ConnectorPlatformSorted = ["notify:send", "tenant:read"];
    private protected static readonly string[] ConnectorEgress = ["api.example.test"];
    private protected static readonly object[] ConnectorSecrets = [new { name = "connector/token", scope = "tenant" }];
    private protected static readonly object ConnectorGrants = new { platform = ConnectorPlatform, data = Array.Empty<string>(), egress = ConnectorEgress, secrets = ConnectorSecrets, ui = new { connectSrc = Array.Empty<string>() } };
    private protected const string ConnectorApprovalPath = "/plugins/" + Connector + "/versions/1.0.0/permissions";
    private protected const string ApprovalAdmin = "approval-admin";
    private protected const string ApprovalMember = "approval-member";
}

// S1-AGT-002 验收 3：安装时由租户管理员批准反向权限的 HTTP API（S1 只有 API，S2 才有 UI），经参考宿主的真实 JWT、租户边界与 RBAC
// （仓库 metadata 的角色模板：tenant.owner / tenant.admin 持 qian.lifecycle:install:approve）。A 的调用者是 Owner，B 的调用者只是使用者。
// S1-AGT-006 补：批准经真实路径写入该租户审计链；tenant.admin 只凭仓库角色模板即可批准，同租户的普通使用者 404。
public sealed partial class LifecycleProfileTests
{
    [Fact]
    public async Task 反向权限批准API经真实JWT与RBAC_A的Owner批准后才能开通_使用者跨租户与无记录同为404_请求体只接受五类()
    {
        using var platform = new LifecyclePlatform();
        await using var host = await Start(lifecycle: platform);
        var lifecycle = await InstallConnector(host, platform);
        using (EventEnvelopeScope.Begin(Operator))
        {
            // 权益之后还有批准：未批准时开通被拒，缺失项写进错误。
            var denied = await Assert.ThrowsAsync<QianErrorException>(() => lifecycle.ProvisionAsync(A, Connector, "operator", Ct).AsTask());
            Assert.Equal((LifecycleErrors.PermissionsUnapproved.Code, "platform notify:send, tenant:read; egress api.example.test; secrets tenant:connector/token"),
                (denied.Error.Code.Code, denied.Error.Args["missing"]));
        }

        var path = "/api/lifecycle/tenants/" + A + ConnectorApprovalPath;
        var grants = ConnectorGrants;
        using (var before = await ApprovalRequest(host, HttpMethod.Get, A, path)) Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        using var approved = await ApprovalRequest(host, HttpMethod.Put, A, path, JsonSerializer.Serialize(grants));
        Assert.True(approved.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await approved.Content.ReadAsStringAsync(Ct));
        Assert.True(approved.Headers.CacheControl is { Private: true, NoStore: true }, approved.Headers.CacheControl?.ToString());
        var view = await approved.Content.ReadFromJsonAsync<JsonElement>(Ct);
        // 批准人取已验证令牌的主体，渠道为租户管理员；批准内容按序数排序保存。
        Assert.Equal((A, Connector, "1.0.0", Writer, PermissionApprovalChannels.Tenant), (view.GetProperty("tenantId").GetString(), view.GetProperty("pluginId").GetString(),
            view.GetProperty("version").GetString(), view.GetProperty("approvedBy").GetString(), view.GetProperty("approvedVia").GetString()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(new
        {
            platform = ConnectorPlatformSorted, data = Array.Empty<string>(), egress = ConnectorEgress, secrets = ConnectorSecrets, ui = new { connectSrc = Array.Empty<string>() }
        }), view.GetProperty("approved")), view.ToString());
        // 同一次批准写入 A 的审计链：主体是 Owner 的服务主体，渠道 tenant，上一次为空。
        AssertApprovalAudit(Assert.Single(await ApprovalAudit(host, A)), "service", Writer);
        using (var read = await ApprovalRequest(host, HttpMethod.Get, A, path))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.True(JsonElement.DeepEquals(view, await read.Content.ReadFromJsonAsync<JsonElement>(Ct)));
        }

        // B 的使用者、A 的 Owner 跨到 B、未安装的版本、未知组件：同为 404，与不存在无法区分。
        var pathB = "/api/lifecycle/tenants/" + B + ConnectorApprovalPath;
        var missing = new[]
        {
            await ApprovalRequest(host, HttpMethod.Put, B, pathB, JsonSerializer.Serialize(grants)),
            await ApprovalRequest(host, HttpMethod.Get, B, pathB),
            await ApprovalRequest(host, HttpMethod.Put, A, pathB, JsonSerializer.Serialize(grants)),
            await ApprovalRequest(host, HttpMethod.Put, A, $"/api/lifecycle/tenants/{A}/plugins/{Connector}/versions/9.9.9/permissions", JsonSerializer.Serialize(grants)),
            await ApprovalRequest(host, HttpMethod.Put, A, $"/api/lifecycle/tenants/{A}/plugins/acme.absent/versions/1.0.0/permissions", JsonSerializer.Serialize(grants)),
        };
        foreach (var response in missing)
            using (response)
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                Assert.Equal("NOT_FOUND", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
            }

        // 请求体只有五类：带租户字段或其它未知成员、未声明的项，持权限者得到 INPUT_INVALID 与 pointer；没有批准权限者同样只得到 404。
        var withTenant = """{"tenantId":"01900000-0000-7000-8000-000000000002","platform":["tenant:read"]}""";
        using (var invalid = await ApprovalRequest(host, HttpMethod.Put, A, path, withTenant))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var error = await invalid.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal(("INPUT_INVALID", "#"), (error.GetProperty("code").GetString(), error.GetProperty("errors")[0].GetProperty("pointer").GetString()));
        }
        using (var undeclared = await ApprovalRequest(host, HttpMethod.Put, A, path, """{"platform":["tenant:read","tenant:write"]}"""))
        {
            Assert.Equal(HttpStatusCode.BadRequest, undeclared.StatusCode);
            Assert.Equal("#/platform/1", (await undeclared.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors")[0].GetProperty("pointer").GetString());
        }
        using (var hidden = await ApprovalRequest(host, HttpMethod.Put, B, pathB, withTenant)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        // 失败的请求不改记录：批准内容仍是上面那一份。
        using (var unchanged = await ApprovalRequest(host, HttpMethod.Get, A, path))
            Assert.True(JsonElement.DeepEquals(view, await unchanged.Content.ReadFromJsonAsync<JsonElement>(Ct)));

        using (EventEnvelopeScope.Begin(Operator))
            Assert.Equal(TenantInstallStates.Installed, (await lifecycle.ProvisionAsync(A, Connector, "operator", Ct)).State);
    }

    [Fact]
    public async Task 反向权限批准API经真实JWT与RBAC_只持仓库角色模板tenant_admin的主体可批准并入审计链_同租户普通使用者404()
    {
        using var platform = new LifecyclePlatform();
        await using var host = await Start(lifecycle: platform);
        var lifecycle = await InstallConnector(host, platform);
        var data = host.App.Services.GetRequiredService<IDataService>(); var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        using (tenant.BeginScope(A))
        {
            // 只授角色、不另授权限点：管理员能否批准完全取决于仓库 plugins/qian.lifecycle/metadata/roles.json（宿主按它把角色模板装进 A）。
            // 参考宿主只收机器主体，两者都是 A 的服务主体；用户主体的批准人与主体类型见 Qian.Lifecycle.Tests。
            var roles = new PostgresTenantRoleStore(data, tenant);
            await roles.AssignAsync("tenant.admin", "service", ApprovalAdmin, "test-bootstrap", Ct);
            await roles.AssignAsync("tenant.user", "service", ApprovalMember, "test-bootstrap", Ct);
        }
        var path = "/api/lifecycle/tenants/" + A + ConnectorApprovalPath;
        var body = JsonSerializer.Serialize(ConnectorGrants);
        // 同租户的普通使用者：批准与读取都与不存在同为 404，不写记录、不记审计。
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Get })
            using (var denied = await ApprovalRequest(host, method, A, path, method == HttpMethod.Put ? body : null, ApprovalMember))
            {
                Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                Assert.Equal("NOT_FOUND", (await denied.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
            }
        Assert.Empty(await ApprovalAudit(host, A));
        // 租户管理员：批准生效，批准人是该主体，渠道 tenant，并写入 A 的审计链。
        using (var approved = await ApprovalRequest(host, HttpMethod.Put, A, path, body, ApprovalAdmin))
        {
            Assert.True(approved.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await approved.Content.ReadAsStringAsync(Ct));
            var view = await approved.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal((ApprovalAdmin, PermissionApprovalChannels.Tenant), (view.GetProperty("approvedBy").GetString(), view.GetProperty("approvedVia").GetString()));
        }
        AssertApprovalAudit(Assert.Single(await ApprovalAudit(host, A)), "service", ApprovalAdmin);
        using (var read = await ApprovalRequest(host, HttpMethod.Get, A, path, subject: ApprovalAdmin)) Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using (EventEnvelopeScope.Begin(Operator))
            Assert.Equal(TenantInstallStates.Installed, (await lifecycle.ProvisionAsync(A, Connector, "operator", Ct)).State);
    }
}

public abstract partial class CustomerCommandTests
{
    // 声明反向权限的连接器组件：平台安装、激活，并授权给 A（权益已到位，只差批准）。
    private protected static async Task<IPluginLifecycleController> InstallConnector(RunningApp host, LifecyclePlatform platform)
    {
        var lifecycle = LifecyclePlatform.Lifecycle(host);
        var files = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
        {
            [".qian-plugin/plugin.json"] = JsonSerializer.SerializeToUtf8Bytes(new
            {
                id = Connector, version = "1.0.0", description = "声明反向权限、经租户管理员批准后才开通的连接器组件。", author = new { name = "Acme" }, license = "Apache-2.0",
                @interface = new { displayName = "connector", category = "testing" }, inject = Array.Empty<string>(), provides = Array.Empty<string>(), scopes = ConnectorScopes,
                policy = new
                {
                    tier = "official", sandbox = "none", authentication = "ON_INSTALL", egress = ConnectorEgress,
                    permissions = new { platform = ConnectorPlatform, secrets = ConnectorSecrets }
                }
            })
        };
        var digest = LifecycleArtifactDigest(files);
        using (EventEnvelopeScope.Begin(Operator))
        {
            var revision = await lifecycle.InstallAsync(new PluginArtifact(files, digest, platform.Sign(digest)), "operator", Ct);
            await lifecycle.ActivateAsync(Connector, revision.Revision, "operator", Ct);
            await lifecycle.EntitleAsync(A, Connector, "operator", Ct);
        }
        return lifecycle;
    }

    // 租户链上的批准条目（经审计组件读取，读前先验整条链）。
    private protected static async Task<AuditChainEntry[]> ApprovalAudit(RunningApp host, string tenantId)
    {
        var audit = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("audit", ScopeKey.Global).Audit();
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(tenantId))
        {
            Assert.True((await audit.VerifyAsync(Ct)).Valid);
            return [.. (await audit.ReadAsync(0, 500, cancellationToken: Ct)).Where(entry => entry.Record.CapabilityId == "qian.lifecycle/permissions.approve")];
        }
    }

    // 连接器在 A 的首次批准：主体即批准人，资源同判定资源，载荷带渠道 tenant 与规范化的本次内容，上一次为 null。
    private protected static void AssertApprovalAudit(AuditChainEntry entry, string actorType, string actor)
    {
        var record = entry.Record;
        Assert.Equal((A, actorType, actor, "api", (string?)("lifecycle/install:" + A + "/" + Connector + "@1.0.0"), "success"),
            (record.TenantId, record.ActorType, record.ActorId, record.Channel, record.Resource, record.Result));
        var expected = JsonSerializer.SerializeToElement(new
        {
            pluginId = Connector, version = "1.0.0", via = PermissionApprovalChannels.Tenant, previous = (object?)null,
            approved = new { platform = ConnectorPlatformSorted, data = Array.Empty<string>(), egress = ConnectorEgress, secrets = ConnectorSecrets, ui = new { connectSrc = Array.Empty<string>() } }
        });
        Assert.True(JsonElement.DeepEquals(expected, record.Payload), record.Payload.GetRawText());
    }

    private protected static Task<HttpResponseMessage> ApprovalRequest(RunningApp host, HttpMethod method, string tenant, string path, string? body = null, string? subject = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        // 默认是 A 的 Owner / B 的使用者（服务主体 Writer）；指定 subject 时以该租户的另一个服务主体调用。
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(tenant, "crm.read crm.write", subject));
        return host.Send(request);
    }
}
