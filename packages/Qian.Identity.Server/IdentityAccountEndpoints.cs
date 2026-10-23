using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Identity.Server;

/// <summary>
/// 内置账号模型的 HTTP 面：登录、选择租户、强制改密、切换租户与自助改密。成功登录后写入可信的服务端账号 Cookie，
/// 由 <c>/connect/authorize</c> 据此签发授权码；Cookie 的内容只来自流水线，从不接受请求体里的主体。
/// 所有写接口都要求防伪令牌（先 GET <c>/api/identity/csrf</c>），响应一律 no-store。
/// </summary>
public static class IdentityAccountEndpoints
{
    internal const string Prefix = "/api/identity";
    private const int MaxBodyBytes = 16 * 1024;
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>映射账号接口；组件卸载后返回 404。须与 <c>MapQianIdentityServer</c> 一起使用。</summary>
    public static IEndpointRouteBuilder MapQianIdentityAccounts(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet(Prefix + "/csrf", (Delegate)Csrf);
        endpoints.MapGet(Prefix + "/captcha", (Delegate)Captcha);
        endpoints.MapPost(Prefix + "/login", (Delegate)Login);
        endpoints.MapPost(Prefix + "/login/select", (Delegate)Select);
        endpoints.MapPost(Prefix + "/login/password", (Delegate)ChangeInitialPassword);
        endpoints.MapPost(Prefix + "/session/switch", (Delegate)Switch);
        endpoints.MapGet(Prefix + "/me", (Delegate)Me);
        endpoints.MapPost(Prefix + "/me/password", (Delegate)ChangeOwnPassword);
        IdentitySessionEndpoints.Map(endpoints);
        IdentityDirectoryEndpoints.Map(endpoints);
        IdentityConnectorEndpoints.Map(endpoints);
        return endpoints;
    }

    // ---- 请求与响应 ----

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    internal sealed record CaptchaBody([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("answer")] string Answer);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record LoginBody(
        [property: JsonPropertyName("client_id")] string? ClientId, [property: JsonPropertyName("identifier")] string? Identifier,
        [property: JsonPropertyName("password")] string? Password, [property: JsonPropertyName("tenant_id")] Guid? TenantId = null,
        [property: JsonPropertyName("captcha")] CaptchaBody? Captcha = null);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record SelectBody([property: JsonPropertyName("ticket")] string? Ticket, [property: JsonPropertyName("membership_id")] Guid? MembershipId = null);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record ChangeInitialBody([property: JsonPropertyName("ticket")] string? Ticket, [property: JsonPropertyName("new_password")] string? NewPassword);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record ChangeOwnBody([property: JsonPropertyName("current_password")] string? CurrentPassword, [property: JsonPropertyName("new_password")] string? NewPassword);

    private sealed record ChoiceView(
        [property: JsonPropertyName("membership_id")] Guid? MembershipId, [property: JsonPropertyName("tenant_id")] Guid? TenantId,
        [property: JsonPropertyName("tenant_code")] string? TenantCode, [property: JsonPropertyName("tenant_name")] string? TenantName,
        [property: JsonPropertyName("display_name")] string DisplayName, [property: JsonPropertyName("platform")] bool Platform,
        [property: JsonPropertyName("tenant_admin")] bool TenantAdmin);

    private sealed record SignInView(
        [property: JsonPropertyName("status")] string Status, [property: JsonPropertyName("ticket")] string? Ticket = null,
        [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt = null, [property: JsonPropertyName("choices")] IReadOnlyList<ChoiceView>? Choices = null,
        [property: JsonPropertyName("tenant_id")] Guid? TenantId = null, [property: JsonPropertyName("membership_id")] Guid? MembershipId = null,
        [property: JsonPropertyName("platform")] bool? Platform = null);

    // ---- 端点 ----

    internal static IdentityAccountService Service(HttpContext http) => http.RequestServices.GetRequiredService<IdentityAccountGate>().Service!;

    private static IResult Csrf(HttpContext http)
    {
        var tokens = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        return Results.Json(new { header = tokens.HeaderName, token = tokens.RequestToken });
    }

    private static async Task<IResult> Captcha(HttpContext http)
    {
        try
        {
            var challenge = await Service(http).IssueCaptchaAsync(http.RequestAborted).ConfigureAwait(false);
            return Results.Json(new { id = challenge.Id, image = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(challenge.ImageSvg)), expires_at = challenge.ExpiresAt });
        }
        catch (QianErrorException error) { return QianResults.Error(error.Error); }
    }

    private static async Task<IResult> Login(HttpContext http)
    {
        if (await Guard(http).ConfigureAwait(false) is { } rejected) return rejected;
        var body = await Read<LoginBody>(http).ConfigureAwait(false);
        if (body is null) return BadInput();
        using var proof = body.Password is null ? null : new AuthenticationProof(Encoding.UTF8.GetBytes(body.Password));
        var input = new LoginInput(body.ClientId ?? "", body.TenantId, null, null, proof)
        {
            Identifier = body.Identifier, Captcha = body.Captcha is null ? null : new CaptchaAnswer(body.Captcha.Id, body.Captcha.Answer)
        };
        return await Complete(http, await Service(http).SignInAsync(input, ContextOf(http), http.RequestAborted).ConfigureAwait(false)).ConfigureAwait(false);
    }

