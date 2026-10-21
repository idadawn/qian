using System.Net;
using System.Net.Http.Headers;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;
using static Qian.Identity.Server.Tests.SessionSupport;

namespace Qian.Identity.Server.Tests;

// 会话中心（S1-IDP-004）：吊销即时生效、旁路（协议授权族、后通道登出、集成事件）随后送达、并发上限与单端模式、租户状态对账、修订递增。
// 真实 PostgreSQL（受限应用角色 + 强制 RLS）、真实 OpenIddict 协议面、真实账号 HTTP 接口，依赖方用已发布的 Kun OIDC 客户端。
public sealed class SessionCenterTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task 改密后该Person的全部会话立即失效_后通道登出与吊销事件在5秒内送达()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var web = host.NewBrowser(); var mobile = host.NewBrowser();
        var (webSession, webTokens) = await SignIn(host, web, phone, tenant.Id);
        var (mobileSession, mobileTokens) = await SignIn(host, mobile, phone, tenant.Id, "mobile");

        var changed = await web.Json(HttpMethod.Post, "api/identity/me/password", new { current_password = Password, new_password = "Another-Pass-42" });
        Assert.Equal(HttpStatusCode.NoContent, changed.Status);

        // 吊销是即时的：两个端的 refresh 都已被拒，账号 Cookie 也换不到授权码。
        Assert.Equal(HttpStatusCode.BadRequest, await Refresh(web, "web", webTokens.RefreshToken!));
        Assert.Equal(HttpStatusCode.BadRequest, await Refresh(mobile, "mobile", mobileTokens.RefreshToken!));
        Assert.Null(await mobile.Code("mobile"));
        Assert.Equal("2", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE revoked_at IS NOT NULL AND revoke_reason = 'password_changed'"));

        // 旁路在 5 秒内完成：吊销事件每个会话一条（信封在会话所属租户内），RP 收到登出令牌。
        await Eventually(async () => await Revoked(host, webSession) is not null && await Revoked(host, mobileSession) is not null, Settle, "两个会话的 session_revoked 事件");
        var revoked = (await Revoked(host, webSession))!;
        Assert.Equal((tenant.Id, "qian.identity", EventActorKind.User, Id(person.Id)), (revoked.TenantId, revoked.Producer, revoked.Actor.Kind, revoked.Actor.Id));
        Assert.Equal("password_changed", revoked.Payload.GetProperty("reason").GetString());
        Assert.Equal(Id(person.Id), revoked.Payload.GetProperty("person_id").GetString());
        await Eventually(() => Task.FromResult(!host.LogoutTokens.IsEmpty), Settle, "RP 收到后通道登出令牌");
        using var kun = host.Kun("web");
        var verified = await kun.ValidateLogoutTokenAsync(Assert.Single(host.LogoutTokens), Ct);
        Assert.Equal((Id(person.Id), Id(webSession)), (verified.Subject, verified.SessionId));
        // 已处理标记落库后不再重复处理：再排空一次，事件仍是一会话一条。
        await host.Service.ProcessRevocationsAsync(100, Ct);
        Assert.Equal(2, (await EventsOf(host, "identity.session_revoked.v1")).Count);
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE revoked_at IS NOT NULL AND revocation_processed_at IS NULL"));
    }

    [Fact]
    public async Task 管理员强制下线某会话后其refresh被拒_无权限者看不到也吊销不了_跨租户的管理员同样不行()
    {
        await using var host = await AccountsHost.Start(postgres);
        var acme = host.Tenants.Add("acme"); var other = host.Tenants.Add("other");
        var (admin, adminPhone) = await CreatePerson(host, "管理员");
        var (victim, victimPhone) = await CreatePerson(host, "受害者");
        var (member, memberPhone) = await CreatePerson(host, "普通成员");
        var (outsider, outsiderPhone) = await CreatePerson(host, "外租户管理员");
        await Join(host, acme, admin.Id, admin: true); await Join(host, acme, victim.Id); await Join(host, acme, member.Id);
        await Join(host, other, outsider.Id, admin: true);
        var adminBrowser = host.NewBrowser(); var victimBrowser = host.NewBrowser(); var memberBrowser = host.NewBrowser(); var outsiderBrowser = host.NewBrowser();
        await adminBrowser.Login(adminPhone, Password, "web", acme.Id);
        var (victimSession, victimTokens) = await SignIn(host, victimBrowser, victimPhone, acme.Id);
        await memberBrowser.Login(memberPhone, Password, "web", acme.Id);
        await outsiderBrowser.Login(outsiderPhone, Password, "web", other.Id);

        // 普通成员既看不了别人的列表（403），也吊销不了别人的会话（404，不泄露它存不存在）；别的租户的管理员同样。
        var denied = await memberBrowser.Json(HttpMethod.Get, $"api/identity/sessions?person={Id(victim.Id)}");
        Assert.Equal((HttpStatusCode.Forbidden, "AUTHZ_DENIED"), (denied.Status, Code(denied.Body)));
        var foreign = await outsiderBrowser.Json(HttpMethod.Get, $"api/identity/sessions?person={Id(victim.Id)}");
        Assert.Equal(HttpStatusCode.OK, foreign.Status);
        Assert.Empty(foreign.Body.GetProperty("sessions").EnumerateArray()); // 别的租户的管理员只能看到「自己租户内」的会话——那里没有这个人的会话
        foreach (var attacker in new[] { memberBrowser, outsiderBrowser })
        {
            var blocked = await attacker.Json(HttpMethod.Post, $"api/identity/sessions/{Id(victimSession)}:revoke");
            Assert.Equal((HttpStatusCode.NotFound, "NOT_FOUND"), (blocked.Status, Code(blocked.Body)));
        }
        var nobody = await adminBrowser.Json(HttpMethod.Post, $"api/identity/sessions/{Id(Guid.NewGuid())}:revoke");
        Assert.Equal(HttpStatusCode.NotFound, nobody.Status);
        Assert.NotNull(await victimBrowser.Code()); // 受害者的会话仍然有效
        var unprotected = await adminBrowser.Json(HttpMethod.Post, $"api/identity/sessions/{Id(victimSession)}:revoke", csrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, unprotected.Status); // 写接口要求防伪令牌

        // 管理员列出受害者的会话并强制下线：refresh 立即被拒，授权端点也不再签发。
        var listed = await adminBrowser.Json(HttpMethod.Get, $"api/identity/sessions?person={Id(victim.Id)}");
        var entry = Assert.Single(listed.Body.GetProperty("sessions").EnumerateArray());
        Assert.Equal((Id(victimSession), Id(victim.Id), Id(acme.Id), "web", "pc", false),
            (entry.GetProperty("id").GetString(), entry.GetProperty("person_id").GetString(), entry.GetProperty("tenant_id").GetString(),
                entry.GetProperty("client_id").GetString(), entry.GetProperty("device_type").GetString(), entry.GetProperty("current").GetBoolean()));
        var revoked = await adminBrowser.Json(HttpMethod.Post, $"api/identity/sessions/{entry.GetProperty("id").GetString()}:revoke");
        Assert.Equal((HttpStatusCode.OK, 1), (revoked.Status, revoked.Body.GetProperty("revoked").GetInt32()));
        Assert.Equal(HttpStatusCode.BadRequest, await Refresh(victimBrowser, "web", victimTokens.RefreshToken!));
        Assert.Null(await victimBrowser.Code());
        Assert.Equal("admin_revoke", await host.Text("SELECT revoke_reason FROM plugin_qian_identity.sessions WHERE id = @s::uuid", ("s", Id(victimSession))));
        // 事件的行为主体是动手的管理员，不是受害者。
        await Eventually(async () => await Revoked(host, victimSession) is not null, Settle, "管理员吊销的 session_revoked 事件");
        var envelope = (await Revoked(host, victimSession))!;
        Assert.Equal((EventActorKind.User, Id(admin.Id), acme.Id), (envelope.Actor.Kind, envelope.Actor.Id, envelope.TenantId));
        Assert.Equal("admin_revoke", envelope.Payload.GetProperty("reason").GetString());
        // 吊销是幂等的：再吊销一次，没有可吊销的有效会话。
        var again = await adminBrowser.Json(HttpMethod.Post, $"api/identity/sessions/{Id(victimSession)}:revoke");
        Assert.Equal(HttpStatusCode.NotFound, again.Status);
    }

    [Fact]
    public async Task 并发上限默认5_第6次登录踢掉最早的会话并发出session_revoked_并发登录也不会击穿上限()
    {
        // 登录限流阈值放大：占位在先的限流会把大量并发中的登录当作尝试计数，这里只验证会话上限。
        await using var host = await AccountsHost.Start(postgres, Policy(max: 100));
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        var member = await Join(host, tenant, person.Id);
        var sessions = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            host.Clock.Advance(TimeSpan.FromSeconds(1));
            var result = await host.SignIn.SignInAsync(Input(phone, Password, "web", tenant.Id), Net, Ct);
            Assert.Equal(SignInStatus.Authenticated, result.Status);
            sessions.Add(result.Session!.SessionId);
        }
        Assert.Equal(new[] { sessions[0] }, await RevokedSessions(host, person.Id));
        Assert.Equal("concurrency_limit", await host.Text("SELECT revoke_reason FROM plugin_qian_identity.sessions WHERE id = @s::uuid", ("s", Id(sessions[0]))));
        Assert.Equal(sessions.Skip(1).Order(), (await host.Service.ListSessionsAsync(person.Id, Ct)).Select(s => s.Id).Order());
        await Eventually(async () => await Revoked(host, sessions[0]) is not null, Settle, "被踢会话的 session_revoked 事件");
        var envelope = (await Revoked(host, sessions[0]))!;
        Assert.Equal(("concurrency_limit", Id(member.Id), tenant.Id), (envelope.Payload.GetProperty("reason").GetString(), envelope.Payload.GetProperty("membership_id").GetString(), envelope.TenantId));
        Assert.Equal((EventActorKind.User, Id(person.Id)), (envelope.Actor.Kind, envelope.Actor.Id)); // 动手的是这次登录的人

        // 同一个人并发登录 12 次：Person 行锁让它们串行，最后恰好留下 5 个有效会话。
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => host.SignIn.SignInAsync(Input(phone, Password, "web", tenant.Id), Net, Ct).AsTask()));
        Assert.All(results, result => Assert.True(result.Status == SignInStatus.Authenticated, result.Error?.Code.Code));
        Assert.Equal(5, (await host.Service.ListSessionsAsync(person.Id, Ct)).Count);
    }

    [Fact]
    public async Task 单端模式下PC后登录踢掉PC先登录的会话_不影响移动端()
    {
        var options = new IdentityAccountOptions { Session = new SessionPolicy { SingleSessionPerDeviceType = true } };
        await using var host = await AccountsHost.Start(postgres, options);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        async Task<Guid> Login(string client)
        {
            host.Clock.Advance(TimeSpan.FromSeconds(1));
            var result = await host.SignIn.SignInAsync(Input(phone, Password, client, tenant.Id), Net, Ct);
            Assert.Equal(SignInStatus.Authenticated, result.Status);
            return result.Session!.SessionId;
        }
        var pc1 = await Login("web"); var phone1 = await Login("mobile");
        Assert.Empty(await RevokedSessions(host, person.Id)); // 不同端互不影响
        var pc2 = await Login("web");
        Assert.Equal(new[] { pc1 }, await RevokedSessions(host, person.Id));
        Assert.Equal("single_device", await host.Text("SELECT revoke_reason FROM plugin_qian_identity.sessions WHERE id = @s::uuid", ("s", Id(pc1))));
        Assert.Equal(new[] { phone1, pc2 }.Order(), (await host.Service.ListSessionsAsync(person.Id, Ct)).Select(s => s.Id).Order());
        await Eventually(async () => await Revoked(host, pc1) is not null, Settle, "被同端挤下线的 session_revoked 事件");
        Assert.Equal("single_device", (await Revoked(host, pc1))!.Payload.GetProperty("reason").GetString());
        var phone2 = await Login("mobile");
        Assert.Equal(new[] { pc1, phone1 }, await RevokedSessions(host, person.Id));
        Assert.Equal(new[] { phone2, pc2 }.Order(), (await host.Service.ListSessionsAsync(person.Id, Ct)).Select(s => s.Id).Order());
    }

    [Fact]
    public async Task 授权变更后identity_revision递增_旧令牌被服务端拒绝_refresh得到带新修订的令牌_其他租户的成员身份不受影响()
    {
        await using var host = await AccountsHost.Start(postgres);
        var acme = host.Tenants.Add("acme"); var beta = host.Tenants.Add("beta");
        var (person, phone) = await CreatePerson(host);
        var inAcme = await Join(host, acme, person.Id); var inBeta = await Join(host, beta, person.Id);
        var browser = host.NewBrowser();
        var (session, first) = await SignIn(host, browser, phone, acme.Id);
        Assert.Equal(0, RevisionOf(first.AccessToken));
        async Task<HttpStatusCode> UserInfo(string accessToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "connect/userinfo");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await browser.Http.SendAsync(request, Ct);
            return response.StatusCode;
        }
        Assert.Equal(HttpStatusCode.OK, await UserInfo(first.AccessToken));

        // 授权引擎在 authz.grant.changed.v1 之后让身份侧递增该主体的修订。
        long revision;
        using (host.Tenant.BeginScope(Id(acme.Id)))
            revision = await host.Accounts.BumpIdentityRevisionAsync(person.Id, "grant_changed", Ct);
        Assert.Equal(1, revision);
        Assert.Equal("1", await host.Text("SELECT identity_revision FROM plugin_qian_identity.memberships WHERE id = @m::uuid", ("m", Id(inAcme.Id))));
        Assert.Equal("0", await host.Text("SELECT identity_revision FROM plugin_qian_identity.memberships WHERE id = @m::uuid", ("m", Id(inBeta.Id)))); // 另一租户的成员身份不变
        Assert.Equal("0", await host.Text("SELECT identity_revision FROM plugin_qian_identity.persons WHERE id = @p::uuid", ("p", Id(person.Id))));

        // 旧令牌（修订 0）被服务端拒绝；会话本身没有被吊销，refresh 换到的新令牌带修订 1 并被接受。
        Assert.Equal(HttpStatusCode.Unauthorized, await UserInfo(first.AccessToken));
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE id = @s::uuid AND revoked_at IS NULL", ("s", Id(session))));
        using var kun = host.Kun("web");
        var refreshed = await kun.RefreshAsync(first.RefreshToken!, Id(person.Id), Ct);
        Assert.Equal(1, RevisionOf(refreshed.AccessToken));
        Assert.Equal(HttpStatusCode.OK, await UserInfo(refreshed.AccessToken));
        var me = await browser.Json(HttpMethod.Get, "api/identity/me");
        Assert.Equal(1, me.Body.GetProperty("identity_revision").GetInt64());
        // 再变更一次继续递增；在没有成员身份的租户里调用是 NOT_FOUND，不会建出任何东西。
        using (host.Tenant.BeginScope(Id(acme.Id))) Assert.Equal(2, await host.Accounts.BumpIdentityRevisionAsync(person.Id, "role_changed", Ct));
        var stranger = (await CreatePerson(host, "路人")).Person;
        using (host.Tenant.BeginScope(Id(acme.Id))) await Rejected("NOT_FOUND", () => host.Accounts.BumpIdentityRevisionAsync(stranger.Id, "x", Ct).AsTask());
        using (host.Tenant.BeginScope(Id(acme.Id))) await Rejected("INPUT_INVALID", () => host.Accounts.BumpIdentityRevisionAsync(person.Id, " ", Ct).AsTask());
    }
}
