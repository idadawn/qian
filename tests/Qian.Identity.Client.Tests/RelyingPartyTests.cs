using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Identity.Abstractions.Session;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Runtime;
using Qian.Testing.Containers;

namespace Qian.Identity.Client.Tests;

// 验收 1：对强制 PKCE 的真实 qian-identity 完成服务端换码并建立 qian.sid 会话；篡改 nonce、iss 或重放 state 的回调一律拒绝。
// 另覆盖刷新的单飞（发行方的 refresh 轮换零容忍复用）、登出的防伪校验与组件卸载，以及会话主体经网关签发器铸成 S2S 后下游接受。
public sealed class RelyingPartyTests(PostgresFixture postgres, RedisFixture redis) : IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>, IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly X509Certificate2 _tls = RelyingParty.CreateTlsCertificate();

    public void Dispose() => _tls.Dispose();

    [Fact]
    public async Task 强制PKCE换码_建立qian_sid会话_主体取自访问令牌的规范身份_重放state被拒()
    {
        var address = RelyingParty.ReserveAddress();
        await using var identity = await IdentityServerHost.Start(postgres, new Uri(address, "auth/callback"));
        await using var rp = await RelyingParty.Start(identity, redis.ConnectionString, address, _tls);
        var (browser, cookies) = RelyingParty.Browser(_tls);
        using var _ = browser;
        using (var anonymous = await browser.GetAsync(new Uri(address, "me"), Ct)) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await identity.SignInAsync(browser);
        var (callback, authorize) = await Authorize(browser, address, "/me?tab=1");
        // 授权请求：PKCE 只用 S256，state / nonce 都是随机值；verifier 不出 RP。受众由发行方按客户端登记签发，RP 只校验。
        Assert.Equal("S256", authorize["code_challenge_method"]); Assert.Equal(43, authorize["code_challenge"].ToString().Length);
        Assert.False(string.IsNullOrEmpty(authorize["state"])); Assert.False(string.IsNullOrEmpty(authorize["nonce"]));
        Assert.Equal(IdentityServerHost.ClientId, authorize["client_id"]); Assert.False(authorize.ContainsKey("code_verifier"));
        Assert.Equal(identity.Issuer, QueryHelpers.ParseQuery(callback.Query)["iss"]);
        using (var completed = await browser.GetAsync(callback, Ct))
        {
            Assert.True(completed.StatusCode == HttpStatusCode.Redirect, await completed.Content.ReadAsStringAsync(Ct));
            Assert.Equal("/me?tab=1", completed.Headers.Location!.OriginalString);
        }
        var session = cookies.GetCookies(address)[IdentityClientDefaults.SessionCookieName];
        Assert.NotNull(session); Assert.True(session.Secure); Assert.True(session.HttpOnly);
        Assert.Null(cookies.GetCookies(address)["kun.challenge"]);
        using (var me = await browser.GetAsync(new Uri(address, "me"), Ct))
        {
            var body = await me.Content.ReadAsStringAsync(Ct);
            Assert.True(me.StatusCode == HttpStatusCode.OK, body);
            var json = JsonDocument.Parse(body).RootElement;
            Assert.Equal(identity.Person.ToString("D"), json.GetProperty("subject").GetString());
            Assert.Equal(json.GetProperty("subject").GetString(), json.GetProperty("userId").GetString());
            Assert.Equal("User", json.GetProperty("kind").GetString());
            Assert.Equal(identity.Tenant.ToString("D"), json.GetProperty("tenant").GetString());
            Assert.Equal(identity.Membership.ToString("D"), json.GetProperty("membership").GetString());
            Assert.True(json.GetProperty("revision").GetInt64() >= 0);
            Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", json.GetProperty("session").GetString());
            Assert.NotEqual(session.Value, json.GetProperty("session").GetString());
        }
        // 同一回调再来一次：事务已消费（Cookie 已删、state 已登记使用），按失效的登录请求拒绝，不建立新会话。
        using (var replay = await browser.GetAsync(callback, Ct)) await AssertError(replay, HttpStatusCode.UnprocessableEntity, "AUTH_CONNECTOR_STATE_INVALID");
        // 日志不含会话标识、授权码或令牌。
        var code = QueryHelpers.ParseQuery(callback.Query)["code"].ToString();
        var leaked = rp.Logs.Messages.Where(message => message.Contains(session.Value, StringComparison.Ordinal) || message.Contains(code, StringComparison.Ordinal)).ToArray();
        Assert.True(leaked.Length == 0, string.Join("\n", leaked));
    }

    [Fact]
    public async Task 篡改nonce的id_token被拒绝_iss不一致与发行方错误也不建立会话()
    {
        var address = RelyingParty.ReserveAddress();
        await using var identity = await IdentityServerHost.Start(postgres, new Uri(address, "auth/callback"));
        var tamper = new NonceTamper(identity.Certificate);
        await using var rp = await RelyingParty.Start(identity, redis.ConnectionString, address, _tls,
            configure: services => services.AddHttpClient(IdentityClientDefaults.HttpClientName).AddHttpMessageHandler(() => tamper));
        var (browser, cookies) = RelyingParty.Browser(_tls);
        using var _ = browser;
        await identity.SignInAsync(browser);
        // 发行方用自己的私钥重新签名、只改 nonce：签名有效，nonce 与本次事务不符，仍须拒绝。
        tamper.Enabled = true;
        var (callback, _) = await Authorize(browser, address, "/me");
        using (var rejected = await browser.GetAsync(callback, Ct)) await AssertError(rejected, HttpStatusCode.Unauthorized, "OIDC_TOKEN_INVALID");
        Assert.True(tamper.Modified);
        Assert.Null(cookies.GetCookies(address)[IdentityClientDefaults.SessionCookieName]);
        tamper.Enabled = false;
        // RFC 9207：回调里的 iss 不是配置的发行方（混淆攻击）。
        var (forged, _) = await Authorize(browser, address, "/me");
        var query = QueryHelpers.ParseQuery(forged.Query).ToDictionary(item => item.Key, item => (string?)item.Value.ToString());
        query["iss"] = "https://attacker.example/";
        using (var mixed = await browser.GetAsync(QueryHelpers.AddQueryString(new Uri(address, "auth/callback").ToString(), query), Ct))
            await AssertError(mixed, HttpStatusCode.BadRequest, "OIDC_ISSUER_MISMATCH");
        // 发行方以 OAuth 错误结束：与连接器回调同一个 AUTH_CONNECTOR_DENIED；上游 error 只进日志（任意文本记为 server_error），不进文案。
        var (denied, _) = await Authorize(browser, address, "/me");
        var state = QueryHelpers.ParseQuery(denied.Query)["state"].ToString();
        using (var cancelled = await browser.GetAsync(QueryHelpers.AddQueryString(new Uri(address, "auth/callback").ToString(),
            new Dictionary<string, string?> { ["error"] = "access_denied", ["state"] = state, ["iss"] = identity.Issuer }), Ct))
        {
            var problem = await AssertError(cancelled, HttpStatusCode.UnprocessableEntity, "AUTH_CONNECTOR_DENIED");
            Assert.DoesNotContain("access_denied", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }
        Assert.Contains(rp.Logs.Messages, message => message.Contains("error=access_denied", StringComparison.Ordinal));
        Assert.Null(cookies.GetCookies(address)[IdentityClientDefaults.SessionCookieName]);
        // 正常的回调仍然成功：拒绝不影响后续登录。
        var (good, _) = await Authorize(browser, address, "/me");
        using (var completed = await browser.GetAsync(good, Ct)) Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
        Assert.NotNull(cookies.GetCookies(address)[IdentityClientDefaults.SessionCookieName]);
    }

    [Fact]
    public async Task 访问令牌临近到期_并发请求只用一次refresh_token_轮换后可继续刷新()
    {
        var address = RelyingParty.ReserveAddress();
        await using var identity = await IdentityServerHost.Start(postgres, new Uri(address, "auth/callback"));
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var counter = new RefreshCounter();
        await using var rp = await RelyingParty.Start(identity, redis.ConnectionString, address, _tls, clock,
            services => services.AddHttpClient(IdentityClientDefaults.HttpClientName).AddHttpMessageHandler(() => counter));
        var (browser, cookies) = RelyingParty.Browser(_tls);
        using var __ = browser;
        await SignIn(identity, browser, address);
        Assert.Equal(0, counter.Count);
        var store = rp.Services.GetRequiredService<ISessionStore>();
        var id = cookies.GetCookies(address)[IdentityClientDefaults.SessionCookieName]!.Value;
        var before = (await store.GetAsync(id, Ct))!;
        // 发行方的访问令牌 15 分钟；RP 时钟拨到到期前约 30 秒，落进「到期前 1 分钟刷新」窗口（发行方仍用真实时钟）。
        clock.Set(DateTimeOffset.UtcNow.AddMinutes(14.5));
        await Concurrently(browser, address, rp);
        Assert.Equal(1, counter.Count);
        var after = (await store.GetAsync(id, Ct))!;
        Assert.NotEqual(before.RefreshToken, after.RefreshToken); Assert.NotEqual(before.AccessToken, after.AccessToken);
        // 再次临近到期（改会话记录的到期时间）：第二次刷新必须用轮换后存回的 refresh token——旧的已作废，复用会让发行方吊销整族。
        await store.SetAsync(id, after with { ExpiresAt = clock.GetUtcNow().AddSeconds(30) }, TimeSpan.FromHours(1), Ct);
        await Concurrently(browser, address, rp);
        Assert.Equal(2, counter.Count);
        using var token = await browser.GetAsync(new Uri(address, "token"), Ct);
        Assert.True((await token.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("present").GetBoolean());
    }

    [Fact]
    public async Task 登出必须带防伪令牌_卸载组件后登录端点404且会话不再生效()
    {
        var address = RelyingParty.ReserveAddress();
        await using var identity = await IdentityServerHost.Start(postgres, new Uri(address, "auth/callback"));
        await using var rp = await RelyingParty.Start(identity, redis.ConnectionString, address, _tls);
        var (browser, cookies) = RelyingParty.Browser(_tls);
        using var _ = browser;
        await SignIn(identity, browser, address);
        using (var forged = await browser.PostAsync(new Uri(address, "auth/logout"), null, Ct)) Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        using (var still = await browser.GetAsync(new Uri(address, "me"), Ct)) Assert.Equal(HttpStatusCode.OK, still.StatusCode);
        using (var csrf = await browser.GetAsync(new Uri(address, "auth/csrf"), Ct))
        {
            Assert.True(csrf.Headers.CacheControl?.NoStore);
            var token = await csrf.Content.ReadFromJsonAsync<JsonElement>(Ct);
            using var logout = new HttpRequestMessage(HttpMethod.Post, new Uri(address, "auth/logout"));
            logout.Headers.Add(token.GetProperty("headerName").GetString()!, token.GetProperty("token").GetString());
            using var done = await browser.SendAsync(logout, Ct);
            Assert.Equal(HttpStatusCode.NoContent, done.StatusCode);
        }
        Assert.Null(cookies.GetCookies(address)[IdentityClientDefaults.SessionCookieName]);
        using (var after = await browser.GetAsync(new Uri(address, "me"), Ct)) Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        // 再登录一次，然后卸载组件：浏览器端点消失，旧会话 Cookie 不再被认可。
        await SignIn(identity, browser, address);
        await rp.Loader.UnmountAsync("identity", ScopeKey.Global, Ct);
        using (var login = await browser.GetAsync(new Uri(address, "auth/login"), Ct)) Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);
        using (var me = await browser.GetAsync(new Uri(address, "me"), Ct)) Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task 身份服务吊销会话后经后通道登出_RP的本地会话随之失效()
    {
        var address = RelyingParty.ReserveAddress();
        await using var identity = await IdentityServerHost.Start(postgres, new Uri(address, "auth/callback"), _tls);
        await using var rp = await RelyingParty.Start(identity, redis.ConnectionString, address, _tls);
        var (browser, _) = RelyingParty.Browser(_tls);
        using var __ = browser;
        await SignIn(identity, browser, address);
        using (var before = await browser.GetAsync(new Uri(address, "me"), Ct)) Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        await identity.DisablePersonAsync();
        // 投递是身份服务的后台工作（吊销即时写库，旁路随后投递 logout_token）；RP 按 issuer + sid 吊销本地会话。
        var revoked = false;
        for (var attempt = 0; attempt < 100 && !revoked; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
            using var me = await browser.GetAsync(new Uri(address, "me"), Ct);
            revoked = me.StatusCode == HttpStatusCode.Unauthorized;
        }
        Assert.True(revoked, string.Join("\n", rp.Logs.Messages));
    }

    [Fact]
    public async Task 会话的规范主体经网关签发器铸成S2S_下游按aud接受_上一跳透传给另一受众被拒()
    {
        var address = RelyingParty.ReserveAddress();
        await using var identity = await IdentityServerHost.Start(postgres, new Uri(address, "auth/callback"));
        using var gateway = new GatewayKeys();
        await using var rp = await RelyingParty.Start(identity, redis.ConnectionString, address, _tls,
            configure: services => services.AddSingleton(provider => gateway.Issuer(provider.GetRequiredService<IHostEnvironment>())));
        await using var platform = await Downstream.Start(gateway, "qian-platform");
        await using var other = await Downstream.Start(gateway, "qian-identity");
        var (browser, _) = RelyingParty.Browser(_tls);
        using var __ = browser;
        await SignIn(identity, browser, address);
        using var me = await browser.GetAsync(new Uri(address, "me"), Ct);
        var session = (await me.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("session").GetString();
        // 网关形态：RP 宿主把 qian.session 认证出的主体交给 Kun 网关签发器，为 qian-platform 签发。
        var token = await browser.GetStringAsync(new Uri(address, "gateway/qian-platform"), Ct);
        var accepted = await platform.WhoAmI(token);
        Assert.Equal(identity.Person.ToString("D"), accepted.GetProperty("subject").GetString());
        Assert.Equal("User", accepted.GetProperty("kind").GetString());
        Assert.Equal(identity.Tenant.ToString("D"), accepted.GetProperty("tenant").GetString());
        Assert.Equal(identity.Membership.ToString("D"), accepted.GetProperty("membership").GetString());
        Assert.Equal(session, accepted.GetProperty("session").GetString());
        Assert.True(accepted.GetProperty("revision").GetInt64() >= 0);
        // qian-platform 把收到的令牌原样转给 qian-identity（透传上一跳令牌）：受众不是后者，拒绝。
        Assert.Equal(HttpStatusCode.Unauthorized, await platform.Forward(token, other));
        await other.AssertRejected(token);
    }

    // 浏览器从 RP 发起登录，跟到发行方授权端点；返回发行方给 RP 回调的地址与 RP 发出的授权请求参数。
    private static async Task<(Uri Callback, Dictionary<string, Microsoft.Extensions.Primitives.StringValues> Authorize)> Authorize(HttpClient browser, Uri address, string returnTo)
    {
        using var login = await browser.GetAsync(new Uri(address, "auth/login?returnTo=" + Uri.EscapeDataString(returnTo)), Ct);
        Assert.True(login.StatusCode == HttpStatusCode.Redirect, await login.Content.ReadAsStringAsync(Ct));
        var authorize = login.Headers.Location!;
        using var redirect = await browser.GetAsync(authorize, Ct);
        Assert.True(redirect.StatusCode == HttpStatusCode.Redirect, await redirect.Content.ReadAsStringAsync(Ct));
        var callback = redirect.Headers.Location!;
        Assert.StartsWith(new Uri(address, "auth/callback").ToString(), callback.ToString(), StringComparison.Ordinal);
        return (callback, QueryHelpers.ParseQuery(authorize.Query));
    }

    private static async Task Concurrently(HttpClient browser, Uri address, RelyingParty rp)
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => browser.GetAsync(new Uri(address, "me"), Ct)));
        foreach (var response in responses)
            using (response) Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct) + "\n" + string.Join("\n", rp.Logs.Messages));
    }

    private static async Task SignIn(IdentityServerHost identity, HttpClient browser, Uri address)
    {
        await identity.SignInAsync(browser);
        var (callback, _) = await Authorize(browser, address, "/me");
        using var completed = await browser.GetAsync(callback, Ct);
        Assert.True(completed.StatusCode == HttpStatusCode.Redirect, await completed.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<JsonElement> AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == status, body);
        var problem = JsonDocument.Parse(body).RootElement;
        Assert.Equal(code, problem.GetProperty("code").GetString());
        return problem;
    }

    internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private long _ticks = start.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
        internal void Set(DateTimeOffset now) => Interlocked.Exchange(ref _ticks, now.UtcTicks);
    }

    // 统计 RP 发往令牌端点的 refresh_token 授权次数。
    private sealed class RefreshCounter : DelegatingHandler
    {
        private int _count;
        internal int Count => Volatile.Read(ref _count);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.Content is not null &&
                (await request.Content.ReadAsStringAsync(cancellationToken)).Contains("grant_type=refresh_token", StringComparison.Ordinal))
                Interlocked.Increment(ref _count);
            return await base.SendAsync(request, cancellationToken);
        }
    }

    // 只在开启时改写令牌端点响应里的 id_token：换掉 nonce 后用发行方的私钥重新签名（kid 不变），签名仍然有效。
    private sealed class NonceTamper(X509Certificate2 certificate) : DelegatingHandler
    {
        internal volatile bool Enabled;
        internal bool Modified { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (!Enabled || request.RequestUri!.AbsolutePath != "/connect/token" || !response.IsSuccessStatusCode) return response;
            var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!.AsObject();
            var original = new JsonWebToken(body["id_token"]!.GetValue<string>());
            var payload = JsonNode.Parse(Base64UrlEncoder.DecodeBytes(original.EncodedPayload))!.AsObject(); payload["nonce"] = "tampered-nonce";
            var credentials = new X509SigningCredentials(certificate, SecurityAlgorithms.RsaSha256);
            body["id_token"] = new JsonWebTokenHandler().CreateToken(payload.ToJsonString(), credentials);
            response.Content.Dispose(); response.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            Modified = true;
            return response;
        }
    }
}
