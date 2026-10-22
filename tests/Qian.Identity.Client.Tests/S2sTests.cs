using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Gateway;
using Kun.Gateway.Configuration;
using Kun.Gateway.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Identity.Client.Tests;

// 验收 2：下游只认网关为本服务受众签发的 S2S 令牌。aud 不匹配、上一跳收到的令牌转发给下一跳、用户访问令牌直接打下游，以及载荷违反冻结契约
//（寿命、闭合对象、条件必填、委托链、算法）一律拒绝；通过的请求，ICurrentUser 的快照与令牌载荷逐字段一致。
public sealed class S2sTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string Tenant = Guid.CreateVersion7().ToString("D");
    private static readonly string Membership = Guid.CreateVersion7().ToString("D");
    private static readonly string Session = Guid.CreateVersion7().ToString("D");
    private readonly GatewayKeys _gateway = new();

    public void Dispose() => _gateway.Dispose();

    [Fact]
    public async Task 网关为本服务签发的用户与服务令牌通过_快照与载荷一致()
    {
        await using var platform = await Downstream.Start(_gateway, "qian-platform");
        using var issuer = _gateway.Issuer(new HostingEnvironment { EnvironmentName = "Production" });
        var user = await platform.WhoAmI(await issuer.IssueForAudienceAsync(User(Tenant), "qian-platform", Ct));
        Assert.Equal("person-1", user.GetProperty("subject").GetString()); Assert.Equal("User", user.GetProperty("kind").GetString());
        Assert.Equal(Tenant, user.GetProperty("tenant").GetString()); Assert.Equal(Membership, user.GetProperty("membership").GetString());
        Assert.Equal(7, user.GetProperty("revision").GetInt64()); Assert.Equal(Session, user.GetProperty("session").GetString());
        Assert.Equal("crm.read", user.GetProperty("scope").GetString()); Assert.Equal("person-1", user.GetProperty("userId").GetString());
        var platformUser = await platform.WhoAmI(await issuer.IssueForAudienceAsync(User(null), "qian-platform", Ct));
        Assert.Equal(JsonValueKind.Null, platformUser.GetProperty("tenant").ValueKind); Assert.Equal(JsonValueKind.Null, platformUser.GetProperty("membership").ValueKind);
        var service = await platform.WhoAmI(await issuer.IssueForAudienceAsync(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "machine"), new Claim("principal_kind", "service"), new Claim("tenant_id", Tenant), new Claim("scope", "crm.write")], "external")), "qian-platform", Ct));
        Assert.Equal("Service", service.GetProperty("kind").GetString()); Assert.Equal(service.GetProperty("session").GetString(), service.GetProperty("tokenId").GetString());
    }

    [Fact]
    public async Task aud不匹配或上一跳透传_下游拒绝()
    {
        await using var platform = await Downstream.Start(_gateway, "qian-platform");
        await using var identity = await Downstream.Start(_gateway, "qian-identity");
        using var issuer = _gateway.Issuer(new HostingEnvironment { EnvironmentName = "Production" });
        var forIdentity = await issuer.IssueForAudienceAsync(User(Tenant), "qian-identity", Ct);
        await platform.AssertRejected(forIdentity);
        // 第一跳 qian-identity 接受；它把入站 Authorization 原样转给 qian-platform，后者拒绝。
        Assert.Equal(Session, (await identity.WhoAmI(forIdentity)).GetProperty("session").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, await identity.Forward(forIdentity, platform));
        // 同时写了两个受众的令牌也不行：每跳一个受众。
        await platform.AssertRejected(_gateway.Mint(Payload(aud: new JsonArray("qian-platform", "qian-identity"))));
        Assert.Equal("person-1", (await platform.WhoAmI(_gateway.Mint(Payload()))).GetProperty("subject").GetString());
    }

    [Fact]
    public async Task 用户访问令牌与其他发行方的令牌直接打下游_拒绝()
    {
        await using var platform = await Downstream.Start(_gateway, "qian-platform");
        // 身份服务签发的访问令牌：typ at+jwt、发行方与密钥都不是网关签发器。
        using var identityKey = RSA.Create(2048);
        var access = Payload(); access["iss"] = "https://id.qian.test/"; access["client_id"] = "qian-web";
        await platform.AssertRejected(Sign(access, new RsaSecurityKey(identityKey) { KeyId = "identity" }, "at+jwt"));
        // 即使是网关的密钥与 issuer，at+jwt 类型也不是 S2S 令牌。
        await platform.AssertRejected(_gateway.Mint(Payload(), "at+jwt"));
        // 对称算法一律不收。
        await platform.AssertRejected(Sign(Payload(), new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)), null, SecurityAlgorithms.HmacSha256));
    }

    [Theory]
    [InlineData("lifetime")]
    [InlineData("nbf")]
    [InlineData("extra")]
    [InlineData("null")]
    [InlineData("tenant")]
    [InlineData("membership")]
    [InlineData("revision")]
    [InlineData("service-membership")]
    [InlineData("service-session")]
    [InlineData("act")]
    [InlineData("agent")]
    [InlineData("session")]
    [InlineData("duplicate")]
    [InlineData("aud-string")]
    [InlineData("roles")]
    public async Task 载荷违反冻结契约_下游拒绝(string violation)
    {
        await using var platform = await Downstream.Start(_gateway, "qian-platform");
        var payload = Payload();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string token;
        switch (violation)
        {
            case "lifetime": payload["exp"] = now + 301; break;
            case "nbf": payload["nbf"] = now + 120; payload["exp"] = now + 100; break;
            case "extra": payload["email"] = "a@example.test"; break;
            case "null": payload["project_id"] = null; break;
            case "tenant": payload.Remove("tenant_id"); break;
            case "membership": payload.Remove("membership_id"); break;
            case "revision": payload.Remove("identity_revision"); break;
            case "service-membership": payload["principal_kind"] = "service"; payload["session_id"] = payload["jti"]!.DeepClone(); payload.Remove("identity_revision"); break;
            case "service-session": payload["principal_kind"] = "service"; payload.Remove("membership_id"); break;
            case "act": payload["act"] = new JsonObject { ["sub"] = "agent-1", ["principal_kind"] = "agent" }; break;
            case "agent": payload["principal_kind"] = "agent"; break;
            case "session": payload["session_id"] = "not-a-uuid"; break;
            case "aud-string": payload["aud"] = "qian-platform"; break;
            case "roles": payload["roles"] = new JsonArray("admin", "admin"); break;
        }
        if (violation == "duplicate")
        {
            // 同名字段出现两次（System.Text.Json 取后者）：视为篡改。
            var json = payload.ToJsonString();
            token = _gateway.MintRaw(json.Insert(json.Length - 1, ",\"sub\":\"someone-else\""));
        }
        else token = _gateway.Mint(payload);
        await platform.AssertRejected(token);
    }

    private static ClaimsPrincipal User(string? tenant)
    {
        var claims = new List<Claim>
        {
            new("sub", "person-1"), new("principal_kind", "user"), new("identity_revision", "7"), new("session_id", Session), new("scope", "crm.read"),
            tenant is null ? new Claim("tenant_id", "", JsonClaimValueTypes.JsonNull) : new Claim("tenant_id", tenant)
        };
        if (tenant is not null) claims.Add(new Claim("membership_id", Membership));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "external"));
    }

    // 合法的用户 S2S 载荷，供各项违约改写。
    private static JsonObject Payload(JsonArray? aud = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new JsonObject
        {
            ["iss"] = GatewayKeys.IssuerName, ["aud"] = aud ?? new JsonArray("qian-platform"), ["sub"] = "person-1", ["principal_kind"] = "user", ["tenant_id"] = Tenant,
            ["membership_id"] = Membership, ["identity_revision"] = 7, ["roles"] = new JsonArray(), ["scope"] = "crm.read", ["session_id"] = Session,
            ["iat"] = now, ["nbf"] = now, ["exp"] = now + 60, ["jti"] = Guid.CreateVersion7().ToString("D")
        };
    }

    private static string Sign(JsonObject payload, SecurityKey key, string? type, string algorithm = SecurityAlgorithms.RsaSha256)
    {
        var credentials = new SigningCredentials(key, algorithm);
        return type is null ? new JsonWebTokenHandler().CreateToken(payload.ToJsonString(), credentials)
            : new JsonWebTokenHandler().CreateToken(payload.ToJsonString(), credentials, new Dictionary<string, object> { ["typ"] = type });
    }
}

