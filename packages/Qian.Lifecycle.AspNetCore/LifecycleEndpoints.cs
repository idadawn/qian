using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Lifecycle.AspNetCore;

/// <summary>管理面装配诊断（PRD 7.2.1、S1-RT-006 复核 R76）与反向权限批准（PRD 7.18）；宿主提供已验证租户的生命周期上下文，HTTP 输入不能指定主体。</summary>
public static class LifecycleEndpoints
{
    private const string Approvals = "/api/lifecycle/tenants/{tenantId}/plugins/{pluginId}/versions/{version}/permissions";

    /// <summary>
    /// 登记 GET /api/lifecycle/diagnostics?scope=tenant:&lt;id&gt;（缺省为调用者所在租户），以及某组件版本反向权限的批准
    /// PUT / GET /api/lifecycle/tenants/{tenantId}/plugins/{pluginId}/versions/{version}/permissions（路径里的租户须与宿主已验证的租户上下文一致，请求体只有五类批准内容）。
    /// 宿主须先配置身份、租户与 ErrorDetail 中间件，并装配权限判定端口；source 返回可注入生命周期控制器的插件上下文。权限与 404 的判定全部在控制器内完成。
    /// 不挂插件端点门禁：scope 合入失败时本 scope 的注册全部收紧，诊断面仍须能说明原因。
    /// </summary>
    public static IEndpointRouteBuilder MapQianLifecycle(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source);
        endpoints.MapGet("/api/lifecycle/diagnostics", Task<IResult> (HttpContext http) => Respond(http, async (lifecycle, user) =>
        {
            var requested = http.Request.Query["scope"];
            var tenant = http.RequestServices.GetRequiredService<ITenantContext>().TenantId;
            var text = requested.Count == 0 ? "tenant:" + tenant : requested.Count == 1 ? requested[0] ?? "" : "";
            var parts = text.Split(':', 2);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0) return QianResults.Error(new QianError(PlatformErrors.NotFound));
            var items = await lifecycle.GetDiagnosticsAsync(new ScopeKey(parts[0], parts[1]), user, http.RequestAborted).ConfigureAwait(false);
            return Results.Json(new LifecycleDiagnosticList(items), LifecycleHttpJson.Default.LifecycleDiagnosticList);
        })).RequireAuthorization().Produces<LifecycleDiagnosticList>();
        endpoints.MapGet(Approvals, (HttpContext http, string tenantId, string pluginId, string version) => Respond(http, async (lifecycle, user) =>
            await lifecycle.GetPermissionApprovalAsync(tenantId, pluginId, version, user, http.RequestAborted).ConfigureAwait(false) is { } view
                ? Results.Json(view, LifecycleHttpJson.Default.PermissionApprovalView) : QianResults.Error(new QianError(PlatformErrors.NotFound))))
            .RequireAuthorization().Produces<PermissionApprovalView>();
        endpoints.MapPut(Approvals, (HttpContext http, string tenantId, string pluginId, string version) => Respond(http, async (lifecycle, user) =>
        {
            // 读不成批准内容（含未知成员，例如租户字段）时以 null 交给控制器：先判定权限，再以 INPUT_INVALID 拒绝，无权限者仍只得到 404。
            PermissionGrants? grants;
            try { grants = await JsonSerializer.DeserializeAsync(http.Request.Body, LifecycleHttpJson.Default.PermissionGrants, http.RequestAborted).ConfigureAwait(false); }
            catch (JsonException) { grants = null; }
            return Results.Json(await lifecycle.ApprovePermissionsAsync(tenantId, pluginId, version, grants, user, http.RequestAborted).ConfigureAwait(false),
                LifecycleHttpJson.Default.PermissionApprovalView);
        })).RequireAuthorization().Produces<PermissionApprovalView>();
        return endpoints;

        async Task<IResult> Respond(HttpContext http, Func<IPluginLifecycleController, ICurrentUser, Task<IResult>> handle)
        {
            http.Response.Headers.CacheControl = "private, no-store";
            try { return await handle(source(http).Lifecycle(), http.RequestServices.GetRequiredService<ICurrentUser>()).ConfigureAwait(false); }
            catch (QianErrorException error) { return QianResults.Error(error.Error); }
            // 生命周期或注册表尚未就绪、已停止时与不存在无法区分。
            catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException) { return QianResults.Error(new QianError(PlatformErrors.NotFound)); }
        }
    }
}

/// <summary>装配诊断的 HTTP 信封：先列 Registry 合入时的当前失败，再列经 Lifecycle 挂载或升级失败、已撤回的条目。</summary>
public sealed record LifecycleDiagnosticList(IReadOnlyList<LifecycleDiagnosticView> Items);
