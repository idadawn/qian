using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Plugin.Sdk.AspNetCore;

namespace Qian.Capabilities.AspNetCore;

/// <summary>宿主提供已验证租户的 Runtime 视图；HTTP 输入不能指定主体或租户。</summary>
public static class CapabilityEndpoints
{
    // v1 别名的弃用时间（RFC 9745 结构化日期，2026-10-07T00:00:00Z）；S2 移除别名（R33）。
    private const string AliasDeprecation = "@1791331200";

    /// <summary>登记认证目录、菜单、偏好、旧别名和实际调用。宿主须先配置身份、租户和 ErrorDetail 中间件。</summary>
    public static IEndpointRouteBuilder MapQianCapabilities(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
        => MapQianCapabilities(endpoints, source, new PluginEndpointBinding("capabilities", "qian.capabilities", CapabilitiesDefinition.Key));

    /// <summary>显式指定实际组件条目的端点绑定，需安装 SDK 的插件端点中间件。</summary>
    public static IEndpointRouteBuilder MapQianCapabilities(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source, PluginEndpointBinding binding)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(binding);
        endpoints.MapGet("/api/capabilities", (HttpContext http, string? kind, string? q) => Catalog(http, kind, q, null)).RequireAuthorization().RequireQianPlugin(binding).Produces<CapabilityCatalog>();
        endpoints.MapGet("/api/kernel/capabilities", (HttpContext http, string? kind, string? q) => Catalog(http, kind, q, "/api/capabilities")).RequireAuthorization().RequireQianPlugin(binding).Produces<CapabilityCatalog>();
        endpoints.MapGet("/api/capabilities/openapi.json", (Func<HttpContext, Task<IResult>>)(http => OpenApi(http, null))).RequireAuthorization().RequireQianPlugin(binding).Produces<JsonElement>();
        endpoints.MapGet("/api/kernel/capabilities/openapi.json", (Func<HttpContext, Task<IResult>>)(http => OpenApi(http, "/api/capabilities/openapi.json"))).RequireAuthorization().RequireQianPlugin(binding).Produces<JsonElement>();
        endpoints.MapGet("/api/capabilities/navigation", (Func<HttpContext, Task<IResult>>)(async http =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            var caller = source(http);
            return Results.Json(new NavigationCatalog(await caller.Navigation().ListAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(), http.RequestAborted).ConfigureAwait(false)));
        })).RequireAuthorization().RequireQianPlugin(binding).Produces<NavigationCatalog>();
        endpoints.MapGet("/api/capabilities/navigation/item", async (HttpContext http, string resourceRef) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            var caller = source(http);
            return Results.Json(await caller.Navigation().GetAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(), resourceRef, http.RequestAborted).ConfigureAwait(false));
        }).RequireAuthorization().RequireQianPlugin(binding).Produces<NavigationItem>();
        endpoints.MapPut("/api/capabilities/navigation/preference", async (HttpContext http, string resourceRef, NavigationPreference preference) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var caller = source(http);
            await caller.Navigation().SetPreferenceAsync(caller, http.RequestServices.GetRequiredService<ICurrentUser>(), resourceRef, preference, http.RequestAborted).ConfigureAwait(false);
            return Results.NoContent();
        }).RequireAuthorization().RequireQianPlugin(binding).Produces(StatusCodes.Status204NoContent);
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
        }).RequireAuthorization().RequireQianPlugin(binding).ExcludeFromDescription();
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

    // 别名响应带 Deprecation 与指向后继路径的 Link（规格 §9.5）；后继带宿主的 PathBase 并保留原查询串，客户端可原样改用新路径。
    private static void Deprecate(HttpContext http, string? successor)
    {
        if (successor is null) return;
        http.Response.Headers["Deprecation"] = AliasDeprecation;
        http.Response.Headers.Link = SuccessorLink(http.Request, successor);
    }

    // 查询串原样来自请求行，可能含 URI 不允许的字符（如 <、>、引号、空格、非 ASCII）；按 RFC 3986 的 query 字符集保留合法字符与
    // 合法的百分号编码，其余按 UTF-8 百分号编码，保证 Link 的 URI-Reference 合法、不会提前闭合尖括号。
    internal static string SuccessorLink(HttpRequest request, string successor)
    {
        var target = new StringBuilder(request.PathBase.Add(new PathString(successor)).ToUriComponent());
        ReadOnlySpan<char> query = request.QueryString.Value;
        Span<byte> bytes = stackalloc byte[4];
        while (!query.IsEmpty)
        {
            // 孤立的代理项按替换字符编码。
            if (Rune.DecodeFromUtf16(query, out var rune, out var consumed) != OperationStatus.Done) rune = Rune.ReplacementChar;
            var c = query[0];
            if (rune.IsAscii && (char.IsAsciiLetterOrDigit(c) || "-._~!$&'()*+,;=:@/?".Contains(c) ||
                (c == '%' && query.Length > 2 && char.IsAsciiHexDigit(query[1]) && char.IsAsciiHexDigit(query[2]))))
                target.Append(c);
            else
                foreach (var b in bytes[..rune.EncodeToUtf8(bytes)]) target.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            query = query[Math.Max(consumed, 1)..];
        }
        return "<" + target + ">; rel=\"successor-version\"";
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
