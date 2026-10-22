using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Json.Schema;
using Kun.Identity.Abstractions.Session;
using Kun.Gateway.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Runtime;
using Qian.Identity.Client;
using Qian.Identity.Client.Tests;
using Qian.Testing;
using Qian.Testing.Containers;
using Qian.Testing.Gateway;
using Qian.Testing.Identity;

namespace Qian.Gateway.Tests;

public sealed class GatewayBoundaryTests(PostgresFixture postgres, RedisFixture redis) : IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Issuer = "https://gateway.qian.test/s2s";

    [Fact]
    public async Task 真实账号PKCE登录后的Cookie换签保持租户与会话_错误Bearer不降级到Cookie()
    {
        using var tls = GatewayTestHost.Certificate(); using var key = RSA.Create(2048); using var reservation = LoopbackPort.Reserve();
        var address = new Uri($"https://localhost:{reservation.Port}/");
        await using var identity = await IdentityServerHost.Start(postgres, new Uri(address, "auth/callback"));
        await using var downstream = await Downstream.Start();
        await using var gateway = await GatewayTestHost.Start(reservation, tls, identity.Issuer, IdentityServerHost.ClientId, IdentityServerHost.Resource, identity.Secret,
            redis.ConnectionString, Issuer, [new("platform", "/api/{**rest}", "qian-platform")], [new("qian-platform", downstream.Address)], key);
        downstream.Jwks = new Uri(gateway.HttpAddress, ".well-known/jwks.json");
        var (client, cookies) = GatewayTestHost.Browser(tls); using var browser = client;
        await identity.SignInAsync(browser); await gateway.CompleteLogin(browser);
        var session = cookies.GetCookies(address)[IdentityClientDefaults.SessionCookieName]; Assert.NotNull(session);
        using (var response = await browser.GetAsync(new Uri(address, "api/protected"), Ct))
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        Assert.True(downstream.Requests.TryDequeue(out var received)); Assert.Empty(received.Cookie);
        var token = new JsonWebToken(received.Token);
        Assert.Equal(identity.Tenant.ToString("D"), token.GetClaim("tenant_id").Value);
        Assert.Equal(identity.Membership.ToString("D"), token.GetClaim("membership_id").Value);
        Assert.Equal(identity.Person.ToString("D"), token.Subject);
        Assert.NotEqual(session.Value, token.GetClaim("session_id").Value);
        var stored = await gateway.Services.GetRequiredService<ISessionStore>().GetAsync(session.Value, Ct);
        Assert.NotNull(stored); Assert.NotNull(stored.AccessToken);
        var (bearerClient, _) = GatewayTestHost.Browser(tls); using var bearer = bearerClient;
        using (var userRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(address, "api/protected")))
        {
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", stored.AccessToken);
            using var userResponse = await bearer.SendAsync(userRequest, Ct);
            Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
        }
        Assert.True(downstream.Requests.TryDequeue(out var bearerReceived));
        Assert.Equal(token.GetClaim("session_id").Value, new JsonWebToken(bearerReceived.Token).GetClaim("session_id").Value);
        using var invalid = new HttpRequestMessage(HttpMethod.Get, new Uri(address, "api/protected"));
        invalid.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        using var denied = await browser.SendAsync(invalid, Ct); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Empty(downstream.Requests);
    }

    [Fact]
    public async Task 真实机器凭据经网关按目标cluster换签_载荷通过冻结Schema_入站S2S与错误Bearer拒绝()
    {
        await using var identity = await IdentityProtocolHost.Start(postgres);
        using var tls = GatewayTestHost.Certificate(); using var key = RSA.Create(2048);
        using var reservation = LoopbackPort.Reserve();
        await using var downstream = await Downstream.Start();
        await using var gateway = await GatewayTestHost.Start(reservation, tls, identity.Issuer, "web", "https://api.qian.test", identity.Secret,
            redis.ConnectionString, Issuer, [new("platform", "/api/{**rest}", "qian-platform"), new("other", "/other/{**rest}", "qian-other")],
            [new("qian-platform", downstream.Address), new("qian-other", downstream.Address)], key);
        downstream.Jwks = new Uri(gateway.HttpAddress, ".well-known/jwks.json");
        var (client, _) = GatewayTestHost.Browser(tls); using var browser = client; browser.BaseAddress = gateway.Address;
        using (var anonymous = await browser.GetAsync("api/protected", Ct)) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var machine = identity.Kun("machine"); var access = (await machine.ClientCredentialsAsync("qian.api", Ct)).AccessToken;
        var first = await Send("api/protected", access); var other = await Send("other/protected", access);
        Assert.NotEqual(access, first); Assert.NotEqual(first, other);
        var schema = JsonSchema.FromText(await File.ReadAllTextAsync(Path.Combine(Root(), "contracts/v0/s2s-claims.schema.json"), Ct),
            new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        foreach (var (token, audience) in new[] { (first, "qian-platform"), (other, "qian-other") })
        {
            var jwt = new JsonWebToken(token); using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(jwt.EncodedPayload));
            var json = payload.RootElement;
            Assert.True(schema.Evaluate(json).IsValid);
            Assert.Equal(new[] { audience }, json.GetProperty("aud").EnumerateArray().Select(item => item.GetString()));
            Assert.Equal(60, json.GetProperty("exp").GetInt64() - json.GetProperty("iat").GetInt64());
            Assert.Equal("service", json.GetProperty("principal_kind").GetString());
            Assert.Equal(JsonValueKind.Null, json.GetProperty("tenant_id").ValueKind);
            Assert.Equal(json.GetProperty("jti").GetString(), json.GetProperty("session_id").GetString());
        }
        using (var previousHop = new HttpRequestMessage(HttpMethod.Get, "api/protected"))
        {
            previousHop.Headers.Authorization = new AuthenticationHeaderValue("Bearer", first);
            using var rejected = await browser.SendAsync(previousHop, Ct); Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }
        await gateway.Loader.UnmountAsync("gateway", ScopeKey.Global, Ct);
        using var unmounted = await browser.GetAsync("api/protected", Ct); Assert.Equal(HttpStatusCode.NotFound, unmounted.StatusCode);
        using var notReady = await browser.GetAsync("readyz", Ct); Assert.Equal(HttpStatusCode.ServiceUnavailable, notReady.StatusCode);
        using var detail = JsonDocument.Parse(await notReady.Content.ReadAsStringAsync(Ct));
        Assert.Equal("UNAVAILABLE", detail.RootElement.GetProperty("code").GetString());

        async Task<string> Send(string path, string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("Cookie", "qian.sid=must-not-reach-downstream");
            using var response = await browser.SendAsync(request, Ct);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            Assert.True(downstream.Requests.TryDequeue(out var received));
            Assert.Empty(received.Cookie);
            return received.Token;
        }
    }

    [Fact]
    public async Task 非可信来源伪造转发头不改变客户端IP与限流分区()
    {
        await using var identity = await IdentityProtocolHost.Start(postgres);
        using var tls = GatewayTestHost.Certificate(); using var key = RSA.Create(2048); using var reservation = LoopbackPort.Reserve();
        await using var downstream = await Downstream.Start();
        await using var gateway = await GatewayTestHost.Start(reservation, tls, identity.Issuer, "web", "https://api.qian.test", identity.Secret,
            redis.ConnectionString, Issuer, [new("platform", "/api/{**rest}", "qian-platform")], [new("qian-platform", downstream.Address)], key,
            ["192.0.2.0/24"]);
        downstream.Jwks = new Uri(gateway.HttpAddress, ".well-known/jwks.json");
        var (client, _) = GatewayTestHost.Browser(tls); using var browser = client; browser.BaseAddress = gateway.Address;
        using var machine = identity.Kun("machine"); var access = (await machine.ClientCredentialsAsync("qian.api", Ct)).AccessToken;
        for (var index = 0; index < 101; index++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/protected");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", $"198.51.100.{index % 250 + 1}");
            using var response = await browser.SendAsync(request, Ct);
            Assert.Equal(index < 100 ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests, response.StatusCode);
            if (index == 100) Assert.NotNull(response.Headers.RetryAfter);
        }
        Assert.Equal(100, downstream.Requests.Count);
        var actualPeer = Assert.Single(downstream.Requests.Select(item => item.ForwardedFor).Distinct(StringComparer.Ordinal));
        Assert.True(IPAddress.TryParse(actualPeer, out var peer) && IPAddress.IsLoopback(peer));
        var resolver = gateway.Services.GetRequiredService<IRateLimitPartitionResolver>();
        var context = new DefaultHttpContext(); context.Connection.RemoteIpAddress = IPAddress.IPv6Loopback;
        Assert.Equal("::1", resolver.ResolvePartitionKey(context));
    }

    [Fact]
    public async Task 十分钟双钥热轮换在途请求零失败_窗口结束旧kid拒绝()
    {
        await using var identity = await IdentityProtocolHost.Start(postgres);
        using var tls = GatewayTestHost.Certificate(); using var oldKey = RSA.Create(2048); using var newKey = RSA.Create(2048);
        using var reservation = LoopbackPort.Reserve(); var clock = new RingClock();
        await using var downstream = await Downstream.Start();
        await using var gateway = await GatewayTestHost.Start(reservation, tls, identity.Issuer, "web", "https://api.qian.test", identity.Secret,
            redis.ConnectionString, Issuer, [new("platform", "/api/{**rest}", "qian-platform")], [new("qian-platform", downstream.Address)], oldKey, clock: clock);
        downstream.Jwks = new Uri(gateway.HttpAddress, ".well-known/jwks.json");
        var (client, _) = GatewayTestHost.Browser(tls); using var browser = client; browser.BaseAddress = gateway.Address;
        using var machine = identity.Kun("machine"); var access = (await machine.ClientCredentialsAsync("qian.api", Ct)).AccessToken;
        await Request(); Assert.True(downstream.Requests.TryDequeue(out var first)); var oldKid = new JsonWebToken(first.Token).Kid;
        downstream.Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inFlight = Enumerable.Range(0, 24).Select(_ => Request()).ToArray();
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (downstream.Requests.Count < 24 && DateTime.UtcNow < deadline) await Task.Delay(10, Ct);
            Assert.Equal(24, downstream.Requests.Count); Assert.All(inFlight, request => Assert.False(request.IsCompleted));
            var replacement = gateway.SigningPath + ".next";
            await GatewayTestHost.WritePrivate(replacement, newKey.ExportRSAPrivateKeyPem()); File.Move(replacement, gateway.SigningPath, overwrite: true);
            clock.Advance(TimeSpan.FromSeconds(2));
        }
        finally { downstream.Hold.TrySetResult(); downstream.Hold = null; }
        await Task.WhenAll(inFlight); await Request();
        var overlapping = await Keys(); Assert.Equal(2, overlapping.Keys.Count); Assert.Contains(overlapping.Keys, item => item.Kid == oldKid);
        Assert.True(await Valid(first.Token, overlapping));
        Assert.Contains(downstream.Requests.Select(item => new JsonWebToken(item.Token).Kid), kid => kid != oldKid);
        clock.Advance(TimeSpan.FromMinutes(10));
        var current = await Keys(); Assert.Single(current.Keys); Assert.DoesNotContain(current.Keys, item => item.Kid == oldKid);
        // 只推进签名环时钟，JWT 本身仍在真实 60 秒寿命内；因此拒绝证明的是旧 kid 移除，而不是令牌过期。
        Assert.False(await Valid(first.Token, current));
        using (var oldRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(downstream.Address), "api/protected")))
        {
            oldRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", first.Token);
            using var direct = new HttpClient(); using var denied = await direct.SendAsync(oldRequest, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        await Request();

        async Task Request()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/protected"); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            using var response = await browser.SendAsync(request, Ct); Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        }
        async Task<JsonWebKeySet> Keys() => new(await browser.GetStringAsync(".well-known/jwks.json", Ct));
    }

    private static async Task<bool> Valid(string token, JsonWebKeySet keys, string audience = "qian-platform") =>
        (await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = Issuer, ValidateAudience = true, ValidAudience = audience,
            ValidateIssuerSigningKey = true, IssuerSigningKeys = keys.GetSigningKeys(), ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ClockSkew = TimeSpan.Zero
        })).IsValid;

    private static string Root()
    {
        var path = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(path, "Qian.slnx"))) path = Directory.GetParent(path)!.FullName;
        return path;
    }
    private sealed class RingClock : TimeProvider
    {
        private long _ticks = DateTimeOffset.UtcNow.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
        internal void Advance(TimeSpan duration) => Interlocked.Add(ref _ticks, duration.Ticks);
    }

    private sealed class Downstream(WebApplication app) : IAsyncDisposable
    {
        internal string Address => app.Urls.Single();
        internal Uri? Jwks { get; set; }
        internal TaskCompletionSource? Hold { get; set; }
        internal ConcurrentQueue<(string Token, string Cookie, string ForwardedFor)> Requests { get; } = new();
        internal static async Task<Downstream> Start()
        {
            var builder = WebApplication.CreateSlimBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            var app = builder.Build(); var host = new Downstream(app);
            app.MapGet("/{**path}", async (HttpContext http) =>
            {
                var header = http.Request.Headers.Authorization.ToString();
                if (!header.StartsWith("Bearer ", StringComparison.Ordinal) || host.Jwks is null) return Results.Unauthorized();
                using var client = new HttpClient(); var keys = new JsonWebKeySet(await client.GetStringAsync(host.Jwks, http.RequestAborted));
                var token = header[7..]; var audience = http.Request.Path.StartsWithSegments("/other") ? "qian-other" : "qian-platform";
                if (!await Valid(token, keys, audience)) return Results.Unauthorized();
                host.Requests.Enqueue((token, http.Request.Headers.Cookie.ToString(), http.Request.Headers["X-Forwarded-For"].ToString()));
                if (host.Hold is { } hold) await hold.Task.WaitAsync(http.RequestAborted);
                return Results.Ok();
            });
            await app.StartAsync(Ct); return host;
        }
        public ValueTask DisposeAsync() => app.DisposeAsync();
    }
}
