using System.Security.Claims;
using Kun.Gateway.Identity;
using Microsoft.AspNetCore.Http;
using Qian.Identity.Abstractions;

namespace Qian.Gateway;

// 输入仅来自身份发行方验签或 Identity.Client 的规范 Cookie 会话；最后的类型/条件校验仍由唯一 Kun S2sTokenIssuer 执行。
internal sealed class GatewayPrincipalResolver(IHttpContextAccessor accessor) : IS2sPrincipalResolver
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "sub", ClaimTypes.NameIdentifier, "principal_kind", "tenant_id", "membership_id", "identity_revision",
        "scope", "roles", "role", ClaimTypes.Role, "session_id", "project_id", "plugin_id"
    };
    public Task<ClaimsPrincipal> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (principal.Identity?.IsAuthenticated != true || principal.HasClaim(claim => claim.Type == IdentityClaimNames.Actor))
            throw new InvalidOperationException("缺少已验证的 S1 主体。");
        var identity = new ClaimsIdentity(principal.Claims.Where(claim => Allowed.Contains(claim.Type)), principal.Identity.AuthenticationType);
        if (principal.Identity.AuthenticationType == GatewayHosting.ExternalScheme)
        {
            foreach (var claim in identity.FindAll(IdentityClaimNames.SessionId).ToArray()) identity.RemoveClaim(claim);
            if (identity.FindFirst(IdentityClaimNames.PrincipalKind)?.Value == "user")
            {
                var sessions = principal.FindAll(IdentityClaimNames.Sid).ToArray();
                if (sessions.Length != 1) throw new InvalidOperationException("身份令牌缺少唯一会话。");
                identity.AddClaim(new Claim(IdentityClaimNames.SessionId, sessions[0].Value));
            }
        }
        // Cookie 不能由其他宿主认证方案或请求字段伪装；这里还要求当前请求实际就是该 principal。
        if (accessor.HttpContext is { } http && !ReferenceEquals(http.User, principal))
            throw new InvalidOperationException("签发主体不属于当前已认证请求。");
        return Task.FromResult(new ClaimsPrincipal(identity));
    }
}
