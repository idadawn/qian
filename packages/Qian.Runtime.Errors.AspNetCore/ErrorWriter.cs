using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Qian.Runtime.Errors.AspNetCore;

/// <summary>按错误码目录渲染并写出 ErrorDetail。</summary>
internal static class ErrorWriter
{
    private static readonly object s_traceIdKey = new();

    public static Task WriteAsync(HttpContext http, QianError error, CancellationToken cancellationToken)
    {
        var catalog = http.RequestServices.GetRequiredService<ErrorCatalog>();
        var culture = CultureInfo.CurrentUICulture;
        var detail = catalog.CreateDetail(error, TraceId(http), culture, Instance(http));
        http.Response.StatusCode = detail.Status;
        http.Response.Headers.ContentLanguage = error.Code.ResolveLocale(culture);
        return http.Response.WriteAsJsonAsync(detail, ErrorDetailJson.TypeInfo, ErrorDetailJson.MediaType, cancellationToken);
    }

    /// <summary>当前请求的 W3C trace-id；没有活动 trace 时为本请求生成一个并在请求内保持不变。</summary>
    public static string TraceId(HttpContext http)
    {
        if (Activity.Current is { } activity && activity.TraceId != default)
            return activity.TraceId.ToHexString();
        if (http.Items.TryGetValue(s_traceIdKey, out var cached) && cached is string id)
            return id;
        id = ActivityTraceId.CreateRandom().ToHexString();
        http.Items[s_traceIdKey] = id;
        return id;
    }

    public static string? Instance(HttpContext http)
    {
        var path = http.Request.PathBase.Add(http.Request.Path);
        return path.HasValue ? path.Value : null;
    }
}

/// <summary>把 <see cref="QianErrorException"/> 与请求体解析失败写成 ErrorDetail；其余异常交给默认处理（规范为 INTERNAL）。</summary>
internal sealed class QianExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var error = exception switch
        {
            QianErrorException q => q.Error,
            Kun.Validation.ValidationException validation => validation.ToQianError(),
            BadHttpRequestException b when PlatformErrors.ForStatus(b.StatusCode).Status == b.StatusCode
                => new QianError(PlatformErrors.ForStatus(b.StatusCode)),
            _ => null,
        };
        if (error is null || httpContext.Response.HasStarted)
            return false;

        await ErrorWriter.WriteAsync(httpContext, error, cancellationToken).ConfigureAwait(false);
        return true;
    }
}

/// <summary>
/// 把任何经 <see cref="IProblemDetailsService"/> 写出的 ProblemDetails 规范为 ErrorDetail 形状：
/// 错误码取扩展成员 <c>code</c>（须已登记、非字段级、状态码一致且无占位符），否则按状态码兜底；
/// 文案一律取错误码目录，丢弃其它扩展成员与 detail 原文（不泄露异常细节）。
/// </summary>
internal static class ProblemDetailsNormalizer
{
    public static void Normalize(ProblemDetailsContext context)
    {
        var http = context.HttpContext;
        var problem = context.ProblemDetails;
        var catalog = http.RequestServices.GetRequiredService<ErrorCatalog>();

        var status = problem.Status ?? (http.Response.StatusCode >= 400 ? http.Response.StatusCode : 500);
        var code = Pick(catalog, problem, status);
        var culture = CultureInfo.CurrentUICulture;
        var message = code.FormatMessage(culture);

        problem.Type = code.Type;
        problem.Title = code.GetTitle(culture);
        problem.Status = status;
        problem.Detail = message;
        problem.Instance = ErrorWriter.Instance(http);
        problem.Extensions.Clear();
        problem.Extensions["code"] = code.Code;
        problem.Extensions["message"] = message;
        problem.Extensions["trace_id"] = ErrorWriter.TraceId(http);

        if (!http.Response.HasStarted)
            http.Response.Headers.ContentLanguage = code.ResolveLocale(culture);
    }

    private static ErrorCode Pick(ErrorCatalog catalog, Microsoft.AspNetCore.Mvc.ProblemDetails problem, int status)
    {
        if (problem.Extensions.TryGetValue("code", out var raw)
            && raw is string name
            && catalog.TryGet(name, out var code)
            && code.Kind != ErrorKind.Field
            && code.Status == status
            && code.Parameters.Count == 0)
        {
            return code;
        }
        return PlatformErrors.ForStatus(status);
    }
}
