using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Core.MultiTenancy;
using Kun.Identity.Configuration;
using Kun.Identity.Implementations;
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
// 平台表面同样把 service / agent 主体交给参考应用（R132 ② 维持，S1-YUE-016）：外部机器客户端带 Bearer 经网关同一路由，以服务主体本人执行、决策记 service，
// 门禁仍是能力目录的 scope 与 RBAC 两道（与经参考应用自己受众的执行入口相同）；Agent 在 S1 的 RBAC 下与不存在一样 404。
public sealed partial class ShellSurfaceTests
{
    [Fact]
    public async Task 用户主体经网关与平台表面通用执行入口以本人执行参考应用能力_命令PEP按其角色判定且审计记user_只读成员新建404_只属乙方的用户取不到甲方客户_带act被拒_用户令牌直调参考应用仍401_服务主体经网关以本人执行记service_Agent主体404()
    {
        using var tls = GatewayTestHost.Certificate(); using var reservation = LoopbackPort.Reserve();
        var address = new Uri($"https://localhost:{reservation.Port}/");
        // 放行判定也入链（采样 1）：用来核对命令 PEP 判的是调用者本人。身份库先于 AddKunSecurity 登记（S1-KUN-017 的前提）。
        IServiceCollection? services = null;
        await using var business = await Start(identity: true, capabilities: true, relyingParty: new Uri(address, "auth/callback"), decisionLogSampling: 1,
            configure: builder => services = builder.Services);
        AssertDataComponentsFirst(business, services!, typeof(Qian.Identity.Server.IdentityDbContext));
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
            redis.ConnectionString, ShellGatewayIssuer, [new("capabilities", "/api/capabilities/{**rest}", PlatformSurfaceHost.Audience) { RateLimiterPolicy = "user" }],
            [new(PlatformSurfaceHost.Audience, surface.Urls.Single())], business.ManagementSigningKey.Rsa, scopes: ["crm.read", "crm.write"]);
        var tenant = business.App.Services.GetRequiredService<ITenantContext>();
        var grants = loader.GetContext("authz", ScopeKey.Global).Inject<ITenantRoleGrant>(TenantRoleGrant.Key);

        // 甲方两个成员：所有者（tenant.owner，客户读写）与使用者（tenant.user，只读）；使用者在乙方也是 tenant.user，另有一个只属于乙方的用户（tenant.user）。
        // 甲方另有两个机器主体的角色绑定：归属甲方的机器客户端（tenant.user，只读）与 Agent（tenant.owner；S1 的 RBAC 不认 Agent，有绑定也不生效）。
        var (owner, ownerPhone) = await NewPerson(business, "外壳所有者"); var (member, memberPhone) = await NewPerson(business, "外壳使用者");
        var (outsider, outsiderPhone) = await NewPerson(business, "乙方用户");
        string ownerId = owner.Id.ToString("D"), memberId = member.Id.ToString("D"), outsiderId = outsider.Id.ToString("D");
        await Join(business, A, owner.Id, "外壳所有者"); await Join(business, A, member.Id, "外壳使用者");
        await Join(business, B, member.Id, "外壳使用者"); await Join(business, B, outsider.Id, "乙方用户");
        using (tenant.BeginScope(A))
        {
            await grants.AssignAsync("tenant.owner", "user", ownerId, "acceptance", Ct);
            await grants.AssignAsync("tenant.user", "user", memberId, "acceptance", Ct);
            await grants.AssignAsync("tenant.user", "service", MachineClientId, "acceptance", Ct);
            await grants.AssignAsync("tenant.owner", "agent", AgentId, "acceptance", Ct);
        }
        using (tenant.BeginScope(B))
        {
            await grants.AssignAsync("tenant.user", "user", memberId, "acceptance", Ct);
            await grants.AssignAsync("tenant.user", "user", outsiderId, "acceptance", Ct);
        }

