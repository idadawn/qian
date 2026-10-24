using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Authz.Abstractions;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Authz.AspNetCore;

// HTTP 入口共用：从已验证请求取主体（只信 ICurrentUser 的快照，不读请求参数），解析执行端口，按入口描述执行一次判定。
internal static class HttpEnforcement
{
    private static readonly object FailureKey = new();

    // 主体不可信时返回要写出的错误；返回 null 表示 subject 可用。
    internal static QianError? ReadSubject(HttpContext http, out EnforcementSubject subject)
    {
        subject = null!;
        var user = http.RequestServices.GetRequiredService<ICurrentUser>(); var principal = user.Principal;
        if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject || http.User.Identity?.IsAuthenticated != true ||
            !Kun.Security.Authentication.PrincipalClaims.TryRead(http.User, out var claimSubject, out var claimTenant, out var claimKind) ||
            claimSubject != principal.Subject || claimTenant != principal.TenantId || claimKind != Kind(principal.Kind))
            return new QianError(PlatformErrors.Unauthenticated);
        subject = new EnforcementSubject(principal.Subject, principal.TenantId, Kind(principal.Kind), principal.IdentityRevision, principal.Actor is not null);
        return null;
    }

    internal static async ValueTask<AuthorizationDecision> EnforceAsync(HttpContext http, AuthzEnforcementSource source, EnforcementSubject subject, string? permission, string entryPoint, string target)
    {
        IAuthzEnforcement enforcement;
        try { enforcement = source.Source(http).Enforcement(); }
        // 授权组件尚未就绪或已卸载时与不存在无法区分（与 me/permissions、T23 的 dependency-removed 一致）。
        catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException or KeyNotFoundException) { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
        return await enforcement.EnforceAsync(new EnforcementRequest(subject, permission, null, "api", entryPoint, target), http.RequestAborted).ConfigureAwait(false);
    }

    // 没有单一权限点的已认证入口只比对 identity_revision（S1-YUE-012）；执行端口取不到时与 EnforceAsync 同一映射（404）。
    internal static async ValueTask<AuthorizationDecision?> CheckRevisionAsync(HttpContext http, AuthzEnforcementSource source, EnforcementSubject subject, string target)
    {
        IAuthzEnforcement enforcement;
        try { enforcement = source.Source(http).Enforcement(); }
        catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException or KeyNotFoundException) { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
        return await enforcement.CheckIdentityRevisionAsync(new EnforcementRequest(subject, null, null, "api", AuthzEnforcement.HttpEntryPoint, target), http.RequestAborted).ConfigureAwait(false);
    }

    internal static string Target(HttpContext http) => http.Request.Method + " " + ((http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? http.Request.Path.Value ?? "/");
    internal static string Kind(PrincipalKind kind) => kind switch { PrincipalKind.User => "user", PrincipalKind.Service => "service", _ => "agent" };
    internal static void Fail(HttpContext http, QianError error) => http.Items[FailureKey] = error;
    internal static QianError? Failure(HttpContext http) => http.Items.TryGetValue(FailureKey, out var value) ? value as QianError : null;
}

/// <summary>ASP.NET Core 授权处理器：对 <c>RequireQianPermission</c> 声明的每个权限点执行一次判定。</summary>
internal sealed class QianPermissionHandler(AuthzEnforcementSource source) : AuthorizationHandler<QianPermissionRequirement, HttpContext>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, QianPermissionRequirement requirement, HttpContext http)
    {
        if (HttpEnforcement.ReadSubject(http, out var subject) is { } unauthenticated) { Deny(context, http, unauthenticated, "unauthenticated"); return; }
        try
        {
            var decision = await HttpEnforcement.EnforceAsync(http, source, subject, requirement.Permission, AuthzEnforcement.HttpEntryPoint, HttpEnforcement.Target(http)).ConfigureAwait(false);
            if (decision.Allow) context.Succeed(requirement);
            else Deny(context, http, new QianError(Denial(decision)), decision.Reason);
        }
        catch (QianErrorException error) { Deny(context, http, error.Error, error.Error.Code.Code); }
    }
    // 审计组件已卸载时整个入口不可用，与不存在同为 404（S1-AUD-001）；旧令牌（identity_revision 低于下限）401，客户端据此换令牌（S1-DEC-027）；
    // 其余拒绝 403。gRPC 拦截器按同一映射给出 NotFound / Unauthenticated / PermissionDenied。
    internal static ErrorCode Denial(AuthorizationDecision decision) => decision.Reason switch
    {
        AuthzEnforcement.AuditUnavailable => PlatformErrors.NotFound,
        AuthzEnforcement.StaleIdentityRevision => PlatformErrors.Unauthenticated,
        _ => PlatformErrors.AuthzDenied
    };
    private static void Deny(AuthorizationHandlerContext context, HttpContext http, QianError error, string reason)
    {
        HttpEnforcement.Fail(http, error);
        context.Fail(new AuthorizationFailureReason(null!, reason));
    }
}

