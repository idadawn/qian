using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Kun.AspNetCore.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Runtime.Errors;

namespace Qian.Data.AspNetCore;

/// <summary>qian.data 的 HTTP 表面：基准类型目录（全局元数据）与数据连接档案（按租户）。</summary>
public static class DataEndpoints
{
    private const int MaxDraftBytes = 16 * 1024;
    private static readonly string[] DraftMembers = ["name", "dialect", "host", "port", "database", "userName", "secretRef", "schema", "encoding"];
    private static readonly string[] RequiredMembers = ["name", "dialect", "host", "port", "database", "userName", "secretRef"];

    /// <summary>
    /// 登记全部数据端点：<see cref="MapQianDataTypes"/> 与 <see cref="MapQianDataConnections"/>。宿主须先装配认证、可信租户边界、
    /// Kun 授权（IPermissionChecker）与 Qian 错误中间件；<paramref name="binding"/> 非空时（平台模式）还要求该组件条目在当前 scope 已挂载。
    /// </summary>
    public static IEndpointRouteBuilder MapQianData(this IEndpointRouteBuilder endpoints, PluginEndpointBinding? binding = null) =>
        endpoints.MapQianDataTypes(binding).MapQianDataConnections(binding);

    /// <summary>登记 <c>GET /api/data/meta/types</c> 与 <c>DELETE /api/data/meta/types/{code}</c>；需要 AddQianDataTypes 与方言组件。</summary>
    public static IEndpointRouteBuilder MapQianDataTypes(this IEndpointRouteBuilder endpoints, PluginEndpointBinding? binding = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var data = endpoints.MapGroup("/api/data");
        Secure(data.MapGet("/meta/types", (HttpContext http, [FromServices] IDataTypeCatalog catalog) =>
            Json(http, catalog.Snapshot(), DataContractJson.DataTypeCatalogTypeInfo)), DataPermissions.TypeRead, binding)
            .Produces<DataTypeCatalog>();
        Secure(data.MapDelete("/meta/types/{code}", async (HttpContext http, string code, [FromServices] IDataTypeCatalog catalog) =>
        {
            await catalog.DeleteTypeAsync(code, http.RequestAborted).ConfigureAwait(false);
            return Results.NoContent();
        }), DataPermissions.TypeDelete, binding).Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }

    /// <summary>
    /// 登记 <c>/api/data/connections</c> 的增删改查与 <c>POST /api/data/connections/{id}:test</c>；需要 AddQianDataConnections。
    /// 档案按租户隔离，没有已验证租户的主体一律 403。
    /// </summary>
    public static IEndpointRouteBuilder MapQianDataConnections(this IEndpointRouteBuilder endpoints, PluginEndpointBinding? binding = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var data = endpoints.MapGroup("/api/data/connections");
        Tenant(Secure(data.MapGet("", async (HttpContext http, [FromServices] IDataConnectionRegistry registry) =>
            Json(http, await registry.ListAsync(http.RequestAborted).ConfigureAwait(false), DataContractJson.ConnectionListTypeInfo)),
            DataPermissions.ConnectionRead, binding)).Produces<DataConnectionList>();
        Tenant(Secure(data.MapPost("", async (HttpContext http, [FromServices] IDataConnectionRegistry registry) =>
        {
            var profile = await registry.CreateAsync(await ReadDraftAsync(http).ConfigureAwait(false), http.RequestAborted).ConfigureAwait(false);
            http.Response.Headers.Location = "/api/data/connections/" + profile.Id.ToString("D");
            return Json(http, profile, DataContractJson.ConnectionProfileTypeInfo, StatusCodes.Status201Created);
        }), DataPermissions.ConnectionWrite, binding)).WithMetadata(new RequestSizeLimitAttribute(MaxDraftBytes)).Produces<DataConnectionProfile>(StatusCodes.Status201Created);
        Tenant(Secure(data.MapGet("/{id:guid}", async (HttpContext http, Guid id, [FromServices] IDataConnectionRegistry registry) =>
            Json(http, await registry.GetAsync(id, http.RequestAborted).ConfigureAwait(false), DataContractJson.ConnectionProfileTypeInfo)),
            DataPermissions.ConnectionRead, binding)).Produces<DataConnectionProfile>();
        Tenant(Secure(data.MapPut("/{id:guid}", async (HttpContext http, Guid id, [FromServices] IDataConnectionRegistry registry) =>
            Json(http, await registry.UpdateAsync(id, await ReadDraftAsync(http).ConfigureAwait(false), http.RequestAborted).ConfigureAwait(false),
                DataContractJson.ConnectionProfileTypeInfo)), DataPermissions.ConnectionWrite, binding))
            .WithMetadata(new RequestSizeLimitAttribute(MaxDraftBytes)).Produces<DataConnectionProfile>();
        Tenant(Secure(data.MapDelete("/{id:guid}", async (HttpContext http, Guid id, [FromServices] IDataConnectionRegistry registry) =>
        {
            await registry.DeleteAsync(id, http.RequestAborted).ConfigureAwait(false);
            return Results.NoContent();
        }), DataPermissions.ConnectionWrite, binding)).Produces(StatusCodes.Status204NoContent);
        // 连通测试会从平台向档案中的主机出网（Agent 契约标 external）；结果不缓存。
        Tenant(Secure(data.MapPost("/{id:guid}:test", async (HttpContext http, Guid id, [FromServices] IDataConnectionRegistry registry) =>
            Json(http, await registry.TestAsync(id, http.RequestAborted).ConfigureAwait(false), DataContractJson.ConnectionTestResultTypeInfo)),
            DataPermissions.ConnectionTest, binding)).Produces<DataConnectionTestResult>();
        return endpoints;
    }