        // 一、所有者经网关登录甲方：列表、新建、按 Id 取回、再列表都 200，新客户属于甲方。
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
        using (var fetched = await ShellInvoke(ownerBrowser, address, GetId, CustomerKey(customer)))
        {
            var body = await fetched.Content.ReadAsStringAsync(Ct);
            Assert.True(fetched.StatusCode == HttpStatusCode.OK, body);
            var item = JsonSerializer.Deserialize<CustomerItem>(body, JsonSerializerOptions.Web)!;
            Assert.Equal((customer, A), (item.Id, item.TenantId));
        }
        Assert.Equal([customer], (await ShellList(ownerBrowser, address)).Items.Select(item => item.Id));

        // 二、使用者（只读）登录甲方：看得到同租户的这条客户；customer.create 对他不可见，执行入口与不存在一样 404。
        var (memberBrowser, _) = GatewayTestHost.Browser(tls); using var memberSession = memberBrowser;
        await ShellSignIn(memberBrowser, business, memberPhone, A); await gateway.CompleteLogin(memberBrowser);
        Assert.Equal([customer], (await ShellList(memberBrowser, address)).Items.Select(item => item.Id));
        var hiddenKey = Guid.CreateVersion7();
        using (var hidden = await ShellInvoke(memberBrowser, address, CreateId, CustomerInput(hiddenKey, "越权客户"), hiddenKey))
            await ErrorDetail(hidden, HttpStatusCode.NotFound, "NOT_FOUND");

        // 三、乙方：使用者切到乙方、只属于乙方的用户登录乙方，列表里都没有甲方的客户（租户只取自令牌，数据走 RLS）。
        //     乙方用户按 Id 直取甲方这条客户（所有者在甲方取得到）：customer.get 在他的目录里可见，执行到数据层被 RLS 挡在乙方之外，与不存在一样 404。
        await ShellSignIn(memberBrowser, business, memberPhone, B); await gateway.CompleteLogin(memberBrowser);
        Assert.Empty((await ShellList(memberBrowser, address)).Items);
        var (outsiderBrowser, _) = GatewayTestHost.Browser(tls); using var outsiderSession = outsiderBrowser;
        await ShellSignIn(outsiderBrowser, business, outsiderPhone, B); await gateway.CompleteLogin(outsiderBrowser);
        Assert.Empty((await ShellList(outsiderBrowser, address)).Items);
        Assert.Contains(GetId, JsonNode.Parse(await Through(outsiderBrowser, address, "api/capabilities"))!["items"]!.AsArray().Select(item => (string?)item!["id"]));
        using (var foreign = await ShellInvoke(outsiderBrowser, address, GetId, CustomerKey(customer)))
            await ErrorDetail(foreign, HttpStatusCode.NotFound, "NOT_FOUND");

        // 四、照网关签发的形状（受众 qian-platform，主体、成员与修订取自所有者的会话快照）直调平台表面：不带 act 的新建照常执行（对照）；
        //     同一请求只多一个 act（S1 不开放委托）时执行入口与不存在一样 404。
        var session = JsonNode.Parse(await SessionSnapshot(ownerBrowser, address))!;
        using var direct = new HttpClient { BaseAddress = new Uri(surface.Urls.Single()) };
        var plainKey = Guid.CreateVersion7(); Guid plainCustomer;
        using (var plain = await direct.SendAsync(Bearer(HttpMethod.Post, "/api/capabilities/" + CreateId + "/invoke", SurfaceToken(business, session, null),
            CustomerInput(plainKey, "手工令牌客户"), plainKey), Ct))
        {
            var body = await plain.Content.ReadAsStringAsync(Ct);
            Assert.True(plain.StatusCode == HttpStatusCode.OK, body);
            plainCustomer = JsonSerializer.Deserialize<CustomerItem>(body, JsonSerializerOptions.Web)!.Id;
        }
        var actor = new { sub = AgentId, principal_kind = "agent" }; var delegatedKey = Guid.CreateVersion7();
        using (var delegated = await direct.SendAsync(Bearer(HttpMethod.Post, "/api/capabilities/" + CreateId + "/invoke", SurfaceToken(business, session, actor),
            CustomerInput(delegatedKey, "委托客户"), delegatedKey), Ct))
            await ErrorDetail(delegated, HttpStatusCode.NotFound, "NOT_FOUND");

