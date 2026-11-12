using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Identity.Server;

// 目录标准能力共用的调用方校验（S1-IDP-009 禁用、S1-IDP-017 新增）。主体、租户与会话只取自宿主认证后的 ICurrentUser 与服务端会话，
// 输入里没有这些字段。依次核对：租户作用域、主体快照、真实权限点、服务端会话与身份修订、本租户管理员资格；
// 只有权限而不是 Identity 管理员的用户仍被拒绝。S1 不执行 Agent 委托，带 act 的主体一律拒绝。
internal static class DirectoryCapabilityGuard
{
    internal static async Task<(Guid Tenant, Guid Person)> AuthorizeAsync(IPluginContext caller, IdentityAccountService accounts, string permission, CancellationToken ct)
    {
        var services = caller.Services.GetService<IHttpContextAccessor>()?.HttpContext?.RequestServices ?? caller.Services;
        var user = services.GetService<ICurrentUser>(); var principal = user?.Principal;
        var tenant = services.GetRequiredService<ITenantContext>().TenantId;
        if (string.IsNullOrWhiteSpace(tenant) || caller.Scope.Kind != "tenant" || caller.Scope.Id != tenant) throw Missing();
        if (user is not { IsAuthenticated: true } || principal is not { Kind: PrincipalKind.User, Actor: null } || user.UserId != principal.Subject ||
            principal.TenantId != tenant ||
            !Guid.TryParseExact(tenant, "D", out var tenantId) || !Guid.TryParseExact(principal.Subject, "D", out var personId) ||
            !Guid.TryParseExact(principal.MembershipId, "D", out var memberId) || !Guid.TryParseExact(principal.SessionId, "D", out var sessionId) || principal.IdentityRevision is not { } revision)
            throw Denied();
        var permissions = services.GetService<IPermissionChecker>() ?? throw Denied();
        if (!(await permissions.CheckAsync(new PermissionRequest(new AuthorizationSubject(principal.Subject, tenant, "user"), permission), ct).ConfigureAwait(false)).Allow) throw Denied();
        var validation = await accounts.ValidateAsync(sessionId, personId, memberId, ct).ConfigureAwait(false);
        if (validation is null || validation.TenantId != tenantId || validation.IdentityRevision != revision) throw Denied();
        var session = new AccountSession(personId, tenantId, memberId, sessionId, revision, [], null, default, default, "", "");
        var actor = await accounts.ResolveCallerAsync(session, ct).ConfigureAwait(false);
        if (actor is not { TenantAdministrator: true } || actor.TenantId != tenantId) throw Denied();
        return (tenantId, personId);
    }

    internal static QianErrorException Missing() => new(new QianError(PlatformErrors.NotFound));
    internal static QianErrorException Denied() => new(new QianError(PlatformErrors.AuthzDenied));
}
