using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Tenancy.AspNetCore;

public static partial class TenancyEndpoints
{
    // 本租户摘要（S1-TEN-021）是本组件唯一面向租户主体的接口：只收同一租户的人类用户，租户取自已验证令牌并须与路径一致。
    // 平台主体读租户走上面的运营接口（超管旁路与审计），这里一律 403；Agent、服务与其它租户的成员同样 403，且不区分目标租户是否存在。
    private static void MapSummary(IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source) =>
        Bind(endpoints.MapGet("/api/tenancy/tenants/{id:guid}/summary", async (HttpContext http, Guid id) =>
        {
            http.Response.Headers.CacheControl = "private, no-store";
            try
            {
                var principal = Authenticated(http);
                var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Qian.Tenancy.Summary");
                var reason = principal.TenantId is null ? "platform_principal"
                    : principal.Kind != PrincipalKind.User || principal.Actor is not null ? "not_user"
                    : !Guid.TryParseExact(principal.TenantId, "D", out var own) || own != id || http.User.FindFirst("tenant_id")?.Value != principal.TenantId ? "other_tenant"
                    : null;
                // 宿主若已按令牌进入租户上下文，只能是同一租户。
                var current = http.RequestServices.GetRequiredService<ITenantContext>();
                if (reason is null && current.TenantId is not null && (!Guid.TryParseExact(current.TenantId, "D", out var entered) || entered != id)) reason = "context_mismatch";
                if (reason is not null) { LogSummaryDenied(logger, reason, principal.Subject); throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied)); }
                var ctx = source(http);
                if (ctx.Scope != ScopeKey.Global || ctx.Entry.Plugin != "qian.tenancy") throw new QianErrorException(new QianError(PlatformErrors.NotFound));
                using var scope = current.BeginScope(id.ToString("D"));
                return Results.Ok(await ctx.Inject<ITenantSummaries>(TenantSummaries.Key).CurrentAsync(http.RequestAborted).ConfigureAwait(false));
            }
            catch (QianErrorException error) { return QianResults.Error(error.Error); }
            catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException)
            { return QianResults.Error(new QianError(PlatformErrors.NotFound)); }
        })).Produces<TenantSummary>();

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "本租户摘要拒绝：{Reason}；主体 {Subject}")]
    private static partial void LogSummaryDenied(ILogger logger, string reason, string subject);
}
