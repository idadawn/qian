using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime;
using Qian.Testing;
using Qian.Testing.Containers;
using Qian.Testing.Gateway;

namespace Qian.Sample.Crm.Tests;

// 用户主体经外壳执行应用能力（S1-YUE-015，裁决 A1）：真实身份 → 网关 BFF 会话 → 网关按路由表换 S2S（受众 qian-platform）→ 平台表面参考宿主的通用执行入口 →
// 参考应用的命令管道以调用者本人执行：命令 PEP 按该用户的角色判定，审计链记 actor_type = user；参考应用自己的受众（qian.sample.crm）仍只收机器主体。
public sealed partial class CustomerCommandTests
{
    [Fact]
    public async Task 用户主体经网关与平台表面通用执行入口以本人执行参考应用能力_命令PEP按其角色判定且审计记user_只读成员新建404_乙方看不到甲方客户_带act被拒_用户令牌直调参考应用仍401()
    {
        using var tls = GatewayTestHost.Certificate(); using var reservation = LoopbackPort.Reserve();
        var address = new Uri($"https://localhost:{reservation.Port}/");
        // 放行判定也入链（采样 1）：用来核对命令 PEP 判的是用户本人。
        await using var business = await Start(identity: true, capabilities: true, relyingParty: new Uri(address, "auth/callback"), decisionLogSampling: 1);
        var loader = Runtime(business).Loader;
        await using var surface = PlatformSurfaceHost.Build(id => loader.GetContext(id, ScopeKey.Global), new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = ShellGatewayIssuer, ValidateAudience = true, ValidAudience = PlatformSurfaceHost.Audience, ValidateLifetime = true,
            ValidateIssuerSigningKey = true, IssuerSigningKey = business.ManagementSigningKey, ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireSignedTokens = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero
        }, builder => { builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders(); });
        await surface.StartAsync(Ct);
        await using var redis = new RedisFixture(); await redis.InitializeAsync();
        // 外壳客户端另申请参考应用的两个 scope（能力声明的 requiredScopes），与联调栈的外壳客户端一致。
        await using var gateway = await GatewayTestHost.Start(reservation, tls, business.Identity.Issuer, GatewayClientId, IdentityResource, business.Identity.Secret,
            redis.ConnectionString, ShellGatewayIssuer, [new("capabilities", "/api/capabilities/{**rest}", PlatformSurfaceHost.Audience)],
            [new(PlatformSurfaceHost.Audience, surface.Urls.Single())], business.ManagementSigningKey.Rsa, scopes: ["crm.read", "crm.write"]);
        var tenant = business.App.Services.GetRequiredService<ITenantContext>();
        var grants = loader.GetContext("authz", ScopeKey.Global).Inject<ITenantRoleGrant>(TenantRoleGrant.Key);

        // 甲方两个成员：所有者（tenant.owner，客户读写）与使用者（tenant.user，只读）；使用者在乙方也是 tenant.user。
        var (owner, ownerPhone) = await NewPerson(business, "外壳所有者"); var (member, memberPhone) = await NewPerson(business, "外壳使用者");
        string ownerId = owner.Id.ToString("D"), memberId = member.Id.ToString("D");
        await Join(business, A, owner.Id, "外壳所有者"); await Join(business, A, member.Id, "外壳使用者"); await Join(business, B, member.Id, "外壳使用者");
        using (tenant.BeginScope(A))
        {
            await grants.AssignAsync("tenant.owner", "user", ownerId, "acceptance", Ct);
            await grants.AssignAsync("tenant.user", "user", memberId, "acceptance", Ct);
        }
        using (tenant.BeginScope(B)) await grants.AssignAsync("tenant.user", "user", memberId, "acceptance", Ct);

        // 一、所有者经网关登录甲方：列表、新建、再列表都 200，新客户属于甲方。
        var (ownerBrowser, _) = GatewayTestHost.Browser(tls); using var ownerSession = ownerBrowser;
        await ShellSignIn(ownerBrowser, business, ownerPhone, A); await gateway.CompleteLogin(ownerBrowser);
        Assert.Empty((await ShellList(ownerBrowser, address)).Items);
        var key = Guid.CreateVersion7(); Guid customer;
        using (var created = await ShellInvoke(ownerBrowser, address, CreateId, CustomerInput(key, "外壳客户"), key))
        {
            var body = await created.Content.ReadAsStringAsync(Ct);
            Assert.True(created.StatusCode == HttpStatusCode.OK, body + business.FailureEvidence);
            var item = JsonSerializer.Deserialize<CustomerItem>(body, JsonSerializerOptions.Web)!;
            Assert.Equal((A, "外壳客户"), (item.TenantId, item.Name));
            customer = item.Id;
        }
        Assert.Equal([customer], (await ShellList(ownerBrowser, address)).Items.Select(item => item.Id));

