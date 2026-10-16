using Kun.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Authz.AspNetCore;

/// <summary>授权的认证 HTTP 表面（PRD 7.5 API 行）；含 me/permissions 与数据权限 meta，其余判定与管理端点归 S1-AZ-005。</summary>
public static class AuthzEndpoints
{
    /// <summary>
    /// 登记 <c>GET /api/authz/me/permissions</c>：列出当前已验证主体经角色授权持有的权限点及其风险、step-up、终端元数据与当前判定。
    /// 同时登记 <c>GET /api/authz/meta</c>，发布同源数据范围/过滤/系统变量元数据；不返回实际授予。
    /// 宿主须先配置身份、租户与 ErrorDetail 中间件；source 返回可注入 <c>Authz</c> 的插件上下文。主体与租户只来自已认证请求，不读请求参数。
    /// </summary>
    public static IEndpointRouteBuilder MapQianAuthz(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source);
        endpoints.MapGet("/api/authz/me/permissions", async (HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            var user = http.RequestServices.GetRequiredService<ICurrentUser>();
            var principal = user.Principal;
            if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject) return QianResults.Error(new QianError(PlatformErrors.Unauthenticated));
            // S1 不接受委托令牌与 Agent 主体，不能把未实现的委托解析静默当成普通调用。
            if (principal.Actor is not null || principal.Kind is not (PrincipalKind.User or PrincipalKind.Service)) return QianResults.Error(new QianError(PlatformErrors.AuthzDenied));
            var subject = new AuthorizationSubject(principal.Subject, principal.TenantId, principal.Kind == PrincipalKind.User ? "user" : "service");
            try
            {
                var grants = await source(http).Authz().ListEffectivePermissionsAsync(subject, http.RequestAborted).ConfigureAwait(false);
                return Results.Json(new AuthzPermissionList(Array.AsReadOnly(grants.Select(grant => new AuthzPermissionView(grant.Definition.Code,
                    grant.Definition.PluginId + ":" + grant.Definition.Resource, grant.Definition.Action, grant.Definition.Risk, grant.Definition.RequiresStepUp,
                    grant.Definition.Terminals, grant.Allowed, grant.Reason)).ToArray())), AuthzHttpJson.Default.AuthzPermissionList);
            }
            catch (QianErrorException error) { return QianResults.Error(error.Error); }
            // 授权组件尚未就绪或已卸载时与不存在无法区分。
            catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException) { return QianResults.Error(new QianError(PlatformErrors.NotFound)); }
        }).RequireAuthorization().Produces<AuthzPermissionList>();
        endpoints.MapGet("/api/authz/meta", (HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            var user = http.RequestServices.GetRequiredService<ICurrentUser>(); var principal = user.Principal;
            if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject) return QianResults.Error(new QianError(PlatformErrors.Unauthenticated));
            if (principal.Actor is not null || principal.Kind is not (PrincipalKind.User or PrincipalKind.Service)) return QianResults.Error(new QianError(PlatformErrors.AuthzDenied));
            try
            {
                _ = source(http).Authz(); // 必须有当前 scope 可见且仍挂载的真实 Provider。
                return Results.Json(DataScopeContracts.Metadata, AuthzHttpJson.Default.JsonElement);
            }
            catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException) { return QianResults.Error(new QianError(PlatformErrors.NotFound)); }
        }).RequireAuthorization();
        return endpoints;
    }
}

/// <summary>me/permissions 的 HTTP 信封；条目按权限点序数排序。</summary>
public sealed record AuthzPermissionList(IReadOnlyList<AuthzPermissionView> Items);

/// <summary>
/// 主体持有的一个权限点：resource 为组件前缀的资源类型；allowed / reason 与同一请求上下文下的 Check 一致，需要 step-up 或终端证明时 allowed 为 false。
/// </summary>
public sealed record AuthzPermissionView(string Permission, string Resource, string Action, PermissionRisk Risk, bool StepUp, IReadOnlyList<string> Terminals,
    bool Allowed, string Reason);
