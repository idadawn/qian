using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qian.Runtime;

namespace Qian.Tenancy.AspNetCore;

public static partial class TenancyEndpoints
{
    private static void MapState(IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
    {
        Bind(endpoints.MapPost("/api/tenancy/tenants/{id:guid}:activate", (HttpContext http, Guid id, ActivateTenant request) =>
            Respond(http, source, "activate", id, async (ctx, principal) => Results.Ok(await ctx.Inject<ITenantLifecycle>(TenantLifecycle.Key)
                .ActivateAsync(id, request, principal.Subject, http.RequestAborted).ConfigureAwait(false))))).Produces<TenantDescriptor>();
        Bind(endpoints.MapPost("/api/tenancy/tenants/{id:guid}:suspend", (HttpContext http, Guid id) =>
            Respond(http, source, "suspend", id, async (ctx, principal) => Results.Ok(await ctx.Inject<ITenantLifecycle>(TenantLifecycle.Key)
                .SuspendAsync(id, principal.Subject, http.RequestAborted).ConfigureAwait(false))))).Produces<TenantDescriptor>();
        Bind(endpoints.MapPost("/api/tenancy/tenants/{id:guid}:resume", (HttpContext http, Guid id) =>
            Respond(http, source, "resume", id, async (ctx, principal) => Results.Ok(await ctx.Inject<ITenantLifecycle>(TenantLifecycle.Key)
                .ResumeAsync(id, principal.Subject, http.RequestAborted).ConfigureAwait(false))))).Produces<TenantDescriptor>();
        Bind(endpoints.MapGet("/api/tenancy/meta/state-machine", (Func<HttpContext, Task<IResult>>)(http => Respond(http, source, "read", null,
            (_, _) => Task.FromResult<IResult>(Results.Json(TenantStateMetadata.Document)))))).Produces<JsonElement>();
    }
}