        // 二、使用者（只读）登录甲方：看得到同租户的这条客户；customer.create 对他不可见，执行入口与不存在一样 404。
        var (memberBrowser, _) = GatewayTestHost.Browser(tls); using var memberSession = memberBrowser;
        await ShellSignIn(memberBrowser, business, memberPhone, A); await gateway.CompleteLogin(memberBrowser);
        Assert.Equal([customer], (await ShellList(memberBrowser, address)).Items.Select(item => item.Id));
        var hiddenKey = Guid.CreateVersion7();
        using (var hidden = await ShellInvoke(memberBrowser, address, CreateId, CustomerInput(hiddenKey, "越权客户"), hiddenKey))
            await ErrorDetail(hidden, HttpStatusCode.NotFound, "NOT_FOUND");

        // 三、使用者切到乙方：乙方的列表里没有甲方的客户（租户只取自令牌，数据走 RLS）。
        await ShellSignIn(memberBrowser, business, memberPhone, B); await gateway.CompleteLogin(memberBrowser);
        Assert.Empty((await ShellList(memberBrowser, address)).Items);

        // 四、照网关签发的形状（受众 qian-platform，主体、成员与修订取自所有者的会话快照）直调平台表面：不带 act 照常执行；
        // 带 act（S1 不开放委托）时执行入口与不存在一样 404。
        var session = JsonNode.Parse(await SessionSnapshot(ownerBrowser, address))!;
        using var direct = new HttpClient { BaseAddress = new Uri(surface.Urls.Single()) };
        using (var plain = await direct.SendAsync(Bearer(HttpMethod.Post, "/api/capabilities/" + ListId + "/invoke", SurfaceToken(business, session, null), ListInput), Ct))
            Assert.True(plain.StatusCode == HttpStatusCode.OK, await plain.Content.ReadAsStringAsync(Ct));
        var actor = new { sub = AgentId, principal_kind = "agent" };
        using (var delegated = await direct.SendAsync(Bearer(HttpMethod.Post, "/api/capabilities/" + CreateId + "/invoke", SurfaceToken(business, session, actor),
            CustomerInput(Guid.CreateVersion7(), "委托客户")), Ct))
            await ErrorDetail(delegated, HttpStatusCode.NotFound, "NOT_FOUND");

