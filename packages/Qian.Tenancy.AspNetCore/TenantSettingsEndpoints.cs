using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qian.Runtime;

namespace Qian.Tenancy.AspNetCore;

public static partial class TenancyEndpoints
{
    private static void MapSettings(IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
    {
        Bind(endpoints.MapGet("/api/tenancy/tenants/{id:guid}/settings/{plugin}", (HttpContext http, Guid id, string plugin) =>
            Respond(http, source, "read", id, async (ctx, _) => Results.Ok(await ctx.Inject<ITenantSettingsOperations>(TenantSettings.OperationsKey)
                .ReadAsync(id, plugin, http.RequestAborted).ConfigureAwait(false))))).Produces<TenantSettingsDocument>();
        Bind(endpoints.MapPut("/api/tenancy/tenants/{id:guid}/settings/{plugin}", (HttpContext http, Guid id, string plugin, UpdateTenantSettings input) =>
            Respond(http, source, "edit", id, async (ctx, principal) => Results.Ok(await ctx.Inject<ITenantSettingsOperations>(TenantSettings.OperationsKey)
                .WriteAsync(id, plugin, input, principal.Subject, http.RequestAborted).ConfigureAwait(false))))).Produces<TenantSettingsDocument>();
        Bind(endpoints.MapGet("/api/tenancy/tenants/{id:guid}/quota", (HttpContext http, Guid id) =>
            Respond(http, source, "read", id, async (ctx, _) => Results.Ok(await ctx.Inject<ITenantSettingsOperations>(TenantSettings.OperationsKey)
                .QuotaAsync(id, http.RequestAborted).ConfigureAwait(false))))).Produces<TenantQuotaView>();
    }
}