/// <summary>网关签发器的真实密钥材料（PEM 文件，同 Kun.Gateway 部署形态），以及下游用来验签的静态 JWKS。</summary>
internal sealed class GatewayKeys : IDisposable
{
    internal const string IssuerName = "https://gateway.qian.test/s2s";
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("qian-gateway-s2s-");
    private string? _kid;

    internal GatewayKeys()
    {
        var fileOptions = new FileStreamOptions { Access = FileAccess.Write, Mode = FileMode.CreateNew };
        if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var file = new StreamWriter(new FileStream(PrivateKey, fileOptions))) file.Write(_rsa.ExportRSAPrivateKeyPem());
        File.WriteAllText(PublicKey, _rsa.ExportRSAPublicKeyPem());
    }

    private string PrivateKey => Path.Combine(_directory.FullName, "private.pem");
    private string PublicKey => Path.Combine(_directory.FullName, "public.pem");

    internal S2sTokenIssuer Issuer(IHostEnvironment environment) => new(Options.Create(new KunGatewayOptions
    {
        ExternalIssuer = "https://id.qian.test/", ExternalAudience = IdentityServerHost.Resource, ExternalJwksUri = "https://id.qian.test/.well-known/jwks",
        InternalIssuer = IssuerName, InternalAudience = "qian-platform", SigningKeyPath = PrivateKey, PublicKeyPath = PublicKey, InternalTokenLifetime = TimeSpan.FromSeconds(60)
    }), environment, NoOpS2sPrincipalResolver.Instance);

    // 签发器给令牌头写的 kid 由密钥环决定；先签一枚取出 kid，再按同一 kid 发布公钥。
    private string Kid
    {
        get
        {
            if (_kid is not null) return _kid;
            using var issuer = Issuer(new HostingEnvironment { EnvironmentName = "Production" });
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "probe"), new Claim("principal_kind", "service"), new Claim("tenant_id", "", JsonClaimValueTypes.JsonNull)], "probe"));
            return _kid = new JsonWebToken(issuer.IssueForAudienceAsync(principal, "probe").GetAwaiter().GetResult()).Kid;
        }
    }

    internal JsonWebKeySet Jwks
    {
        get
        {
            var parameters = _rsa.ExportParameters(false);
            var jwk = new { kty = "RSA", use = "sig", alg = "RS256", kid = Kid, n = Base64UrlEncoder.Encode(parameters.Modulus), e = Base64UrlEncoder.Encode(parameters.Exponent) };
            return new JsonWebKeySet(JsonSerializer.Serialize(new { keys = new[] { jwk } }));
        }
    }

    /// <summary>用网关密钥直接签任意载荷，构造签名有效但内容违约的令牌。</summary>
    internal string Mint(JsonObject payload, string? type = null) => MintRaw(payload.ToJsonString(), type);

    internal string MintRaw(string payload, string? type = null)
    {
        var credentials = new SigningCredentials(new RsaSecurityKey(_rsa) { KeyId = Kid }, SecurityAlgorithms.RsaSha256);
        return type is null ? new JsonWebTokenHandler().CreateToken(payload, credentials)
            : new JsonWebTokenHandler().CreateToken(payload, credentials, new Dictionary<string, object> { ["typ"] = type });
    }

    public void Dispose() { _rsa.Dispose(); _directory.Delete(recursive: true); }
}

