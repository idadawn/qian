using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Json.Schema;
using Kun.Identity.Abstractions.Session;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Qian.Audit;
using Qian.Config;
using Qian.Identity.Client;
using Qian.Identity.Client.Tests;
using Qian.Testing;
using Qian.Testing.Containers;
using Qian.Testing.NativeAudit;
using StackExchange.Redis;

namespace Qian.Gateway.Host.AotTests;

[Collection(GatewayHostGroup.Name)]
public sealed class GatewayHostTests(PostgresFixture postgres, RedisFixture redis, RealApolloFixture apollo)
    : IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>, IClassFixture<RealApolloFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string S2sIssuer = "https://gateway.qian.native/s2s";
    [Fact]
    public async Task 实际生产宿主运行完整依赖_Cookie与Bearer换S2S_真实Apollo与密钥访问入审计链()
    {
        var directory = Directory.CreateTempSubdirectory("qian-gateway-host-").FullName;
        try
        {
            var database = await NativeAuditDatabase.Create(postgres, Ct);
            string[] databaseSecrets = [new NpgsqlConnectionStringBuilder(database.TenantConnection).Password!, new NpgsqlConnectionStringBuilder(database.PlatformConnection).Password!];
            using var tls = GatewayProcess.Certificate(); using var signing = RSA.Create(2048); using var reservation = LoopbackPort.Reserve();
            var address = new Uri($"https://localhost:{reservation.Port}/");
            await using var identity = await IdentityServerHost.Start(postgres, new Uri(address, "auth/callback"));
            var (browserClient, cookies) = GatewayProcess.Browser(tls); using var browser = browserClient;
            var (backchannel, _) = GatewayProcess.Browser(tls); using var jwksClient = backchannel;
            await using var downstream = await Downstream.Start(new Uri(address, ".well-known/jwks.json"), jwksClient);
            var appId = "gateway-" + Guid.NewGuid().ToString("N")[..12];
            await apollo.CreateAppAsync(appId, Ct);
            // 初始发行地址只来自真实 Apollo 发布；JWT 的 iss 断言能证明生产配置源被使用。
            await apollo.PublishAsync(appId, "Gateway:Issuer", S2sIssuer, Ct);
            var secretsRoot = Path.Combine(directory, "secrets"); Directory.CreateDirectory(Path.Combine(secretsRoot, "qian", "identity"));
            var auditFile = Path.Combine(secretsRoot, "audit"); var platformFile = Path.Combine(secretsRoot, "platform-audit");
            var redisFile = Path.Combine(secretsRoot, "redis"); var tlsFile = Path.Combine(secretsRoot, "tls.pfx"); var signingFile = Path.Combine(secretsRoot, "signing.pem");
            await GatewayProcess.PrivateFile(auditFile, database.TenantConnection, Ct);
            await GatewayProcess.PrivateFile(platformFile, database.PlatformConnection, Ct);
            await GatewayProcess.PrivateFile(redisFile, redis.ConnectionString, Ct);
            await GatewayProcess.PrivateFile(tlsFile, tls.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pkcs12), Ct);
            await GatewayProcess.PrivateFile(signingFile, signing.ExportRSAPrivateKeyPem(), Ct);
            await GatewayProcess.PrivateFile(Path.Combine(secretsRoot, "qian", "identity", "client-secret"), identity.Secret, Ct);
            var configuration = new
            {
                Urls = address.AbsoluteUri,
                Kestrel = new { Certificates = new { Default = new { Path = tlsFile } } },
                Apollo = new { AppId = appId, Env = "UAT", ConfigServer = new[] { apollo.ConfigServiceUri.AbsoluteUri }, Namespaces = new[] { "application" }, CacheDir = Path.Combine(directory, "apollo-cache") },
                Gateway = new
                {
                    IdentityIssuer = identity.Issuer, IdentityAudience = IdentityServerHost.Resource,
                    Issuer = "https://bootstrap.invalid/s2s", SigningKeyPath = signingFile, PublicKeyPath = Path.Combine(secretsRoot, "public.pem"),
                    TrustedProxyNetworks = Array.Empty<string>(), Routes = new[] { new { Id = "platform", Path = "/api/{**rest}", Cluster = "qian-platform", RateLimiterPolicy = "tenant" } },
                    Clusters = new[] { new { Id = "qian-platform", Address = downstream.Address } }
                },
                Identity = new { ClientId = IdentityServerHost.ClientId, RedirectUri = new Uri(address, "auth/callback").AbsoluteUri,
                    SecretMountRoot = secretsRoot, DataProtectionKeysDirectory = Path.Combine(directory, "data-protection") },
                ConnectionFiles = new { Audit = auditFile, PlatformAudit = platformFile, Redis = redisFile },
                Audit = new { TenantRole = database.Roles.TenantLogin, PlatformRole = database.Roles.PlatformLogin, FunctionOwner = database.Roles.FunctionOwner }
            };
            await File.WriteAllTextAsync(Path.Combine(directory, "appsettings.json"), JsonSerializer.Serialize(configuration), Ct);
            reservation.Dispose();
            await using var gateway = GatewayProcess.Start(directory, [.. databaseSecrets, identity.Secret, database.TenantConnection, database.PlatformConnection, redis.ConnectionString]);
            await gateway.WaitReady(browser, address, Ct);
            using (var anonymous = await browser.GetAsync(new Uri(address, "api/native-acceptance"), Ct))
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            Assert.Empty(downstream.Requests);
            await identity.SignInAsync(browser);
            using (var begin = await browser.GetAsync(new Uri(address, "auth/login?returnTo=%2F"), Ct))
            {
                Assert.Equal(HttpStatusCode.Redirect, begin.StatusCode);
                using var authorize = await browser.GetAsync(begin.Headers.Location, Ct); Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
                Assert.Equal(address.Host, authorize.Headers.Location!.Host);
                using var callback = await browser.GetAsync(authorize.Headers.Location, Ct);
                Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode); Assert.Equal("/", callback.Headers.Location!.OriginalString);
            }
            var sessionCookie = cookies.GetCookies(address)[IdentityClientDefaults.SessionCookieName]; Assert.NotNull(sessionCookie);
            using (var response = await browser.GetAsync(new Uri(address, "api/native-acceptance"), Ct)) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(downstream.Requests.TryDequeue(out var first)); Assert.True(first.Cookie.Length == 0, "下游不能收到 Cookie。");
            var jwt = new JsonWebToken(first.Token); Assert.Equal(S2sIssuer, jwt.Issuer);
            Assert.Equal(identity.Tenant.ToString("D"), jwt.GetClaim("tenant_id").Value);
            Assert.Equal(identity.Person.ToString("D"), jwt.Subject);
            Assert.Equal("qian-platform", Assert.Single(jwt.Audiences)); Assert.Equal(60, (jwt.ValidTo - jwt.ValidFrom).TotalSeconds);
            Assert.False(string.Equals(sessionCookie.Value, jwt.GetClaim("session_id").Value, StringComparison.Ordinal), "S2S 不能泄漏不透明会话 Cookie。");
            using (var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(jwt.EncodedPayload)))
            {
                var schema = JsonSchema.FromText(await File.ReadAllTextAsync(Path.Combine(Root(), "contracts/v0/s2s-claims.schema.json"), Ct), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
                Assert.True(schema.Evaluate(payload.RootElement).IsValid);
            }
            await using var sessionReader = await SessionReader(identity, address);
            var session = await sessionReader.GetRequiredService<ISessionStore>().GetAsync(sessionCookie.Value, Ct);
            Assert.NotNull(session); Assert.NotNull(session.AccessToken);
            var (bearerClient, _) = GatewayProcess.Browser(tls); using var bearer = bearerClient;
            using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(address, "api/native-acceptance")))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.99");
                using var response = await bearer.SendAsync(request, Ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
            Assert.True(downstream.Requests.TryDequeue(out var second)); Assert.True(second.Cookie.Length == 0, "下游不能收到 Cookie。");
            Assert.False(string.Equals(session.AccessToken, second.Token, StringComparison.Ordinal), "禁止透传入站访问令牌。");
            Assert.Equal(jwt.GetClaim("session_id").Value, new JsonWebToken(second.Token).GetClaim("session_id").Value);
            Assert.True(IPAddress.TryParse(second.ForwardedFor, out var peer) && IPAddress.IsLoopback(peer));
            using (var invalid = new HttpRequestMessage(HttpMethod.Get, new Uri(address, "api/native-acceptance")))
            {
                invalid.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
                using var response = await browser.SendAsync(invalid, Ct); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
            Assert.Empty(downstream.Requests);
            await using var audit = await NativeAuditReader.Start(database, Ct);
            var initial = await WaitAudit(audit, entries => entries.Any(e => e.Record.CapabilityId == "qian.config:secret:resolve") &&
                entries.Any(e => e.Record.CapabilityId == ConfigEvents.ReleaseAppliedCapabilityId));
            var previous = initial.Count(e => e.Record.CapabilityId == ConfigEvents.ReleaseAppliedCapabilityId);
            await apollo.PublishAsync(appId, "Acceptance:Revision", "second", Ct);
            var final = await WaitAudit(audit, entries => entries.Count(e => e.Record.CapabilityId == ConfigEvents.ReleaseAppliedCapabilityId) > previous);
            Assert.True((await audit.Verify(Ct)).Valid);
            Assert.All(final, entry => Assert.Null(entry.Record.TenantId));
            var persisted = JsonSerializer.Serialize(final);
            foreach (var secret in databaseSecrets.Append(identity.Secret))
            {
                Assert.False(persisted.Contains(secret, StringComparison.Ordinal), "审计链不能含凭据。");
                Assert.False(gateway.Output.Contains(secret, StringComparison.Ordinal), "生产宿主日志不能含凭据。");
            }
            await gateway.Stop();
            TestContext.Current.TestOutputHelper!.WriteLine($"生产宿主 runtime={(gateway.Native ? "NativeAOT" : "JIT预检")}；真实 Apollo/Redis/受限 PG/Identity Cookie 与 Bearer 换签通过；平台审计 {final.Count} 条，验链通过，正常停机。");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
    private async Task<ServiceProvider> SessionReader(IdentityServerHost identity, Uri address)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddDataProtection();
        var connection = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(_ => connection);
        services.AddQianIdentityClient(new IdentityClientOptions
        {
            Authority = new Uri(identity.Issuer), ClientId = IdentityServerHost.ClientId, Resource = IdentityServerHost.Resource,
            RedirectUri = new Uri(address, "auth/callback"), RequireHttpsMetadata = false, SessionKeyPrefix = "qian:gateway:sessions:"
        });
        return services.BuildServiceProvider();
    }
    private static async Task<IReadOnlyList<AuditChainEntry>> WaitAudit(NativeAuditReader audit, Func<IReadOnlyList<AuditChainEntry>, bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        do { var entries = await audit.Read(Ct); if (predicate(entries)) return entries; await Task.Delay(200, Ct); } while (DateTime.UtcNow < deadline);
        Assert.Fail("真实生产宿主未在 90 秒内把配置/密钥事件写入审计链。"); return [];
    }
    private static string Root()
    {
        var path = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(path, "Qian.slnx"))) path = Directory.GetParent(path)!.FullName; return path;
    }
    private sealed class Downstream(WebApplication app, Uri jwks, HttpClient client) : IAsyncDisposable
    {
        internal string Address => app.Urls.Single();
        internal ConcurrentQueue<(string Token, string Cookie, string ForwardedFor)> Requests { get; } = new();
        internal static async Task<Downstream> Start(Uri jwks, HttpClient client)
        {
            var builder = WebApplication.CreateSlimBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            var app = builder.Build(); var host = new Downstream(app, jwks, client);
            app.MapGet("/api/native-acceptance", (Func<HttpContext, Task<IResult>>)host.Handle); await app.StartAsync(Ct); return host;
        }
        private async Task<IResult> Handle(HttpContext http)
        {
            var header = http.Request.Headers.Authorization.ToString(); if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return Results.Unauthorized();
            var token = header[7..]; var keys = new JsonWebKeySet(await client.GetStringAsync(jwks, http.RequestAborted));
            var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = S2sIssuer, ValidateAudience = true, ValidAudience = "qian-platform", ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys.GetSigningKeys(), ValidAlgorithms = [SecurityAlgorithms.RsaSha256], RequireSignedTokens = true,
                RequireExpirationTime = true, ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            });
            if (!result.IsValid) return Results.Unauthorized();
            Requests.Enqueue((token, http.Request.Headers.Cookie.ToString(), http.Request.Headers["X-Forwarded-For"].ToString())); return Results.Ok();
        }
        public ValueTask DisposeAsync() => app.DisposeAsync();
    }
}
