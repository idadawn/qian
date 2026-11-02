using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Qian.Capabilities;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Tenancy.Signup;

public static partial class TenancySignupEndpoints
{
    /// <summary>
    /// 映射申请人接口（规格 §5.1）：发验证码、核验并建立申请人会话、读取 / 退出会话、租户号可用性、本人的申请、提交与撤回。
    /// 接口公开（不经登录），只认本组件的申请人会话 Cookie（HttpOnly、SameSite=Strict、Path=/api/signup，回环之外 Secure）；
    /// 写请求只收 application/json，且 Origin（缺省时 Sec-Fetch-Site）必须同源。宿主在反向代理之后须先启用转发头，使请求的协议与主机即对外的源。
    /// </summary>
    public static IEndpointRouteBuilder MapQianTenancySignupApplicant(this IEndpointRouteBuilder endpoints, Func<HttpContext, IPluginContext> source)
    {
        ArgumentNullException.ThrowIfNull(endpoints); ArgumentNullException.ThrowIfNull(source);
        Json(Public(endpoints.MapPost(ApplicantPrefix + "verifications", (HttpContext http, StartSignupVerification input) =>
            Applicant(http, source, write: true, async (service, request) =>
                Results.Json(await service.StartVerificationAsync(input, request, http.RequestAborted).ConfigureAwait(false), statusCode: StatusCodes.Status202Accepted),
                () => ValidateBody(input)))))
            .Produces<SignupVerificationTicket>(StatusCodes.Status202Accepted)
            .Produces<ErrorDetail>(StatusCodes.Status429TooManyRequests, ErrorDetailJson.MediaType).Produces<ErrorDetail>(StatusCodes.Status503ServiceUnavailable, ErrorDetailJson.MediaType);
        Json(Public(endpoints.MapPost(ApplicantPrefix + "session", (HttpContext http, OpenSignupSession input) =>
            Applicant(http, source, write: true, async (service, request) =>
            {
                var grant = await service.OpenSessionAsync(input, request, http.RequestAborted).ConfigureAwait(false);
                WriteCookie(http, grant);
                return Results.Ok(grant.Applicant);
            }, () => ValidateBody(input))))).Produces<SignupApplicant>();
        Public(endpoints.MapGet(ApplicantPrefix + "session", (Func<HttpContext, Task<IResult>>)(http =>
            Applicant(http, source, write: false, async (service, _) =>
                await service.GetApplicantAsync(Token(http) ?? "", http.RequestAborted).ConfigureAwait(false) is { } applicant ? Results.Ok(applicant) : Unauthenticated(http)))))
            .Produces<SignupApplicant>();
        Public(endpoints.MapDelete(ApplicantPrefix + "session", (Func<HttpContext, Task<IResult>>)(http =>
            Applicant(http, source, write: true, async (service, _) =>
            {
                if (Token(http) is { } token) await service.CloseSessionAsync(token, http.RequestAborted).ConfigureAwait(false);
                ClearCookie(http);
                return Results.NoContent();
            })))).Produces(StatusCodes.Status204NoContent);
        Public(endpoints.MapGet(ApplicantPrefix + "code-availability", (HttpContext http, [MinLength(1), MaxLength(64)] string code) =>
            Applicant(http, source, write: false, async (service, request) => Results.Ok(await service.CheckCodeAsync(code, request, http.RequestAborted).ConfigureAwait(false)),
                () => ValidateQuery(http, nameof(code), code))))
            .Produces<SignupCodeAvailability>().Produces<ErrorDetail>(StatusCodes.Status429TooManyRequests, ErrorDetailJson.MediaType);
        Public(endpoints.MapGet(ApplicantPrefix + "me/applications", (Func<HttpContext, Task<IResult>>)(http =>
            Applicant(http, source, write: false, async (service, _) => Results.Ok(await service.ListMineAsync(Token(http) ?? "", http.RequestAborted).ConfigureAwait(false))))))
            .Produces<SignupApplicantApplications>();
        Json(Public(endpoints.MapPost(ApplicantPrefix + "me/applications", (HttpContext http, SubmitSignupApplication input) =>
            Applicant(http, source, write: true, async (service, _) =>
            {
                var submission = await service.SubmitAsync(Token(http) ?? "", input, http.RequestAborted).ConfigureAwait(false);
                WriteCookie(http, submission.Session);
                return Results.Json(submission.Application, statusCode: StatusCodes.Status201Created);
            }, () => ValidateBody(input)))))
            .Produces<SignupApplicantApplication>(StatusCodes.Status201Created).Produces<ErrorDetail>(StatusCodes.Status422UnprocessableEntity, ErrorDetailJson.MediaType);
        Json(Public(endpoints.MapPost(ApplicantPrefix + "me/applications/{id:guid}:withdraw", (HttpContext http, Guid id) =>
            Applicant(http, source, write: true, async (service, _) =>
                Results.Ok(await service.WithdrawAsync(Token(http) ?? "", id, http.RequestAborted).ConfigureAwait(false)),
                validateAsync: () => EnsureEmptyBodyAsync(http.Request, requireJson: true)))))
            .Produces<SignupApplicantApplication>();
        return endpoints;
    }

