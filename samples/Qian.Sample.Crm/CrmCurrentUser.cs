using System.Text.Json;
using Kun.Security.Authentication;
using Kun.Security.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Identity.Abstractions;

namespace Qian.Sample.Crm;

// 只消费认证事件已验证的 SecurityToken；请求头、正文与客户端提供的布尔值均不是身份来源。
internal sealed class CrmCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
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
            { context.Fail("无效服务身份。"); return; }
            using var document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != root.EnumerateObject().Count())
            { context.Fail("身份字段重复或无效。"); return; }
            var principal = root.Deserialize(IdentityJson.PrincipalTypeInfo);
            if (principal is null || principal.Kind != PrincipalKind.Service || principal.Subject != subject || principal.TenantId != tenant || principal.Actor is not null)
            { context.Fail("身份快照不一致。"); return; }
            context.HttpContext.Items[Key] = principal;
        }
        catch (JsonException) { context.Fail("身份快照无效。"); }
    }
}
