using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Qian.Capabilities;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Tenancy.Signup;

/// <summary>
/// 开通申请的两组 HTTP 端点（规格 §5）。申请人接口是公开接口，须挂在身份服务的公开入口上，与认证页族同源，不经业务的租户入口管线；
/// 运营接口在宿主认证之后从 global 的 <c>qian.tenancy.signup</c> 上下文解析，按 R113 同序完成平台授权。两组端点都只登记契约归属
/// （<see cref="PlatformHttpContract"/>），不进租户可见的聚合文档。
/// </summary>
public static partial class TenancySignupEndpoints
{
    /// <summary>申请人会话 Cookie 的名称。</summary>
    public const string CookieName = "qian.signup";
    /// <summary>申请人会话 Cookie 的路径，也是全部开通申请接口的前缀。</summary>
    public const string CookiePath = "/api/signup";
    private const string ApplicantPrefix = "/api/signup/";
    private static readonly string[] ApplicantPaths = ["/api/signup/verifications", "/api/signup/session", "/api/signup/code-availability", "/api/signup/me"];

    /// <summary>
    /// 是否申请人接口（发码、会话、租户号可用性、本人的申请）。混合部署的宿主用它把申请人请求分到身份服务的公开分支，
    /// 运营接口（<c>/api/signup/applications</c>、<c>/api/signup/meta</c>）不在其内。
    /// </summary>
    public static bool IsApplicantPath(PathString path) => ApplicantPaths.Any(prefix => path.StartsWithSegments(prefix, StringComparison.Ordinal));

    private static ITenantSignup Service(HttpContext http, Func<HttpContext, IPluginContext> source)
    {
        var ctx = source(http);
        if (ctx.Scope != ScopeKey.Global || ctx.Entry.Plugin != TenancySignupDefinition.ComponentId) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        return ctx.TenancySignup();
    }

    // 契约里的长度、取值与格式声明在执行之前按同一组标注检查；未知字段与类型不符在绑定时就已拒绝（400 BAD_REQUEST）。
    private static void ValidateBody(object body)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(body, new ValidationContext(body), results, validateAllProperties: true)) return;
        var member = results[0].MemberNames.FirstOrDefault();
        throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Pointer = member is null ? "#" : "#/" + JsonNamingPolicy.CamelCase.ConvertName(member) });
    }

    private static void ValidateQuery(HttpContext http, string name, object? value)
    {
        var parameter = http.GetEndpoint()!.Metadata.OfType<IParameterBindingMetadata>().Single(item => item.Name == name).ParameterInfo;
        if (value is null && parameter.HasDefaultValue && parameter.DefaultValue is null) return;
        if (parameter.GetCustomAttributes<ValidationAttribute>().Any(rule => !rule.IsValid(value)))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Pointer = "#/" + name });
    }

    // 没有请求体的写操作：请求体可以缺省、为空或是空对象，带任何字段一律 400（与有请求体的接口拒绝未知字段同口径）。
    private static async Task EnsureEmptyBodyAsync(HttpRequest request, bool requireJson)
    {
        if (requireJson && !request.HasJsonContentType()) throw new QianErrorException(new QianError(PlatformErrors.UnsupportedMediaType));
        if (request.ContentLength is > 1024) throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var buffer = new char[1025]; var length = 0; int read;
        while (length < buffer.Length && (read = await reader.ReadAsync(buffer.AsMemory(length), request.HttpContext.RequestAborted).ConfigureAwait(false)) > 0) length += read;
        if (length > 1024) throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
        var text = new string(buffer, 0, length);
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Object && !document.RootElement.EnumerateObject().Any()) return;
        }
        catch (JsonException) { }
        throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
    }

    private static QianErrorResult Failure(HttpContext http, QianErrorException error)
    {
        if (error.Error.Code == PlatformErrors.RateLimited && error.Data[TenancySignupErrors.RetryAfterDataKey] is int seconds)
            http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return QianResults.Error(error.Error);
    }

    // 回环地址上的本机联调（请求的主机名与连接都是回环）可以不带 Secure；其余一律 Secure。
    private static bool RequiresSecureCookie(HttpContext http)
    {
        if (http.Request.IsHttps) return true;
        var host = http.Request.Host.Host;
        var loopbackHost = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));
        return !(loopbackHost && http.Connection.RemoteIpAddress is { } remote && IPAddress.IsLoopback(remote));
    }
}
