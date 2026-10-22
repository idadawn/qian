using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Qian.Identity.Abstractions;

namespace Qian.Identity.Client;

/// <summary>
/// 规范身份：从已验签的访问令牌（会话或外部 Bearer）投影出网关铸 S2S 所需的字段。OIDC 的 sid 映射为 session_id，
/// 不透明的浏览器 Cookie 永不当作会话标识；字段形状与冻结 S2S 契约的条件规则一致，缺失或残缺即拒绝。
/// </summary>
internal sealed partial record CanonicalIdentity(string Subject, PrincipalKind Kind, string? TenantId, string? MembershipId, long? IdentityRevision,
    string SessionId, string Scope, IReadOnlyList<string> Amr, string? Acr, Principal Snapshot)
{
    internal const string SessionIdClaim = IdentityClaimNames.SessionId;

    // 把规范声明并进已认证的会话身份。沿用会话身份原有的主体声明（防伪令牌按同一主体标识生成与校验），租户以访问令牌为准且两边必须一致。
    internal ClaimsIdentity Merge(ClaimsIdentity session, string authenticationType)
    {
        var identity = new ClaimsIdentity(session.Claims.Where(claim => claim.Type != IdentityClaimNames.TenantId), authenticationType);
        identity.AddClaim(TenantId is null ? new Claim(IdentityClaimNames.TenantId, "", JsonClaimValueTypes.JsonNull) : new Claim(IdentityClaimNames.TenantId, TenantId));
        identity.AddClaim(new Claim(IdentityClaimNames.PrincipalKind, Snapshot.Kind == PrincipalKind.Service ? "service" : "user"));
        if (MembershipId is not null) identity.AddClaim(new Claim(IdentityClaimNames.MembershipId, MembershipId));
        if (IdentityRevision is { } revision) identity.AddClaim(new Claim(IdentityClaimNames.IdentityRevision, revision.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64));
        identity.AddClaim(new Claim(SessionIdClaim, SessionId));
        identity.AddClaim(new Claim(IdentityClaimNames.Scope, Scope));
        foreach (var method in Amr) identity.AddClaim(new Claim(IdentityClaimNames.AuthenticationMethods, method));
        if (Acr is not null) identity.AddClaim(new Claim(IdentityClaimNames.AuthenticationContext, Acr));
        return identity;
    }

    // 外部 Bearer 没有会话身份可沿用：直接以 sub 为主体。
    internal ClaimsIdentity ToIdentity(string authenticationType) =>
        Merge(new ClaimsIdentity([new Claim(IdentityClaimNames.Subject, Subject)], authenticationType), authenticationType);

    // 会话身份来自同一次登录的 id_token：主体必须相同；id_token 带租户时必须与访问令牌一致，不带（平台身份）时访问令牌也不能带。
    internal bool Matches(ClaimsIdentity session)
    {
        var subjects = session.Claims.Where(claim => claim.Type is ClaimTypes.NameIdentifier or IdentityClaimNames.Subject).Select(claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray();
        var tenants = session.FindAll(IdentityClaimNames.TenantId).Where(claim => claim.ValueType != JsonClaimValueTypes.JsonNull).Select(claim => claim.Value).ToArray();
        return subjects.Length == 1 && subjects[0] == Subject && tenants.Length <= 1 && (tenants.Length == 0 ? TenantId is null : tenants[0] == TenantId);
    }

    /// <summary>避免记录型的默认字符串化把主体、会话标识写进日志。</summary>
    public override string ToString() => "CanonicalIdentity(redacted)";

    internal static CanonicalIdentity? From(JsonWebToken token, ClaimsIdentity claims, bool sessionOnly)
    {
        if (Single(claims, IdentityClaimNames.Subject) is not { Length: > 0 and <= 255 } subject || subject.Any(char.IsControl)) return null;
        var kind = Single(claims, IdentityClaimNames.PrincipalKind) switch { "user" => PrincipalKind.User, "service" when !sessionOnly => PrincipalKind.Service, _ => (PrincipalKind?)null };
        if (kind is null || claims.HasClaim(claim => claim.Type == IdentityClaimNames.Actor)) return null;
        var tenants = claims.FindAll(IdentityClaimNames.TenantId).ToArray();
        if (tenants.Length != 1) return null;
        string? tenant = tenants[0].ValueType == JsonClaimValueTypes.JsonNull ? null : tenants[0].Value;
        if (tenant is not null && !Uuid(tenant)) return null;
        var membership = Single(claims, IdentityClaimNames.MembershipId);
        if (membership is not null && !Uuid(membership) || claims.FindAll(IdentityClaimNames.MembershipId).Count() > 1) return null;
        var revisionText = Single(claims, IdentityClaimNames.IdentityRevision);
        long? revision = revisionText is null ? null : long.TryParse(revisionText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
        if (revision < 0 || claims.FindAll(IdentityClaimNames.IdentityRevision).Count() > 1) return null;
        var jti = Single(claims, IdentityClaimNames.TokenId);
        string? session;
        if (kind == PrincipalKind.User)
        {
            // 用户：identity_revision 必填；租户身份必带成员，平台身份不带；会话取发行方的 sid（UUID）。
            if (revision is null || (tenant is null) != (membership is null)) return null;
            session = Single(claims, IdentityClaimNames.Sid);
            if (session is null || !Uuid(session) || claims.FindAll(IdentityClaimNames.Sid).Count() > 1) return null;
        }
        else
        {
            // 机器主体没有交互会话：不得带成员，session_id 取本令牌的 jti。
            if (membership is not null || jti is null || !Uuid(jti)) return null;
            session = jti;
        }
        var scopes = claims.FindAll(IdentityClaimNames.Scope).SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal).ToArray();
        var scope = string.Join(' ', scopes);
        if (scope.Length > 4096 || !ScopePattern().IsMatch(scope)) return null;
        var amr = claims.FindAll(IdentityClaimNames.AuthenticationMethods).Select(claim => claim.Value).ToArray();
        var acr = Single(claims, IdentityClaimNames.AuthenticationContext);
        if (claims.FindAll(IdentityClaimNames.AuthenticationContext).Count() > 1) return null;
        var issuedAt = Seconds(token.IssuedAt); var expires = Seconds(token.ValidTo);
        var notBefore = token.ValidFrom == DateTime.MinValue ? issuedAt : Seconds(token.ValidFrom);
        var snapshot = new Principal
        {
            Issuer = token.Issuer, Audience = [.. token.Audiences], Subject = subject, Kind = kind.Value, TenantId = tenant, MembershipId = membership,
            IdentityRevision = revision, Roles = [], Scope = scope, SessionId = session, IssuedAt = issuedAt, NotBefore = notBefore, ExpiresAt = expires,
            TokenId = jti ?? session
        };
        return new CanonicalIdentity(subject, kind.Value, tenant, membership, revision, session, scope, amr, acr, snapshot);
    }

    internal static bool Uuid(string value) => UuidPattern().IsMatch(value);
    private static long Seconds(DateTime value) => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds();

    private static string? Single(ClaimsIdentity claims, string type)
    {
        var values = claims.FindAll(type).ToArray();
        return values.Length == 1 && !string.IsNullOrWhiteSpace(values[0].Value) && values[0].ValueType != JsonClaimValueTypes.JsonNull ? values[0].Value : null;
    }

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.CultureInvariant)]
    private static partial Regex UuidPattern();
    // RFC 6749 §3.3 的 scope-token，与冻结 S2S 契约的 scope 模式一致。
    [GeneratedRegex(@"^([\x21\x23-\x5B\x5D-\x7E]+( [\x21\x23-\x5B\x5D-\x7E]+)*)?$", RegexOptions.CultureInvariant)]
    internal static partial Regex ScopePattern();
}