    private static RouteHandlerBuilder Public(RouteHandlerBuilder route) => route.AllowAnonymous().WithMetadata(new PlatformHttpContract(TenancySignupDefinition.ComponentId))
        .Produces<ErrorDetail>(StatusCodes.Status400BadRequest, ErrorDetailJson.MediaType).Produces<ErrorDetail>(StatusCodes.Status401Unauthorized, ErrorDetailJson.MediaType)
        .Produces<ErrorDetail>(StatusCodes.Status403Forbidden, ErrorDetailJson.MediaType).Produces<ErrorDetail>(StatusCodes.Status404NotFound, ErrorDetailJson.MediaType)
        .Produces<ErrorDetail>(StatusCodes.Status409Conflict, ErrorDetailJson.MediaType);

    // 写请求只收 application/json。
    private static RouteHandlerBuilder Json(RouteHandlerBuilder route) => route.Produces<ErrorDetail>(StatusCodes.Status415UnsupportedMediaType, ErrorDetailJson.MediaType);

    // 处理顺序：写请求的同源核对 → 输入校验 → 组件上下文 → 执行。响应一律不缓存。
    private static async Task<IResult> Applicant(HttpContext http, Func<HttpContext, IPluginContext> source, bool write,
        Func<ITenantSignup, SignupRequestContext, Task<IResult>> execute, Action? validate = null, Func<Task>? validateAsync = null)
    {
        http.Response.Headers.CacheControl = "no-store";
        try
        {
            if (write && !SameOrigin(http.Request)) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
            validate?.Invoke();
            if (validateAsync is not null) await validateAsync().ConfigureAwait(false);
            var service = Service(http, source);
            var result = await execute(service, new SignupRequestContext(http.Connection.RemoteIpAddress?.ToString())).ConfigureAwait(false);
            return result;
        }
        catch (QianErrorException error)
        {
            // 会话无效时顺带清掉浏览器里的旧 Cookie。
            if (error.Error.Code == PlatformErrors.Unauthenticated) ClearCookie(http);
            return Failure(http, error);
        }
        catch (Exception error) when (error is ServiceNotReadyException or ObjectDisposedException)
        { return QianResults.Error(new QianError(PlatformErrors.NotFound)); }
    }

    // 同源：Origin 与请求的协议 + 主机一致；浏览器不带 Origin 时看 Sec-Fetch-Site。两者都没有的请求不是同源页面发出的，拒绝。
    private static bool SameOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin;
        if (origin.Count > 0)
            return origin.Count == 1 && string.Equals(origin[0], request.Scheme + "://" + request.Host.Value, StringComparison.OrdinalIgnoreCase);
        var site = request.Headers["Sec-Fetch-Site"];
        return site.Count == 1 && string.Equals(site[0], "same-origin", StringComparison.Ordinal);
    }

    private static string? Token(HttpContext http) => http.Request.Cookies.TryGetValue(CookieName, out var token) && !string.IsNullOrEmpty(token) ? token : null;

    private static QianErrorResult Unauthenticated(HttpContext http)
    {
        ClearCookie(http);
        return QianResults.Error(new QianError(PlatformErrors.Unauthenticated));
    }

    private static void WriteCookie(HttpContext http, SignupSessionGrant grant) => http.Response.Cookies.Append(CookieName, grant.Token, new CookieOptions
    {
        HttpOnly = true, SameSite = SameSiteMode.Strict, Path = CookiePath, Secure = RequiresSecureCookie(http), Expires = grant.ExpiresAt, IsEssential = true
    });

    private static void ClearCookie(HttpContext http)
    {
        if (!http.Request.Cookies.ContainsKey(CookieName) || http.Response.HasStarted) return;
        http.Response.Cookies.Delete(CookieName, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Path = CookiePath, Secure = RequiresSecureCookie(http), IsEssential = true });
    }
}