/// <summary>授权失败写成 ErrorDetail；没有 PEP 记录的失败交给默认处理器（Challenge / Forbid）。</summary>
internal sealed class QianAuthzResultHandler(AuthorizationMiddlewareResultHandler fallback) : IAuthorizationMiddlewareResultHandler
{
    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Succeeded && HttpEnforcement.Failure(context) is { } error && !context.Response.HasStarted)
        {
            context.Response.Headers.CacheControl = "private, no-store";
            return QianResults.Error(error).ExecuteAsync(context);
        }
        return fallback.HandleAsync(next, context, policy, authorizeResult);
    }
}

/// <summary>
/// 没有声明权限点的已认证端点：先比对令牌 identity_revision，旧令牌 401（处理器内判定的平台表面与只要求已认证的端点，S1-YUE-012）；
/// 其中带组件端点绑定的，生产模式缺省拒绝并记录，开发模式告警放行；显式标记 <see cref="PermissionEnforcedInHandlerAttribute"/> 的端点不在缺省拒绝范围内。
/// 声明了权限点的端点在 <see cref="QianPermissionHandler"/> 的判定里已比对修订，匿名端点不比对。
/// </summary>
internal sealed partial class UndeclaredPermissionMiddleware(RequestDelegate next, AuthzEnforcementSource source, ILogger<UndeclaredPermissionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null || endpoint.Metadata.GetOrderedMetadata<Kun.Authorization.RequiresPermissionAttribute>().Count != 0)
        { await next(context).ConfigureAwait(false); return; }
        if (context.User.Identity?.IsAuthenticated == true && HttpEnforcement.ReadSubject(context, out var current) is null && current.TenantId is not null && current.IdentityRevision is not null &&
            await HttpEnforcement.CheckRevisionAsync(context, source, current, HttpEnforcement.Target(context)).ConfigureAwait(false) is { } stale)
        {
            context.Response.Headers.CacheControl = "private, no-store";
            throw new QianErrorException(new QianError(QianPermissionHandler.Denial(stale)));
        }
        if (endpoint.Metadata.GetMetadata<PluginEndpointBinding>() is null || endpoint.Metadata.GetMetadata<PermissionEnforcedInHandlerAttribute>() is not null)
        { await next(context).ConfigureAwait(false); return; }
        var target = HttpEnforcement.Target(context);
        if (!source.IsProduction(context.RequestServices)) { LogUndeclared(logger, target); await next(context).ConfigureAwait(false); return; }
        if (HttpEnforcement.ReadSubject(context, out var subject) is { } unauthenticated) throw new QianErrorException(unauthenticated);
        var decision = await HttpEnforcement.EnforceAsync(context, source, subject, null, AuthzEnforcement.HttpEntryPoint, target).ConfigureAwait(false);
        if (!decision.Allow) throw new QianErrorException(new QianError(QianPermissionHandler.Denial(decision)));
        await next(context).ConfigureAwait(false);
    }
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "组件端点 {Target} 没有声明权限点；开发模式放行，生产模式将缺省拒绝。")]
    private static partial void LogUndeclared(ILogger logger, string target);
}