/// <summary>按发行方 JWKS 校验访问令牌：签名、issuer、受众（Resource）、类型 at+jwt 与寿命，再投影规范身份。</summary>
internal sealed partial class IdentityAccessTokens(IdentityClientRuntime runtime, TimeProvider clock, ILogger<IdentityAccessTokens> logger)
{
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };

    internal async ValueTask<CanonicalIdentity?> ValidateAsync(string token, bool sessionOnly, CancellationToken ct)
    {
        var audience = runtime.Options.Resource ?? throw new InvalidOperationException("未配置 Resource 时不校验访问令牌。");
        for (var attempt = 0; ; attempt++)
        {
            OpenIdConnectConfiguration metadata;
            try { metadata = await runtime.Metadata.GetConfigurationAsync(ct).ConfigureAwait(false); }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                IdentityClientRuntime.Log.MetadataUnavailable(logger, error.GetType().Name);
                return null;
            }
            if (!string.Equals(metadata.Issuer?.TrimEnd('/'), runtime.Options.Issuer, StringComparison.Ordinal)) return null;
            var now = clock.GetUtcNow().UtcDateTime;
            var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = metadata.Issuer, ValidateAudience = true, ValidAudience = audience,
                ValidateIssuerSigningKey = true, RequireSignedTokens = true, IssuerSigningKeys = metadata.SigningKeys,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256], ValidTypes = ["at+jwt"],
                RequireExpirationTime = true, ValidateLifetime = true, ClockSkew = Skew,
                LifetimeValidator = (notBefore, expires, _, _) => expires is { } exp && exp > now - Skew && (notBefore is null || notBefore <= now + Skew)
            }).ConfigureAwait(false);
            if (result.IsValid && result.SecurityToken is JsonWebToken jwt)
                return CanonicalIdentity.From(jwt, result.ClaimsIdentity, sessionOnly) ?? Rejected("claims");
            // 发行方轮换了签名密钥：强制刷新一次元数据后重试（刷新间隔由 ConfigurationManager 限流）。
            if (attempt == 0 && result.Exception is SecurityTokenSignatureKeyNotFoundException) { runtime.Metadata.RequestRefresh(); continue; }
            return Rejected(result.Exception?.GetType().Name ?? "invalid");
        }
    }

    private CanonicalIdentity? Rejected(string reason)
    {
        Log.TokenRejected(logger, reason);
        return null;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Identity Client 拒绝访问令牌；reason={Reason}。")]
        internal static partial void TokenRejected(ILogger logger, string reason);
    }
}

internal static class IdentityJsonPayload
{
    // 已验签 JWT 的原始载荷：重复字段视为篡改（System.Text.Json 默认取最后一个，所以必须先查）。
    internal static JsonDocument? Parse(JsonWebToken token)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload)); }
        catch (Exception error) when (error is JsonException or FormatException or ArgumentException) { return null; }
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() == root.EnumerateObject().Count())
            return document;
        document.Dispose();
        return null;
    }
}
