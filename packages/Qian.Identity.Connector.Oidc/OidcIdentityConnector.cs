using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Config;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Identity.Connector.Oidc;

/// <summary>
/// 通用 OIDC 上游连接器：授权码 + PKCE（S256）。state、nonce 与 code_verifier 由身份服务生成并核对关联，本类只做协议：
/// 发现文档（issuer 逐字一致）、JWKS（遇到未知 kid 刷新一次）、换码（client_secret_basic 或 client_secret_post），
/// 并校验 id_token 的签名（只收 RS / PS / ES 族）、iss、aud、azp、有效期与 nonce；授权响应带 iss（RFC 9207）时一并核对。
/// 客户端密钥只经 secretRef 在换码时现取现用，本类不保存明文。实例可并发使用，缓存的只有发现文档与公钥。
/// </summary>
public sealed class OidcIdentityConnector : IIdentityConnector, IDisposable
{
    private const int MaxResponseBytes = 1 << 20;
    private static readonly TimeSpan MetadataLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan KeyRefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);
    private static readonly string[] SigningAlgorithms =
    [
        SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512,
        SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.RsaSsaPssSha384, SecurityAlgorithms.RsaSsaPssSha512,
        SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.EcdsaSha384, SecurityAlgorithms.EcdsaSha512
    ];

    private readonly OidcConnectorOptions _options;
    private readonly ISecretResolver _secrets;
    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private readonly JsonWebTokenHandler _tokens = new() { MapInboundClaims = false };
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private Metadata? _metadata;
    private Keys? _keys;

    /// <summary>
    /// 以已校验的配置构造。<paramref name="secrets"/> 解析 <see cref="OidcConnectorOptions.ClientSecret"/>；
    /// <paramref name="handler"/> 承担全部出站请求，由调用方持有与释放，应当关闭自动跟随重定向。
    /// </summary>
    public OidcIdentityConnector(OidcConnectorOptions options, ISecretResolver secrets, HttpMessageHandler handler, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(secrets); ArgumentNullException.ThrowIfNull(handler);
        options.Validate();
        _options = options; _secrets = secrets; _clock = clock ?? TimeProvider.System;
        _http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = MaxResponseBytes };
        Descriptor = new ConnectorDescriptor(options.Name, options.DisplayName, "oidc", [.. options.Terminals], ConfigSchema.Value);
    }

    /// <summary>嵌入的配置 Schema，与组件文件夹 <c>plugins/qian.identity.connector.oidc/schemas/config.schema.json</c> 是同一份文件。</summary>
    internal static readonly Lazy<JsonElement> ConfigSchema = new(() =>
    {
        using var stream = typeof(OidcIdentityConnector).Assembly.GetManifestResourceStream("qian.identity.connector.oidc/config.schema.json")
            ?? throw new InvalidOperationException("缺少嵌入的配置 Schema。");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    });

    /// <inheritdoc />
    public ConnectorDescriptor Descriptor { get; }

    /// <inheritdoc />
    public override string ToString() => $"OidcIdentityConnector({_options.Name})";

    /// <summary>释放内部的 HTTP 客户端与刷新锁；出站处理器归构造方，不在这里释放。</summary>
    public void Dispose()
    {
        _http.Dispose();
        _refresh.Dispose();
    }

    /// <inheritdoc />
    public async Task<ChallengeResult> BeginSignInAsync(ConnectorContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.CodeVerifier is not { Length: >= 43 and <= 128 } verifier) throw new ArgumentException("缺少 PKCE code_verifier。", nameof(context));
        var metadata = await MetadataAsync(cancellationToken).ConfigureAwait(false);
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = _options.ClientId, ["redirect_uri"] = context.CallbackUri.AbsoluteUri, ["scope"] = string.Join(' ', _options.Scopes),
            ["state"] = context.State, ["nonce"] = context.Nonce, ["code_challenge"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
            ["code_challenge_method"] = "S256"
        };
        var target = metadata.Authorization.AbsoluteUri;
        var separator = metadata.Authorization.Query.Length > 0 ? '&' : '?';
        return new ChallengeResult(new Uri(target + separator + string.Join('&', query.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)))), null);
    }

    /// <inheritdoc />
    public async Task<ExternalAssertion> CompleteSignInAsync(ConnectorCallback callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var context = callback.Context;
        if (context.CodeVerifier is not { Length: >= 43 and <= 128 } verifier) throw new ArgumentException("缺少 PKCE code_verifier。", nameof(callback));
        var metadata = await MetadataAsync(cancellationToken).ConfigureAwait(false);
        // RFC 9207：授权响应的 iss 与配置不符说明响应来自别的身份源（混淆攻击）；上游声明支持时它必须出现。
        var hasIssuer = callback.Parameters.TryGetValue("iss", out var issuer);
        if (hasIssuer ? !string.Equals(issuer, _options.Issuer, StringComparison.Ordinal) : metadata.RequiresIssuerParameter)
            throw Fail(IdentityErrors.ConnectorStateInvalid);
        if (!callback.Parameters.TryGetValue("code", out var code) || code.Length is 0 or > 2048) throw Fail(PlatformErrors.BadGateway);
        var idToken = await RedeemAsync(metadata, code, context.CallbackUri, verifier, cancellationToken).ConfigureAwait(false);
        var token = await ValidateAsync(idToken, cancellationToken).ConfigureAwait(false);
        if (!token.TryGetPayloadValue<string>("nonce", out var nonce) || !FixedTimeEquals(nonce, context.Nonce)) throw Fail(IdentityErrors.ConnectorStateInvalid);
        // aud 有多个值时 azp 必须是本客户端；给了 azp 也必须是本客户端。
        var audiences = token.Audiences.ToArray();
        token.TryGetPayloadValue<string>("azp", out var azp);
        if (azp is not null ? azp != _options.ClientId : audiences.Length > 1) throw Fail(PlatformErrors.BadGateway);
        if (token.Subject is not { Length: > 0 and <= 255 } subject || subject.Any(char.IsControl)) throw Fail(PlatformErrors.BadGateway);
        return new ExternalAssertion(_options.Issuer, subject, null, Profile(token), Amr(token));
    }

    // ---- 换码与校验 ----

    private async Task<string> RedeemAsync(Metadata metadata, string code, Uri redirectUri, string verifier, CancellationToken ct)
    {
        using var secret = await _secrets.ResolveAsync(_options.ClientSecret, ct).ConfigureAwait(false);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = redirectUri.AbsoluteUri, ["code_verifier"] = verifier
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, metadata.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (metadata.BasicAuthentication)
        {
            // RFC 6749 §2.3.1：Basic 认证前先对 client_id 与 secret 各自做表单编码。
            var pair = FormEncode(_options.ClientId) + ":" + FormEncode(secret.RevealUtf8());
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(pair)));
        }
        else { form["client_id"] = _options.ClientId; form["client_secret"] = secret.RevealUtf8(); }
        request.Content = new FormUrlEncodedContent(form);
        using var response = await SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK) throw Fail(PlatformErrors.BadGateway);
        using var document = await ReadJsonAsync(response, ct).ConfigureAwait(false);
        return document.RootElement.TryGetProperty("id_token", out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } idToken
            ? idToken : throw Fail(PlatformErrors.BadGateway);
    }

    private async Task<JsonWebToken> ValidateAsync(string idToken, CancellationToken ct)
    {
        var keys = await KeysAsync(false, ct).ConfigureAwait(false);
        var result = await _tokens.ValidateTokenAsync(idToken, Parameters(keys)).ConfigureAwait(false);
        // 上游轮换了签名密钥：刷新一次 JWKS 再验（刷新有最小间隔，伪造的 kid 不能逼它反复拉取；没刷新就不重验）。
        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException &&
            await KeysAsync(true, ct).ConfigureAwait(false) is var refreshed && !ReferenceEquals(refreshed, keys))
            result = await _tokens.ValidateTokenAsync(idToken, Parameters(refreshed)).ConfigureAwait(false);
        return result is { IsValid: true, SecurityToken: JsonWebToken token } ? token : throw Fail(PlatformErrors.BadGateway);
    }

    private TokenValidationParameters Parameters(Keys keys) => new()
    {
        ValidIssuer = _options.Issuer, ValidateIssuer = true, ValidAudience = _options.ClientId, ValidateAudience = true,
        IssuerSigningKeys = keys.Values, ValidAlgorithms = SigningAlgorithms, RequireSignedTokens = true, ValidateLifetime = true, RequireExpirationTime = true,
        ClockSkew = ClockSkew, ValidateIssuerSigningKey = false, TryAllIssuerSigningKeys = true, LifetimeValidator = Lifetime
    };

    // 有效期按注入的时钟判断（容差 2 分钟）；exp 必须出现。
    private bool Lifetime(DateTime? notBefore, DateTime? expires, SecurityToken token, TokenValidationParameters parameters)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        return expires is { } exp && now <= exp.ToUniversalTime() + ClockSkew && (notBefore is not { } nbf || now + ClockSkew >= nbf.ToUniversalTime());
    }

    private static JsonElement Profile(JsonWebToken token)
    {
        var profile = new JsonObject();
        foreach (var name in (string[])["name", "preferred_username", "email"])
            if (token.TryGetPayloadValue<string>(name, out var value) && value is { Length: > 0 and <= 320 } && !value.Any(char.IsControl)) profile[name] = value;
        if (token.TryGetPayloadValue<bool>("email_verified", out var verified)) profile["email_verified"] = verified;
        return JsonSerializer.SerializeToElement(profile);
    }

    private static string[] Amr(JsonWebToken token)
    {
        if (!token.TryGetPayloadValue<string[]>("amr", out var values) || values is null) return [];
        return [.. values.Where(value => value is { Length: > 0 and <= 32 } && value.All(char.IsAsciiLetterOrDigit)).Distinct(StringComparer.Ordinal).Take(8)];
    }

    // ---- 发现文档与公钥 ----

    private sealed record Metadata(Uri Authorization, Uri Token, Uri Jwks, bool BasicAuthentication, bool RequiresIssuerParameter, DateTimeOffset ExpiresAt);

    private sealed record Keys(IReadOnlyList<SecurityKey> Values, DateTimeOffset LoadedAt, DateTimeOffset ExpiresAt);

    private async Task<Metadata> MetadataAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref _metadata) is { } cached && cached.ExpiresAt > _clock.GetUtcNow()) return cached;
        await _refresh.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_metadata is { } current && current.ExpiresAt > _clock.GetUtcNow()) return current;
            var loaded = await LoadMetadataAsync(ct).ConfigureAwait(false);
            Volatile.Write(ref _metadata, loaded);
            return loaded;
        }
        finally { _refresh.Release(); }
    }

    private async Task<Metadata> LoadMetadataAsync(CancellationToken ct)
    {
        var discovery = new Uri(_options.Issuer.TrimEnd('/') + "/.well-known/openid-configuration");
        using var request = new HttpRequestMessage(HttpMethod.Get, discovery);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK) throw Fail(PlatformErrors.BadGateway);
        using var document = await ReadJsonAsync(response, ct).ConfigureAwait(false);
        var root = document.RootElement;
        // 发现文档的 issuer 必须与配置逐字一致（OIDC Discovery §4.3），端点必须满足与 issuer 相同的传输要求。
        if (root.ValueKind != JsonValueKind.Object || Text(root, "issuer") != _options.Issuer) throw Fail(PlatformErrors.BadGateway);
        var authorization = Endpoint(root, "authorization_endpoint"); var token = Endpoint(root, "token_endpoint"); var jwks = Endpoint(root, "jwks_uri");
        if (Strings(root, "code_challenge_methods_supported") is { } methods && !methods.Contains("S256")) throw Fail(PlatformErrors.BadGateway);
        // 缺省的客户端认证是 client_secret_basic（OIDC Discovery §3）；只声明了 post 时才放进表单。
        var auth = Strings(root, "token_endpoint_auth_methods_supported");
        var basic = auth is null || auth.Contains("client_secret_basic") || !auth.Contains("client_secret_post");
        var requiresIssuer = root.TryGetProperty("authorization_response_iss_parameter_supported", out var flag) && flag.ValueKind == JsonValueKind.True;
        return new Metadata(authorization, token, jwks, basic, requiresIssuer, _clock.GetUtcNow() + MetadataLifetime);
    }

    private Uri Endpoint(JsonElement root, string name) =>
        Text(root, name) is { } value && OidcConnectorOptions.IsAllowedEndpoint(value, _options.RequireHttps, out var uri) ? uri! : throw Fail(PlatformErrors.BadGateway);

    private async Task<Keys> KeysAsync(bool refresh, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        if (!refresh && Volatile.Read(ref _keys) is { } cached && cached.ExpiresAt > now) return cached;
        var metadata = await MetadataAsync(ct).ConfigureAwait(false);
        await _refresh.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 强制刷新也有最小间隔；等锁期间别的请求可能已经刷新过。
            if (_keys is { } current && (refresh ? now - current.LoadedAt < KeyRefreshInterval : current.ExpiresAt > now)) return current;
            using var request = new HttpRequestMessage(HttpMethod.Get, metadata.Jwks);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await SendAsync(request, ct).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK) throw Fail(PlatformErrors.BadGateway);
            using var document = await ReadJsonAsync(response, ct).ConfigureAwait(false);
            JsonWebKeySet set;
            try { set = new JsonWebKeySet(document.RootElement.GetRawText()) { SkipUnresolvedJsonWebKeys = true }; }
            catch (Exception error) when (error is ArgumentException or JsonException) { throw Fail(PlatformErrors.BadGateway); }
            // 只留非对称公钥：对称（oct）密钥即便出现在 JWKS 里也不参与验签，HS 族签名因此无从通过。
            var values = set.GetSigningKeys().Where(key => key is RsaSecurityKey or ECDsaSecurityKey or X509SecurityKey).ToArray();
            if (values.Length == 0) throw Fail(PlatformErrors.BadGateway);
            var loaded = new Keys(values, _clock.GetUtcNow(), _clock.GetUtcNow() + MetadataLifetime);
            Volatile.Write(ref _keys, loaded);
            return loaded;
        }
        finally { _refresh.Release(); }
    }

    // ---- HTTP ----

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try { return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false); }
        catch (HttpRequestException) { throw Fail(PlatformErrors.BadGateway); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw Fail(PlatformErrors.GatewayTimeout); }
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength is > MaxResponseBytes) throw Fail(PlatformErrors.BadGateway);
        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaxResponseBytes) throw Fail(PlatformErrors.BadGateway);
                buffer.Write(chunk, 0, read);
            }
            try { return JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 }); }
            catch (JsonException) { throw Fail(PlatformErrors.BadGateway); }
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string[]? Strings(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)] : null;

    private static string FormEncode(string value) => WebUtility.UrlEncode(value);

    private static bool FixedTimeEquals(string? left, string right) =>
        left is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

    private static QianErrorException Fail(ErrorCode code) => new(new QianError(code));
}
