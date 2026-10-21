using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;
using static Qian.Identity.Server.Tests.SessionSupport;

namespace Qian.Identity.Server.Tests;

// 会话中心的管理面与登出：平台 / 租户管理员 / 本人三种视角，在线用户，登出事件，旁路处理的多副本安全。
public sealed class SessionAdminTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static IEnumerable<Guid> People(JsonElement body) => body.GetProperty("users").EnumerateArray().Select(user => user.GetProperty("person_id").GetGuid());

    [Fact]
    public async Task 平台看得见所有租户的在线用户与会话_租户管理员只看本租户_普通成员403_在线窗口随活动滑动()
    {
        await using var host = await AccountsHost.Start(postgres);
        var acme = host.Tenants.Add("acme"); var beta = host.Tenants.Add("beta");
        var (platform, platformPhone) = await CreatePerson(host, "平台运营", platform: true);
        var (admin, adminPhone) = await CreatePerson(host, "甲方管理员");
        var (user, userPhone) = await CreatePerson(host, "甲方成员");
        var (other, otherPhone) = await CreatePerson(host, "乙方成员");
        await Join(host, acme, admin.Id, admin: true); await Join(host, acme, user.Id); await Join(host, beta, other.Id);
        var platformBrowser = host.NewBrowser(); var adminBrowser = host.NewBrowser(); var userBrowser = host.NewBrowser(); var otherBrowser = host.NewBrowser();
        var platformLogin = await platformBrowser.Login(platformPhone, Password);
        Assert.Equal(("authenticated", true), (platformLogin.Body.GetProperty("status").GetString(), platformLogin.Body.GetProperty("platform").GetBoolean()));
        await adminBrowser.Login(adminPhone, Password, "web", acme.Id); await userBrowser.Login(userPhone, Password, "web", acme.Id); await otherBrowser.Login(otherPhone, Password, "web", beta.Id);

        // 平台视角：所有租户加平台身份自己，共 4 个在线用户；按租户过滤后只剩该租户的。
        var all = await platformBrowser.Json(HttpMethod.Get, "api/identity/online-users");
        Assert.Equal(HttpStatusCode.OK, all.Status);
        Assert.Equal(new[] { admin.Id, user.Id, other.Id, platform.Id }.Order(), People(all.Body).Order());
        var self = all.Body.GetProperty("users").EnumerateArray().Single(entry => entry.GetProperty("person_id").GetGuid() == platform.Id);
        Assert.False(self.TryGetProperty("tenant_id", out _)); // 平台身份的会话不属于任何租户，空值字段省略
        Assert.Equal((1, "平台运营"), (self.GetProperty("sessions").GetInt32(), self.GetProperty("display_name").GetString()));
        Assert.Equal(["pc"], self.GetProperty("device_types").EnumerateArray().Select(device => device.GetString()!));
        var onlyAcme = await platformBrowser.Json(HttpMethod.Get, $"api/identity/online-users?tenant_id={Id(acme.Id)}");
        Assert.Equal(new[] { admin.Id, user.Id }.Order(), People(onlyAcme.Body).Order());
        Assert.Equal(2, (await platformBrowser.Json(HttpMethod.Get, "api/identity/online-users?limit=2&offset=2")).Body.GetProperty("users").GetArrayLength());
        foreach (var bad in new[] { "limit=0", "limit=201", "offset=-1", "tenant_id=not-a-guid" })
            Assert.Equal(HttpStatusCode.BadRequest, (await platformBrowser.Json(HttpMethod.Get, "api/identity/online-users?" + bad)).Status);

        // 租户管理员只看本租户（传别的租户的参数也不管用）；普通成员 403；未登录 401。
        var adminView = await adminBrowser.Json(HttpMethod.Get, $"api/identity/online-users?tenant_id={Id(beta.Id)}");
        Assert.Equal(new[] { admin.Id, user.Id }.Order(), People(adminView.Body).Order());
        var denied = await userBrowser.Json(HttpMethod.Get, "api/identity/online-users");
        Assert.Equal((HttpStatusCode.Forbidden, "AUTHZ_DENIED"), (denied.Status, Code(denied.Body)));
        var anonymous = await host.NewBrowser().Json(HttpMethod.Get, "api/identity/online-users");
        Assert.Equal((HttpStatusCode.Unauthorized, "UNAUTHENTICATED"), (anonymous.Status, Code(anonymous.Body)));

        // 会话列表：本人（缺省 person）带 current 标记；平台可看任何人并按租户缩小。
        var own = await userBrowser.Json(HttpMethod.Get, "api/identity/sessions");
        var ownEntry = Assert.Single(own.Body.GetProperty("sessions").EnumerateArray());
        Assert.Equal((user.Id, acme.Id, true, "web", "pc"),
            (ownEntry.GetProperty("person_id").GetGuid(), ownEntry.GetProperty("tenant_id").GetGuid(), ownEntry.GetProperty("current").GetBoolean(),
                ownEntry.GetProperty("client_id").GetString(), ownEntry.GetProperty("device_type").GetString()));
        Assert.Equal("127.0.0.1", ownEntry.GetProperty("remote_address").GetString());
        var seen = await platformBrowser.Json(HttpMethod.Get, $"api/identity/sessions?person={Id(other.Id)}");
        Assert.False(Assert.Single(seen.Body.GetProperty("sessions").EnumerateArray()).GetProperty("current").GetBoolean());
        Assert.Empty((await platformBrowser.Json(HttpMethod.Get, $"api/identity/sessions?person={Id(other.Id)}&tenant_id={Id(acme.Id)}")).Body.GetProperty("sessions").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await platformBrowser.Json(HttpMethod.Get, "api/identity/sessions?person=oops")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.NewBrowser().Json(HttpMethod.Get, "api/identity/sessions")).Status);

        // 在线窗口随活动滑动：超过 15 分钟没有活动的会话不再算在线（会话本身仍有效）；一次校验（如访问 /me）后重新在线。
        host.Clock.Advance(TimeSpan.FromMinutes(16));
        var stale = await platformBrowser.Json(HttpMethod.Get, "api/identity/online-users"); // 发起查询本身就是一次活动，所以平台自己仍在线
        Assert.Equal([platform.Id], People(stale.Body));
        Assert.Equal(HttpStatusCode.OK, (await userBrowser.Json(HttpMethod.Get, "api/identity/me")).Status);
        var again = await platformBrowser.Json(HttpMethod.Get, "api/identity/online-users");
        Assert.Equal(new[] { user.Id, platform.Id }.Order(), People(again.Body).Order());
    }

    [Fact]
    public async Task 本人吊销自己的会话_revoke_all按身份划定范围_吊销当前会话同时清除Cookie()
    {
        await using var host = await AccountsHost.Start(postgres);
        var acme = host.Tenants.Add("acme"); var beta = host.Tenants.Add("beta");
        var (platform, platformPhone) = await CreatePerson(host, "平台运营", platform: true);
        var (admin, adminPhone) = await CreatePerson(host, "甲方管理员");
        var (person, phone) = await CreatePerson(host, "两边的人");
        var (bystander, bystanderPhone) = await CreatePerson(host, "路人");
        await Join(host, acme, admin.Id, admin: true); await Join(host, acme, person.Id); await Join(host, beta, person.Id); await Join(host, acme, bystander.Id);
        var adminBrowser = host.NewBrowser(); var platformBrowser = host.NewBrowser(); var bystanderBrowser = host.NewBrowser();
        await adminBrowser.Login(adminPhone, Password, "web", acme.Id); await platformBrowser.Login(platformPhone, Password); await bystanderBrowser.Login(bystanderPhone, Password, "web", acme.Id);
        var inAcme = host.NewBrowser(); var inBeta = host.NewBrowser(); var onPhone = host.NewBrowser();
        var (sessionAcme, tokensAcme) = await SignIn(host, inAcme, phone, acme.Id);
        var (sessionBeta, tokensBeta) = await SignIn(host, inBeta, phone, beta.Id);
        var (sessionPhone, tokensPhone) = await SignIn(host, onPhone, phone, acme.Id, "mobile");

        // 本人看到自己跨租户跨端的全部会话，当前会话带标记；只吊销另一个会话时，当前会话不受影响。
        var mine = await inAcme.Json(HttpMethod.Get, "api/identity/sessions");
        Assert.Equal(new[] { sessionAcme, sessionBeta, sessionPhone }.Order(), mine.Body.GetProperty("sessions").EnumerateArray().Select(entry => entry.GetProperty("id").GetGuid()).Order());
        Assert.Equal(sessionAcme, Assert.Single(mine.Body.GetProperty("sessions").EnumerateArray(), entry => entry.GetProperty("current").GetBoolean()).GetProperty("id").GetGuid());
        var selfRevoke = await inAcme.Json(HttpMethod.Post, $"api/identity/sessions/{Id(sessionPhone)}:revoke");
        Assert.Equal((HttpStatusCode.OK, 1), (selfRevoke.Status, selfRevoke.Body.GetProperty("revoked").GetInt32()));
        Assert.Equal(HttpStatusCode.BadRequest, await Refresh(onPhone, "mobile", tokensPhone.RefreshToken!));
        Assert.Equal(HttpStatusCode.OK, (await inAcme.Json(HttpMethod.Get, "api/identity/me")).Status);
        Assert.Equal("user_revoke", await host.Text("SELECT revoke_reason FROM plugin_qian_identity.sessions WHERE id = @s::uuid", ("s", Id(sessionPhone))));
        await Eventually(async () => await Revoked(host, sessionPhone) is not null, Settle, "本人吊销的 session_revoked 事件");

        // 无权限者：普通成员管不了别人（403），未登录是 401，缺防伪令牌是 400。
        var forbidden = await bystanderBrowser.Json(HttpMethod.Post, $"api/identity/persons/{Id(person.Id)}:revoke-all");
        Assert.Equal((HttpStatusCode.Forbidden, "AUTHZ_DENIED"), (forbidden.Status, Code(forbidden.Body)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.NewBrowser().Json(HttpMethod.Post, $"api/identity/persons/{Id(person.Id)}:revoke-all")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await platformBrowser.Json(HttpMethod.Post, $"api/identity/persons/{Id(person.Id)}:revoke-all", csrf: false)).Status);

        // 租户管理员对该人 revoke-all：只吊销甲方租户内的会话，乙方的不受影响（传别的租户的参数也越不了权）。
        var adminRevoke = await adminBrowser.Json(HttpMethod.Post, $"api/identity/persons/{Id(person.Id)}:revoke-all?tenant_id={Id(beta.Id)}");
        Assert.Equal((HttpStatusCode.OK, 1), (adminRevoke.Status, adminRevoke.Body.GetProperty("revoked").GetInt32())); // 甲方内只剩 web 这一个有效会话
        Assert.Equal("admin_revoke_all", await host.Text("SELECT revoke_reason FROM plugin_qian_identity.sessions WHERE id = @s::uuid", ("s", Id(sessionAcme))));
        Assert.Equal(HttpStatusCode.BadRequest, await Refresh(inAcme, "web", tokensAcme.RefreshToken!));
        Assert.Equal(HttpStatusCode.OK, (await inBeta.Json(HttpMethod.Get, "api/identity/me")).Status); // 乙方会话仍然有效

        // 平台身份：可以按租户缩小，也可以吊销某人的全部会话；事件的行为主体是动手的平台身份。
        var narrowed = await platformBrowser.Json(HttpMethod.Post, $"api/identity/persons/{Id(person.Id)}:revoke-all?tenant_id={Id(acme.Id)}");
        Assert.Equal(0, narrowed.Body.GetProperty("revoked").GetInt32()); // 甲方已经没有有效会话
        Assert.Equal(HttpStatusCode.OK, (await inBeta.Json(HttpMethod.Get, "api/identity/me")).Status);
        var everything = await platformBrowser.Json(HttpMethod.Post, $"api/identity/persons/{Id(person.Id)}:revoke-all");
        Assert.Equal(1, everything.Body.GetProperty("revoked").GetInt32()); // 剩下乙方那一个
        Assert.Equal(HttpStatusCode.BadRequest, await Refresh(inBeta, "web", tokensBeta.RefreshToken!));
        Assert.Equal(HttpStatusCode.Unauthorized, (await inBeta.Json(HttpMethod.Get, "api/identity/me")).Status);
        Assert.Equal(HttpStatusCode.OK, (await platformBrowser.Json(HttpMethod.Get, "api/identity/me")).Status); // 平台自己的会话没被波及
        await Eventually(async () => await Revoked(host, sessionBeta) is not null, Settle, "平台吊销的 session_revoked 事件");
        var envelope = (await Revoked(host, sessionBeta))!;
        Assert.Equal((EventActorKind.User, Id(platform.Id), "admin_revoke_all", beta.Id), (envelope.Actor.Kind, envelope.Actor.Id, envelope.Payload.GetProperty("reason").GetString(), envelope.TenantId));

        // 本人 revoke-all 连当前会话一并吊销，账号 Cookie 随之清除。
        var (freshSession, fresh) = await SignIn(host, inAcme, phone, acme.Id);
        var selfAll = await inAcme.Json(HttpMethod.Post, $"api/identity/persons/{Id(person.Id)}:revoke-all");
        Assert.Equal(1, selfAll.Body.GetProperty("revoked").GetInt32());
        Assert.False(inAcme.HasAccountCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await inAcme.Json(HttpMethod.Get, "api/identity/me")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, await Refresh(inAcme, "web", fresh.RefreshToken!));
        Assert.Equal("user_revoke_all", await host.Text("SELECT revoke_reason FROM plugin_qian_identity.sessions WHERE id = @s::uuid", ("s", Id(freshSession))));

        // 平台身份的会话不属于任何租户：吊销照样走完旁路，事件的信封不带租户。
        var platformSession = Assert.Single(await host.Service.ListSessionsAsync(platform.Id, Ct)).Id;
        Assert.Equal(1, (await platformBrowser.Json(HttpMethod.Post, $"api/identity/persons/{Id(platform.Id)}:revoke-all")).Body.GetProperty("revoked").GetInt32());
        await Eventually(async () => await Revoked(host, platformSession) is not null, Settle, "平台会话的 session_revoked 事件");
        Assert.Null((await Revoked(host, platformSession))!.TenantId);
    }

    [Fact]
    public async Task 登出走logout事件而不是session_revoked_RP收到后通道登出_refresh被拒()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var browser = host.NewBrowser();
        var (session, tokens) = await SignIn(host, browser, phone, tenant.Id);
        Assert.Equal(HttpStatusCode.Redirect, await Logout(host, browser, tokens.IdToken!));
        Assert.Equal(HttpStatusCode.BadRequest, await Refresh(browser, "web", tokens.RefreshToken!));
        Assert.Equal("logout", await host.Text("SELECT revoke_reason FROM plugin_qian_identity.sessions WHERE id = @s::uuid", ("s", Id(session))));

        // 登出发 identity.logout.v1（信封在会话所属租户内），不会再重复发一条 session_revoked；RP 在 5 秒内收到登出令牌。
        await Eventually(async () => (await EventsOf(host, "identity.logout.v1")).Count == 1, Settle, "登出事件");
        var envelope = Assert.Single(await EventsOf(host, "identity.logout.v1"));
        Assert.Equal((Id(session), "logout", Id(person.Id), tenant.Id), (envelope.Payload.GetProperty("session_id").GetString(), envelope.Payload.GetProperty("reason").GetString(), envelope.Payload.GetProperty("person_id").GetString(), envelope.TenantId));
        Assert.Empty(await EventsOf(host, "identity.session_revoked.v1"));
        await Eventually(() => Task.FromResult(!host.LogoutTokens.IsEmpty), Settle, "RP 收到后通道登出令牌");
        using var kun = host.Kun("web");
        var verified = await kun.ValidateLogoutTokenAsync(Assert.Single(host.LogoutTokens), Ct);
        Assert.Equal((Id(person.Id), Id(session)), (verified.Subject, verified.SessionId));
    }

    [Fact]
    public async Task 两个副本同时处理吊销旁路_每个会话恰好一条事件_重复排空不再产生新事件()
    {
        await using var host = await AccountsHost.Start(postgres, new IdentityAccountOptions { Session = new SessionPolicy { MaxConcurrentSessions = 20 } });
        await StopProcessor(host); // 由测试亲手扮演两个副本，竞争同一批待处理的吊销
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var sessions = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            host.Clock.Advance(TimeSpan.FromSeconds(1));
            var result = await host.SignIn.SignInAsync(Input(phone, Password, "web", tenant.Id), Net, Ct);
            sessions.Add(result.Session!.SessionId);
        }
        await host.Service.RevokeSessionsAsync(new SessionRevocation(person.Id, null, "admin_revoke_all", "test"), Ct);
        Assert.Equal("10", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE revoked_at IS NOT NULL AND revocation_processed_at IS NULL"));

        // 第二个副本：同一数据库、同一套协议与索引，只是另一个服务实例（各有各的互斥量与退避状态）。
        using var replica = new IdentityAccountService(host.Loader.GetContext("identity", ScopeKey.Global), host.Services.GetRequiredService<IdentityServerOptions>()) { Clock = host.Clock };
        var rounds = await Task.WhenAll(Enumerable.Range(0, 4).Select(round => Task.Run(async () => round % 2 == 0
            ? await host.Service.ProcessRevocationsAsync(3, Ct) : await replica.ProcessRevocationsAsync(4, Ct), Ct)));
        Assert.InRange(rounds.Sum(), 1, 10);
        while (await host.Service.ProcessRevocationsAsync(50, Ct) + await replica.ProcessRevocationsAsync(50, Ct) > 0) { /* 排空 */ }

        var events = await EventsOf(host, "identity.session_revoked.v1");
        Assert.Equal(sessions.Order(), events.Select(e => Guid.ParseExact(e.Payload.GetProperty("session_id").GetString()!, "D")).Order()); // 每个会话恰好一条
        Assert.All(events, e => Assert.Equal(tenant.Id, e.TenantId));
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE revoked_at IS NOT NULL AND revocation_processed_at IS NULL"));
        // 已处理的会话不会再被处理：再排空一次，什么也不发生。
        Assert.Equal(0, await host.Service.ProcessRevocationsAsync(50, Ct) + await replica.ProcessRevocationsAsync(50, Ct));
        Assert.Equal(10, (await EventsOf(host, "identity.session_revoked.v1")).Count);
    }
}
