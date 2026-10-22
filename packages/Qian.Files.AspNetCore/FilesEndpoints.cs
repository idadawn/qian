using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Qian.Authz.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Files.AspNetCore;

/// <summary><see cref="FilesEndpoints.MapQianFiles"/> 的可选项。</summary>
public sealed class FilesEndpointOptions
{
    /// <summary>端点绑定的安装条目 ID（<see cref="PluginEndpointBinding.EntryId"/>）。</summary>
    public string EntryId { get; init; } = "files";
    /// <summary>平台模式：再加 <c>RequireQianPlugin</c>，要求条目在请求租户 scope 已挂载（宿主须 AddQianPluginEndpoints + UseQianPluginEndpoints）。</summary>
    public bool RequirePlugin { get; init; }
    /// <summary>local 后端直传 / 直下端点解析 Files 的上下文（通常是 qian.files 的 global 上下文）；为 null 时不登记这两个端点。</summary>
    public Func<HttpContext, IPluginContext>? TransferSource { get; init; }
    /// <summary>直传 / 直下端点的路径前缀，须与组件配置 storage.endpoint 的路径一致；不要放在租户解析或强制认证的路径下。</summary>
    public string TransferPath { get; init; } = "/files/blobs";
}

/// <summary>qian.files 的 HTTP 表面：每个端点调用 <see cref="IFileService"/> 的能力门面，错误一律 ErrorDetail。</summary>
public static class FilesEndpoints
{
    private const int MaxRequestBytes = 16 * 1024;
    private const string IdempotencyHeader = "Idempotency-Key";

