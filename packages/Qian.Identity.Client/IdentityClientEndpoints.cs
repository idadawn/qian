using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kun.Identity.Abstractions;
using Kun.Identity.Abstractions.Session;
using Kun.Identity.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Identity.Client;

/// <summary>
/// 浏览器登录事务（服务端换码，BFF）：login 生成 PKCE + state + nonce 并跳转发行方；回调先消费一次性事务（state 校验与重放拒绝，
/// PKCE 材料随事务 Cookie 用后即删），再比对 RFC 9207 的 iss、读 error，然后带原始 verifier 与 nonce 换码，建立 <c>qian.sid</c> 会话。
/// 外壳直接换码的令牌端点属于 D5 的待定方案，这里不提供。
/// </summary>
internal static partial class IdentityClientEndpoints
{
    // OAuth 2.0 / OIDC / RFC 8707 定义的授权错误码；其余一律记为 server_error，不把发行方的任意文本带进错误详情。
    private static readonly HashSet<string> ProviderErrors = new(StringComparer.Ordinal)
    {
        "invalid_request", "unauthorized_client", "access_denied", "unsupported_response_type", "invalid_scope", "server_error", "temporarily_unavailable",
        "interaction_required", "login_required", "account_selection_required", "consent_required", "invalid_request_uri", "invalid_request_object",
        "request_not_supported", "request_uri_not_supported", "registration_not_supported", "invalid_target"
    };

