using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Registry.AspNetCore;

/// <summary>管理面 scope 视图（PRD 7.2.1、组件规格 §9.5）；宿主提供已验证租户的注册表上下文，HTTP 输入不能指定主体或租户。</summary>
public static class RegistryEndpoints
{
    private const string KernelPrefix = "/api/kernel";

    /// <summary>
    /// 登记 scope 视图与 v1 别名；宿主须先配置身份、租户与 ErrorDetail 中间件，并装配权限判定端口。source 返回可注入注册表的插件上下文，
    /// scope 由路径给出、由注册表按已验证租户判定。不挂插件端点门禁：scope 合入失败时本 scope 的注册全部收紧，诊断面仍须能说明原因。
    /// </summary>
    public static IEndpointRouteBuilder MapQianRegistry(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source);
        endpoints.MapGet("/api/registry/scopes/{scopeKey}/entries", (HttpContext http, string scopeKey) => List(http, scopeKey, false))
            .RequireAuthorization().Produces<RegistryEntryList>();
        endpoints.MapGet(KernelPrefix + "/scopes/{scopeKey}/entries", (HttpContext http, string scopeKey) => List(http, scopeKey, true))
            .RequireAuthorization().Produces<RegistryEntryList>();
        endpoints.MapGet("/api/registry/scopes/{scopeKey}/entries/{entryId}", (HttpContext http, string scopeKey, string entryId) => Entry(http, scopeKey, entryId, false))
            .RequireAuthorization().Produces<RegistryEntryView>();
        endpoints.MapGet(KernelPrefix + "/scopes/{scopeKey}/entries/{entryId}", (HttpContext http, string scopeKey, string entryId) => Entry(http, scopeKey, entryId, true))
            .RequireAuthorization().Produces<RegistryEntryView>();
        return endpoints;

        Task<IResult> List(HttpContext http, string scopeKey, bool alias) => Respond(http, alias, async (registry, scope, user) =>
            Results.Json(new RegistryEntryList(await registry.GetScopeViewAsync(scope, user, http.RequestAborted).ConfigureAwait(false)),
                RegistryHttpJson.Default.RegistryEntryList), scopeKey);

        Task<IResult> Entry(HttpContext http, string scopeKey, string entryId, bool alias) => Respond(http, alias, async (registry, scope, user) =>
            Results.Json(await registry.GetScopeEntryAsync(scope, entryId, user, http.RequestAborted).ConfigureAwait(false),
                RegistryHttpJson.Default.RegistryEntryView), scopeKey);

        // 403 原因码与 404 都以结果返回：异常处理器会清空响应头，别名的 Deprecation 与 Link 必须随错误响应一起写出。
        async Task<IResult> Respond(HttpContext http, bool alias, Func<IRegistryService, ScopeKey, ICurrentUser, Task<IResult>> read, string scopeKey)
        {
            http.Response.Headers.CacheControl = "private, no-store";
            if (alias) DeprecatedAlias.Apply(http, "/api/registry" + http.Request.Path.Value![KernelPrefix.Length..]);
            var parts = scopeKey.Split(':', 2);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0) return QianResults.Error(new QianError(PlatformErrors.NotFound));
            try { return await read(source(http).Registry(), new ScopeKey(parts[0], parts[1]), http.RequestServices.GetRequiredService<ICurrentUser>()).ConfigureAwait(false); }
            catch (QianErrorException error) { return QianResults.Error(error.Error); }
            // 与插件端点中间件一致：注册表尚未就绪或已停止时与不存在无法区分。
            catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException) { return QianResults.Error(new QianError(PlatformErrors.NotFound)); }
        }
    }
}

/// <summary>scope 视图的 HTTP 信封；条目按 entryId 序数排序。</summary>
public sealed record RegistryEntryList(IReadOnlyList<RegistryEntryView> Items);