        // 五、参考应用自己的受众（qian.sample.crm）仍只收机器主体：同一用户的 S2S 直调它的 REST（读与写）与能力执行入口都 401
        // （本宿主验签不放行 user，身份适配器也只认平台表面交来的 user 快照，两道各自兜底，响应一样）。
        var revision = (long)session["identityRevision"]!;
        string UserToken() => business.Token(A, "crm.read crm.write", ownerId, "user", identityRevision: revision);
        using (var rest = await business.Send(Get("/api/customers", UserToken())))
            await ErrorDetail(rest, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        var restKey = Guid.CreateVersion7();
        using (var write = await business.Send(Bearer(HttpMethod.Post, "/api/customers", UserToken(), CustomerInput(restKey, "直调客户"), restKey)))
            await ErrorDetail(write, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        using (var invoke = await business.Send(Bearer(HttpMethod.Post, "/api/capabilities/" + ListId + "/invoke", UserToken(), ListInput)))
            await ErrorDetail(invoke, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");

        // 六、外部机器客户端：归属甲方的机器客户端向真实身份服务取 client_credentials 令牌，带 Bearer 经网关同一路由到平台表面的通用执行入口，
        //     列表以该服务主体本人执行（命令 PEP 按它的 tenant.user 判定）；没有写权限点的新建、令牌不含 crm.read 时的列表都与不存在一样 404（RBAC 与 scope 两道门禁）。
        //     同一服务主体经参考应用自己受众的执行入口同样 200：平台表面对服务主体没有放出新的权限面，判定还是这两道。
        using var machine = new KunOidcClient(new KunOidcClientOptions { Name = MachineClientId, Authority = business.Identity.Issuer, ClientId = MachineClientId,
            ClientSecret = business.Identity.Secret, RequireHttpsMetadata = false }, business.App.Services.GetRequiredService<IHttpClientFactory>());
        var machineToken = (await machine.ClientCredentialsAsync("crm.read crm.write", Ct)).AccessToken;
        var (externalClient, _) = GatewayTestHost.Browser(tls); using var external = externalClient;
        var gatewayList = new Uri(address, "api/capabilities/" + ListId + "/invoke").AbsoluteUri; var gatewayCreate = new Uri(address, "api/capabilities/" + CreateId + "/invoke").AbsoluteUri;
        using (var listed = await external.SendAsync(Bearer(HttpMethod.Post, gatewayList, machineToken, ListInput), Ct))
        {
            var body = await listed.Content.ReadAsStringAsync(Ct);
            Assert.True(listed.StatusCode == HttpStatusCode.OK, body + business.FailureEvidence);
            Assert.Equal([customer, plainCustomer], JsonSerializer.Deserialize<CustomerPage>(body, JsonSerializerOptions.Web)!.Items.Select(item => item.Id));
        }
        var machineKey = Guid.CreateVersion7();
        using (var unpermitted = await external.SendAsync(Bearer(HttpMethod.Post, gatewayCreate, machineToken, CustomerInput(machineKey, "机器客户"), machineKey), Ct))
            await ErrorDetail(unpermitted, HttpStatusCode.NotFound, "NOT_FOUND");
        var writeOnly = (await machine.ClientCredentialsAsync("crm.write", Ct)).AccessToken;
        using (var unscoped = await external.SendAsync(Bearer(HttpMethod.Post, gatewayList, writeOnly, ListInput), Ct))
            await ErrorDetail(unscoped, HttpStatusCode.NotFound, "NOT_FOUND");
        using (var own = await business.Send(Bearer(HttpMethod.Post, "/api/capabilities/" + ListId + "/invoke", business.Token(A, "crm.read", MachineClientId), ListInput)))
            Assert.True(own.StatusCode == HttpStatusCode.OK, await own.Content.ReadAsStringAsync(Ct));

        // 七、Agent 主体（身份服务在 S1 不签发，照网关形状手工签）：在甲方有 tenant.owner 绑定也到不了参考应用的命令管道，平台表面与不存在一样 404。
        using (var agent = await direct.SendAsync(Bearer(HttpMethod.Post, "/api/capabilities/" + ListId + "/invoke", SurfaceMachineToken(business, "agent", AgentId, A), ListInput), Ct))
            await ErrorDetail(agent, HttpStatusCode.NotFound, "NOT_FOUND");

        // 甲方：只有所有者的两次新建落库（经外壳、经手工令牌），客户建档事件信封的 actor 都是所有者本人；参考应用的命令审计按链序是这两次新建与中间那次按 Id 取回（列表不记），
        // 行事方都是所有者本人（user、person_id），通道沿用能力登记的 api（裁决 ③）。命令 PEP 的判定按链序逐条核对，全是按本人记的放行：用户记 user，机器客户端记 service；拒绝判定、Agent 的判定都没有。
        // 乙方：没有写入，只有两位乙方成员那两次列表的放行（乙方用户取甲方客户那次已过 PEP，但执行失败、整个命令事务连同放行记录一起回滚）。
        var (ownerTrail, ownerDecisions, ownerEvents) = await TenantTrail(business, A, customers: 2);
        Assert.All(ownerEvents, customerEvent => Assert.Equal((CrmEvents.CustomerCreated, "user", ownerId), (customerEvent.GetProperty("type").GetString(),
            customerEvent.GetProperty("actor").GetProperty("kind").GetString(), customerEvent.GetProperty("actor").GetProperty("id").GetString())));
        Assert.Equal(2, ownerEvents.Length);
        var commands = ownerTrail.Where(record => record.CapabilityId.StartsWith("qian.sample.crm/", StringComparison.Ordinal)).ToArray();
        Assert.Equal([CreateId, GetId, CreateId], commands.Select(command => command.CapabilityId));
        Assert.All(commands, command => Assert.Equal(("user", ownerId, (string?)null, "api", "success"), (command.ActorType, command.ActorId, command.OnBehalfOf, command.Channel, command.Result)));
        Assert.Equal([Allowed(ownerId, ListId), Allowed(ownerId, CreateId), Allowed(ownerId, GetId), Allowed(ownerId, ListId), Allowed(memberId, ListId), Allowed(ownerId, CreateId),
            Allowed(MachineClientId, ListId), Allowed(MachineClientId, ListId)], ownerDecisions);
        Assert.Equal([(ownerId, "user"), (memberId, "user"), (MachineClientId, "service")],
            ownerTrail.Where(record => record.CapabilityId == AuthzEnforcement.DecisionCapabilityId).Select(record => (record.ActorId, record.ActorType)).Distinct());
        var (otherTrail, otherDecisions, otherEvents) = await TenantTrail(business, B, customers: 0);
        Assert.Empty(otherEvents);
        Assert.DoesNotContain(otherTrail, record => record.CapabilityId.StartsWith("qian.sample.crm/", StringComparison.Ordinal));
        Assert.Equal([Allowed(memberId, ListId), Allowed(outsiderId, ListId)], otherDecisions);
        Assert.All(otherTrail.Where(record => record.CapabilityId == AuthzEnforcement.DecisionCapabilityId), record => Assert.Equal("user", record.ActorType));

        static (string, string, string, string, string) Allowed(string actor, string target) => (actor, "allowed", AuthzEnforcement.CommandEntryPoint, target, "role_grant");
    }
}

public abstract partial class CustomerCommandTests
{
    private protected const string ListInput = """{"skip":0,"take":100}""";
    private protected static string CustomerInput(Guid key, string name) => JsonSerializer.Serialize(new CreateCustomerCommand(key, name), JsonSerializerOptions.Web);
    private protected static string CustomerKey(Guid id) => JsonSerializer.Serialize(new GetCustomerQuery(id), JsonSerializerOptions.Web);

    // 外壳 bridge.invoke 的形状：先取防伪令牌（非安全方法），经网关的规范路径发到平台表面的通用执行入口；写能力带 Idempotency-Key（与正文里的业务幂等键同值）。
    private protected static async Task<HttpResponseMessage> ShellInvoke(HttpClient browser, Uri address, string capability, string body, Guid? idempotencyKey = null)
    {
        var csrf = JsonNode.Parse(await Through(browser, address, "auth/csrf"))!;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address, "api/capabilities/" + capability + "/invoke")) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add((string)csrf["headerName"]!, (string)csrf["token"]!);
        if (idempotencyKey is { } key) request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return await browser.SendAsync(request, Ct);
    }

