using System.Text.Json;
using Kun.Security.Authentication;
using Kun.Security.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Identity.Abstractions;

namespace Qian.Sample.Crm;

// 只消费认证事件已验证的 SecurityToken；请求头、正文与客户端提供的布尔值均不是身份来源。
// 接受服务与 Agent 两类机器主体：Agent 只用于验证能力目录与执行入口的过滤（S1 的 RBAC 不授予 Agent 权限，REST 端点仍只收服务主体）；
// 任何带 act 的委托令牌都拒绝。
internal sealed class CrmCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private static readonly object Key = new();
    private readonly JwtCurrentUser _current = new(accessor);
    public Principal? Principal => accessor.HttpContext?.Items[Key] as Principal;
    public string? UserId => _current.UserId;
    public IReadOnlyList<string> Roles => _current.Roles;
    public bool IsAuthenticated => _current.IsAuthenticated && Principal is { Kind: PrincipalKind.Service or PrincipalKind.Agent } snapshot &&
        _current.UserId == snapshot.Subject && _current.PrincipalKind == KindName(snapshot.Kind);

    internal static void Capture(TokenValidatedContext context)
    {
        try
        {
            if (context.SecurityToken is not JsonWebToken token || !PrincipalClaims.TryRead(context.Principal, out var subject, out var tenant, out var kind) || kind is not ("service" or "agent"))
            { context.Fail("无效机器身份。"); return; }
            using var document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != root.EnumerateObject().Count())
            { context.Fail("身份字段重复或无效。"); return; }
            var principal = root.Deserialize(IdentityJson.PrincipalTypeInfo);
            if (principal is null || principal.Kind is not (PrincipalKind.Service or PrincipalKind.Agent) || KindName(principal.Kind) != kind ||
                principal.Subject != subject || principal.TenantId != tenant || principal.Actor is not null)
            { context.Fail("身份快照不一致。"); return; }
            context.HttpContext.Items[Key] = principal;
        }
        catch (JsonException) { context.Fail("身份快照无效。"); }
    }

    private static string KindName(PrincipalKind kind) => kind == PrincipalKind.Agent ? "agent" : "service";
}
