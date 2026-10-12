using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qian.Identity.Abstractions;
using Qian.Runtime;

namespace Qian.Capabilities.AspNetCore;

/// <summary>宿主提供已验证租户的 Runtime 视图；HTTP 输入不能指定主体或租户。</summary>
public static class CapabilityEndpoints
{
    /// <summary>登记认证目录、菜单、偏好、旧别名和实际调用。宿主须先配置身份、租户和 ErrorDetail 中间件。</summary>
    public static IEndpointRouteBuilder MapQianCapabilities(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source);
        endpoints.MapGet("/api/capabilities", (HttpContext http, string? kind, string? q) => Catalog(http, kind, q, false)).RequireAuthorization();
        endpoints.MapGet("/api/kernel/capabilities", (HttpContext http, string? kind, string? q) => Catalog(http, kind, q, true)).RequireAuthorization();
        endpoints.MapGet("/api/capabilities/openapi.json", (Func<HttpContext, Task<IResult>>)(http => OpenApi(http, false))).RequireAuthorization();
        endpoints.MapGet("/api/kernel/capabilities/openapi.json", (Func<HttpContext, Task<IResult>>)(http => OpenApi(http, true))).RequireAuthorization();
        endpoints.MapGet("/api/capabilities/navigation", (Func<HttpContext, Task<IResult>>)(async http =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            var caller = source(http);
            return Results.Json(new { items = await caller.Navigation().ListAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(), http.RequestAborted).ConfigureAwait(false), cacheScope = "private" });
        })).RequireAuthorization();
        endpoints.MapGet("/api/capabilities/navigation/item", async (HttpContext http, string resourceRef) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            var caller = source(http);
            return Results.Json(await caller.Navigation().GetAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(), resourceRef, http.RequestAborted).ConfigureAwait(false));
        }).RequireAuthorization();
        endpoints.MapPut("/api/capabilities/navigation/preference", async (HttpContext http, string resourceRef, NavigationPreference preference) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var caller = source(http);
            await caller.Navigation().SetPreferenceAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(), resourceRef, preference, http.RequestAborted).ConfigureAwait(false);
            return Results.NoContent();
        }).RequireAuthorization();
        endpoints.MapPost("/api/capabilities/{plugin}/{name}/invoke", async (HttpContext http, string plugin, string name, JsonElement input) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var caller = source(http);
            var result = await caller.Capabilities().InvokeAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(),
                plugin + "/" + name, input, http.RequestAborted).ConfigureAwait(false);
            return Results.Json(result);
        }).RequireAuthorization();
        return endpoints;

        async Task<IResult> OpenApi(HttpContext http, bool alias)
        {
            http.Response.Headers.CacheControl = "private, no-store";
            if (alias) http.Response.Headers["Deprecation"] = "@1791331200";
            var caller = source(http);
            return Results.Json(await caller.Capabilities().GetOpenApiAsync(caller,
                http.RequestServices.GetRequiredService<ICurrentUser>(), http.RequestAborted).ConfigureAwait(false));
        }

        async Task<IResult> Catalog(HttpContext http, string? kind, string? q, bool alias)
        {
            http.Response.Headers.CacheControl = "private, no-store";
            if (alias) http.Response.Headers["Deprecation"] = "@1791331200";
            var caller = source(http);
            var result = await caller.Capabilities().ListAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(),
                kind, q, http.RequestAborted).ConfigureAwait(false);
            return Results.Json(new { items = result.Items, ttlMs = result.TtlMs, cacheScope = result.CacheScope });
        }
    }
}