    private protected static async Task<CustomerPage> ShellList(HttpClient browser, Uri address)
    {
        using var response = await ShellInvoke(browser, address, ListId, ListInput);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<CustomerPage>(body, JsonSerializerOptions.Web)!;
    }

    private protected static HttpRequestMessage Bearer(HttpMethod method, string path, string token, string body, Guid? idempotencyKey = null)
    {
        var message = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (idempotencyKey is { } key) message.Headers.Add("Idempotency-Key", key.ToString("D"));
        return message;
    }

    // 与网关签给平台表面的 S2S 同形（RS256、网关 issuer、受众 qian-platform、60 秒）；主体、租户、成员与身份修订取自会话快照，actor 给出时多一个 act。
    private protected static string SurfaceToken(RunningApp host, JsonNode session, object? actor)
    {
        var claims = new Dictionary<string, object?>
        {
            ["sub"] = (string?)session["subject"], ["principal_kind"] = "user", ["tenant_id"] = (string?)session["tenantId"],
            ["membership_id"] = (string?)session["membershipId"], ["identity_revision"] = (long)session["identityRevision"]!
        };
        if (actor is not null) claims["act"] = actor;
        return SignSurfaceToken(host, claims);
    }

    // 机器主体的同形令牌：没有成员与身份修订，session_id 即 jti。身份服务在 S1 不签发 Agent 令牌，Agent 主体只能这样构造。
    private protected static string SurfaceMachineToken(RunningApp host, string kind, string subject, string tenant) =>
        SignSurfaceToken(host, new Dictionary<string, object?> { ["sub"] = subject, ["principal_kind"] = kind, ["tenant_id"] = tenant });

    private static string SignSurfaceToken(RunningApp host, Dictionary<string, object?> claims)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.CreateVersion7().ToString();
        var payload = new Dictionary<string, object?>(claims)
        {
            ["iss"] = ShellGatewayIssuer, ["aud"] = new[] { PlatformSurfaceHost.Audience }, ["roles"] = Array.Empty<string>(), ["scope"] = "crm.read crm.write",
            ["session_id"] = id, ["jti"] = id, ["iat"] = now, ["nbf"] = now, ["exp"] = now + 60
        };
        return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(payload), new SigningCredentials(host.ManagementSigningKey, SecurityAlgorithms.RsaSha256));
    }

    // 租户链上的全部记录与决策日志、本租户的 Outbox 事件信封（Outbox 表供投递器跨租户读取，按信封的 tenant_id 过滤）；顺带核对客户行数与整条链的哈希。
    private protected static async Task<(AuditRecord[] Records, (string Actor, string Result, string EntryPoint, string Target, string Reason)[] Decisions, JsonElement[] Events)> TenantTrail(
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
            return (records, await Decisions(host, tenantId), [.. events.Select(text => JsonSerializer.Deserialize<JsonElement>(text))]);
        }
    }
}