    private static async Task<IResult> Select(HttpContext http)
    {
        if (await Guard(http).ConfigureAwait(false) is { } rejected) return rejected;
        var body = await Read<SelectBody>(http).ConfigureAwait(false);
        if (body?.Ticket is null) return BadInput();
        return await Complete(http, await Service(http).SelectAsync(new TenantSelection(body.Ticket, body.MembershipId), ContextOf(http), http.RequestAborted).ConfigureAwait(false)).ConfigureAwait(false);
    }

    private static async Task<IResult> ChangeInitialPassword(HttpContext http)
    {
        if (await Guard(http).ConfigureAwait(false) is { } rejected) return rejected;
        var body = await Read<ChangeInitialBody>(http).ConfigureAwait(false);
        if (body?.Ticket is null || body.NewPassword is null) return BadInput();
        using var next = new AuthenticationProof(Encoding.UTF8.GetBytes(body.NewPassword));
        return await Complete(http, await Service(http).ChangeInitialPasswordAsync(new InitialPasswordChange(body.Ticket, next), ContextOf(http), http.RequestAborted).ConfigureAwait(false)).ConfigureAwait(false);
    }

    private static async Task<IResult> Switch(HttpContext http)
    {
        if (await Guard(http).ConfigureAwait(false) is { } rejected) return rejected;
        if (await CurrentSession(http).ConfigureAwait(false) is not { } current) return QianResults.Error(new QianError(PlatformErrors.Unauthenticated));
        return await Complete(http, await Service(http).BeginSwitchAsync(current, ContextOf(http), http.RequestAborted).ConfigureAwait(false)).ConfigureAwait(false);
    }

    private static async Task<IResult> Me(HttpContext http)
    {
        if (await CurrentSession(http).ConfigureAwait(false) is not { } current) return QianResults.Error(new QianError(PlatformErrors.Unauthenticated));
        var validation = await Service(http).ValidateAsync(current.SessionId, current.PersonId, current.MembershipId, http.RequestAborted).ConfigureAwait(false);
        if (validation is null) return QianResults.Error(new QianError(PlatformErrors.Unauthenticated));
        return Results.Json(new
        {
            person_id = current.PersonId, tenant_id = validation.TenantId, membership_id = validation.MembershipId, identity_revision = validation.IdentityRevision,
            amr = current.Amr, acr = current.Acr, client_id = current.ClientId, device_type = current.DeviceType, expires_at = current.ExpiresAt
        }, JsonOptions);
    }

    private static async Task<IResult> ChangeOwnPassword(HttpContext http)
    {
        if (await Guard(http).ConfigureAwait(false) is { } rejected) return rejected;
        if (await CurrentSession(http).ConfigureAwait(false) is not { } current) return QianResults.Error(new QianError(PlatformErrors.Unauthenticated));
        var body = await Read<ChangeOwnBody>(http).ConfigureAwait(false);
        if (body?.CurrentPassword is null || body.NewPassword is null) return BadInput();
        using var existing = new AuthenticationProof(Encoding.UTF8.GetBytes(body.CurrentPassword));
        using var next = new AuthenticationProof(Encoding.UTF8.GetBytes(body.NewPassword));
        try { await Service(http).ChangePasswordAsync(current.PersonId, new PasswordChange(existing, next), http.RequestAborted).ConfigureAwait(false); }
        catch (QianErrorException error) { return QianResults.Error(error.Error); }
        // 改密使该 Person 的全部会话失效，当前 Cookie 随之作废，需要重新登录。
        await http.SignOutAsync(http.RequestServices.GetRequiredService<IdentityServerOptions>().SignInScheme).ConfigureAwait(false);
        return Results.NoContent();
    }

    // ---- 公共 ----

