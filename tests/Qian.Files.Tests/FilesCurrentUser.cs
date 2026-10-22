using System.Text.Json;
using Kun.Security.Authentication;
using Kun.Security.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Identity.Abstractions;

namespace Qian.Files.Tests;

// 与参考宿主的 CrmCurrentUser 同一做法：只消费认证事件已验证的令牌，主体快照取自令牌载荷；验收只用服务主体，带 act 的委托令牌一律拒绝。
internal sealed class FilesCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private static readonly object Key = new();
    private readonly JwtCurrentUser _current = new(accessor);
    public Principal? Principal => accessor.HttpContext?.Items[Key] as Principal;
    public string? UserId => _current.UserId;
    public IReadOnlyList<string> Roles => _current.Roles;
    public bool IsAuthenticated => _current.IsAuthenticated && Principal is { Kind: PrincipalKind.Service } snapshot &&
        _current.UserId == snapshot.Subject && _current.PrincipalKind == "service";

    internal static void Capture(TokenValidatedContext context)
    {
        try
        {
            if (context.SecurityToken is not JsonWebToken token || !PrincipalClaims.TryRead(context.Principal, out var subject, out var tenant, out var kind) || kind != "service")
            { context.Fail("无效机器身份。"); return; }
            using var document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload));
            var principal = document.RootElement.Deserialize(IdentityJson.PrincipalTypeInfo);
            if (principal is null || principal.Kind != PrincipalKind.Service || principal.Subject != subject || principal.TenantId != tenant || principal.Actor is not null)
            { context.Fail("身份快照不一致。"); return; }
            context.HttpContext.Items[Key] = principal;
        }
        catch (JsonException) { context.Fail("身份快照无效。"); }
    }
}
