using System.Text.Json;
using Kun.Security.Authentication;
using Kun.Security.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Identity.Abstractions;

namespace Qian.Sample.Crm;

// 只消费认证事件已验证的 SecurityToken；请求头、正文与客户端提供的布尔值均不是身份来源。
// 本宿主（受众 qian.sample.crm）验签只接受服务与 Agent 两类机器主体：Agent 只用于验证能力目录与执行入口的过滤（S1 的 RBAC 不授予 Agent 权限，REST 端点仍只收服务主体）。
// 同进程的平台表面宿主（受众 qian-platform）借用本宿主的运行时，它验证过的主体经 AcceptPlatformSurface 交进同一个快照位（S1-YUE-015 裁决 A1）：
// 经通用执行入口调用的能力在本应用的命令管道里以调用者本人执行，user 也不例外，由命令 PEP 按其角色判定、审计记 actor_type = user。
// 任何带 act 的委托令牌都拒绝。
internal sealed class CrmCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private static readonly object Key = new();
    private readonly JwtCurrentUser _current = new(accessor);
    public Principal? Principal => (accessor.HttpContext?.Items[Key] as Snapshot)?.Principal;
    public string? UserId => _current.UserId;
    public IReadOnlyList<string> Roles => _current.Roles;
    // user 只认平台表面交来的快照：本宿主自己的验签从不放行 user，这里再挡一道。
    public bool IsAuthenticated => _current.IsAuthenticated && accessor.HttpContext?.Items[Key] is Snapshot { Principal: { Actor: null } snapshot } entry &&
        (snapshot.Kind is PrincipalKind.Service or PrincipalKind.Agent || snapshot.Kind == PrincipalKind.User && entry.PlatformSurface) &&
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
            context.HttpContext.Items[Key] = new Snapshot(principal, PlatformSurface: false);
        }
        catch (JsonException) { context.Fail("身份快照无效。"); }
    }

    // 平台表面宿主在它自己的认证事件里交来的已验证主体（受众 qian-platform，验签与快照一致性由它负责）；只收 user / service / agent，带 act 的不收。
    internal static void AcceptPlatformSurface(HttpContext http, Principal principal)
    {
        if (principal is { Kind: PrincipalKind.User or PrincipalKind.Service or PrincipalKind.Agent, Actor: null }) http.Items[Key] = new Snapshot(principal, PlatformSurface: true);
    }

    private static string KindName(PrincipalKind kind) => kind switch { PrincipalKind.User => "user", PrincipalKind.Agent => "agent", _ => "service" };

    private sealed record Snapshot(Principal Principal, bool PlatformSurface);
}
