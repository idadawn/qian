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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
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
        // 交互式重新认证、同意和账号选择必须由后续账号流水线落实，不能静默忽略调用方的安全要求。
        if (request.GetPromptValues().Any(prompt => prompt != PromptValues.None)) return Reject(Errors.InteractionRequired);
        var authentication = await http.AuthenticateAsync(options.SignInScheme).ConfigureAwait(false);
        if (!authentication.Succeeded || authentication.Principal is not { } cookie)
            return request.HasPromptValue(PromptValues.None) ? Reject(Errors.LoginRequired) :
                Results.Challenge(new AuthenticationProperties { RedirectUri = http.Request.GetEncodedPathAndQuery() }, [options.SignInScheme]);
        if (request.MaxAge is { } age && (!long.TryParse(cookie.GetClaim("auth_time"), NumberStyles.None, CultureInfo.InvariantCulture, out var authenticatedAt) ||
            authenticatedAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() || DateTimeOffset.UtcNow.ToUnixTimeSeconds() - authenticatedAt > age)) return Reject(Errors.LoginRequired);
        var principal = UserPrincipal(cookie);
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
            var requested = request.GetResources();
            var resources = requested.IsEmpty ? principal.GetResources().ToArray() : requested.ToArray();
            if (ResourcesFor(client, resources) is null || resources.Except(principal.GetResources(), StringComparer.Ordinal).Any())
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
        // 只发布身份投影，永不把其他 Cookie、角色或权限声明带入协议响应。
        var claims = UserClaims.Where(name => name is not "sid" && principal.HasClaim(claim => claim.Type == name))
            .ToDictionary(name => name, name => name == "amr" ? (object?)principal.GetClaims(name).ToArray() : principal.GetClaim(name), StringComparer.Ordinal);
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
            var authorizations = http.RequestServices.GetRequiredService<IOpenIddictAuthorizationManager>();
            var applications = http.RequestServices.GetRequiredService<IOpenIddictApplicationManager>();
            var tokens = http.RequestServices.GetRequiredService<IOpenIddictTokenManager>();
            var db = http.RequestServices.GetRequiredService<IdentityDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted).ConfigureAwait(false);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"qian.identity:logout:" + subject + ":" + sid}, 0))", http.RequestAborted).ConfigureAwait(false);
            foreach (var client in options.Clients)
            {
                var application = await applications.FindByClientIdAsync(client.Application.ClientId!, http.RequestAborted).ConfigureAwait(false);
                if (application is null) continue;
                var applicationId = await applications.GetIdAsync(application, http.RequestAborted).ConfigureAwait(false);
                var revoked = false;
                var matching = new List<object>();
                await foreach (var authorization in authorizations.FindAsync(subject, applicationId, Statuses.Valid, AuthorizationTypes.AdHoc, null, http.RequestAborted).ConfigureAwait(false)) matching.Add(authorization);
                foreach (var authorization in matching)
                {
                    var properties = await authorizations.GetPropertiesAsync(authorization, http.RequestAborted).ConfigureAwait(false);
                    if (!properties.TryGetValue("sid", out var value) || value.GetString() != sid) continue;
                    var id = await authorizations.GetIdAsync(authorization, http.RequestAborted).ConfigureAwait(false);
                    await authorizations.TryRevokeAsync(authorization, http.RequestAborted).ConfigureAwait(false);
                    await tokens.RevokeByAuthorizationIdAsync(id!, http.RequestAborted).ConfigureAwait(false);
                    revoked = true;
                }
                if (revoked && client.BackchannelLogoutUri is not null &&
                    !await db.LogoutDeliveries.AnyAsync(row => row.ClientId == client.Application.ClientId && row.SessionId == sid, http.RequestAborted).ConfigureAwait(false))
                    db.LogoutDeliveries.Add(new LogoutDelivery { ClientId = client.Application.ClientId!, Subject = subject, SessionId = sid });
            }
            await db.SaveChangesAsync(http.RequestAborted).ConfigureAwait(false);
            await transaction.CommitAsync(http.RequestAborted).ConfigureAwait(false);
        }
        await http.SignOutAsync(options.SignInScheme).ConfigureAwait(false);
        return Results.SignOut(authenticationSchemes: [Server]);
    }

    private static ClaimsPrincipal? UserPrincipal(ClaimsPrincipal cookie)
    {
        foreach (var name in new[] { "sub", "tenant_id", "membership_id", "identity_revision", "sid" })
            if (cookie.FindAll(name).Count() != 1) return null;
        if (string.IsNullOrWhiteSpace(cookie.GetClaim("sub")) || cookie.FindAll("acr").Count() > 1 || cookie.FindAll("auth_time").Count() > 1) return null;
        if (!Guid.TryParseExact(cookie.GetClaim("tenant_id"), "D", out var tenant) || tenant == Guid.Empty ||
            !Guid.TryParseExact(cookie.GetClaim("membership_id"), "D", out var membership) || membership == Guid.Empty ||
            !Guid.TryParseExact(cookie.GetClaim("sid"), "D", out var sid) || sid == Guid.Empty ||
            !long.TryParse(cookie.GetClaim("identity_revision"), NumberStyles.None, CultureInfo.InvariantCulture, out _)) return null;
        var identity = new ClaimsIdentity("Qian.Identity.Server");
        foreach (var claim in cookie.Claims.Where(claim => UserClaims.Contains(claim.Type, StringComparer.Ordinal)))
            identity.AddClaim(new Claim(claim.Type, claim.Value, claim.Type is "identity_revision" or "auth_time" ? ClaimValueTypes.Integer64 : claim.ValueType));
        identity.AddClaim(new Claim("principal_kind", "user"));
        var principal = new ClaimsPrincipal(identity);
        principal.SetDestinations(_ => [Destinations.AccessToken, Destinations.IdentityToken]);
        return principal;
    }
    private static IdentityClientRegistration? Client(IdentityServerOptions options, string? id) => options.Clients.SingleOrDefault(client => client.Application.ClientId == id);
    private static string[]? ResourcesFor(IdentityClientRegistration client, IEnumerable<string> requested)
    {
        var values = requested.ToArray(); if (values.Length == 0) values = client.Resources.ToArray();
        return values.Length == 0 || values.Except(client.Resources, StringComparer.Ordinal).Any() ? null : values;
    }
    private static IResult Reject(string error) => Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?>
        { [OpenIddictServerAspNetCoreConstants.Properties.Error] = error }), [Server]);
}
