using System.Text.Json;
using System.Text.Json.Serialization;
using Kun.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qian.Capabilities;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Config.AspNetCore;

/// <summary>平台特性开关草稿写入表面；Bearer 认证，不接受 Cookie 认证，也不提供发布端点。</summary>
public static class FeatureFlagEndpoints
{
    /// <summary>挂载可选的草稿入口；source 指向 qian.config.flags 的当前实际上下文，执行端口每次重做权限和审计。</summary>
    public static IEndpointRouteBuilder MapQianFeatureFlags(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source);
        endpoints.MapPost("/api/config/flags", (RequestDelegate)(async http =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            try
            {
                var user = http.RequestServices.GetService<ICurrentUser>();
                var principal = user?.Principal;
                if (user is not { IsAuthenticated: true } || principal is null || user.UserId != principal.Subject ||
                    http.User.Identity?.IsAuthenticated != true || http.User.FindFirst("sub")?.Value != principal.Subject)
                    throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
                if (principal.TenantId is not null || principal.Kind != PrincipalKind.User || principal.Actor is not null ||
                    http.User.Claims.Any(claim => claim.Type == "tenant_id" && !string.IsNullOrEmpty(claim.Value)))
                    throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
                if (!http.Request.HasJsonContentType()) throw new QianErrorException(new QianError(PlatformErrors.UnsupportedMediaType));
                var bytes = new byte[65_537]; var length = 0;
                while (length < bytes.Length)
                {
                    var read = await http.Request.Body.ReadAsync(bytes.AsMemory(length), http.RequestAborted).ConfigureAwait(false);
                    if (read == 0) break;
                    length += read;
                }
                if (length == bytes.Length) throw new QianErrorException(new QianError(PlatformErrors.PayloadTooLarge));
                FeatureFlagDraft request;
                try { request = JsonSerializer.Deserialize(bytes.AsSpan(0, length), FlagHttpJson.Default.FeatureFlagDraft) ?? throw new JsonException(); }
                catch (JsonException) { throw new QianErrorException(new QianError(PlatformErrors.InputInvalid)); }
                var context = source(http);
                if (context.Scope != ScopeKey.Global) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
                var result = await context.Inject<IFeatureFlagDrafts>(FeatureFlagManagement.Key).SetAsync(request, http.RequestAborted).ConfigureAwait(false);
                await Results.Json(result, FlagHttpJson.Default.FeatureFlagDraftResult).ExecuteAsync(http).ConfigureAwait(false);
            }
            catch (QianErrorException error) { await QianResults.Error(error.Error).ExecuteAsync(http).ConfigureAwait(false); }
            catch (Exception error) when (error is ObjectDisposedException or ServiceNotReadyException or KeyNotFoundException)
            { await QianResults.Error(new QianError(PlatformErrors.NotFound)).ExecuteAsync(http).ConfigureAwait(false); }
        })).RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Bearer" })
            .WithMetadata(new PlatformHttpContract("qian.config.flags"), new RequiresPermissionAttribute(FeatureFlagManagement.SetPermission))
            .WithMetadata(new Microsoft.AspNetCore.Http.Metadata.AcceptsMetadata(["application/json"], typeof(FeatureFlagDraft), false),
                new ProducesResponseTypeMetadata(200, typeof(FeatureFlagDraftResult), ["application/json"]),
                new ProducesResponseTypeMetadata(400, typeof(ErrorDetail), ["application/problem+json"]),
                new ProducesResponseTypeMetadata(401, typeof(ErrorDetail), ["application/problem+json"]),
                new ProducesResponseTypeMetadata(403, typeof(ErrorDetail), ["application/problem+json"]),
                new ProducesResponseTypeMetadata(404, typeof(ErrorDetail), ["application/problem+json"]),
                new ProducesResponseTypeMetadata(502, typeof(ErrorDetail), ["application/problem+json"]));
        return endpoints;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true, AllowDuplicateProperties = false)]
[JsonSerializable(typeof(FeatureFlagDraft))][JsonSerializable(typeof(FeatureFlagDraftResult))]
internal sealed partial class FlagHttpJson : JsonSerializerContext;
