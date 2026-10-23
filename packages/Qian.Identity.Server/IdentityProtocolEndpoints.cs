using System.Collections.Immutable;
using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Identity.Server;

/// <summary>由 OpenIddict 先完成协议校验后才到达的应用端点。</summary>
public static class IdentityProtocolEndpoints
{
    private const string Server = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;
    private static readonly string[] UserClaims = ["sub", "tenant_id", "membership_id", "identity_revision", "sid", "amr", "acr", "auth_time"];

    /// <summary>映射授权、换签、用户投影和登出；其余协议端点由 OpenIddict 处理。</summary>
    public static IEndpointRouteBuilder MapQianIdentityServer(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods("/connect/authorize", ["GET", "POST"], (Delegate)Authorize);
        endpoints.MapPost("/connect/token", (Delegate)Token);
        endpoints.MapMethods("/connect/userinfo", ["GET", "POST"], (Delegate)UserInfo);
        endpoints.MapMethods("/connect/logout", ["GET", "POST"], (Delegate)Logout);
        return endpoints;
    }

    private static async Task<IResult> Authorize(HttpContext http)
    {
        var request = http.GetOpenIddictServerRequest()!;
        var options = http.RequestServices.GetRequiredService<IdentityServerOptions>();
        var client = Client(options, request.ClientId);
        if (client is null) return Reject(Errors.InvalidClient);
        if (request.HasScope(Scopes.OpenId) && string.IsNullOrWhiteSpace(request.Nonce)) return Reject(Errors.InvalidRequest);
        // 交互式重新认证与同意必须由后续账号流水线落实，不能静默忽略调用方的安全要求；账号选择（select_account）由同源选择页落实（S1-YUE-002）。
        var prompts = request.GetPromptValues();
        if (prompts.Any(prompt => prompt is not (PromptValues.None or PromptValues.SelectAccount))) return Reject(Errors.InteractionRequired);
        var selectAccount = prompts.Contains(PromptValues.SelectAccount, StringComparer.Ordinal);
        // OIDC Core 3.1.2.1：none 不得与其他值并用。
        if (selectAccount && request.HasPromptValue(PromptValues.None)) return Reject(Errors.InvalidRequest);
        // 去掉 prompt 的授权请求原地址：登录与选择页完成后回到这里签发，不再重复要求选择。
        var resume = selectAccount ? WithoutPrompt(http, request) : http.Request.GetEncodedPathAndQuery();
        var authentication = await http.AuthenticateAsync(options.SignInScheme).ConfigureAwait(false);
        if (!authentication.Succeeded || authentication.Principal is not { } cookie)
            return request.HasPromptValue(PromptValues.None) ? Reject(Errors.LoginRequired) :
                Results.Challenge(new AuthenticationProperties { RedirectUri = resume }, [options.SignInScheme]);
        if (request.MaxAge is { } age && (!long.TryParse(cookie.GetClaim("auth_time"), NumberStyles.None, CultureInfo.InvariantCulture, out var authenticatedAt) ||
            authenticatedAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() || DateTimeOffset.UtcNow.ToUnixTimeSeconds() - authenticatedAt > age)) return Reject(Errors.LoginRequired);
        AccountValidation? validation = null;
        if (http.RequestServices.GetRequiredService<IdentityAccountGate>().Service is { } accounts)
        {
            // 内置账号模式：账号 Cookie 只是会话的指针，授权前必须对照数据库——会话被吊销、账号停用、成员或租户失效，都不再签发。
            var session = IdentityAccountEndpoints.SessionOf(cookie);
            validation = session is null ? null : await accounts.ValidateAsync(session.SessionId, session.PersonId, session.MembershipId, http.RequestAborted).ConfigureAwait(false);
            if (validation is null)
            {
                await http.SignOutAsync(options.SignInScheme).ConfigureAwait(false);
                return request.HasPromptValue(PromptValues.None) ? Reject(Errors.LoginRequired) :
                    Results.Challenge(new AuthenticationProperties { RedirectUri = resume }, [options.SignInScheme]);
            }
        }
        // 账号 Cookie 仍有效且调用方要求选择账号：302 到同源选择页，页面经 POST /api/identity/session/switch 与 /login/select 换会话后回到 return_url。
        // return_url 固定为本服务的同源授权地址，不是开放重定向；没有内置账号模型的宿主没有选择页，按 interaction_required 拒绝。
        if (selectAccount)
            return validation is null ? Reject(Errors.InteractionRequired)
                : Results.Redirect(options.Accounts.SelectionPath + "?return_url=" + Uri.EscapeDataString(resume));
        var principal = UserPrincipal(cookie, validation);
        if (principal is null) return Reject(Errors.LoginRequired);
        var resources = ResourcesFor(client, request.GetResources());
        if (resources is null) return Reject(Errors.InvalidTarget);
        principal.SetScopes(request.GetScopes()); principal.SetResources(resources);
        var applications = http.RequestServices.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applications.FindByClientIdAsync(request.ClientId!, http.RequestAborted).ConfigureAwait(false);
        if (application is null) return Reject(Errors.InvalidClient);
        var authorizations = http.RequestServices.GetRequiredService<IOpenIddictAuthorizationManager>();
        // 每次授权建立独立族；复用已消费 refresh 时只吊销同族，不能借共享的永久授权误杀别的登录。
        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            Principal = principal, Subject = principal.GetClaim(Claims.Subject), Status = Statuses.Valid, Type = AuthorizationTypes.AdHoc,
            ApplicationId = await applications.GetIdAsync(application, http.RequestAborted).ConfigureAwait(false), CreationDate = DateTimeOffset.UtcNow
        };
        descriptor.Scopes.UnionWith(principal.GetScopes());
        descriptor.Properties["sid"] = JsonSerializer.SerializeToElement(principal.GetClaim("sid"));
        var authorization = await authorizations.CreateAsync(descriptor, http.RequestAborted).ConfigureAwait(false);
        principal.SetAuthorizationId(await authorizations.GetIdAsync(authorization, http.RequestAborted).ConfigureAwait(false));
        return Results.SignIn(principal, authenticationScheme: Server);
    }

    private static async Task<IResult> Token(HttpContext http)
    {
        var request = http.GetOpenIddictServerRequest()!;
        var options = http.RequestServices.GetRequiredService<IdentityServerOptions>();
        var client = Client(options, request.ClientId);
        if (client is null) return Reject(Errors.InvalidClient);
        ClaimsPrincipal principal;
        if (request.IsClientCredentialsGrantType())
        {
            var resources = ResourcesFor(client, request.GetResources());
            if (resources is null) return Reject(Errors.InvalidTarget);
            var identity = new ClaimsIdentity("Qian.Identity.Server");
            identity.AddClaim(new Claim(Claims.Subject, request.ClientId!));
            identity.AddClaim(new Claim("principal_kind", "service"));
            identity.AddClaim(client.TenantId is { } tenant ? new Claim("tenant_id", tenant.ToString()) : new Claim("tenant_id", "", JsonClaimValueTypes.JsonNull));
            if (client.PluginId is { } plugin) identity.AddClaim(new Claim("plugin_id", plugin));
            principal = new ClaimsPrincipal(identity); principal.SetScopes(request.GetScopes()); principal.SetResources(resources);
            principal.SetDestinations(_ => [Destinations.AccessToken]);
        }
        else if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            // OpenIddict 校验密文、PKCE、client、状态、到期和整族复用；不自行解析未验证令牌。
            var ticket = await http.AuthenticateAsync(Server).ConfigureAwait(false);
            if (!ticket.Succeeded || ticket.Principal is not { } authenticated) return Reject(Errors.InvalidGrant);
            principal = authenticated;
            if (http.RequestServices.GetRequiredService<IdentityAccountGate>().Service is { } accounts && principal.GetClaim("principal_kind") == "user")
            {
                // 换码与 refresh 同样要对照账号会话：改密、停用、登出之后，旧授权码和 refresh token 都不能再换新令牌。
                if (!Guid.TryParseExact(principal.GetClaim("sub"), "D", out var person) || !Guid.TryParseExact(principal.GetClaim("sid"), "D", out var sid)) return Reject(Errors.InvalidGrant);
                Guid? membership = Guid.TryParseExact(principal.GetClaim("membership_id"), "D", out var member) ? member : null;
                var validation = await accounts.ValidateAsync(sid, person, membership, http.RequestAborted).ConfigureAwait(false);
                if (validation is null) return Reject(Errors.InvalidGrant);
                ReplaceRevision(principal, validation.IdentityRevision);
                ReplaceName(principal, validation.DisplayName);
                RestorePlatformTenant(principal);
            }
            // 换码与 refresh 只能在原授权的受众内收窄（RFC 8707），不带 resource 时沿用原授权；收窄后的集合同时是新 refresh token 的上限。
            var granted = principal.GetResources();
            var requested = request.GetResources();
            var resources = requested.IsEmpty ? granted.ToArray() : requested.Distinct(StringComparer.Ordinal).ToArray();
            if (resources.Length == 0 || ResourcesFor(client, resources) is null || resources.Except(granted, StringComparer.Ordinal).Any())
                return Reject(Errors.InvalidTarget);
            principal.SetResources(resources);
        }
        else return Reject(Errors.UnsupportedGrantType);
        return Results.SignIn(principal, authenticationScheme: Server);
    }

    private static async Task<IResult> UserInfo(HttpContext http)
    {
        var ticket = await http.AuthenticateAsync(Server).ConfigureAwait(false);
        if (!ticket.Succeeded || ticket.Principal is not { } principal || principal.GetClaim("principal_kind") != "user" || !principal.HasScope(Scopes.OpenId)) return Reject(Errors.InvalidToken);
        if (http.RequestServices.GetRequiredService<IdentityAccountGate>().Service is { } accounts)
        {
            // 访问令牌是自包含的 JWT，到期前签名一直有效；内置账号模式下 userinfo 额外对照数据库：会话被吊销、账号 / 成员 / 租户失效，
            // 或令牌里的 identity_revision 已落后于当前修订（授权变更、改密、停用），都按失效令牌拒绝，不再给出身份投影。
            if (!Guid.TryParseExact(principal.GetClaim("sub"), "D", out var person) || !Guid.TryParseExact(principal.GetClaim("sid"), "D", out var sid)) return Reject(Errors.InvalidToken);
            Guid? membership = Guid.TryParseExact(principal.GetClaim("membership_id"), "D", out var member) ? member : null;
            var validation = await accounts.ValidateAsync(sid, person, membership, http.RequestAborted).ConfigureAwait(false);
            if (validation is null || !long.TryParse(principal.GetClaim("identity_revision"), NumberStyles.None, CultureInfo.InvariantCulture, out var revision) || revision < validation.IdentityRevision)
                return Reject(Errors.InvalidToken);
        }
        // 只发布身份投影，永不把其他 Cookie、角色或权限声明带入协议响应。
        var claims = UserClaims.Where(name => name is not "sid" && principal.HasClaim(claim => claim.Type == name))
            .ToDictionary(name => name, name => name == "amr" ? (object?)principal.GetClaims(name).ToArray() :
                principal.HasClaim(claim => claim.Type == name && claim.ValueType == JsonClaimValueTypes.JsonNull) ? null : principal.GetClaim(name), StringComparer.Ordinal);
        return Results.Json(claims);
    }

    private static async Task<IResult> Logout(HttpContext http)
    {
        var options = http.RequestServices.GetRequiredService<IdentityServerOptions>();
        var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
        if (HttpMethods.IsGet(http.Request.Method))
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            // 动作 URL 保留已经过 OpenIddict 校验的 id_token_hint / redirect / state；所有值经 HTML 编码。
            var action = HtmlEncoder.Default.Encode(http.Request.PathBase + http.Request.Path);
            var token = HtmlEncoder.Default.Encode(tokens.RequestToken!); var name = HtmlEncoder.Default.Encode(tokens.FormFieldName);
            var fields = string.Concat(http.GetOpenIddictServerRequest()!.GetParameters().Select(parameter =>
                $"<input type=\"hidden\" name=\"{HtmlEncoder.Default.Encode(parameter.Key)}\" value=\"{HtmlEncoder.Default.Encode(parameter.Value.ToString() ?? "")}\">"));
            return Results.Content($"<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>退出登录</title><form method=\"post\" action=\"{action}\">{fields}<input type=\"hidden\" name=\"{name}\" value=\"{token}\"><button type=\"submit\">确认退出登录</button></form></html>", "text/html; charset=utf-8");
        }
        if (!await antiforgery.IsRequestValidAsync(http).ConfigureAwait(false)) return QianResults.Error(new QianError(PlatformErrors.InputInvalid));
        var ticket = await http.AuthenticateAsync(Server).ConfigureAwait(false);
        var principal = ticket.Principal ?? (await http.AuthenticateAsync(options.SignInScheme).ConfigureAwait(false)).Principal;
        if (principal?.GetClaim(Claims.Subject) is { } subject && principal.GetClaim("sid") is { } sid)
        {
            if (http.RequestServices.GetRequiredService<IdentityAccountGate>().Service is { } accounts && Guid.TryParseExact(subject, "D", out var person) && Guid.TryParseExact(sid, "D", out var sessionId))
                await accounts.RevokeSessionsAsync(new SessionRevocation(person, sessionId, "logout", "logout:" + sid), http.RequestAborted).ConfigureAwait(false);
            // 协议侧收尾与会话中心的旁路处理共用同一段：授权族、令牌与后通道登出投递，幂等。这里同步做完，登出响应返回时 RP 的 refresh 已经失效。
            await IdentityProtocolSessions.RevokeAsync(http.RequestServices, options, subject, sid, http.RequestAborted).ConfigureAwait(false);
        }
        await http.SignOutAsync(options.SignInScheme).ConfigureAwait(false);
        return Results.SignOut(authenticationSchemes: [Server]);
    }

    private static ClaimsPrincipal? UserPrincipal(ClaimsPrincipal cookie, AccountValidation? validation)
    {
        foreach (var name in new[] { "sub", "identity_revision", "sid" })
            if (cookie.FindAll(name).Count() != 1) return null;
        if (string.IsNullOrWhiteSpace(cookie.GetClaim("sub")) || cookie.FindAll("acr").Count() > 1 || cookie.FindAll("auth_time").Count() > 1) return null;
        // 租户身份同时带 tenant_id 与 membership_id；平台身份两者都没有（令牌里 tenant_id 显式为 null）。只有一个的属于篡改或残缺，拒绝。
        var tenantClaims = cookie.FindAll("tenant_id").Count(); var membershipClaims = cookie.FindAll("membership_id").Count();
        if (tenantClaims != membershipClaims || tenantClaims > 1) return null;
        var platform = tenantClaims == 0;
        if (!platform && (!Guid.TryParseExact(cookie.GetClaim("tenant_id"), "D", out var tenant) || tenant == Guid.Empty ||
            !Guid.TryParseExact(cookie.GetClaim("membership_id"), "D", out var membership) || membership == Guid.Empty)) return null;
        if (!Guid.TryParseExact(cookie.GetClaim("sid"), "D", out var sid) || sid == Guid.Empty ||
            !long.TryParse(cookie.GetClaim("identity_revision"), NumberStyles.None, CultureInfo.InvariantCulture, out var revision)) return null;
        var identity = new ClaimsIdentity("Qian.Identity.Server");
        foreach (var claim in cookie.Claims.Where(claim => UserClaims.Contains(claim.Type, StringComparer.Ordinal)))
            identity.AddClaim(new Claim(claim.Type, claim.Value, claim.Type is "identity_revision" or "auth_time" ? ClaimValueTypes.Integer64 : claim.ValueType));
        if (platform) identity.AddClaim(new Claim("tenant_id", "", JsonClaimValueTypes.JsonNull));
        identity.AddClaim(new Claim("principal_kind", "user"));
        var principal = new ClaimsPrincipal(identity);
        // 账号会话校验给出的最新修订覆盖 Cookie 里的旧值：修订只增不减，签出的令牌总是当下的事实。
        if (validation is not null) ReplaceRevision(principal, validation.IdentityRevision);
        principal.SetDestinations(claim => claim.Type == "tenant_id" && claim.ValueType == JsonClaimValueTypes.JsonNull
            ? new[] { Destinations.AccessToken } : new[] { Destinations.AccessToken, Destinations.IdentityToken });
        // 标准 name claim 只进 id_token（外壳显示用）：访问令牌仍只含身份与会话类 claim，资源服务器拿不到也不该依赖显示名。
        ReplaceName(principal, validation?.DisplayName);
        return principal;
    }

    // 显示名来自账号会话校验（成员显示名；平台身份取自然人显示名），换签与刷新时同样以当下的事实覆盖。
    private static void ReplaceName(ClaimsPrincipal principal, string? name)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var old in identity.FindAll(Claims.Name).ToArray()) identity.RemoveClaim(old);
        if (string.IsNullOrWhiteSpace(name)) return;
        var claim = new Claim(Claims.Name, name);
        claim.SetDestinations(Destinations.IdentityToken);
        identity.AddClaim(claim);
    }

    // 授权请求去掉 prompt 后的同源地址（路径 + 查询串）；POST 提交的参数同样改写成 GET 查询串，OpenIddict 两种方法都接受。
    private static string WithoutPrompt(HttpContext http, OpenIddictRequest request)
    {
        var pairs = new List<KeyValuePair<string, string?>>();
        foreach (var parameter in request.GetParameters())
        {
            if (parameter.Key == Parameters.Prompt) continue;
            foreach (var value in (StringValues)parameter.Value) if (value is not null) pairs.Add(new(parameter.Key, value));
        }
        return QueryHelpers.AddQueryString((http.Request.PathBase + http.Request.Path).ToString(), pairs);
    }

    // 授权码与 refresh token 是序列化的 JWT：平台身份的 JSON null tenant_id 经 IdentityModel 会退化成空字符串。
    // 兑换时把它恢复成带 JSON_NULL 类型的 claim，访问令牌里才是真正的 null 而不是空串（空串不是任何租户，也绝不能被当成租户 ID 使用）。
    private static void RestorePlatformTenant(ClaimsPrincipal principal)
    {
        foreach (var identity in principal.Identities)
            foreach (var empty in identity.FindAll("tenant_id").Where(claim => claim.Value.Length == 0 && claim.ValueType != JsonClaimValueTypes.JsonNull).ToArray())
            {
                identity.RemoveClaim(empty);
                var restored = new Claim("tenant_id", "", JsonClaimValueTypes.JsonNull);
                restored.SetDestinations(Destinations.AccessToken);
                identity.AddClaim(restored);
            }
    }

    private static void ReplaceRevision(ClaimsPrincipal principal, long revision)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var old in identity.FindAll("identity_revision").ToArray()) identity.RemoveClaim(old);
        var claim = new Claim("identity_revision", revision.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64);
        claim.SetDestinations(Destinations.AccessToken, Destinations.IdentityToken);
        identity.AddClaim(claim);
    }
    private static IdentityClientRegistration? Client(IdentityServerOptions options, string? id) => options.Clients.SingleOrDefault(client => client.Application.ClientId == id);
    // 协议管道已按白名单逐字比较、查过客户端的 rsrc: 权限；这里再按白名单兜底一次，并决定不带 resource 时的受众：
    // 只有单资源客户端按登记缺省；多资源客户端必须指定，否则一张令牌带上全部受众，可在资源服务器之间重放。
    private static string[]? ResourcesFor(IdentityClientRegistration client, IEnumerable<string> requested)
    {
        var values = requested.Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length == 0) return client.Resources.Count == 1 ? [client.Resources[0]] : null;
        return values.Except(client.Resources, StringComparer.Ordinal).Any() ? null : values;
    }
    private static IResult Reject(string error) => Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?>
        { [OpenIddictServerAspNetCoreConstants.Properties.Error] = error }), [Server]);
}
