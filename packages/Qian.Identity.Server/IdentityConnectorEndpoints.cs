using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Identity.Server;

/// <summary>
/// 连接器登录的 HTTP 面（PRD 7.4.2）：按租户 scope 列出连接器与详情（清单，不含任何配置值或密钥）、发起、统一回调
/// <c>/connect/callback/{connectorId}</c>、界面落点读取流程、确认绑定两步。未挂载、被过滤与不存在的连接器一律 404。
/// 流程与发起它的浏览器绑定：浏览器密钥放在 HttpOnly、SameSite=Lax 的 Cookie 里（回调是上游发起的顶级 GET 跳转，Strict 收不到）。
/// </summary>
internal static class IdentityConnectorEndpoints
{
    internal const string BrowserCookie = "qian.identity.browser";
    private const int MaxCallbackParameters = 32;
    private const int MaxCallbackValue = 4096;

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        var prefix = IdentityAccountEndpoints.Prefix;
        endpoints.MapGet(prefix + "/connectors", (Delegate)List);
        endpoints.MapGet(prefix + "/connectors/{connectorId}", (Delegate)Describe);
        endpoints.MapPost(prefix + "/connectors/{connectorId}/begin", (Delegate)Begin);
        endpoints.MapGet(ConnectorManifest.CallbackTemplate, (Delegate)Callback);
        endpoints.MapGet(prefix + "/connector-flows/{flow}", (Delegate)Flow);
        endpoints.MapPost(prefix + "/connector-flows/{flow}/signin", (Delegate)FlowSignIn);
        endpoints.MapPost(prefix + "/connector-flows/{flow}/bind", (Delegate)FlowBind);
    }

    // ---- 请求与响应 ----

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record BeginBody([property: JsonPropertyName("client_id")] string? ClientId, [property: JsonPropertyName("tenant")] string? Tenant,
        [property: JsonPropertyName("return_url")] string? ReturnUrl = null);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record SignInBody([property: JsonPropertyName("identifier")] string? Identifier, [property: JsonPropertyName("password")] string? Password,
        [property: JsonPropertyName("captcha")] IdentityAccountEndpoints.CaptchaBody? Captcha = null);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record BindBody([property: JsonPropertyName("mode")] string? Mode);

    private sealed record ConnectorView([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("protocol")] string Protocol, [property: JsonPropertyName("terminals")] IReadOnlyList<string> Terminals);

    // ---- 端点 ----

    private static IdentityAccountService? Accounts(HttpContext http) => http.RequestServices.GetRequiredService<IdentityAccountGate>().Service;

    private static QianErrorResult NotFound() => QianResults.Error(new QianError(PlatformErrors.NotFound));

    // 登录页的连接器区：只来自服务端，按租户号对应的 scope 过滤；未知租户号回落全局集合，不报错也不泄露租户是否存在。
    private static async Task<IResult> List(HttpContext http, string? tenant)
    {
        if (Accounts(http) is not { } accounts) return NotFound();
        var found = await accounts.FindTenantAsync(tenant, http.RequestAborted).ConfigureAwait(false);
        var items = accounts.ConnectorsIn(found?.Id).List().Select(d => new ConnectorView(d.Id, d.DisplayName, d.Protocol, d.Terminals)).ToArray();
        return Results.Json(new { items }, IdentityAccountEndpoints.JsonOptions);
    }

    // 详情即连接器清单：协议、终端、配置 Schema、回调模板与本环境的回调地址（供在上游登记）。不含任何配置值，密钥与其引用都不回传。
    private static async Task<IResult> Describe(HttpContext http, string connectorId, string? tenant)
    {
        if (Accounts(http) is not { } accounts) return NotFound();
        var found = await accounts.FindTenantAsync(tenant, http.RequestAborted).ConfigureAwait(false);
        if (accounts.ConnectorsIn(found?.Id).Find(connectorId)?.Descriptor is not { } descriptor) return NotFound();
        return Results.Json(new
        {
            id = descriptor.Id, display_name = descriptor.DisplayName, protocol = descriptor.Protocol, terminals = descriptor.Terminals,
            config_schema = descriptor.ConfigSchema, callback_template = descriptor.CallbackTemplate, callback_uri = CallbackUri(http, descriptor.Id).AbsoluteUri
        }, IdentityAccountEndpoints.JsonOptions);
    }

    private static async Task<IResult> Begin(HttpContext http, string connectorId)
    {
        if (Accounts(http) is not { } accounts) return NotFound();
        if (await IdentityAccountEndpoints.Guard(http).ConfigureAwait(false) is { } rejected) return rejected;
        var body = await IdentityAccountEndpoints.Read<BeginBody>(http).ConfigureAwait(false);
        if (body is null) return IdentityAccountEndpoints.BadInput();
        if (!ConnectorManifest.IsValidId(connectorId)) return NotFound();
        var browser = EnsureBrowser(http);
        try
        {
            var start = await accounts.BeginConnectorAsync(connectorId, body.ClientId, body.Tenant, body.ReturnUrl, browser, CallbackUri(http, connectorId),
                IdentityAccountEndpoints.ContextOf(http), http.RequestAborted).ConfigureAwait(false);
            return Results.Json(new { flow = start.Flow, redirect_uri = start.RedirectUri.AbsoluteUri, expires_at = start.ExpiresAt }, IdentityAccountEndpoints.JsonOptions);
        }
        catch (QianErrorException error) { return QianResults.Error(error.Error); }
    }

    // 上游回调：处理后 302 回界面落点（?flow=），由流程的 next 驱动下一步；state 无效（未知、过期之外的篡改、别的浏览器）直接 422。
    private static async Task<IResult> Callback(HttpContext http, string connectorId)
    {
        if (Accounts(http) is not { } accounts) return NotFound();
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        var malformed = http.Request.Query.Count > MaxCallbackParameters;
        // 同名参数出现多次即视为篡改：只收单值参数，重复的 state 自然缺失。
        foreach (var (name, values) in http.Request.Query)
        {
            if (values.Count != 1 || values[0] is not { } value) continue;
            if (value.Length > MaxCallbackValue) { malformed = true; break; }
            parameters[name] = value;
        }
        if (malformed) parameters.Remove("state");
        var result = await accounts.CompleteConnectorAsync(connectorId, parameters, BrowserSecret(http), CallbackUri(http, connectorId), IdentityAccountEndpoints.ContextOf(http),
            http.RequestAborted).ConfigureAwait(false);
        if (result.Flow is not { } flow) return QianResults.Error(result.Error ?? new QianError(PlatformErrors.NotFound));
        if (result.Session is { } session) await SignInAsync(http, session).ConfigureAwait(false);
        return Results.Redirect(Landing(http, accounts, connectorId, flow));
    }

    private static async Task<IResult> Flow(HttpContext http, Guid flow)
    {
        if (Accounts(http) is not { } accounts) return NotFound();
        var view = await accounts.GetConnectorFlowAsync(flow, BrowserSecret(http), http.RequestAborted).ConfigureAwait(false);
        return view is null ? NotFound() : Results.Json(Render(http, view), IdentityAccountEndpoints.JsonOptions);
    }

    // 确认绑定第一步：登录已有账号（与密码登录同一条限流与审计）。成功返回更新后的流程，不建立会话。
    private static async Task<IResult> FlowSignIn(HttpContext http, Guid flow)
    {
        if (Accounts(http) is not { } accounts) return NotFound();
        if (await IdentityAccountEndpoints.Guard(http).ConfigureAwait(false) is { } rejected) return rejected;
        var body = await IdentityAccountEndpoints.Read<SignInBody>(http).ConfigureAwait(false);
        if (body is null) return IdentityAccountEndpoints.BadInput();
        using var proof = body.Password is null ? null : new AuthenticationProof(Encoding.UTF8.GetBytes(body.Password));
        var input = new LoginInput("", null, null, null, proof)
        {
            Identifier = body.Identifier, Captcha = body.Captcha is null ? null : new CaptchaAnswer(body.Captcha.Id, body.Captcha.Answer)
        };
        var (view, failure) = await accounts.VerifyConnectorAccountAsync(flow, BrowserSecret(http), input, IdentityAccountEndpoints.ContextOf(http), http.RequestAborted).ConfigureAwait(false);
        if (failure is not null)
        {
            if (failure.RetryAfter is { } wait) http.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            return QianResults.Error(failure.Error!);
        }
        return Results.Json(Render(http, view!), IdentityAccountEndpoints.JsonOptions);
    }

    // 确认绑定第二步：link / create / cancel。成功写入账号 Cookie，返回去处（流程里的 return_url）。
    private static async Task<IResult> FlowBind(HttpContext http, Guid flow)
    {
        if (Accounts(http) is not { } accounts) return NotFound();
        if (await IdentityAccountEndpoints.Guard(http).ConfigureAwait(false) is { } rejected) return rejected;
        var body = await IdentityAccountEndpoints.Read<BindBody>(http).ConfigureAwait(false);
        if (body is null) return IdentityAccountEndpoints.BadInput();
        var result = await accounts.ConfirmConnectorBindingAsync(flow, BrowserSecret(http), body.Mode, IdentityAccountEndpoints.ContextOf(http), http.RequestAborted).ConfigureAwait(false);
        if (result is null) return Results.NoContent();
        if (result.Status != SignInStatus.Authenticated) return QianResults.Error(result.Error ?? new QianError(PlatformErrors.Internal));
        var session = result.Session!;
        await SignInAsync(http, session).ConfigureAwait(false);
        var view = await accounts.GetConnectorFlowAsync(flow, BrowserSecret(http), http.RequestAborted).ConfigureAwait(false);
        return Results.Json(new { status = "authenticated", tenant_id = session.TenantId, membership_id = session.MembershipId, return_url = view?.ReturnUrl },
            IdentityAccountEndpoints.JsonOptions);
    }

    // ---- 公共 ----

    private static Task SignInAsync(HttpContext http, AccountSession session) =>
        http.SignInAsync(http.RequestServices.GetRequiredService<IdentityServerOptions>().SignInScheme, IdentityAccountEndpoints.CookiePrincipal(session),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = session.ExpiresAt });

    // 回调地址取自受信的 issuer，不取请求的 Host 头。
    private static Uri CallbackUri(HttpContext http, string connectorId)
    {
        var issuer = http.RequestServices.GetRequiredService<IdentityServerOptions>().Issuer.AbsoluteUri;
        return new Uri(new Uri(issuer.EndsWith('/') ? issuer : issuer + "/"), ConnectorManifest.CallbackPath(connectorId).TrimStart('/'));
    }

    private static string Landing(HttpContext http, IdentityAccountService accounts, string connectorId, Guid flow) =>
        http.Request.PathBase + accounts.ConnectorLanding.Replace("{connectorId}", connectorId, StringComparison.Ordinal) + "?flow=" + flow.ToString("D");

    private static string? BrowserSecret(HttpContext http) =>
        http.Request.Cookies.TryGetValue(BrowserCookie, out var value) && value is { Length: 43 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? value : null;

    // 同一浏览器的多个流程共用一个浏览器密钥；每个流程只存它的哈希。
    private static string EnsureBrowser(HttpContext http)
    {
        if (BrowserSecret(http) is { } existing) return existing;
        var secret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        http.Response.Cookies.Append(BrowserCookie, secret, new CookieOptions
        {
            HttpOnly = true, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Lax, IsEssential = true,
            Path = http.Request.PathBase.HasValue ? http.Request.PathBase.Value : "/"
        });
        return secret;
    }

    private static Dictionary<string, object?> Render(HttpContext http, IdentityAccountService.ConnectorFlowView view)
    {
        ErrorDetail? error = null;
        if (view.Error is { } failure)
        {
            var catalog = http.RequestServices.GetRequiredService<ErrorCatalog>();
            var trace = System.Diagnostics.Activity.Current is { } activity && activity.TraceId != default ? activity.TraceId.ToHexString() : System.Diagnostics.ActivityTraceId.CreateRandom().ToHexString();
            error = catalog.CreateDetail(failure, trace, CultureInfo.CurrentUICulture);
        }
        return new Dictionary<string, object?>
        {
            ["flow"] = view.Id, ["status"] = view.Status, ["next"] = view.Next, ["expires_at"] = view.ExpiresAt,
            ["connector"] = new Dictionary<string, object?> { ["id"] = view.ConnectorId, ["display_name"] = view.ConnectorName, ["protocol"] = view.Protocol },
            ["tenant"] = new Dictionary<string, object?> { ["code"] = view.TenantCode, ["name"] = view.TenantName },
            ["binding"] = view.Binding is not { } binding ? null : new Dictionary<string, object?>
            {
                ["external"] = new Dictionary<string, object?> { ["display_name"] = binding.DisplayName, ["email"] = binding.Email, ["username"] = binding.Username },
                ["expires_at"] = binding.ExpiresAt, ["allow_jit"] = binding.AllowJit, ["verified"] = binding.Verified,
                ["account"] = binding.Verified ? new Dictionary<string, object?> { ["display_name"] = binding.AccountDisplayName, ["account"] = binding.Account } : null
            },
            ["error"] = error is null ? null : JsonSerializer.SerializeToElement(error, ErrorDetailJson.TypeInfo),
            ["upstream_error_description"] = view.UpstreamDescription,
            ["return_url"] = view.ReturnUrl
        };
    }
}
