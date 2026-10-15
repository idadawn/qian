using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Capabilities;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Tenancy.AspNetCore;

/// <summary>租户运营接口：宿主认证后从 global qian.tenancy 上下文解析；须排除租户入口中间件。</summary>
public static partial class TenancyEndpoints
{
    /// <summary>映射平台租户列表、新建、编辑；平台超管旁路与审计不可缺席。</summary>
    public static IEndpointRouteBuilder MapQianTenancy(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source);
        Bind(endpoints.MapGet("/api/tenancy/tenants", (HttpContext http, int? skip, int? take) =>
            Respond(http, source, "read", null, async (ctx, _) => Results.Ok(new TenantList(await ctx.Inject<ITenantOperations>(TenantOperations.Key)
                .ListAsync(skip ?? 0, take ?? 50, http.RequestAborted).ConfigureAwait(false)))))).Produces<TenantList>();
        Bind(endpoints.MapPost("/api/tenancy/tenants", (HttpContext http, CreateTenant input) =>
            Respond(http, source, "create", null, async (ctx, principal) =>
            {
                var administrator = input.Administrator ?? throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
                var result = await ctx.Inject<ITenantProvisioning>(TenantProvisioning.Key).ProvisionAsync(input, administrator, principal.Subject, http.RequestAborted).ConfigureAwait(false);
                return Results.Created("/api/tenancy/tenants/" + result.Id, result);
            }))).Produces<TenantDescriptor>(StatusCodes.Status201Created);
        Bind(endpoints.MapPut("/api/tenancy/tenants/{id:guid}", (HttpContext http, Guid id, UpdateTenant input) =>
            Respond(http, source, "edit", id, async (ctx, _) => Results.Ok(await ctx.Inject<ITenantOperations>(TenantOperations.Key)
                .UpdateAsync(id, input, http.RequestAborted).ConfigureAwait(false))))).Produces<TenantDescriptor>();
        MapState(endpoints, source);
        return endpoints;
    }
    private static RouteHandlerBuilder Bind(RouteHandlerBuilder route) => route.RequireAuthorization().WithMetadata(new PlatformHttpContract("qian.tenancy"));
    private static async Task<IResult> Respond(HttpContext http, Func<HttpContext, IPluginContext> source, string action, Guid? id,
        Func<IPluginContext, Principal, Task<IResult>> execute)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        try
        {
            var user = http.RequestServices.GetRequiredService<ICurrentUser>(); var principal = user.Principal;
            if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject || http.User.Identity?.IsAuthenticated != true ||
                http.User.FindFirst("sub")?.Value != principal.Subject) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
            if (principal.TenantId is not null || principal.Actor is not null || principal.Kind != PrincipalKind.User ||
                http.User.Claims.Any(claim => claim.Type == "tenant_id" && !string.IsNullOrEmpty(claim.Value)) ||
                http.RequestServices.GetRequiredService<ITenantContext>().TenantId is not null) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
            var ctx = source(http);
            if (ctx.Scope != ScopeKey.Global || ctx.Entry.Plugin != "qian.tenancy") throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            var decision = await ctx.Inject<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey).AuthorizeAsync(
                new SuperadminAccess(new AuthorizationSubject(principal.Subject, null, "user"), "qian.tenancy:tenant:" + action,
                    null, id is null ? "tenancy/tenants" : "tenancy/tenants/" + id, "api"), http.RequestAborted).ConfigureAwait(false);
            if (!decision.Allow) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
            using var origin = EventEnvelopeScope.Begin(new EventOrigin("qian.tenancy", EventActor.User(principal.Subject)));
            return await execute(ctx, principal).ConfigureAwait(false);
        }
        catch (QianErrorException error) { return QianResults.Error(error.Error); }
        catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException)
        { return QianResults.Error(new QianError(PlatformErrors.NotFound)); }
    }
}

/// <summary>平台租户列表的非敏感投影。</summary>
public sealed record TenantList(IReadOnlyList<TenantDescriptor> Items);