    /// <summary>
    /// 登记 POST /api/files/uploads:presign、POST /api/files/{id}:complete、GET /api/files/{id}、GET /api/files/{id}/download-url、DELETE /api/files/{id}、
    /// GET /api/files/quota，
    /// 以及 <see cref="FilesEndpointOptions.TransferSource"/> 非空时 local 后端的 PUT / GET {TransferPath}/{key}。
    /// <paramref name="source"/> 返回当前请求可注入 Files 的组件上下文（请求租户收窄）；<paramref name="requirePermission"/> 由宿主把权限点接到 PEP，
    /// 例如 <c>(endpoint, permission) =&gt; endpoint.RequireQianPermission(permission)</c>（Qian.Authz.AspNetCore）。只加元数据而不接 PEP 的回调在构建端点时即被拒绝。
    /// 宿主须先装配认证、可信租户边界、PEP 与 ErrorDetail 中间件（AddQianErrors / UseQianErrors）。
    /// </summary>
    public static IEndpointRouteBuilder MapQianFiles(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source,
        Action<RouteHandlerBuilder, string> requirePermission, FilesEndpointOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(requirePermission);
        options ??= new FilesEndpointOptions();
        var files = endpoints.MapGroup("/api/files");
        Bind(files.MapPost("/uploads:presign", async (HttpContext http) =>
        {
            var request = await ReadAsync(http).ConfigureAwait(false);
            var upload = await Files(http, source).PresignUploadAsync(request, http.RequestAborted).ConfigureAwait(false);
            return Json(http, upload, FilesContractJson.PresignedUploadTypeInfo, StatusCodes.Status201Created);
        }), FilesPermissions.Write, FilesCapabilities.PresignUpload)
            .Accepts<PresignUploadRequest>("application/json").WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
            .Produces<PresignedUpload>(StatusCodes.Status201Created).Produces<ErrorDetail>(StatusCodes.Status422UnprocessableEntity, ErrorDetailJson.MediaType);
        Bind(files.MapPost("/{id:guid}:complete", async (HttpContext http, Guid id) =>
            Json(http, await Files(http, source).CompleteAsync(new CompleteUploadRequest(IdempotencyKey(http), id), http.RequestAborted).ConfigureAwait(false),
                FilesContractJson.FileDescriptorTypeInfo)), FilesPermissions.Write, FilesCapabilities.CompleteUpload)
            .Produces<FileDescriptor>().Produces<ErrorDetail>(StatusCodes.Status409Conflict, ErrorDetailJson.MediaType)
            .Produces<ErrorDetail>(StatusCodes.Status422UnprocessableEntity, ErrorDetailJson.MediaType);
        Bind(files.MapGet("/{id:guid}", async (HttpContext http, Guid id) =>
            Json(http, await Files(http, source).GetAsync(id, http.RequestAborted).ConfigureAwait(false), FilesContractJson.FileDescriptorTypeInfo)),
            FilesPermissions.Read, FilesCapabilities.Get)
            .Produces<FileDescriptor>();
        Bind(files.MapGet("/{id:guid}/download-url", async (HttpContext http, Guid id) =>
            Json(http, await Files(http, source).GetDownloadUrlAsync(id, http.RequestAborted).ConfigureAwait(false), FilesContractJson.DownloadTicketTypeInfo)),
            FilesPermissions.Read, FilesCapabilities.DownloadUrl)
            .Produces<DownloadTicket>().Produces<ErrorDetail>(StatusCodes.Status409Conflict, ErrorDetailJson.MediaType);
        Bind(files.MapDelete("/{id:guid}", async (HttpContext http, Guid id) =>
            Json(http, await Files(http, source).DeleteAsync(new DeleteFileRequest(IdempotencyKey(http), id), http.RequestAborted).ConfigureAwait(false),
                FilesContractJson.FileDescriptorTypeInfo)), FilesPermissions.Delete, FilesCapabilities.Delete)
            .Produces<FileDescriptor>();
        Bind(files.MapGet("/quota", async (HttpContext http) =>
        {
            var quota = await Files(http, source).GetQuotaAsync(http.RequestAborted).ConfigureAwait(false);
            return Json(http, quota, FilesContractJson.FileQuotaTypeInfo);
        }), FilesPermissions.Read, FilesCapabilities.Quota)
            .Produces<FileQuota>();
        if (options.TransferSource is { } transfer) MapTransfer(endpoints, transfer, options.TransferPath);
        return endpoints;

        RouteHandlerBuilder Bind(RouteHandlerBuilder route, string permission, string capability)
        {
            var binding = new PluginEndpointBinding(options.EntryId, "qian.files", CapabilityBindingRegistration.Key, capability);
            if (options.RequirePlugin) route.RequireQianPlugin(binding); else route.WithMetadata(binding);
            route.Produces<ErrorDetail>(StatusCodes.Status400BadRequest, ErrorDetailJson.MediaType).Produces<ErrorDetail>(StatusCodes.Status404NotFound, ErrorDetailJson.MediaType);
            requirePermission(route, permission);
            // 回调必须真的把权限点接到授权管道：只有权限元数据而没有授权要求，会让缺省拒绝放行却没有任何判定。
            route.Add(endpoint =>
            {
                if (!endpoint.Metadata.OfType<Kun.Authorization.RequiresPermissionAttribute>().Any(item => item.Permission == permission) ||
                    (!endpoint.Metadata.OfType<IAuthorizeData>().Any() && !endpoint.Metadata.OfType<AuthorizationPolicy>().Any()))
                    throw new InvalidOperationException("文件端点 " + endpoint.DisplayName + " 的权限点 " + permission + " 没有接到 PEP。");
            });
            return route;
        }
    }

