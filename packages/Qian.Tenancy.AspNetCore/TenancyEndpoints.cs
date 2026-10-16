using Kun.Authorization;
using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
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
        Bind(endpoints.MapGet("/api/tenancy/tenants", (HttpContext http, [Range(0, int.MaxValue)] int skip = 0, [Range(1, 100)] int take = 50,
            [AllowedValues("pending", "trial", "active", "grace", "suspended")] string? status = null, [MaxLength(200)] string? keyword = null) =>
            Respond(http, source, "read", null, async (ctx, _) =>
            {
                var filter = status switch
                {
                    null => (TenantStatus?)null, "pending" => TenantStatus.Pending, "trial" => TenantStatus.Trial,
                    "active" => TenantStatus.Active, "grace" => TenantStatus.Grace, "suspended" => TenantStatus.Suspended,
                    _ => throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Errors = [new QianFieldError(PlatformErrors.ValueNotAllowed, "#/status")] })
                };
                var result = await ctx.Inject<ITenantOperations>(TenantOperations.Key).QueryAsync(skip, take, filter, keyword, http.RequestAborted).ConfigureAwait(false);
                return Results.Ok(new TenantList(result.Items) { Total = result.Total });
            },
                () => { ValidateQuery(http, nameof(skip), skip); ValidateQuery(http, nameof(take), take); ValidateQuery(http, nameof(status), status); ValidateQuery(http, nameof(keyword), keyword); }))).Produces<TenantList>();
        Bind(endpoints.MapPost("/api/tenancy/tenants", (HttpContext http, CreateTenant input) =>
            Respond(http, source, "create", null, async (ctx, principal) =>
            {
                var administrator = input.Administrator ?? throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
                var result = await ctx.Inject<ITenantProvisioning>(TenantProvisioning.Key).ProvisionAsync(input, administrator, principal.Subject, http.RequestAborted).ConfigureAwait(false);
                return Results.Created("/api/tenancy/tenants/" + result.Id, result);
            }, () =>
            {
                if (input.Administrator is null || input.Isolation != TenantIsolation.Row)
                    throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
            }))).Produces<TenantDescriptor>(StatusCodes.Status201Created);
        Bind(endpoints.MapGet("/api/tenancy/tenants/{id:guid}", (HttpContext http, Guid id) =>
            Respond(http, source, "read", id, async (ctx, _) => Results.Ok(await ctx.Tenancy().GetByIdAsync(id, http.RequestAborted).ConfigureAwait(false)
                ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound)))))).Produces<TenantDescriptor>();
        Bind(endpoints.MapPut("/api/tenancy/tenants/{id:guid}", (HttpContext http, Guid id, UpdateTenant input) =>
            Respond(http, source, "edit", id, async (ctx, _) => Results.Ok(await ctx.Inject<ITenantOperations>(TenantOperations.Key)
                .UpdateAsync(id, input, http.RequestAborted).ConfigureAwait(false))))).Produces<TenantDescriptor>();
        MapState(endpoints, source);
        MapSettings(endpoints, source);
        return endpoints;
    }
    private static RouteHandlerBuilder Bind(RouteHandlerBuilder route) => route.RequireAuthorization().WithMetadata(new PlatformHttpContract("qian.tenancy"))
        .Produces<ErrorDetail>(400, ErrorDetailJson.MediaType).Produces<ErrorDetail>(401, ErrorDetailJson.MediaType)
        .Produces<ErrorDetail>(403, ErrorDetailJson.MediaType).Produces<ErrorDetail>(404, ErrorDetailJson.MediaType).Produces<ErrorDetail>(409, ErrorDetailJson.MediaType);
    private static async Task<IResult> Respond(HttpContext http, Func<HttpContext, IPluginContext> source, string action, Guid? id,
        Func<IPluginContext, Principal, Task<IResult>> execute, Action? validate = null)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        try
        {
            var user = http.RequestServices.GetRequiredService<ICurrentUser>(); var principal = user.Principal;
            if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject || http.User.Identity?.IsAuthenticated != true ||
                http.User.FindFirst("sub")?.Value != principal.Subject) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
            if (principal.TenantId is not null || principal.Actor is not null || principal.Kind != PrincipalKind.User ||
                http.User.Claims.Any(claim => claim.Type == "tenant_id" && !string.IsNullOrEmpty(claim.Value)) ||
                http.RequestServices.GetRequiredService<ITenantContext>().TenantId is not null)
            { Denied("tenant_principal"); throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied)); }
            validate?.Invoke();
            var ctx = source(http);
            if (ctx.Scope != ScopeKey.Global || ctx.Entry.Plugin != "qian.tenancy")
            { Denied("context_mismatch"); throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
            var decision = await ctx.Inject<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey).AuthorizeAsync(
                new SuperadminAccess(new AuthorizationSubject(principal.Subject, null, "user"), "qian.tenancy:tenant:" + action,
                    null, id is null ? "tenancy/tenants" : "tenancy/tenants/" + id, "api"), http.RequestAborted).ConfigureAwait(false);
            if (!decision.Allow)
            {
                LogDenied(http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Qian.Tenancy.Operations"),
                    decision.Reason, principal.Subject, "qian.tenancy:tenant:" + action);
                throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
            }
            void Denied(string reason) => LogDenied(http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Qian.Tenancy.Operations"), reason, principal.Subject, "qian.tenancy:tenant:" + action);
            using var origin = EventEnvelopeScope.Begin(new EventOrigin("qian.tenancy", EventActor.User(principal.Subject)));
            return await execute(ctx, principal).ConfigureAwait(false);
        }
        catch (QianErrorException error) { return QianResults.Error(error.Error); }
        catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException)
        { return QianResults.Error(new QianError(PlatformErrors.NotFound)); }
    }
    private static void ValidateQuery(HttpContext http, string name, object? value)
    {
        var parameter = http.GetEndpoint()!.Metadata.OfType<IParameterBindingMetadata>().Single(item => item.Name == name).ParameterInfo;
        if (value is null && parameter.HasDefaultValue && parameter.DefaultValue is null) return;
        if (parameter.GetCustomAttributes<ValidationAttribute>().Any(rule => !rule.IsValid(value)))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Pointer = "#/" + name });
    }
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "租户运营授权拒绝：{Reason}；主体 {Subject}；权限 {Permission}")]
    private static partial void LogDenied(ILogger logger, string reason, string subject, string permission);
}

/// <summary>平台租户列表的非敏感投影。</summary>
public sealed record TenantList(IReadOnlyList<TenantDescriptor> Items)
{
    /// <summary>筛选后总数，与当前页读取自同一数据库快照。</summary>
    public int Total { get; init; }
}