        // 五、参考应用自己的受众（qian.sample.crm）仍只收机器主体：同一用户的 S2S 直调它的 REST 与能力执行入口都 401
        // （本宿主验签不放行 user，身份适配器也只认平台表面交来的 user 快照，两道各自兜底，响应一样）。
        var revision = (long)session["identityRevision"]!;
        using (var rest = await business.Send(Get("/api/customers", business.Token(A, "crm.read crm.write", ownerId, "user", identityRevision: revision))))
            await ErrorDetail(rest, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        using (var invoke = await business.Send(Bearer(HttpMethod.Post, "/api/capabilities/" + ListId + "/invoke",
            business.Token(A, "crm.read crm.write", ownerId, "user", identityRevision: revision), ListInput)))
            await ErrorDetail(invoke, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");

        // 甲方：只有所有者那一次新建落库，客户建档事件信封的 actor 也是所有者本人；参考应用的命令审计恰一条，行事方是所有者本人（user、person_id），
        // 通道沿用能力登记的 api（裁决 ③）；命令 PEP 对所有者的新建与列表、使用者的列表都按本人记放行，链上没有任何拒绝判定。乙方：没有写入，只有使用者那次列表的放行。
        var (ownerTrail, ownerDecisions, ownerEvents) = await TenantTrail(business, A, customers: 1);
        var customerEvent = Assert.Single(ownerEvents);
        Assert.Equal((CrmEvents.CustomerCreated, "user", ownerId), (customerEvent.GetProperty("type").GetString(), customerEvent.GetProperty("actor").GetProperty("kind").GetString(),
            customerEvent.GetProperty("actor").GetProperty("id").GetString()));
        var command = Assert.Single(ownerTrail, record => record.CapabilityId.StartsWith("qian.sample.crm/", StringComparison.Ordinal));
        Assert.Equal((CreateId, "user", ownerId, (string?)null, "api", "success"), (command.CapabilityId, command.ActorType, command.ActorId, command.OnBehalfOf, command.Channel, command.Result));
        Assert.Contains((ownerId, "allowed", AuthzEnforcement.CommandEntryPoint, CreateId, "role_grant"), ownerDecisions);
        Assert.Contains((ownerId, "allowed", AuthzEnforcement.CommandEntryPoint, ListId, "role_grant"), ownerDecisions);
        Assert.Contains((memberId, "allowed", AuthzEnforcement.CommandEntryPoint, ListId, "role_grant"), ownerDecisions);
        Assert.All(ownerDecisions, decision => Assert.Equal("allowed", decision.Result));
        var (otherTrail, otherDecisions, otherEvents) = await TenantTrail(business, B, customers: 0);
        Assert.Empty(otherEvents);
        Assert.DoesNotContain(otherTrail, record => record.CapabilityId.StartsWith("qian.sample.crm/", StringComparison.Ordinal));
        Assert.Equal([(memberId, "allowed", AuthzEnforcement.CommandEntryPoint, ListId, "role_grant")], otherDecisions);
    }

    private const string ListInput = """{"skip":0,"take":100}""";
    private static string CustomerInput(Guid key, string name) => JsonSerializer.Serialize(new CreateCustomerCommand(key, name), JsonSerializerOptions.Web);

    // 外壳 bridge.invoke 的形状：先取防伪令牌（非安全方法），经网关的规范路径发到平台表面的通用执行入口；写能力带 Idempotency-Key（与正文里的业务幂等键同值）。
    private static async Task<HttpResponseMessage> ShellInvoke(HttpClient browser, Uri address, string capability, string body, Guid? idempotencyKey = null)
    {
        var csrf = JsonNode.Parse(await Through(browser, address, "auth/csrf"))!;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address, "api/capabilities/" + capability + "/invoke")) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add((string)csrf["headerName"]!, (string)csrf["token"]!);
        if (idempotencyKey is { } key) request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return await browser.SendAsync(request, Ct);
    }

    private static async Task<CustomerPage> ShellList(HttpClient browser, Uri address)
    {
        using var response = await ShellInvoke(browser, address, ListId, ListInput);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<CustomerPage>(body, JsonSerializerOptions.Web)!;
    }

    private static HttpRequestMessage Bearer(HttpMethod method, string path, string token, string body)
    {
        var message = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    // 与网关签给平台表面的 S2S 同形（RS256、网关 issuer、受众 qian-platform、60 秒）；主体、租户、成员与身份修订取自会话快照，actor 给出时多一个 act。
    private static string SurfaceToken(RunningApp host, JsonNode session, object? actor)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.CreateVersion7().ToString();
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = ShellGatewayIssuer, ["aud"] = new[] { PlatformSurfaceHost.Audience }, ["sub"] = (string?)session["subject"], ["principal_kind"] = "user",
            ["tenant_id"] = (string?)session["tenantId"], ["membership_id"] = (string?)session["membershipId"], ["identity_revision"] = (long)session["identityRevision"]!,
            ["roles"] = Array.Empty<string>(), ["scope"] = "crm.read crm.write", ["session_id"] = id, ["jti"] = id, ["iat"] = now, ["nbf"] = now, ["exp"] = now + 60
        };
        if (actor is not null) payload["act"] = actor;
        return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(payload), new SigningCredentials(host.ManagementSigningKey, SecurityAlgorithms.RsaSha256));
    }

    // 租户链上的全部记录与决策日志、本租户的 Outbox 事件信封（Outbox 表供投递器跨租户读取，按信封的 tenant_id 过滤）；顺带核对客户行数与整条链的哈希。
    private static async Task<(AuditRecord[] Records, (string Actor, string Result, string EntryPoint, string Target, string Reason)[] Decisions, JsonElement[] Events)> TenantTrail(
        RunningApp host, string tenantId, int customers)
    {
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(tenantId))
        {
            List<string> events;
            await using (var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct))
            {
                Assert.Equal(customers, await session.Context.Customers.CountAsync(Ct));
                events = await session.Context.OutboxMessages.Where(row => row.TenantId == tenantId).Select(row => row.Payload).ToListAsync(Ct);
            }
            var trail = Runtime(host).Source().Audit();
            var records = (await trail.ReadAsync(0, 200, cancellationToken: Ct)).Select(entry => entry.Record).ToArray();
            Assert.True((await trail.VerifyAsync(Ct)).Valid);
            Assert.All(records.Where(record => record.CapabilityId == AuthzEnforcement.DecisionCapabilityId), record => Assert.Equal("user", record.ActorType));
            return (records, await Decisions(host, tenantId), [.. events.Select(text => JsonSerializer.Deserialize<JsonElement>(text))]);
        }
    }
}
