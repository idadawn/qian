using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Identity.Connector.Oidc.Tests;

// 通用 OIDC 连接器的协议行为：对着进程内的 OP 替身走完整授权码 + PKCE，并逐项篡改 id_token 与授权响应，确认都被拒绝。
public sealed class OidcConnectorTests : IDisposable
{
    private static readonly Uri Callback = new("https://login.qian.test/connect/callback/oidc");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly ManualClock _clock = new(DateTimeOffset.UtcNow);
    private readonly FakeOpenIdProvider _op;
    private readonly FakeSecrets _secrets = new();
    private readonly OidcIdentityConnector _connector;

    public OidcConnectorTests()
    {
        _op = new FakeOpenIdProvider(_clock);
        _connector = new OidcIdentityConnector(Options(), _secrets, _op, _clock);
    }

    public void Dispose() { _connector.Dispose(); _op.Dispose(); }

    private static OidcConnectorOptions Options(string? secret = null) => new()
    {
        Issuer = FakeOpenIdProvider.Issuer, ClientId = FakeOpenIdProvider.ClientId, ClientSecret = secret ?? FakeOpenIdProvider.SecretRef
    };

    private static ConnectorContext Context(string? nonce = null) =>
        new("web", Guid.CreateVersion7(), Callback, Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32)), nonce ?? Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32)))
        { CodeVerifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32)) };

    // 发起 → OP 同意 → 回调：返回连接器的断言或它抛出的错误码。
    private async Task<(ExternalAssertion? Assertion, ErrorCode? Error)> RoundTrip(Func<Dictionary<string, string>, Dictionary<string, string>>? tamper = null, ConnectorContext? context = null)
    {
        context ??= Context();
        var challenge = await _connector.BeginSignInAsync(context, Ct);
        var (code, state, _) = _op.Authorize(challenge.RedirectUri!);
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = code, ["state"] = state };
        if (tamper is not null) parameters = tamper(parameters);
        try { return (await _connector.CompleteSignInAsync(new ConnectorCallback(context, parameters), Ct), null); }
        catch (QianErrorException error) { return (null, error.Error.Code); }
    }

    [Fact]
    public async Task 授权码加PKCE走通_断言只带外部身份与展示资料()
    {
        var context = Context();
        var challenge = await _connector.BeginSignInAsync(context, Ct);
        var target = challenge.RedirectUri!;
        Assert.StartsWith(FakeOpenIdProvider.Issuer + "/protocol/openid-connect/auth?kc_idp_hint=corp&", target.AbsoluteUri, StringComparison.Ordinal);
        var query = FakeOpenIdProvider.Query(target.Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal(FakeOpenIdProvider.ClientId, query["client_id"]);
        Assert.Equal(Callback.AbsoluteUri, query["redirect_uri"]);
        Assert.Equal("openid profile email", query["scope"]);
        Assert.Equal(context.State, query["state"]);
        Assert.Equal(context.Nonce, query["nonce"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal(Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(context.CodeVerifier!))), query["code_challenge"]);
        // 授权地址里没有任何客户端密钥材料。
        Assert.DoesNotContain("secret", target.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_secrets.Resolved);

        var (code, state, _) = _op.Authorize(target);
        var assertion = await _connector.CompleteSignInAsync(new ConnectorCallback(context, new Dictionary<string, string> { ["code"] = code, ["state"] = state }), Ct);
        Assert.Equal(FakeOpenIdProvider.Issuer, assertion.Issuer);
        Assert.Equal("user-8f1c", assertion.Subject);
        Assert.Null(assertion.UnionId);
        Assert.Equal("张三", assertion.Profile.GetProperty("name").GetString());
        Assert.Equal("zhangsan", assertion.Profile.GetProperty("preferred_username").GetString());
        Assert.Equal("zhangsan@corp.example", assertion.Profile.GetProperty("email").GetString());
        Assert.True(assertion.Profile.GetProperty("email_verified").GetBoolean());
        Assert.Equal(["pwd", "mfa"], assertion.Amr);
        Assert.Equal("ExternalAssertion(redacted)", assertion.ToString());
        // 密钥只在换码时经 secretRef 现取，按规范先表单编码再走 Basic。
        Assert.Equal([FakeOpenIdProvider.SecretRef], _secrets.Resolved);
        Assert.Equal(("basic", FakeOpenIdProvider.ClientSecret), _op.LastClientAuth);
    }

    [Fact]
    public async Task 上游只声明post时密钥放进表单()
    {
        _op.AuthMethods = ["client_secret_post"];
        var (assertion, error) = await RoundTrip();
        Assert.Null(error);
        Assert.NotNull(assertion);
        Assert.Equal(("post", FakeOpenIdProvider.ClientSecret), _op.LastClientAuth);
    }

    [Fact]
    public async Task 签名不对_被拒为上游响应无效()
    {
        // 同一个 kid、不同的私钥：伪造的签名。
        var forged = FakeOpenIdProvider.NewKey("key-1");
        _op.Mint = grant => _op.Token(grant, key: forged);
        Assert.Equal(PlatformErrors.BadGateway, (await RoundTrip()).Error);
    }

    [Fact]
    public async Task 不签名或用对称算法伪签的令牌一律拒绝()
    {
        _op.Mint = grant => FakeOpenIdProvider.Unsigned(new Dictionary<string, object>
        {
            ["iss"] = FakeOpenIdProvider.Issuer, ["aud"] = FakeOpenIdProvider.ClientId, ["sub"] = "user-8f1c", ["nonce"] = grant.Nonce,
            ["exp"] = _clock.GetUtcNow().AddMinutes(5).ToUnixTimeSeconds(), ["iat"] = _clock.GetUtcNow().ToUnixTimeSeconds()
        });
        Assert.Equal(PlatformErrors.BadGateway, (await RoundTrip()).Error);
        // 拿客户端密钥当 HMAC 密钥伪签（经典的算法混淆）：即便 JWKS 里真发布了对称密钥也不参与验签。
        var hmac = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(FakeOpenIdProvider.ClientSecret)) { KeyId = "hs-1" };
        _op.Published.Add(hmac);
        // 让缓存过期，确保这次验签用的 JWKS 里真有这把对称密钥。
        _clock.Advance(TimeSpan.FromHours(2));
        _op.Mint = grant => _op.Token(grant, key: hmac, algorithm: SecurityAlgorithms.HmacSha256);
        Assert.Equal(PlatformErrors.BadGateway, (await RoundTrip()).Error);
        Assert.Equal(2, _op.Hits[FakeOpenIdProvider.Issuer + "/protocol/openid-connect/certs"]);
    }

    [Theory]
    [InlineData("iss", "https://evil.example.test/realms/corp")]
    [InlineData("aud", "another-client")]
    [InlineData("sub", null)]
    [InlineData("azp", "another-client")]
    public async Task iss_aud_sub_azp_不符都被拒绝(string claim, string? value)
    {
        _op.Mint = grant => _op.Token(grant, new Dictionary<string, object?> { [claim] = value });
        Assert.Equal(PlatformErrors.BadGateway, (await RoundTrip()).Error);
    }

    [Fact]
    public async Task 多个受众却没有azp时拒绝_azp为本客户端时接受()
    {
        _op.Mint = grant => _op.Token(grant, new Dictionary<string, object?> { ["aud"] = new[] { FakeOpenIdProvider.ClientId, "other" } });
        Assert.Equal(PlatformErrors.BadGateway, (await RoundTrip()).Error);
        _op.Mint = grant => _op.Token(grant, new Dictionary<string, object?> { ["aud"] = new[] { FakeOpenIdProvider.ClientId, "other" }, ["azp"] = FakeOpenIdProvider.ClientId });
        Assert.Null((await RoundTrip()).Error);
    }

    [Fact]
    public async Task nonce不符或缺失_按登录请求失效处理()
    {
        _op.Mint = grant => _op.Token(grant, new Dictionary<string, object?> { ["nonce"] = "replayed-nonce" });
        Assert.Equal(IdentityErrors.ConnectorStateInvalid, (await RoundTrip()).Error);
        _op.Mint = grant => _op.Token(grant, new Dictionary<string, object?> { ["nonce"] = null });
        Assert.Equal(IdentityErrors.ConnectorStateInvalid, (await RoundTrip()).Error);
    }

    [Fact]
    public async Task 过期令牌被拒_时钟容差两分钟内仍接受()
    {
        _op.Mint = grant => _op.Token(grant, expiresIn: TimeSpan.FromMinutes(-3));
        Assert.Equal(PlatformErrors.BadGateway, (await RoundTrip()).Error);
        _op.Mint = grant => _op.Token(grant, expiresIn: TimeSpan.FromMinutes(-1));
        Assert.Null((await RoundTrip()).Error);
    }

    [Fact]
    public async Task 授权响应的iss按RFC9207核对()
    {
        Assert.Equal(IdentityErrors.ConnectorStateInvalid, (await RoundTrip(p => { p["iss"] = "https://evil.example.test/realms/corp"; return p; })).Error);
        Assert.Null((await RoundTrip(p => { p["iss"] = FakeOpenIdProvider.Issuer; return p; })).Error);
        // 上游声明支持 iss 参数后，缺了它就是可疑响应。
        _op.IssParameterSupported = true;
        _clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(IdentityErrors.ConnectorStateInvalid, (await RoundTrip()).Error);
        Assert.Null((await RoundTrip(p => { p["iss"] = FakeOpenIdProvider.Issuer; return p; })).Error);
    }

    [Fact]
    public async Task 缺code或上游拒绝换码_都是上游响应无效()
    {
        Assert.Equal(PlatformErrors.BadGateway, (await RoundTrip(p => { p.Remove("code"); return p; })).Error);
        Assert.Equal(PlatformErrors.BadGateway, (await RoundTrip(p => { p["code"] = "never-issued"; return p; })).Error);
        // PKCE 不符（verifier 被换）：OP 拒绝换码。
        var context = Context();
        var challenge = await _connector.BeginSignInAsync(context, Ct);
        var (code, state, _) = _op.Authorize(challenge.RedirectUri!);
        var swapped = context with { CodeVerifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32)) };
        var error = await Assert.ThrowsAsync<QianErrorException>(() =>
            _connector.CompleteSignInAsync(new ConnectorCallback(swapped, new Dictionary<string, string> { ["code"] = code, ["state"] = state }), Ct));
        Assert.Equal(PlatformErrors.BadGateway, error.Error.Code);
    }

    [Fact]
    public async Task 超大响应与非JSON响应都按上游无效处理()
    {
        _op.TokenResponse = () => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[(1 << 20) + 1]) };
        Assert.Equal(PlatformErrors.BadGateway, (await RoundTrip()).Error);
        _op.TokenResponse = () => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("<html>login</html>") };
        Assert.Equal(PlatformErrors.BadGateway, (await RoundTrip()).Error);
    }

    [Fact]
    public async Task 发现文档issuer不一致时不出站()
    {
        _op.DiscoveryIssuer = FakeOpenIdProvider.Issuer + "/";
        var error = await Assert.ThrowsAsync<QianErrorException>(() => _connector.BeginSignInAsync(Context(), Ct));
        Assert.Equal(PlatformErrors.BadGateway, error.Error.Code);
    }

    [Fact]
    public async Task 发现文档与公钥缓存一小时_未知kid最多每30秒刷新一次()
    {
        Assert.Null((await RoundTrip()).Error);
        Assert.Null((await RoundTrip()).Error);
        var discovery = FakeOpenIdProvider.Issuer + "/.well-known/openid-configuration";
        var jwks = FakeOpenIdProvider.Issuer + "/protocol/openid-connect/certs";
        Assert.Equal(1, _op.Hits[discovery]);
        Assert.Equal(1, _op.Hits[jwks]);

        // 上游轮换密钥：30 秒内不刷新（防止伪造 kid 逼它反复拉取），过了间隔刷新一次即通过。
        var rotated = FakeOpenIdProvider.NewKey("key-2");
        _op.SigningKey = rotated; _op.Published = [rotated];
        Assert.Equal(PlatformErrors.BadGateway, (await RoundTrip()).Error);
        Assert.Equal(1, _op.Hits[jwks]);
        _clock.Advance(TimeSpan.FromSeconds(31));
        Assert.Null((await RoundTrip()).Error);
        Assert.Equal(2, _op.Hits[jwks]);
        // 刚刷新过：伪造的 kid 不再触发拉取。
        _op.Mint = grant => _op.Token(grant, key: FakeOpenIdProvider.NewKey("key-forged"));
        Assert.Equal(PlatformErrors.BadGateway, (await RoundTrip()).Error);
        Assert.Equal(2, _op.Hits[jwks]);
        _op.Mint = null;

        _clock.Advance(TimeSpan.FromHours(1));
        Assert.Null((await RoundTrip()).Error);
        Assert.Equal(2, _op.Hits[discovery]);
    }

    [Fact]
    public async Task 密钥解析失败时不换码()
    {
        using var connector = new OidcIdentityConnector(Options("secretRef://platform/identity/unknown"), _secrets, _op, _clock);
        var context = Context();
        var challenge = await connector.BeginSignInAsync(context, Ct);
        var (code, state, _) = _op.Authorize(challenge.RedirectUri!);
        await Assert.ThrowsAsync<QianErrorException>(() => connector.CompleteSignInAsync(new ConnectorCallback(context, new Dictionary<string, string> { ["code"] = code, ["state"] = state }), Ct));
        Assert.False(_op.Hits.ContainsKey(FakeOpenIdProvider.Issuer + "/protocol/openid-connect/token"));
    }
}