    internal static async Task LoginAsync(HttpContext http)
    {
        var services = http.RequestServices; var options = services.GetRequiredService<IdentityClientOptions>();
        var returnTo = ReturnPath(options, http.Request.Query["returnTo"]);
        string url;
        try
        {
            url = await services.GetRequiredService<ChallengeHelper>().BuildLoginUrlAsync(http, IdentityClientDefaults.ProviderName, options.RedirectUri!.AbsoluteUri,
                returnTo, options.Scope, http.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or InvalidOperationException or JsonException or SecurityTokenException or TaskCanceledException && !http.RequestAborted.IsCancellationRequested)
        {
            Unavailable(http, error);
            await Error(http, PlatformErrors.BadGateway).ConfigureAwait(false);
            return;
        }
        http.Response.Redirect(url);
    }

    internal static async Task CallbackAsync(HttpContext http)
    {
        var services = http.RequestServices; var options = services.GetRequiredService<IdentityClientOptions>(); var ct = http.RequestAborted;
        var query = http.Request.Query;
        // 先消费事务：无论后续成败，事务 Cookie 都已删除、state 已作废。
        var transaction = await services.GetRequiredService<ChallengeHelper>().ConsumeAsync(http, Single(query, "state") ?? "", ct).ConfigureAwait(false);
        if (transaction is null || transaction.Client != IdentityClientDefaults.ProviderName || transaction.RedirectUri != options.RedirectUri!.AbsoluteUri)
        {
            await Reject(http, "state", IdentityErrors.OidcStateInvalid).ConfigureAwait(false);
            return;
        }
        var runtime = services.GetRequiredService<IdentityClientRuntime>();
        IOidcClient client; OidcConfiguration discovery;
        try { client = await runtime.ClientAsync(ct).ConfigureAwait(false); discovery = await client.DiscoverAsync(ct).ConfigureAwait(false); }
        catch (Exception error) when (error is HttpRequestException or InvalidOperationException or JsonException or SecurityTokenException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            Unavailable(http, error);
            await Error(http, PlatformErrors.BadGateway).ConfigureAwait(false);
            return;
        }
        var issuer = query["iss"];
        if (issuer.Count > 1 || issuer.Count == 1 && issuer[0] != discovery.Issuer || issuer.Count == 0 && options.RequireIssuerParameter)
        {
            await Reject(http, "iss", IdentityErrors.OidcIssuerMismatch).ConfigureAwait(false);
            return;
        }
        if (query.ContainsKey("error"))
        {
            var code = Single(query, "error") is { } value && ProviderErrors.Contains(value) ? value : "server_error";
            var logger = Logger(http);
            Log.ProviderRejected(logger, code);
            await Error(http, IdentityErrors.OidcProviderError, new Dictionary<string, string> { ["error"] = code }).ConfigureAwait(false);
            return;
        }
        if (Single(query, "code") is not { Length: > 0 and <= 2048 } authorizationCode)
        {
            await Reject(http, "code", IdentityErrors.OidcStateInvalid).ConfigureAwait(false);
            return;
        }
        OidcTokenResponse tokens;
        try
        {
            tokens = await client.ExchangeCodeAsync(new OidcCodeExchange(authorizationCode, options.RedirectUri.AbsoluteUri, transaction.CodeVerifier, transaction.Nonce), ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or InvalidOperationException or JsonException or SecurityTokenException or ArgumentException or FormatException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            await Reject(http, "exchange:" + error.GetType().Name, IdentityErrors.OidcTokenInvalid).ConfigureAwait(false);
            return;
        }
        // 规范模式：会话建立前就确认访问令牌能投影出与 id_token 同一主体的规范身份，避免登录成功却始终无法认证。
        if (options.Resource is not null && (tokens.ValidatedPrincipal?.Identity is not ClaimsIdentity idToken ||
            await services.GetRequiredService<IdentityAccessTokens>().ValidateAsync(tokens.AccessToken, sessionOnly: true, ct).ConfigureAwait(false) is not { } identity ||
            !identity.Matches(idToken)))
        {
            await Reject(http, "access_token", IdentityErrors.OidcTokenInvalid).ConfigureAwait(false);
            return;
        }
        var previous = http.Request.Cookies[IdentityClientDefaults.SessionCookieName];
        await services.GetRequiredService<SignInHelper>().SignInAsync(http, tokens, ct).ConfigureAwait(false);
        // 新会话总是新标识；同一浏览器的旧会话随之作废，不留孤儿会话。
        if (!string.IsNullOrEmpty(previous)) await services.GetRequiredService<ISessionStore>().DeleteAsync(previous, ct).ConfigureAwait(false);
        http.Response.Redirect(transaction.ReturnUrl);
    }

    // 防伪校验由 Kun Cookie 中间件对已认证的非安全方法统一执行；没有会话时登出是幂等的空操作。
    internal static async Task LogoutAsync(HttpContext http)
    {
        await http.RequestServices.GetRequiredService<SignInHelper>().SignOutAsync(http, http.RequestAborted).ConfigureAwait(false);
        http.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    internal static async Task CsrfAsync(HttpContext http)
    {
        if (http.User.Identity?.IsAuthenticated != true)
        {
            await Error(http, PlatformErrors.Unauthenticated).ConfigureAwait(false);
            return;
        }
        var tokens = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        await http.Response.WriteAsJsonAsync(new CsrfToken(tokens.HeaderName ?? IdentityClientDefaults.AntiforgeryHeader, tokens.RequestToken!),
            IdentityClientJsonContext.Default.CsrfToken, cancellationToken: http.RequestAborted).ConfigureAwait(false);
    }

    // returnTo：同源相对路径，解码后不得含 . / .. 段（浏览器会先归一化路径，否则可绕过白名单）；白名单按路径段前缀匹配；不合法时回缺省落点。
    internal static string ReturnPath(IdentityClientOptions options, StringValues values)
    {
        if (values.Count != 1 || !IdentityClientOptions.IsLocalPath(values[0])) return options.DefaultReturnPath;
        var value = values[0]!;
        var end = value.IndexOfAny(['?', '#']);
        string path;
        try { path = Uri.UnescapeDataString(end < 0 ? value : value[..end]); }
        catch (UriFormatException) { return options.DefaultReturnPath; }
        if (!IdentityClientOptions.IsLocalPath(path) || path.Split('/').Any(segment => segment is "." or "..")) return options.DefaultReturnPath;
        return options.AllowedReturnPaths.Count == 0 || options.AllowedReturnPaths.Any(prefix => prefix == "/" || path == prefix.TrimEnd('/') ||
            path.StartsWith(prefix.TrimEnd('/') + "/", StringComparison.Ordinal)) ? value : options.DefaultReturnPath;
    }

    private static string? Single(IQueryCollection query, string name) => query[name] is { Count: 1 } values ? values[0] : null;

    private static Task Reject(HttpContext http, string reason, ErrorCode code)
    {
        var logger = Logger(http);
        Log.CallbackRejected(logger, reason);
        return Error(http, code);
    }

    private static Task Error(HttpContext http, ErrorCode code, IReadOnlyDictionary<string, string>? args = null) =>
        QianResults.Error(new QianError(code, args)).ExecuteAsync(http);

    private static void Unavailable(HttpContext http, Exception error)
    {
        var logger = Logger(http);
        var type = error.GetType().Name;
        Log.ProviderUnavailable(logger, type);
    }

    private static ILogger Logger(HttpContext http) => http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Qian.Identity.Client");

    private static partial class Log
    {
        [LoggerMessage(EventId = 40, Level = LogLevel.Information, Message = "Identity Client 拒绝登录回调；reason={Reason}。")]
        internal static partial void CallbackRejected(ILogger logger, string reason);
        [LoggerMessage(EventId = 41, Level = LogLevel.Warning, Message = "Identity Client 无法访问发行方；error_type={ErrorType}。")]
        internal static partial void ProviderUnavailable(ILogger logger, string errorType);
        [LoggerMessage(EventId = 42, Level = LogLevel.Information, Message = "Identity Client 收到发行方的授权错误；error={Error}。")]
        internal static partial void ProviderRejected(ILogger logger, string error);
    }
}

internal sealed record CsrfToken(string HeaderName, string Token);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CsrfToken))]
internal sealed partial class IdentityClientJsonContext : JsonSerializerContext;
