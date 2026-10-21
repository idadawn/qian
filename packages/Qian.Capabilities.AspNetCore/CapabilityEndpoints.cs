using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Qian.Authz.Abstractions;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Plugin.Sdk.AspNetCore;

namespace Qian.Capabilities.AspNetCore;

/// <summary>宿主提供已验证租户的 Runtime 视图；HTTP 输入不能指定主体或租户。</summary>
public static class CapabilityEndpoints
{
    // 目录、OpenAPI、菜单与执行入口没有单一权限点：可见集合与执行都按调用者权限在处理器内判定（S1-AZ-004 的 HTTP PEP 据此不缺省拒绝）。
    private static readonly PermissionEnforcedInHandlerAttribute InHandler = new("能力目录、菜单与执行入口按调用者可见能力过滤并逐能力判定，不对应单一权限点。");

    /// <summary>登记认证目录、菜单、偏好、旧别名和实际调用。宿主须先配置身份、租户和 ErrorDetail 中间件。</summary>
    public static IEndpointRouteBuilder MapQianCapabilities(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
        => MapQianCapabilities(endpoints, source, new PluginEndpointBinding("capabilities", "qian.capabilities", CapabilitiesDefinition.Key));

    /// <summary>显式指定实际组件条目的端点绑定，需安装 SDK 的插件端点中间件。</summary>
    public static IEndpointRouteBuilder MapQianCapabilities(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source, PluginEndpointBinding binding)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(binding);
        endpoints.MapGet("/api/capabilities", (HttpContext http, string? kind, string? q) => Catalog(http, kind, q, null)).RequireAuthorization().RequireQianPlugin(binding).WithMetadata(InHandler).Produces<CapabilityCatalog>();
        endpoints.MapGet("/api/kernel/capabilities", (HttpContext http, string? kind, string? q) => Catalog(http, kind, q, "/api/capabilities")).RequireAuthorization().RequireQianPlugin(binding).WithMetadata(InHandler).Produces<CapabilityCatalog>();
        endpoints.MapGet("/api/capabilities/openapi.json", (Func<HttpContext, Task<IResult>>)(http => OpenApi(http, null))).RequireAuthorization().RequireQianPlugin(binding).WithMetadata(InHandler).Produces<JsonElement>();
        endpoints.MapGet("/api/kernel/capabilities/openapi.json", (Func<HttpContext, Task<IResult>>)(http => OpenApi(http, "/api/capabilities/openapi.json"))).RequireAuthorization().RequireQianPlugin(binding).WithMetadata(InHandler).Produces<JsonElement>();
        endpoints.MapGet("/api/capabilities/navigation", (Func<HttpContext, Task<IResult>>)(async http =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            var caller = source(http);
            return Results.Json(new NavigationCatalog(await caller.Navigation().ListAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(), http.RequestAborted).ConfigureAwait(false)));
        })).RequireAuthorization().RequireQianPlugin(binding).WithMetadata(InHandler).Produces<NavigationCatalog>();
        endpoints.MapGet("/api/capabilities/navigation/item", async (HttpContext http, string resourceRef) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            var caller = source(http);
            return Results.Json(await caller.Navigation().GetAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(), resourceRef, http.RequestAborted).ConfigureAwait(false));
        }).RequireAuthorization().RequireQianPlugin(binding).WithMetadata(InHandler).Produces<NavigationItem>();
        endpoints.MapPut("/api/capabilities/navigation/preference", async (HttpContext http, string resourceRef, NavigationPreference preference) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var caller = source(http);
            await caller.Navigation().SetPreferenceAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(), resourceRef, preference, http.RequestAborted).ConfigureAwait(false);
            return Results.NoContent();
        }).RequireAuthorization().RequireQianPlugin(binding).WithMetadata(InHandler).Produces(StatusCodes.Status204NoContent);
        // 通用执行入口（R75）：请求与响应的形状随能力而变，可见集合随调用者权限而变；ApiExplorer 只能描述静态、与调用者无关的
        // 路由，故不列此模板，由 /api/capabilities/openapi.json 按同一判定为每个可见能力列出具体路径与真实输入 / 输出 Schema。
        endpoints.MapPost("/api/capabilities/{plugin}/{name}/invoke", async (HttpContext http, string plugin, string name, JsonElement input) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var key = IdempotencyKey(http.Request.Headers["Idempotency-Key"]);
            var caller = source(http);
            var result = await caller.Capabilities().InvokeAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(),
                plugin + "/" + name, input, key, http.RequestAborted).ConfigureAwait(false);
            if (result.Replayed) http.Response.Headers["Idempotent-Replayed"] = "true";
            return Results.Json(result.Output);
        }).RequireAuthorization().RequireQianPlugin(binding).WithMetadata(InHandler).ExcludeFromDescription();
        return endpoints;

        async Task<IResult> OpenApi(HttpContext http, string? successor)
        {
            http.Response.Headers.CacheControl = "private, no-store";
            Deprecate(http, successor);
            var caller = source(http);
            return Results.Json(await caller.Capabilities().GetOpenApiAsync(caller,
                http.RequestServices.GetRequiredService<ICurrentUser>(), http.RequestAborted).ConfigureAwait(false));
        }

        async Task<IResult> Catalog(HttpContext http, string? kind, string? q, string? successor)
        {
            http.Response.Headers.CacheControl = "private, no-store";
            Deprecate(http, successor);
            var caller = source(http);
            var result = await caller.Capabilities().ListAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(),
                kind, q, http.RequestAborted).ConfigureAwait(false);
            // 直接写出声明的具名类型；源生成契约固定成员名，不受宿主 JSON 命名策略影响。
            return Results.Json(result, CapabilityHttpJson.Default.CapabilityCatalog);
        }
    }

    // 别名响应带 Deprecation 与指向后继路径的 Link（规格 §9.5），与其它 v1 别名共用 SDK 的同一实现。
    private static void Deprecate(HttpContext http, string? successor)
    {
        if (successor is not null) DeprecatedAlias.Apply(http, successor);
    }

    // Idempotency-Key 取 UUID（RFC 9562 文本形式，可按结构化字段带引号）；多值、空值、非 UUID 或全零均视为无法解析的请求。
    private static Guid? IdempotencyKey(StringValues values)
    {
        if (values.Count == 0) return null;
        if (values.Count != 1 || values[0] is not { } raw) throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
        var text = raw.Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"') text = text[1..^1];
        if (!Guid.TryParseExact(text, "D", out var key) || key == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
        return key;
    }
}
