using System.ComponentModel.DataAnnotations;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Authz.Abstractions;
using Qian.Capabilities;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Tenancy.Signup;

public static partial class TenancySignupEndpoints
{
    /// <summary>
    /// 映射平台运营接口（规格 §5.2）：列表与计数、详情、通过、驳回、重试，以及状态机元数据。宿主认证之后从 global 的 <c>qian.tenancy.signup</c> 上下文解析；
    /// 只收人类平台用户（无 tenant_id、无委托、非 Agent），经 Authz 超管旁路放行并在平台审计链各记一条旁路记录；须排除租户入口中间件。
    /// 权限点 <c>qian.tenancy.signup:application:read|approve|reject</c>，重试按 approve 授权。
    /// </summary>
    public static IEndpointRouteBuilder MapQianTenancySignupOperations(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source);
        Bind(endpoints.MapGet(ApplicantPrefix + "applications", (HttpContext http, [AllowedValues("submitted", "provisioning", "approved", "failed", "rejected", "withdrawn")] string? status = null,
            [MaxLength(200)] string? keyword = null, [AllowedValues(SignupCompanySizes.Micro, SignupCompanySizes.Small, SignupCompanySizes.Medium, SignupCompanySizes.Large, SignupCompanySizes.Enterprise)] string? size = null,
            [Range(0, int.MaxValue)] int skip = 0, [Range(1, 100)] int take = 50) =>
            Operate(http, source, "read", null, async (service, _) =>
            {
                var filter = status is null ? (SignupApplicationStatus?)null : Enum.GetValues<SignupApplicationStatus>().Single(value => SignupStatuses.Name(value) == status);
                return Results.Ok(await service.QueryAsync(new SignupApplicationQuery(skip, take) { Status = filter, Keyword = keyword, Size = size }, http.RequestAborted).ConfigureAwait(false));
            }, () =>
            {
                ValidateQuery(http, nameof(status), status); ValidateQuery(http, nameof(keyword), keyword); ValidateQuery(http, nameof(size), size);
                ValidateQuery(http, nameof(skip), skip); ValidateQuery(http, nameof(take), take);
            }))).Produces<SignupApplicationList>();
        Bind(endpoints.MapGet(ApplicantPrefix + "applications/{id:guid}", (HttpContext http, Guid id) =>
            Operate(http, source, "read", id, async (service, _) => Results.Ok(await service.GetAsync(id, http.RequestAborted).ConfigureAwait(false)))))
            .Produces<SignupApplicationDetail>();
        Bind(endpoints.MapPost(ApplicantPrefix + "applications/{id:guid}:approve", (HttpContext http, Guid id, ApproveSignupApplication input) =>
            Operate(http, source, "approve", id, async (service, principal) =>
                Results.Ok(await service.ApproveAsync(id, input, principal.Subject, http.RequestAborted).ConfigureAwait(false)), () => ValidateBody(input))))
            .Produces<SignupApplicationDetail>();
        Bind(endpoints.MapPost(ApplicantPrefix + "applications/{id:guid}:reject", (HttpContext http, Guid id, RejectSignupApplication input) =>
            Operate(http, source, "reject", id, async (service, principal) =>
                Results.Ok(await service.RejectAsync(id, input, principal.Subject, http.RequestAborted).ConfigureAwait(false)), () => ValidateBody(input))))
            .Produces<SignupApplicationDetail>();
        Bind(endpoints.MapPost(ApplicantPrefix + "applications/{id:guid}:retry", (HttpContext http, Guid id) =>
            Operate(http, source, "approve", id, async (service, principal) =>
                Results.Ok(await service.RetryAsync(id, principal.Subject, http.RequestAborted).ConfigureAwait(false)),
                validateAsync: () => EnsureEmptyBodyAsync(http.Request, requireJson: false))))
            .Produces<SignupApplicationDetail>();
        Bind(endpoints.MapGet(ApplicantPrefix + "meta/state-machine", (Func<HttpContext, Task<IResult>>)(http =>
            Operate(http, source, "read", null, (_, _) => Task.FromResult<IResult>(Results.Ok(SignupStateMachine.Current))))))
            .Produces<SignupStateMachine>();
        return endpoints;
    }

    private static RouteHandlerBuilder Bind(RouteHandlerBuilder route) => route.RequireAuthorization().WithMetadata(new PlatformHttpContract(TenancySignupDefinition.ComponentId))
        .Produces<ErrorDetail>(StatusCodes.Status400BadRequest, ErrorDetailJson.MediaType).Produces<ErrorDetail>(StatusCodes.Status401Unauthorized, ErrorDetailJson.MediaType)
        .Produces<ErrorDetail>(StatusCodes.Status403Forbidden, ErrorDetailJson.MediaType).Produces<ErrorDetail>(StatusCodes.Status404NotFound, ErrorDetailJson.MediaType)
        .Produces<ErrorDetail>(StatusCodes.Status409Conflict, ErrorDetailJson.MediaType);

    // R113 的处理顺序：认证及平台主体检查 → 输入校验 → 组件上下文核对 → 超管旁路与审计 → 执行。旁路记录的资源是申请集合或目标申请路径。
    private static async Task<IResult> Operate(HttpContext http, Func<HttpContext, IPluginContext> source, string action, Guid? id,
        Func<ITenantSignup, Principal, Task<IResult>> execute, Action? validate = null, Func<Task>? validateAsync = null)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var permission = TenancySignupDefinition.ComponentId + ":application:" + action;
        try
        {
            var user = http.RequestServices.GetRequiredService<ICurrentUser>(); var principal = user.Principal;
            if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject || http.User.Identity?.IsAuthenticated != true ||
                http.User.FindFirst("sub")?.Value != principal.Subject) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
            void Denied(string reason) => LogDenied(http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Qian.Tenancy.Signup.Operations"), reason, principal.Subject, permission);
            if (principal.TenantId is not null || principal.Actor is not null || principal.Kind != PrincipalKind.User ||
                http.User.Claims.Any(claim => claim.Type == "tenant_id" && !string.IsNullOrEmpty(claim.Value)) ||
                http.RequestServices.GetRequiredService<ITenantContext>().TenantId is not null)
            { Denied("tenant_principal"); throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied)); }
            validate?.Invoke();
            if (validateAsync is not null) await validateAsync().ConfigureAwait(false);
            var ctx = source(http);
            if (ctx.Scope != ScopeKey.Global || ctx.Entry.Plugin != TenancySignupDefinition.ComponentId)
            { Denied("context_mismatch"); throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
            // 超管旁路是可选依赖：所在 profile 没有 Authz 时运营接口与不存在相同。
            var bypass = ctx.TryInject<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey) ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            var decision = await bypass.AuthorizeAsync(new SuperadminAccess(new AuthorizationSubject(principal.Subject, null, "user"), permission, null,
                id is null ? "signup/applications" : "signup/applications/" + id.Value.ToString("D"), "api"), http.RequestAborted).ConfigureAwait(false);
            if (!decision.Allow) { Denied(decision.Reason ?? "denied"); throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied)); }
            var service = ctx.TenancySignup();
            using var origin = EventEnvelopeScope.Begin(new EventOrigin(TenancySignupDefinition.ComponentId, EventActor.User(principal.Subject)));
            return await execute(service, principal).ConfigureAwait(false);
        }
        catch (QianErrorException error) { return Failure(http, error); }
        catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException)
        { return QianResults.Error(new QianError(PlatformErrors.NotFound)); }
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "开通申请运营授权拒绝：{Reason}；主体 {Subject}；权限 {Permission}")]
    private static partial void LogDenied(ILogger logger, string reason, string subject, string permission);
}
