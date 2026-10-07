using Microsoft.AspNetCore.Http;

namespace Qian.Runtime.Errors.AspNetCore;

/// <summary>端点返回错误的结果工厂。</summary>
public static class QianResults
{
    /// <summary>把一次出错写成 problem+json 的 ErrorDetail（状态码取错误码的状态码）。</summary>
    public static QianErrorResult Error(QianError error) => new(error);
}

/// <summary>写出 ErrorDetail 的 <see cref="IResult"/>。</summary>
public sealed class QianErrorResult : IResult, IStatusCodeHttpResult, IContentTypeHttpResult
{
    internal QianErrorResult(QianError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    /// <summary>出错内容。</summary>
    public QianError Error { get; }

    /// <inheritdoc />
    public int? StatusCode => Error.Code.Status;

    /// <inheritdoc />
    public string ContentType => ErrorDetailJson.MediaType;

    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return ErrorWriter.WriteAsync(httpContext, Error, httpContext.RequestAborted);
    }
}
