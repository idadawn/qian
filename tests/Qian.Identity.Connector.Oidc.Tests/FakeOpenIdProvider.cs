using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Config;

namespace Qian.Identity.Connector.Oidc.Tests;

// 进程内的 OpenID Provider 替身：发现文档、JWKS、授权（由测试模拟用户同意）与令牌端点。令牌端点按真实 OP 的规则核对
// 客户端认证、授权码一次性、redirect_uri 与 PKCE，再按测试指定的方式签出 id_token，用来覆盖签名 / iss / aud / nonce 等各类篡改。
internal sealed class FakeOpenIdProvider : HttpMessageHandler
{
    internal const string Issuer = "https://idp.example.test/realms/corp";
    internal const string ClientId = "qian-identity";
    // 含需要表单编码的字符，验证 Basic 认证先编码再拼接；长度够做 HS256 密钥（模拟用客户端密钥伪签的攻击）。
    internal const string ClientSecret = "test-client-secret:with/&+=chars-0123456789";
    internal const string SecretRef = "secretRef://platform/identity/connector-oidc/client-secret";

    private readonly ConcurrentDictionary<string, Grant> _codes = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;

    internal FakeOpenIdProvider(TimeProvider clock)
    {
        _clock = clock;
        SigningKey = NewKey("key-1");
        Published = [SigningKey];
    }

    internal sealed record Grant(string ClientId, string RedirectUri, string Challenge, string Nonce);

    /// <summary>当前签名密钥。</summary>
    internal RsaSecurityKey SigningKey { get; set; }
    /// <summary>JWKS 发布的公钥。</summary>
    internal List<SecurityKey> Published { get; set; }
    /// <summary>发现文档里的 issuer（默认与配置一致）。</summary>
    internal string DiscoveryIssuer { get; set; } = Issuer;
    /// <summary>声明的客户端认证方式；null 表示不声明（按规范缺省 client_secret_basic）。</summary>
    internal string[]? AuthMethods { get; set; } = ["client_secret_basic", "client_secret_post"];
    /// <summary>是否声明 RFC 9207（授权响应带 iss）。</summary>
    internal bool IssParameterSupported { get; set; }
    /// <summary>令牌端点在核对通过后调用它来签出 id_token；缺省签出合规的令牌。</summary>
    internal Func<Grant, string>? Mint { get; set; }
    /// <summary>令牌端点直接返回的响应（用于上游报错、超大响应）。</summary>
    internal Func<HttpResponseMessage>? TokenResponse { get; set; }
    /// <summary>各路径的请求次数。</summary>
    internal ConcurrentDictionary<string, int> Hits { get; } = new(StringComparer.Ordinal);
    /// <summary>令牌端点最近一次收到的客户端认证（basic / post）与密钥。</summary>
    internal (string Method, string? Secret)? LastClientAuth { get; private set; }

    internal static RsaSecurityKey NewKey(string kid) => new(RSA.Create(2048)) { KeyId = kid };

