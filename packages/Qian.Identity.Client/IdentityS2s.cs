using System.Text.Json;
using System.Text.RegularExpressions;
using Kun.Security.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Qian.Identity.Abstractions;

namespace Qian.Identity.Client;

/// <summary>
/// 下游服务的 S2S 校验：Kun.Security 负责验签（只收 RS256 / ES256）、issuer、受众与寿命；本类型再按冻结契约
/// （contracts/v0 s2s-claims 与 README §4）逐条检查载荷，通过后把快照放进请求项供 <see cref="ICurrentUser"/> 读取。
/// 「每跳一个」受众：aud 必须恰好是本服务，上一跳收到的令牌转发到下一跳一律拒绝；用户访问令牌（typ at+jwt、发行方不同、载荷不闭合）同样进不来。
/// </summary>
internal static partial class IdentityS2sValidation
{
    internal static void Configure(JwtBearerOptions jwt, IdentityS2sOptions options, TimeProvider clock, ILogger logger)
    {
        jwt.MapInboundClaims = false;
        jwt.TokenValidationParameters.ValidTypes = ["JWT"];
        jwt.TokenValidationParameters.RequireSignedTokens = true;
        jwt.TokenValidationParameters.RequireExpirationTime = true;
        jwt.Events ??= new JwtBearerEvents();
        var previous = jwt.Events.OnTokenValidated;
        jwt.Events.OnTokenValidated = async context =>
        {
            await previous(context).ConfigureAwait(false);
            if (context.Result is { Succeeded: false }) return;
            var reason = Check(context, options, clock);
            if (reason is null) return;
            Log.Rejected(logger, reason);
            context.Fail("S2S 令牌不符合契约。");
        };
        var failed = jwt.Events.OnAuthenticationFailed;
        jwt.Events.OnAuthenticationFailed = async context =>
        {
            var type = context.Exception.GetType().Name;
            Log.Rejected(logger, type);
            await failed(context).ConfigureAwait(false);
        };
    }

    // 返回拒绝原因的类别名；只记类别，不记令牌、主体或会话标识。
    private static string? Check(TokenValidatedContext context, IdentityS2sOptions options, TimeProvider clock)
    {
        if (context.SecurityToken is not JsonWebToken token) return "token";
        using var payload = IdentityJsonPayload.Parse(token);
        if (payload is null) return "payload";
        // 契约是闭合对象：可选字段出现即须有值，只有 tenant_id 允许显式 null。
        if (payload.RootElement.EnumerateObject().Any(property => property.Value.ValueKind == JsonValueKind.Null && property.Name != IdentityClaimNames.TenantId)) return "null";
        Principal? principal;
        try { principal = payload.RootElement.Deserialize(IdentityJson.PrincipalTypeInfo); }
        catch (JsonException) { return "shape"; }
        if (principal is null) return "shape";
        if (principal.Issuer != options.Issuer || principal.Audience is not [var audience] || audience != options.Audience) return "audience";
        if (principal.Subject is not { Length: > 0 and <= 255 } || principal.Subject.Any(char.IsControl)) return "sub";
        if (principal.Kind is not (PrincipalKind.User or PrincipalKind.Service) || principal.Actor is not null) return "principal_kind";
        if (principal.TenantId is not null && !CanonicalIdentity.Uuid(principal.TenantId)) return "tenant_id";
        if (principal.MembershipId is not null && !CanonicalIdentity.Uuid(principal.MembershipId)) return "membership_id";
        if (principal.ProjectId is not null && !CanonicalIdentity.Uuid(principal.ProjectId)) return "project_id";
        if (principal.PluginId is not null && !PluginIdPattern().IsMatch(principal.PluginId)) return "plugin_id";
        if (principal.Kind == PrincipalKind.User && (principal.IdentityRevision is null || principal.TenantId is not null && principal.MembershipId is null)) return "user";
        if (principal.Kind == PrincipalKind.Service && (principal.MembershipId is not null || principal.SessionId != principal.TokenId)) return "service";
        if (principal.IdentityRevision < 0) return "identity_revision";
        if (!CanonicalIdentity.Uuid(principal.SessionId) || !CanonicalIdentity.Uuid(principal.TokenId)) return "session_id";
        if (principal.Roles.Count > 32 || principal.Roles.Distinct(StringComparer.Ordinal).Count() != principal.Roles.Count || principal.Roles.Any(role => role is not { Length: > 0 and <= 64 })) return "roles";
        if (principal.Scope.Length > 4096 || !CanonicalIdentity.ScopePattern().IsMatch(principal.Scope)) return "scope";
        var lifetime = principal.ExpiresAt - principal.IssuedAt;
        if (principal.IssuedAt < 0 || lifetime is <= 0 or > 300 || principal.NotBefore < principal.IssuedAt || principal.NotBefore >= principal.ExpiresAt) return "lifetime";
        if (principal.IssuedAt > clock.GetUtcNow().ToUnixTimeSeconds() + 30) return "iat";
        // 授权 PEP 交叉比对 Kun 读取的声明与快照，这里先保证两者一致。
        if (!PrincipalClaims.TryRead(context.Principal, out var subject, out var tenant, out var kind) || subject != principal.Subject || tenant != principal.TenantId ||
            kind != (principal.Kind == PrincipalKind.User ? "user" : "service")) return "claims";
        context.HttpContext.Items[IdentityClientCurrentUser.PrincipalKey] = principal;
        return null;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex PluginIdPattern();

    private static partial class Log
    {
        [LoggerMessage(EventId = 30, Level = LogLevel.Information, Message = "Identity Client 拒绝 S2S 令牌；reason={Reason}。")]
        internal static partial void Rejected(ILogger logger, string reason);
    }
}
