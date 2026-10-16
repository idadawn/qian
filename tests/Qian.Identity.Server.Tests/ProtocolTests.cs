using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using Kun.Identity.Configuration;
using Kun.Identity.Implementations;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Runtime;
using Qian.Testing.Containers;
using Qian.Testing.Identity;

namespace Qian.Identity.Server.Tests;

public sealed partial class ProtocolTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task 真实OpenIddict与已发布Kun完成PKCE_nonce校验_刷新轮换复用整族吊销_无权限声明()
    {
        await using var host = await Start();
        using var discovery = JsonDocument.Parse(await host.Browser.GetStringAsync(".well-known/openid-configuration", Ct));
        Assert.Equal(host.Issuer, discovery.RootElement.GetProperty("issuer").GetString());
        using var client = host.Kun("web");
        var code = await host.Code();
        var tokens = await client.ExchangeCodeAsync(new(code.Code, host.Redirect, code.Verifier, code.Nonce), Ct);
        Assert.Equal(host.Person.ToString(), tokens.ValidatedPrincipal!.FindFirst("sub")!.Value);
        Assert.Equal(host.Tenant.ToString(), tokens.ValidatedPrincipal.FindFirst("tenant_id")!.Value);
        Assert.InRange(tokens.ExpiresIn, 895, 900);
        var access = new JsonWebToken(tokens.AccessToken);
        Assert.DoesNotContain(access.Claims, claim => claim.Type is "permissions" or "permission" or "roles" or "role");
        Assert.Equal("user", access.GetClaim("principal_kind").Value);
        var refreshed = await client.RefreshAsync(tokens.RefreshToken!, host.Person.ToString(), Ct);
        Assert.NotEqual(tokens.RefreshToken, refreshed.RefreshToken);
        using (var replay = await host.Grant("web", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = tokens.RefreshToken! }))
            Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using (var family = await host.Grant("web", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refreshed.RefreshToken! }))
            Assert.Equal(HttpStatusCode.BadRequest, family.StatusCode);
        var nonceCode = await host.Code();
        using var transport = new NonceTamperFactory(host.Certificate);
        using var tampered = new KunOidcClient(new KunOidcClientOptions { Name = "nonce-negative", Authority = host.Issuer, ClientId = "web", ClientSecret = host.Secret, RequireHttpsMetadata = false }, transport);
        await Assert.ThrowsAsync<SecurityTokenValidationException>(() => tampered.ExchangeCodeAsync(new(nonceCode.Code, host.Redirect, nonceCode.Verifier, nonceCode.Nonce), Ct));
        Assert.True(transport.Modified);
        var wrongVerifier = await host.Code();
        await Assert.ThrowsAsync<HttpRequestException>(() => client.ExchangeCodeAsync(new(wrongVerifier.Code, host.Redirect, new string('a', 64), wrongVerifier.Nonce), Ct));
        using var noPkce = await host.Browser.GetAsync(QueryHelpers.AddQueryString("connect/authorize", new Dictionary<string, string?>
        { ["client_id"] = "web", ["response_type"] = "code", ["scope"] = "openid", ["redirect_uri"] = host.Redirect, ["nonce"] = "required" }), Ct);
        Assert.DoesNotContain("code=", noPkce.Headers.Location?.Query ?? "", StringComparison.Ordinal);
        Assert.True(noPkce.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Redirect);
        TestContext.Current.TestOutputHelper?.WriteLine($"Kun {typeof(KunOidcClient).Assembly.GetName().Version}; issuer={host.Issuer}; client=web (confidential, PKCE S256); private test Cookie represents the upstream authenticated account, not a password login implementation.");
    }

    [Fact]
    public async Task 机器令牌与受众白名单_真实撤销和内省_卸载组件后协议面不可用()
    {
        await using var host = await Start();
        using var machine = host.Kun("machine");
        var tokens = await machine.ClientCredentialsAsync("qian.api", Ct);
        var access = new JsonWebToken(tokens.AccessToken);
        Assert.Equal("machine", access.Subject); Assert.Equal("service", access.GetClaim("principal_kind").Value);
        using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(access.EncodedPayload));
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("tenant_id").ValueKind);
        Assert.Equal(["https://api.qian.test"], access.Audiences); Assert.False(access.TryGetClaim("membership_id", out _));
        using var before = await host.ProtocolPost("connect/introspection", "machine", new() { ["token"] = tokens.AccessToken });
        Assert.True(JsonDocument.Parse(await before.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("active").GetBoolean());
        using var revoked = await host.ProtocolPost("connect/revocation", "machine", new() { ["token"] = tokens.AccessToken });
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        using var after = await host.ProtocolPost("connect/introspection", "machine", new() { ["token"] = tokens.AccessToken });
        Assert.False(JsonDocument.Parse(await after.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("active").GetBoolean());
        using var forbidden = await host.Grant("machine", new() { ["grant_type"] = "client_credentials", ["scope"] = "qian.api", ["resource"] = "https://untrusted.test" });
        Assert.Equal(HttpStatusCode.BadRequest, forbidden.StatusCode);
        await host.Loader.UnmountAsync("identity", ScopeKey.Global, Ct);
        using var gone = await host.Browser.GetAsync(".well-known/openid-configuration", Ct); Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task 跨宿主刷新复用吊销_同一数据库即时拒绝另一个副本的旧族()
    {
        await using var first = await Start(); await using var second = await Start(first);
        using var client = first.Kun("web"); var code = await first.Code();
        var tokens = await client.ExchangeCodeAsync(new(code.Code, first.Redirect, code.Verifier, code.Nonce), Ct);
        using var refresh = await second.Grant("web", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = tokens.RefreshToken! });
        Assert.True(refresh.IsSuccessStatusCode, await refresh.Content.ReadAsStringAsync(Ct));
        using var rotated = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync(Ct));
        using var replay = await first.Grant("web", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = tokens.RefreshToken! });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using var family = await second.Grant("web", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = rotated.RootElement.GetProperty("refresh_token").GetString()! });
        Assert.Equal(HttpStatusCode.BadRequest, family.StatusCode);
    }

    [Fact]
    public async Task 登出必须CSRF确认_持久后通道通知经Kun验签_旧刷新被拒_日志无会话或密钥原文()
    {
        await using var host = await Start(); using var client = host.Kun("web"); var code = await host.Code();
        var tokens = await client.ExchangeCodeAsync(new(code.Code, host.Redirect, code.Verifier, code.Nonce), Ct);
        host.Backchannel.Failures = 1;
        var logout = new Dictionary<string, string> { ["id_token_hint"] = tokens.IdToken!, ["post_logout_redirect_uri"] = host.Issuer + "signed-out" };
        using (var rejected = await host.Browser.PostAsync("connect/logout", new FormUrlEncodedContent(logout), Ct))
            Assert.True(rejected.StatusCode == HttpStatusCode.BadRequest, (await rejected.Content.ReadAsStringAsync(Ct)).Split("HEADERS")[0]);
        var url = QueryHelpers.AddQueryString("connect/logout", logout.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)));
        using var confirmation = await host.Browser.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var html = await confirmation.Content.ReadAsStringAsync(Ct);
        foreach (Match field in HiddenInputs().Matches(html)) logout[WebUtility.HtmlDecode(field.Groups[1].Value)] = WebUtility.HtmlDecode(field.Groups[2].Value);
        using var accepted = await host.Browser.PostAsync("connect/logout", new FormUrlEncodedContent(logout), Ct);
        Assert.True(accepted.StatusCode == HttpStatusCode.Redirect, await accepted.Content.ReadAsStringAsync(Ct));
        using var refresh = await host.Grant("web", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = tokens.RefreshToken! });
        Assert.Equal(HttpStatusCode.BadRequest, refresh.StatusCode);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (host.LogoutTokens.IsEmpty && DateTimeOffset.UtcNow < deadline) await Task.Delay(25, Ct);
        Assert.False(host.LogoutTokens.IsEmpty, string.Join("\n", host.Logs.Messages.Where(message => message.Contains("failed", StringComparison.OrdinalIgnoreCase) || message.Contains("Exception", StringComparison.Ordinal)).TakeLast(10)));
        Assert.True(host.Backchannel.Attempts >= 2);
        var notification = Assert.Single(host.LogoutTokens);
        var verified = await client.ValidateLogoutTokenAsync(notification, Ct);
        Assert.Equal(host.Person.ToString(), verified.Subject); Assert.Equal(host.SessionId.ToString(), verified.SessionId);
        Assert.All(host.Logs.Messages, message =>
        {
            Assert.DoesNotContain(host.SessionId.ToString(), message, StringComparison.Ordinal);
            Assert.DoesNotContain(host.Secret, message, StringComparison.Ordinal);
            Assert.DoesNotContain(tokens.AccessToken, message, StringComparison.Ordinal);
            Assert.DoesNotContain(tokens.IdToken!, message, StringComparison.Ordinal);
            Assert.DoesNotContain(tokens.RefreshToken!, message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task 公开客户端也强制PKCE_重启轮换保留旧钥并用新钥签发_JWKS不泄露私钥()
    {
        await using var oldHost = await Start(); using var client = oldHost.Kun("public"); var code = await oldHost.Code("public");
        var tokens = await client.ExchangeCodeAsync(new(code.Code, oldHost.Issuer + "public-callback", code.Verifier, code.Nonce), Ct);
        var oldAccess = new JsonWebToken(tokens.AccessToken);
        await oldHost.StopAsync();
        await using var rotated = await IdentityProtocolHost.Start(postgres, oldHost, rotateSigningKey: true, reuseAddress: true);
        using var metadata = JsonDocument.Parse(await rotated.Browser.GetStringAsync(".well-known/openid-configuration", Ct));
        var keys = new JsonWebKeySet(await rotated.Browser.GetStringAsync(metadata.RootElement.GetProperty("jwks_uri").GetString()!, Ct));
        Assert.Equal(2, keys.Keys.Count); Assert.All(keys.Keys, key => { Assert.Null(key.D); Assert.Null(key.P); Assert.Null(key.Q); });
        var refreshed = await client.RefreshAsync(tokens.RefreshToken!, oldHost.Person.ToString(), Ct);
        Assert.NotEqual(oldAccess.Kid, new JsonWebToken(refreshed.AccessToken).Kid);
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(tokens.AccessToken, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = oldHost.Issuer, ValidateAudience = true, ValidAudience = "https://api.qian.test",
            ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
            ValidateIssuerSigningKey = true, IssuerSigningKeys = keys.GetSigningKeys(), ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        });
        Assert.True(validation.IsValid, validation.Exception?.Message);
        using var noVerifier = await rotated.Grant("public", new() { ["grant_type"] = "authorization_code", ["code"] = (await rotated.Code("public")).Code, ["redirect_uri"] = rotated.Issuer + "public-callback" });
        Assert.Equal(HttpStatusCode.BadRequest, noVerifier.StatusCode);
    }

    // 真实 token 响应返回后只改 id_token 的 nonce，并用测试发行方同一密钥签名；保证拒绝来自 nonce 关联而非签名损坏。
    private sealed class NonceTamperFactory(X509Certificate2 certificate) : IHttpClientFactory, IDisposable
    {
        private readonly List<HttpClient> _clients = [];
        internal bool Modified { get; private set; }
        public HttpClient CreateClient(string name)
        {
            var client = new HttpClient(new Tamper(certificate, () => Modified = true)); _clients.Add(client); return client;
        }
        public void Dispose() { foreach (var client in _clients) client.Dispose(); }
        private sealed class Tamper(X509Certificate2 certificate, Action changed) : DelegatingHandler(new HttpClientHandler())
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (request.RequestUri!.AbsolutePath == "/connect/token" && response.IsSuccessStatusCode)
                {
                    var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!.AsObject();
                    var token = new JsonWebToken(body["id_token"]!.GetValue<string>());
                    var payload = JsonNode.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload))!.AsObject(); payload["nonce"] = "tampered-nonce";
                    body["id_token"] = new JsonWebTokenHandler().CreateToken(payload.ToJsonString(), new X509SigningCredentials(certificate, SecurityAlgorithms.RsaSha256));
                    response.Content.Dispose(); response.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"); changed();
                }
                return response;
            }
        }
    }

    [GeneratedRegex("<input type=\"hidden\" name=\"([^\"]+)\" value=\"([^\"]*)\">")]
    private static partial Regex HiddenInputs();

    private Task<IdentityProtocolHost.Running> Start(IdentityProtocolHost.Running? primary = null) => IdentityProtocolHost.Start(postgres, primary);
}