    // 写接口：JSON、体积上限与防伪令牌。登录会写会话 Cookie，缺少防伪令牌就是登录型 CSRF（把受害者登进攻击者的账号）。
    internal static async Task<IResult?> Guard(HttpContext http)
    {
        if (!http.Request.HasJsonContentType()) return QianResults.Error(new QianError(PlatformErrors.UnsupportedMediaType));
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit) limit.MaxRequestBodySize = MaxBodyBytes;
        if (http.Request.ContentLength is > MaxBodyBytes)
        {
            // 超限的请求体不读取也不排空：响应后直接关闭连接，否则服务器在收尾时才发现超限，会把已写好的 413 连同连接一起丢掉。
            http.Response.Headers.Connection = "close";
            return QianResults.Error(new QianError(PlatformErrors.PayloadTooLarge));
        }
        if (!await http.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(http).ConfigureAwait(false)) return QianResults.Error(new QianError(PlatformErrors.InputInvalid));
        return null;
    }

    // 无请求体的写接口（如吊销会话）：只要求防伪令牌，不要求 JSON 请求体。
    internal static async Task<IResult?> GuardAction(HttpContext http)
    {
        if (!await http.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(http).ConfigureAwait(false)) return QianResults.Error(new QianError(PlatformErrors.InputInvalid));
        return null;
    }

    internal static async Task<T?> Read<T>(HttpContext http, JsonSerializerOptions? json = null) where T : class
    {
        try { return await JsonSerializer.DeserializeAsync<T>(http.Request.Body, json ?? JsonOptions, http.RequestAborted).ConfigureAwait(false); }
        catch (JsonException) { return null; }
        catch (BadHttpRequestException) { return null; }
    }

    internal static QianErrorResult BadInput() => QianResults.Error(new QianError(PlatformErrors.BadRequest));

    internal static LoginContext ContextOf(HttpContext http) => new(http.Connection.RemoteIpAddress?.ToString(), http.Request.Headers.UserAgent.ToString());

    private static async Task<IResult> Complete(HttpContext http, SignInResult result)
    {
        switch (result.Status)
        {
            case SignInStatus.Authenticated:
                var session = result.Session!;
                var options = http.RequestServices.GetRequiredService<IdentityServerOptions>();
                await http.SignInAsync(options.SignInScheme, CookiePrincipal(session), new AuthenticationProperties { IsPersistent = false, ExpiresUtc = session.ExpiresAt }).ConfigureAwait(false);
                return Results.Json(new SignInView("authenticated", TenantId: session.TenantId, MembershipId: session.MembershipId, Platform: session.TenantId is null), JsonOptions);
            case SignInStatus.SelectionRequired:
                return Results.Json(new SignInView("selection_required", result.Ticket!.Token, result.Ticket.ExpiresAt,
                    [.. result.Choices.Select(c => new ChoiceView(c.MembershipId, c.TenantId, c.TenantCode, c.TenantName, c.DisplayName, c.IsPlatform, c.IsTenantAdministrator))]), JsonOptions);
            case SignInStatus.PasswordChangeRequired:
                return Results.Json(new SignInView("password_change_required", result.Ticket!.Token, result.Ticket.ExpiresAt), JsonOptions);
            default:
                if (result.RetryAfter is { } wait) http.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                return QianResults.Error(result.Error!);
        }
    }

    // ---- 账号 Cookie ----

    internal static async Task<AccountSession?> CurrentSession(HttpContext http)
    {
        var options = http.RequestServices.GetRequiredService<IdentityServerOptions>();
        var authentication = await http.AuthenticateAsync(options.SignInScheme).ConfigureAwait(false);
        return authentication.Succeeded ? SessionOf(authentication.Principal!) : null;
    }

    /// <summary>Cookie 里放的是流水线刚建立的会话事实；sid 与修订由服务端写入，之后每次授权前仍会对照数据库重新校验。</summary>
    internal static ClaimsPrincipal CookiePrincipal(AccountSession session)
    {
        var claims = new List<Claim>
        {
            new("sub", session.PersonId.ToString("D")), new("sid", session.SessionId.ToString("D")),
            new("identity_revision", session.IdentityRevision.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new("auth_time", session.AuthenticatedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new("session_exp", session.ExpiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new("client_id", session.ClientId), new("device_type", session.DeviceType)
        };
        // 平台身份没有租户与成员：两个 claim 都不写，协议端点据此签发 tenant_id 为 null 的令牌。
        if (session.TenantId is { } tenant) claims.Add(new Claim("tenant_id", tenant.ToString("D")));
        if (session.MembershipId is { } membership) claims.Add(new Claim("membership_id", membership.ToString("D")));
        claims.AddRange(session.Amr.Select(method => new Claim("amr", method)));
        if (session.Acr is not null) claims.Add(new Claim("acr", session.Acr));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Qian.Identity.SignIn"));
    }

    internal static AccountSession? SessionOf(ClaimsPrincipal cookie)
    {
        string? One(string name) => cookie.FindAll(name).Count() == 1 ? cookie.FindFirst(name)!.Value : null;
        if (!Guid.TryParseExact(One("sub"), "D", out var person) || !Guid.TryParseExact(One("sid"), "D", out var sid) ||
            !long.TryParse(One("identity_revision"), NumberStyles.None, CultureInfo.InvariantCulture, out var revision) ||
            !long.TryParse(One("auth_time"), NumberStyles.None, CultureInfo.InvariantCulture, out var authenticatedAt) ||
            !long.TryParse(One("session_exp"), NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ||
            One("client_id") is not { } client || One("device_type") is not { } device) return null;
        Guid? tenant = null, membership = null;
        if (cookie.FindAll("tenant_id").Any() != cookie.FindAll("membership_id").Any()) return null;
        if (cookie.FindAll("tenant_id").Any())
        {
            if (!Guid.TryParseExact(One("tenant_id"), "D", out var t) || !Guid.TryParseExact(One("membership_id"), "D", out var m)) return null;
            tenant = t; membership = m;
        }
        return new AccountSession(person, tenant, membership, sid, revision, [.. cookie.FindAll("amr").Select(c => c.Value)], One("acr"),
            DateTimeOffset.FromUnixTimeSeconds(authenticatedAt), DateTimeOffset.FromUnixTimeSeconds(expires), client, device);
    }
}