/// <summary>只装 S2S 校验的下游服务：<c>/whoami</c> 回显 ICurrentUser 快照；<c>/forward</c> 把入站 Authorization 原样转给另一个下游（模拟透传）。</summary>
internal sealed class Downstream : IAsyncDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    private Downstream(WebApplication app, Uri address) { _app = app; Address = address; _client = new HttpClient { BaseAddress = address }; }

    internal Uri Address { get; }

    internal static async Task<Downstream> Start(GatewayKeys gateway, string audience)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        // 构建前用不到地址：监听端口 0 由 Kestrel 原子分配，启动后再读实际地址，没有「先取端口再绑定」的空档（S1-QA-016）。
        builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Services.AddQianIdentityS2s(new IdentityS2sOptions { Issuer = GatewayKeys.IssuerName, Audience = audience, SigningKeys = gateway.Jwks });
        builder.Services.AddAuthorization(); builder.Services.AddHttpClient();
        var app = builder.Build();
        app.UseQianErrors(); app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/whoami", (ICurrentUser user) => user.Principal is { } principal
            ? Results.Json(new { subject = principal.Subject, kind = principal.Kind.ToString(), tenant = principal.TenantId, membership = principal.MembershipId,
                revision = principal.IdentityRevision, session = principal.SessionId, tokenId = principal.TokenId, scope = principal.Scope, userId = user.UserId })
            : Results.StatusCode(StatusCodes.Status500InternalServerError)).RequireAuthorization();
        app.MapGet("/forward", async (HttpContext http, string target, IHttpClientFactory factory) =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(target), "whoami"));
            request.Headers.TryAddWithoutValidation("Authorization", http.Request.Headers.Authorization.ToString());
            using var response = await factory.CreateClient().SendAsync(request, http.RequestAborted);
            return Results.Json(new { status = (int)response.StatusCode });
        }).RequireAuthorization();
        await app.StartAsync(Ct);
        return new Downstream(app, new Uri(app.Urls.Single() + "/"));
    }

    internal async Task<JsonElement> WhoAmI(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    internal async Task AssertRejected(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, body);
        Assert.Equal("UNAUTHENTICATED", JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
    }

    /// <summary>以 token 调本服务的 /forward，让它把同一令牌转给 target；返回 target 的响应状态。</summary>
    internal async Task<HttpStatusCode> Forward(string token, Downstream target)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "forward?target=" + Uri.EscapeDataString(target.Address.ToString()));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _client.SendAsync(request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return (HttpStatusCode)JsonDocument.Parse(body).RootElement.GetProperty("status").GetInt32();
    }

    public async ValueTask DisposeAsync() { _client.Dispose(); await _app.DisposeAsync(); }
}