    // 签名即授权：直传 / 直下不走 PEP，也不读租户上下文，由网关校验 HMAC 签名与过期；签名不符、过期与对象不存在一律 404。
    // 显式匿名：宿主设了缺省鉴权策略（FallbackPolicy）时，浏览器直传不带令牌也能到达签名校验。
    private static void MapTransfer(IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix) || !prefix.StartsWith('/') || prefix.EndsWith('/')) throw new ArgumentException("直传路径前缀须以 / 开头且不以 / 结尾。", nameof(prefix));
        const string Reason = "local 后端的直传 / 直下地址由 qian.files 预签名能力签发：能力调用已经过 PEP，端点只校验 HMAC 签名与过期，签名覆盖方法、对象键与全部查询参数。";
        endpoints.MapPut(prefix + "/{**key}", async (HttpContext http, string key) =>
        {
            var result = await Gateway(http, source).ReceiveAsync(Request(http, key), http.Request.Body, http.RequestAborted).ConfigureAwait(false);
            return result switch
            {
                FileTransferResult.Stored => Results.StatusCode(StatusCodes.Status201Created),
                FileTransferResult.Conflict => throw new QianErrorException(new QianError(PlatformErrors.Conflict)),
                FileTransferResult.TooLarge => throw new QianErrorException(new QianError(PlatformErrors.PayloadTooLarge)),
                _ => throw new QianErrorException(new QianError(PlatformErrors.NotFound))
            };
        }).WithMetadata(new PermissionEnforcedInHandlerAttribute(Reason), new DisableRequestSizeLimitAttribute()).AllowAnonymous().ExcludeFromDescription();
        endpoints.MapGet(prefix + "/{**key}", async (HttpContext http, string key) =>
        {
            var download = await Gateway(http, source).OpenAsync(Request(http, key), http.RequestAborted).ConfigureAwait(false)
                ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            // 附件下载、禁止嗅探、沙箱化：即使浏览器直接打开，活跃内容也不会在本站源下执行。
            http.Response.Headers.ContentDisposition = download.ContentDisposition;
            http.Response.Headers.XContentTypeOptions = "nosniff";
            http.Response.Headers.ContentSecurityPolicy = "sandbox";
            http.Response.Headers.CacheControl = "private, no-store";
            http.Response.ContentLength = download.Length;
            return Results.Stream(download.Content, download.ContentType);
        }).WithMetadata(new PermissionEnforcedInHandlerAttribute(Reason)).AllowAnonymous().ExcludeFromDescription();
    }

    private static IFileService Files(HttpContext http, Func<HttpContext, IPluginContext> source)
    {
        try { return source(http).Inject<IFileService>(FilesDefinition.Key); }
        catch (Exception error) when (error is ServiceNotReadyException or KeyNotFoundException or ObjectDisposedException or InvalidOperationException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }

    private static IFileTransferGateway Gateway(HttpContext http, Func<HttpContext, IPluginContext> source)
    {
        try { return source(http).Inject<IFileService>(FilesDefinition.Key).Transfer ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
        catch (Exception error) when (error is ServiceNotReadyException or KeyNotFoundException or ObjectDisposedException or InvalidOperationException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }

    // 查询参数与请求头只取单值；重复参数让签名校验失败（缺字段），不在端点里猜哪一个才算数。
    private static FileTransferRequest Request(HttpContext http, string key)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, values) in http.Request.Query) if (values.Count == 1 && values[0] is { } value) query[name] = value;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "Content-Type", "X-Kun-Max-Bytes" })
            if (http.Request.Headers.TryGetValue(name, out var values) && values.Count == 1 && values[0] is { } value) headers[name] = value;
        return new FileTransferRequest(key, query, headers);
    }

    private static async Task<PresignUploadRequest> ReadAsync(HttpContext http)
    {
        if (!http.Request.HasJsonContentType()) throw new QianErrorException(new QianError(PlatformErrors.UnsupportedMediaType));
        try
        {
            return await JsonSerializer.DeserializeAsync(http.Request.Body, FilesContractJson.PresignUploadRequestTypeInfo, http.RequestAborted).ConfigureAwait(false)
                ?? throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        }
        catch (JsonException) { throw new QianErrorException(new QianError(PlatformErrors.InputInvalid)); }
    }

    // complete 与 delete 天然按状态幂等；客户端可用 Idempotency-Key 头带自己的键，缺省由端点生成。
    private static Guid IdempotencyKey(HttpContext http)
    {
        if (!http.Request.Headers.TryGetValue(IdempotencyHeader, out var values)) return Guid.CreateVersion7();
        return values.Count == 1 && Guid.TryParse(values[0], out var key) && key != Guid.Empty ? key
            : throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
    }

    private static IResult Json<T>(HttpContext http, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, int status = StatusCodes.Status200OK)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        return Results.Json(value, typeInfo, statusCode: status);
    }
}
