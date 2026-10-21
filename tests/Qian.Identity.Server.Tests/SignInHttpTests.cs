using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kun.Identity.Abstractions;
using Kun.Identity.Implementations;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Tenancy;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;

namespace Qian.Identity.Server.Tests;

// 账号 HTTP 面的端到端：防伪令牌、登录、选择租户、切换、授权码换令牌，用已发布的 Kun OIDC 客户端当依赖方。
public sealed class SignInHttpTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static string Id(Guid id) => id.ToString("D");

    private static string Code(JsonElement problem) => problem.GetProperty("code").GetString()!;

    private static async Task<(string Code, string Verifier, string Nonce)> RequireCode(AccountsHost.Browser browser)
    {
        var code = await browser.Code();
        Assert.NotNull(code);
        return code.Value;
    }

    private static async Task<OidcTokenResponse> Tokens(AccountsHost.Running host, AccountsHost.Browser browser, KunOidcClient client)
    {
        var (code, verifier, nonce) = await RequireCode(browser);
        return await client.ExchangeCodeAsync(new(code, host.Redirect, verifier, nonce), Ct);
    }

    [Fact]
    public async Task 登录_选择租户_授权码换令牌_令牌带身份声明且不含权限_切换租户重签_旧会话的刷新被拒()
    {
        await using var world = await TwoTenants(postgres);
        var (host, a, b, person, phone, inA, inB) = world;
        var browser = host.NewBrowser();
        Assert.Null(await browser.Code()); // 没有会话：被引导去登录而不是签发
        var login = await browser.Login(phone, Password);
        Assert.Equal(HttpStatusCode.OK, login.Status);
        Assert.Equal("selection_required", login.Body.GetProperty("status").GetString());
        Assert.Contains("no-store", login.Raw.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, login.Body.GetProperty("choices").GetArrayLength());
        // 选择之前没有账号会话：授权端点仍不签发。
        Assert.False(browser.HasAccountCookie); Assert.Null(await browser.Code());
        var selected = await browser.Select(login.Body.GetProperty("ticket").GetString()!, inA.Id);
        Assert.Equal(HttpStatusCode.OK, selected.Status);
        Assert.Equal("authenticated", selected.Body.GetProperty("status").GetString());
        Assert.Equal((a.Id, inA.Id, false), (selected.Body.GetProperty("tenant_id").GetGuid(), selected.Body.GetProperty("membership_id").GetGuid(), selected.Body.GetProperty("platform").GetBoolean()));
        Assert.True(browser.HasAccountCookie);
        var me = await browser.Json(HttpMethod.Get, "api/identity/me");
        Assert.Equal(HttpStatusCode.OK, me.Status);
        Assert.Equal((person.Id, a.Id, inA.Id, 0L), (me.Body.GetProperty("person_id").GetGuid(), me.Body.GetProperty("tenant_id").GetGuid(), me.Body.GetProperty("membership_id").GetGuid(), me.Body.GetProperty("identity_revision").GetInt64()));

        using var kun = host.Kun("web");
        var first = await Tokens(host, browser, kun);
        var accessA = new JsonWebToken(first.AccessToken);
        var sidA = await host.Text("SELECT id FROM plugin_qian_identity.sessions WHERE revoked_at IS NULL");
        Assert.Equal(Id(person.Id), first.ValidatedPrincipal!.FindFirst("sub")!.Value);
        Assert.Equal(Id(a.Id), first.ValidatedPrincipal.FindFirst("tenant_id")!.Value);
        Assert.Equal(Id(inA.Id), first.ValidatedPrincipal.FindFirst("membership_id")!.Value);
        Assert.Equal(sidA, first.ValidatedPrincipal.FindFirst("sid")!.Value);
        Assert.Equal("pwd", Assert.Single(first.ValidatedPrincipal.FindAll("amr")).Value);
        Assert.Equal("0", first.ValidatedPrincipal.FindFirst("identity_revision")!.Value);
        Assert.Equal((Id(person.Id), Id(a.Id), Id(inA.Id), sidA, "user"),
            (accessA.Subject, accessA.GetClaim("tenant_id").Value, accessA.GetClaim("membership_id").Value, accessA.GetClaim("sid").Value, accessA.GetClaim("principal_kind").Value));
        Assert.Contains("qian.api", accessA.GetClaim("scope").Value, StringComparison.Ordinal);
        Assert.False(accessA.TryGetClaim("acr", out _));
        // 令牌承载身份，不承载权限。
        Assert.DoesNotContain(accessA.Claims, claim => claim.Type is "permissions" or "permission" or "roles" or "role" or "groups");
        Assert.DoesNotContain(first.ValidatedPrincipal.Claims, claim => claim.Type is "permissions" or "permission" or "roles" or "role" or "groups");

        // 在外壳切换租户：重新走选择令牌，签发新会话与新令牌；认证时刻不变。
        var begin = await browser.Json(HttpMethod.Post, "api/identity/session/switch", new { });
        Assert.Equal(HttpStatusCode.OK, begin.Status);
        Assert.Equal("selection_required", begin.Body.GetProperty("status").GetString());
        Assert.Equal(2, begin.Body.GetProperty("choices").GetArrayLength());
        var switched = await browser.Select(begin.Body.GetProperty("ticket").GetString()!, inB.Id);
        Assert.Equal((b.Id, inB.Id), (switched.Body.GetProperty("tenant_id").GetGuid(), switched.Body.GetProperty("membership_id").GetGuid()));
        var second = await Tokens(host, browser, kun);
        var accessB = new JsonWebToken(second.AccessToken);
        Assert.Equal((Id(b.Id), Id(inB.Id)), (accessB.GetClaim("tenant_id").Value, accessB.GetClaim("membership_id").Value));
        Assert.NotEqual(sidA, accessB.GetClaim("sid").Value);
        Assert.Equal(accessA.GetClaim("auth_time").Value, accessB.GetClaim("auth_time").Value);
        Assert.Equal(Id(person.Id), accessB.Subject);
        Assert.Equal("switch", await host.Text("SELECT revoke_reason FROM plugin_qian_identity.sessions WHERE id = @sid::uuid", ("sid", sidA)));
        // 旧会话（A）已吊销：它的刷新被拒；新会话的刷新可用且仍是 B。
        using (var stale = await browser.Grant("web", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = first.RefreshToken! }))
            Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        var refreshed = await kun.RefreshAsync(second.RefreshToken!, Id(person.Id), Ct);
        Assert.Equal(Id(b.Id), new JsonWebToken(refreshed.AccessToken).GetClaim("tenant_id").Value);
    }

    [Fact]
    public async Task 写接口都要求防伪令牌_要求JSON_体积上限_未知字段与坏JSON被拒绝_未登录访问受保护接口是401_响应一律no_store()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var browser = host.NewBrowser();
        foreach (var path in new[] { "api/identity/login", "api/identity/login/select", "api/identity/login/password", "api/identity/session/switch", "api/identity/me/password" })
        {
            var noToken = await browser.Json(HttpMethod.Post, path, new { }, csrf: false);
            Assert.Equal((HttpStatusCode.BadRequest, "INPUT_INVALID"), (noToken.Status, Code(noToken.Body)));
            Assert.Equal("application/problem+json", noToken.Raw.Content.Headers.ContentType!.MediaType);
        }
        var (header, token) = await browser.Csrf();
        async Task<HttpResponseMessage> Send(HttpContent content)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/identity/login") { Content = content };
            request.Headers.Add(header, token);
            return await browser.Http.SendAsync(request, Ct);
        }
        using (var plain = await Send(new StringContent("{}", Encoding.UTF8, "text/plain"))) Assert.Equal(HttpStatusCode.UnsupportedMediaType, plain.StatusCode);
        using (var huge = await Send(new StringContent(new string('a', 17_000), Encoding.UTF8, "application/json"))) Assert.Equal(HttpStatusCode.RequestEntityTooLarge, huge.StatusCode);
        using (var broken = await Send(new StringContent("{not json", Encoding.UTF8, "application/json"))) Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
        var unknown = await browser.Json(HttpMethod.Post, "api/identity/login", new { client_id = "web", identifier = phone, password = Password, evil = 1 });
        Assert.Equal((HttpStatusCode.BadRequest, "BAD_REQUEST"), (unknown.Status, Code(unknown.Body)));
        var missing = await browser.Json(HttpMethod.Post, "api/identity/login", new { client_id = "web", identifier = phone });
        Assert.Equal(HttpStatusCode.BadRequest, missing.Status);
        Assert.Equal("#/proof", missing.Body.GetProperty("errors")[0].GetProperty("pointer").GetString());
        Assert.False(browser.HasAccountCookie);
        // 未登录访问受保护接口：401，且不泄露任何账号信息。
        foreach (var (method, path, body) in new (HttpMethod, string, object?)[]
                 { (HttpMethod.Get, "api/identity/me", null), (HttpMethod.Post, "api/identity/session/switch", new { }), (HttpMethod.Post, "api/identity/me/password", new { current_password = "x", new_password = "y" }) })
        {
            var anonymous = await browser.Json(method, path, body);
            Assert.Equal((HttpStatusCode.Unauthorized, "UNAUTHENTICATED"), (anonymous.Status, Code(anonymous.Body)));
        }
        // 安全响应头：账号接口一律不缓存、不嗅探。
        var any = await browser.Login(phone, "Wrong-Pass-1");
        Assert.Equal(HttpStatusCode.Unauthorized, any.Status);
        Assert.True(any.Raw.Headers.CacheControl!.NoStore);
        Assert.Contains("no-cache", any.Raw.Headers.Pragma.ToString(), StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(any.Raw.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(any.Raw.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("frame-ancestors 'none'", Assert.Single(any.Raw.Headers.GetValues("Content-Security-Policy")), StringComparison.Ordinal);
        Assert.False(any.Raw.Headers.Contains("Set-Cookie") && any.Raw.Headers.GetValues("Set-Cookie").Any(cookie => cookie.Contains("qian.identity.signin", StringComparison.Ordinal)));
        // 登录失败的响应体不回显账号或密码。
        Assert.DoesNotContain(phone, any.Body.GetRawText(), StringComparison.Ordinal); Assert.DoesNotContain("Wrong-Pass-1", any.Body.GetRawText(), StringComparison.Ordinal);
        Assert.Equal("INVALID_CREDENTIALS", Code(any.Body));
        Assert.Equal(32, any.Body.GetProperty("trace_id").GetString()!.Length);
    }

    [Fact]
    public async Task 连续输错后返回423与Retry_After_锁定期间正确密码也不签发_恢复后可登录()
    {
        await using var host = await AccountsHost.Start(postgres, Policy(max: 3));
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var browser = host.NewBrowser();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var failed = await browser.Login(phone, "Wrong-Pass-1");
            Assert.Equal((HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS"), (failed.Status, Code(failed.Body)));
            Assert.False(failed.Raw.Headers.Contains("Retry-After"));
        }
        var locked = await browser.Login(phone, "Wrong-Pass-1");
        Assert.Equal(((HttpStatusCode)423, "ACCOUNT_LOCKED"), (locked.Status, Code(locked.Body)));
        Assert.Equal("900", Assert.Single(locked.Raw.Headers.GetValues("Retry-After")));
        host.Clock.Advance(TimeSpan.FromSeconds(30));
        var during = await browser.Login(phone, Password);
        Assert.Equal((HttpStatusCode)423, during.Status);
        Assert.Equal("870", Assert.Single(during.Raw.Headers.GetValues("Retry-After")));
        Assert.False(browser.HasAccountCookie);
        host.Clock.Advance(TimeSpan.FromMinutes(15));
        var recovered = await browser.Login(phone, Password);
        Assert.Equal("authenticated", recovered.Body.GetProperty("status").GetString());
        Assert.True(browser.HasAccountCookie);
    }

    [Fact]
    public async Task 初始密码账号先被引导改密_弱密码被拒_改密成功才建立会话并可换令牌()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host, requireChange: true);
        var member = await Join(host, tenant, person.Id);
        var browser = host.NewBrowser();
        var login = await browser.Login(phone, Password);
        Assert.Equal(HttpStatusCode.OK, login.Status);
        Assert.Equal("password_change_required", login.Body.GetProperty("status").GetString());
        var ticket = login.Body.GetProperty("ticket").GetString()!;
        Assert.False(login.Body.TryGetProperty("choices", out _));
        Assert.False(browser.HasAccountCookie); Assert.Null(await browser.Code()); // 改密之前没有会话，授权端点不签发
        // 改密令牌不能当选择令牌用。
        var misuse = await browser.Select(ticket, member.Id);
        Assert.Equal((HttpStatusCode.Unauthorized, "LOGIN_TICKET_INVALID"), (misuse.Status, Code(misuse.Body)));
        var weak = await browser.Json(HttpMethod.Post, "api/identity/login/password", new { ticket, new_password = "short" });
        Assert.Equal(((HttpStatusCode)422, "PASSWORD_POLICY_VIOLATION"), (weak.Status, Code(weak.Body)));
        Assert.False(browser.HasAccountCookie);
        var changed = await browser.Json(HttpMethod.Post, "api/identity/login/password", new { ticket, new_password = "Brand-New-Pass-7" });
        Assert.Equal(HttpStatusCode.OK, changed.Status);
        Assert.Equal("authenticated", changed.Body.GetProperty("status").GetString());
        Assert.True(browser.HasAccountCookie);
        using var kun = host.Kun("web");
        var tokens = await Tokens(host, browser, kun);
        Assert.Equal(Id(member.Id), new JsonWebToken(tokens.AccessToken).GetClaim("membership_id").Value);
        Assert.Equal("1", new JsonWebToken(tokens.AccessToken).GetClaim("identity_revision").Value); // 改密使修订加一，令牌带的是最新修订
        var replay = await browser.Json(HttpMethod.Post, "api/identity/login/password", new { ticket, new_password = "Another-New-Pass-8" });
        Assert.Equal((HttpStatusCode.Unauthorized, "LOGIN_TICKET_INVALID"), (replay.Status, Code(replay.Body)));
    }

    [Fact]
    public async Task 会话被吊销_账号停用或租户冻结后_授权端点不再签发_me返回401_旧刷新被拒_Cookie随之清除()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        var member = await Join(host, tenant, person.Id);
        using var kun = host.Kun("web");

        // 管理员吊销该账号的全部会话。
        var browser = host.NewBrowser();
        Assert.Equal("authenticated", (await browser.Login(phone, Password)).Body.GetProperty("status").GetString());
        var tokens = await Tokens(host, browser, kun);
        await host.Service.RevokeSessionsAsync(new SessionRevocation(person.Id, null, "forced", "k1"), Ct);
        Assert.Null(await browser.Code());
        Assert.False(browser.HasAccountCookie); // 授权端点发现会话已失效，随手清掉 Cookie
        using (var stale = await browser.Grant("web", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = tokens.RefreshToken! }))
            Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);

        // 租户被冻结：已登录的会话立即不可用。
        var frozen = host.NewBrowser();
        await frozen.Login(phone, Password);
        Assert.Equal(HttpStatusCode.OK, (await frozen.Json(HttpMethod.Get, "api/identity/me")).Status);
        host.Tenants.SetStatus(tenant.Id, TenantStatus.Suspended);
        Assert.Equal(HttpStatusCode.Unauthorized, (await frozen.Json(HttpMethod.Get, "api/identity/me")).Status);
        Assert.Null(await frozen.Code());
        var denied = await frozen.Login(phone, Password);
        Assert.Equal((HttpStatusCode.Forbidden, "TENANT_NOT_ACTIVE"), (denied.Status, Code(denied.Body)));
        host.Tenants.SetStatus(tenant.Id, TenantStatus.Active);

        // 账号被停用：同上，且输对密码才会被告知已停用。
        var disabled = host.NewBrowser();
        await disabled.Login(phone, Password);
        await host.Accounts.SetPersonStatusAsync(person.Id, AccountStatuses.Disabled, "ops", Ct);
        Assert.Null(await disabled.Code());
        Assert.Equal((HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS"), ((await disabled.Login(phone, "Wrong-Pass-1")).Status, Code((await disabled.Login(phone, "Wrong-Pass-1")).Body)));
        var off = await disabled.Login(phone, Password);
        Assert.Equal((HttpStatusCode.Forbidden, "ACCOUNT_DISABLED"), (off.Status, Code(off.Body)));
        await host.Accounts.SetPersonStatusAsync(person.Id, AccountStatuses.Active, "ops", Ct);

        // 成员被停用：只影响该成员的会话。
        var removed = host.NewBrowser();
        await removed.Login(phone, Password);
        using (host.Tenant.BeginScope(Id(tenant.Id))) await host.Accounts.SetMembershipStatusAsync(member.Id, AccountStatuses.Disabled, "ops", Ct);
        Assert.Null(await removed.Code());
        var none = await removed.Login(phone, Password);
        Assert.Equal((HttpStatusCode.Forbidden, "NO_AVAILABLE_TENANT"), (none.Status, Code(none.Body)));
    }

    [Fact]
    public async Task 自助改密使全部会话与刷新失效_旧密码不能再登录_当前密码错误与弱新密码被拒()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        using var kun = host.Kun("web");
        var browser = host.NewBrowser(); var other = host.NewBrowser();
        await browser.Login(phone, Password); await other.Login(phone, Password);
        var tokens = await Tokens(host, browser, kun);
        var wrong = await browser.Json(HttpMethod.Post, "api/identity/me/password", new { current_password = "Wrong-Pass-1", new_password = "Brand-New-Pass-7" });
        Assert.Equal(((HttpStatusCode)422, "CURRENT_PASSWORD_INVALID"), (wrong.Status, Code(wrong.Body)));
        var weak = await browser.Json(HttpMethod.Post, "api/identity/me/password", new { current_password = Password, new_password = "short" });
        Assert.Equal(((HttpStatusCode)422, "PASSWORD_POLICY_VIOLATION"), (weak.Status, Code(weak.Body)));
        Assert.Equal(HttpStatusCode.OK, (await browser.Json(HttpMethod.Get, "api/identity/me")).Status);
        var changed = await browser.Json(HttpMethod.Post, "api/identity/me/password", new { current_password = Password, new_password = "Brand-New-Pass-7" });
        Assert.Equal(HttpStatusCode.NoContent, changed.Status);
        Assert.False(browser.HasAccountCookie);
        // 该账号的全部会话（含另一台设备）与刷新令牌一并失效。
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.Json(HttpMethod.Get, "api/identity/me")).Status);
        Assert.Null(await other.Code());
        using (var stale = await browser.Grant("web", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = tokens.RefreshToken! }))
            Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.Login(phone, Password)).Status);
        Assert.Equal("authenticated", (await browser.Login(phone, "Brand-New-Pass-7")).Body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task 平台身份会话_令牌的tenant_id显式为null且不含成员_ID令牌与用户信息不带租户()
    {
        await using var host = await AccountsHost.Start(postgres);
        var (operatorPerson, phone) = await CreatePerson(host, "运营", platform: true);
        var browser = host.NewBrowser();
        var login = await browser.Login(phone, Password);
        Assert.Equal("authenticated", login.Body.GetProperty("status").GetString());
        Assert.True(login.Body.GetProperty("platform").GetBoolean());
        Assert.False(login.Body.TryGetProperty("tenant_id", out _)); Assert.False(login.Body.TryGetProperty("membership_id", out _));
        var me = await browser.Json(HttpMethod.Get, "api/identity/me");
        Assert.Equal(Id(operatorPerson.Id), me.Body.GetProperty("person_id").GetString());
        Assert.False(me.Body.TryGetProperty("tenant_id", out _)); Assert.False(me.Body.TryGetProperty("membership_id", out _));
        using var kun = host.Kun("web");
        var tokens = await Tokens(host, browser, kun);
        var access = new JsonWebToken(tokens.AccessToken);
        using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(access.EncodedPayload));
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("tenant_id").ValueKind);
        Assert.False(payload.RootElement.TryGetProperty("membership_id", out _));
        Assert.Equal(Id(operatorPerson.Id), access.Subject);
        Assert.Null(tokens.ValidatedPrincipal!.FindFirst("tenant_id")); Assert.Null(tokens.ValidatedPrincipal.FindFirst("membership_id"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "connect/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var response = await browser.Http.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var info = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(Id(operatorPerson.Id), info.RootElement.GetProperty("sub").GetString());
        Assert.Equal(JsonValueKind.Null, info.RootElement.GetProperty("tenant_id").ValueKind);
        Assert.False(info.RootElement.TryGetProperty("membership_id", out _));
        Assert.False(info.RootElement.TryGetProperty("sid", out _)); // sid 只在令牌里，不经用户信息端点外泄
    }

    [Fact]
    public async Task 图形验证码经HTTP签发与校验_策略要求时缺失返回428_关闭时接口是404()
    {
        await using (var off = await AccountsHost.Start(postgres))
            Assert.Equal(HttpStatusCode.NotFound, (await off.NewBrowser().Json(HttpMethod.Get, "api/identity/captcha")).Status);
        await using var host = await AccountsHost.Start(postgres, Policy(captcha: CaptchaMode.Always));
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var browser = host.NewBrowser();
        var missing = await browser.Login(phone, Password);
        Assert.Equal(((HttpStatusCode)428, "CAPTCHA_REQUIRED"), (missing.Status, Code(missing.Body)));
        var challenge = await browser.Json(HttpMethod.Get, "api/identity/captcha");
        Assert.Equal(HttpStatusCode.OK, challenge.Status);
        var image = challenge.Body.GetProperty("image").GetString()!;
        Assert.StartsWith("data:image/svg+xml;base64,", image, StringComparison.Ordinal);
        var answer = Solve(Encoding.UTF8.GetString(Convert.FromBase64String(image["data:image/svg+xml;base64,".Length..])));
        var id = challenge.Body.GetProperty("id").GetString();
        var wrong = await browser.Login(phone, Password, captcha: new { id, answer = "AAAAA" });
        Assert.Equal((HttpStatusCode.BadRequest, "CAPTCHA_INVALID"), (wrong.Status, Code(wrong.Body)));
        // 答错也会消耗这个挑战，不能对同一挑战反复猜。
        Assert.Equal("CAPTCHA_INVALID", Code((await browser.Login(phone, Password, captcha: new { id, answer })).Body));
        var fresh = await browser.Json(HttpMethod.Get, "api/identity/captcha");
        var freshAnswer = Solve(Encoding.UTF8.GetString(Convert.FromBase64String(fresh.Body.GetProperty("image").GetString()!["data:image/svg+xml;base64,".Length..])));
        var ok = await browser.Login(phone, Password, captcha: new { id = fresh.Body.GetProperty("id").GetString(), answer = freshAnswer });
        Assert.Equal("authenticated", ok.Body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task 卸载身份组件后账号接口与协议面都返回404()
    {
        await using var host = await AccountsHost.Start(postgres);
        var browser = host.NewBrowser();
        Assert.Equal(HttpStatusCode.OK, (await browser.Json(HttpMethod.Get, "api/identity/csrf")).Status);
        await host.Loader.UnmountAsync("identity", ScopeKey.Global, Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Json(HttpMethod.Get, "api/identity/csrf")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.Json(HttpMethod.Get, "api/identity/me")).Status);
        using var discovery = await browser.Http.GetAsync(".well-known/openid-configuration", Ct);
        Assert.Equal(HttpStatusCode.NotFound, discovery.StatusCode);
    }
}