    private static RouteHandlerBuilder Secure(RouteHandlerBuilder builder, string permission, PluginEndpointBinding? binding)
    {
        builder.RequireKunPermission(permission);
        return binding is null ? builder : builder.RequireQianPlugin(binding);
    }

    // 数据连接按租户隔离：没有已验证租户的主体（如平台级身份）不能读写任何档案。
    private static RouteHandlerBuilder Tenant(RouteHandlerBuilder builder) => builder.AddEndpointFilter(async (context, next) =>
    {
        if (string.IsNullOrEmpty(context.HttpContext.RequestServices.GetRequiredService<ITenantContext>().TenantId))
            throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
        return await next(context).ConfigureAwait(false);
    });

    private static IResult Json<T>(HttpContext http, T value, JsonTypeInfo<T> typeInfo, int status = StatusCodes.Status200OK)
    {
        http.Response.Headers.CacheControl = "no-store";
        return Results.Json(value, typeInfo, statusCode: status);
    }

    // 先按成员白名单检查（password 等未声明成员一律拒绝，并指出位置），再按源生成元数据反序列化。
    private static async Task<DataConnectionDraft> ReadDraftAsync(HttpContext http)
    {
        if (!http.Request.HasJsonContentType()) throw new QianErrorException(new QianError(PlatformErrors.UnsupportedMediaType));
        JsonDocument document;
        try { document = await JsonDocument.ParseAsync(http.Request.Body, default, http.RequestAborted).ConfigureAwait(false); }
        catch (JsonException) { throw new QianErrorException(new QianError(PlatformErrors.BadRequest)); }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
            var errors = new List<QianFieldError>();
            foreach (var member in root.EnumerateObject())
                if (!DraftMembers.Contains(member.Name, StringComparer.Ordinal))
                    errors.Add(new QianFieldError(PlatformErrors.UnknownProperty, ErrorPointer.Input(member.Name)));
            foreach (var name in RequiredMembers)
                if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
                    errors.Add(new QianFieldError(PlatformErrors.Required, ErrorPointer.Input(name)));
            if (errors.Count == 0)
            {
                try { return root.Deserialize(DataContractJson.ConnectionDraftTypeInfo)!; }
                catch (JsonException error) { errors.Add(new QianFieldError(PlatformErrors.TypeMismatch, Pointer(error.Path))); }
            }
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Errors = errors });
        }
    }

    // JsonException.Path 形如 $.port；只取顶层成员名，其余情况指向整个输入。
    private static string Pointer(string? path) =>
        path is { Length: > 2 } && path.StartsWith("$.", StringComparison.Ordinal) && DraftMembers.Contains(path[2..], StringComparer.Ordinal)
            ? ErrorPointer.Input(path[2..])
            : ErrorPointer.Input();
}