    /// <summary>模拟用户在 OP 上同意：从连接器给出的授权地址里取参数，发一个绑定 PKCE challenge 的授权码。</summary>
    internal (string Code, string State, Dictionary<string, string> Query) Authorize(Uri authorization)
    {
        var query = Query(authorization.Query);
        var code = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));
        _codes[code] = new Grant(query["client_id"], query["redirect_uri"], query["code_challenge"], query["nonce"]);
        return (code, query["state"], query);
    }

    /// <summary>按 OP 的规则签一张 id_token；claims 覆盖缺省值，值为 null 表示去掉该声明。</summary>
    internal string Token(Grant grant, IDictionary<string, object?>? claims = null, SecurityKey? key = null, string algorithm = SecurityAlgorithms.RsaSha256, TimeSpan? expiresIn = null)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var payload = new Dictionary<string, object>
        {
            ["iss"] = Issuer, ["aud"] = ClientId, ["sub"] = "user-8f1c", ["nonce"] = grant.Nonce, ["name"] = "张三", ["preferred_username"] = "zhangsan",
            ["email"] = "zhangsan@corp.example", ["email_verified"] = true, ["amr"] = new[] { "pwd", "mfa" }
        };
        foreach (var (name, value) in claims ?? new Dictionary<string, object?>())
            if (value is null) payload.Remove(name); else payload[name] = value;
        var expires = now + (expiresIn ?? TimeSpan.FromMinutes(5));
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = payload, IssuedAt = expires < now ? expires - TimeSpan.FromMinutes(10) : now, NotBefore = expires < now ? expires - TimeSpan.FromMinutes(10) : now, Expires = expires,
            SigningCredentials = new SigningCredentials(key ?? SigningKey, algorithm)
        };
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    /// <summary>不签名的令牌（alg: none）。</summary>
    internal static string Unsigned(IDictionary<string, object> payload) =>
        Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""") + "." + Base64UrlEncoder.Encode(JsonSerializer.Serialize(payload)) + ".";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.GetLeftPart(UriPartial.Path);
        Hits.AddOrUpdate(path, 1, (_, count) => count + 1);
        if (request.Method == HttpMethod.Get && path == Issuer + "/.well-known/openid-configuration")
        {
            var document = new JsonObject
            {
                ["issuer"] = DiscoveryIssuer, ["authorization_endpoint"] = Issuer + "/protocol/openid-connect/auth?kc_idp_hint=corp",
                ["token_endpoint"] = Issuer + "/protocol/openid-connect/token", ["jwks_uri"] = Issuer + "/protocol/openid-connect/certs",
                ["response_types_supported"] = new JsonArray("code"), ["code_challenge_methods_supported"] = new JsonArray("plain", "S256"),
                ["id_token_signing_alg_values_supported"] = new JsonArray("RS256")
            };
            if (AuthMethods is not null) document["token_endpoint_auth_methods_supported"] = new JsonArray([.. AuthMethods.Select(m => JsonValue.Create(m))]);
            if (IssParameterSupported) document["authorization_response_iss_parameter_supported"] = true;
            return Json(document.ToJsonString());
        }
        if (request.Method == HttpMethod.Get && path == Issuer + "/protocol/openid-connect/certs")
        {
            var keys = new JsonArray();
            foreach (var key in Published)
            {
                var jwk = key is RsaSecurityKey rsa ? JsonWebKeyConverter.ConvertFromRSASecurityKey(rsa) : JsonWebKeyConverter.ConvertFromSecurityKey(key);
                keys.Add(JsonNode.Parse(JsonSerializer.Serialize(new { kty = jwk.Kty, kid = jwk.Kid, use = "sig", n = jwk.N, e = jwk.E, k = jwk.K }, OmitNulls)));
            }
            return Json(new JsonObject { ["keys"] = keys }.ToJsonString());
        }
        if (request.Method == HttpMethod.Post && path == Issuer + "/protocol/openid-connect/token")
        {
            if (TokenResponse is { } custom) return custom();
            var form = Query(await request.Content!.ReadAsStringAsync(cancellationToken));
            string? id, secret;
            if (request.Headers.Authorization is { Scheme: "Basic" } basic)
            {
                var pair = Encoding.UTF8.GetString(Convert.FromBase64String(basic.Parameter!)).Split(':', 2);
                (id, secret) = (WebUtility.UrlDecode(pair[0]), WebUtility.UrlDecode(pair[1]));
                LastClientAuth = ("basic", secret);
            }
            else
            {
                (id, secret) = (form.GetValueOrDefault("client_id"), form.GetValueOrDefault("client_secret"));
                LastClientAuth = ("post", secret);
            }
            if (id != ClientId || secret != ClientSecret) return Error(HttpStatusCode.Unauthorized, "invalid_client");
            if (form.GetValueOrDefault("grant_type") != "authorization_code" || !_codes.TryRemove(form.GetValueOrDefault("code") ?? "", out var grant))
                return Error(HttpStatusCode.BadRequest, "invalid_grant");
            var verifier = form.GetValueOrDefault("code_verifier") ?? "";
            if (grant.RedirectUri != form.GetValueOrDefault("redirect_uri") || Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))) != grant.Challenge)
                return Error(HttpStatusCode.BadRequest, "invalid_grant");
            var idToken = Mint is { } mint ? mint(grant) : Token(grant);
            return Json(new JsonObject { ["access_token"] = "opaque-access", ["token_type"] = "Bearer", ["expires_in"] = 300, ["id_token"] = idToken }.ToJsonString());
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static readonly JsonSerializerOptions OmitNulls = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Error(HttpStatusCode status, string error) =>
        new(status) { Content = new StringContent($$"""{"error":"{{error}}"}""", Encoding.UTF8, "application/json") };

    internal static Dictionary<string, string> Query(string query) =>
        query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(part => part.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')), pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : "", StringComparer.Ordinal);
}

/// <summary>只认一个 secretRef 的密钥解析替身，记录被解析的引用。</summary>
internal sealed class FakeSecrets : ISecretResolver
{
    internal ConcurrentQueue<string> Resolved { get; } = new();

    public ValueTask<SecretValue> ResolveAsync(string reference, CancellationToken cancellationToken = default)
    {
        Resolved.Enqueue(reference);
        if (reference != FakeOpenIdProvider.SecretRef) throw new Qian.Runtime.Errors.QianErrorException(new Qian.Runtime.Errors.QianError(SecretErrors.AccessDenied));
        return ValueTask.FromResult(new SecretValue(Encoding.UTF8.GetBytes(FakeOpenIdProvider.ClientSecret)));
    }
}

internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private long _ticks = start.UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
    internal void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}
