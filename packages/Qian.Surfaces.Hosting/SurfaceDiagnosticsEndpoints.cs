using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qian.Runtime;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces.Hosting;

/// <summary>供 CLI 读取的表面监视诊断；不是完整生命周期管理 API。</summary>
public static class SurfaceDiagnosticsEndpoints
{
    /// <summary>宿主必须指定实际授权策略，并从可信身份提供 scope；不接受 URL 中的租户参数。</summary>
    public static IEndpointRouteBuilder MapQianSurfaceDiagnostics(this IEndpointRouteBuilder endpoints,
        Func<HttpContext, IPluginContext> source, string authorizationPolicy)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationPolicy);
        endpoints.MapGet("/api/lifecycle/diagnostics/surfaces", (HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            return Results.Json(SurfaceRuntimeDiagnostics.Read(source(http)));
        }).RequireAuthorization(authorizationPolicy).ExcludeFromDescription();
        return endpoints;
    }
}
