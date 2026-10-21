using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Identity.Server;

/// <summary>
/// 会话中心的管理面：某人的会话列表、吊销单个会话、吊销某人的全部会话、在线用户。调用者由账号 Cookie 里的会话确定，且每次都对照数据库校验。
/// 资格按身份分三种：本人可以看和吊销自己的会话；平台身份（平台资格 + 平台会话）可以管任何人；租户管理员只能管本租户内的会话。
/// 无权限时单个会话按「不存在」处理（不泄露会话是否存在），列表类按 403。这是在授权引擎的权限点（qian.identity:session:*）接入 PEP 之前的内置判定。
/// 写接口同样要求防伪令牌。会话标识 sid 只出现在这些响应里，不进日志（日志只记哈希）。
/// </summary>
internal static class IdentitySessionEndpoints
{
    private const int MaxPage = 200;

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(IdentityAccountEndpoints.Prefix + "/sessions", (Delegate)List);
        endpoints.MapPost(IdentityAccountEndpoints.Prefix + "/sessions/{sid:guid}:revoke", (Delegate)Revoke);
        endpoints.MapPost(IdentityAccountEndpoints.Prefix + "/persons/{id:guid}:revoke-all", (Delegate)RevokeAll);
        endpoints.MapGet(IdentityAccountEndpoints.Prefix + "/online-users", (Delegate)Online);
    }

    private sealed record SessionBody(
        [property: JsonPropertyName("id")] Guid Id, [property: JsonPropertyName("person_id")] Guid PersonId,
        [property: JsonPropertyName("membership_id")] Guid? MembershipId, [property: JsonPropertyName("tenant_id")] Guid? TenantId,
        [property: JsonPropertyName("client_id")] string ClientId, [property: JsonPropertyName("device_type")] string DeviceType,
        [property: JsonPropertyName("remote_address")] string? RemoteAddress, [property: JsonPropertyName("user_agent")] string? UserAgent,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt, [property: JsonPropertyName("last_seen_at")] DateTimeOffset LastSeenAt,
        [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt, [property: JsonPropertyName("current")] bool Current);

    private sealed record OnlineBody(
        [property: JsonPropertyName("person_id")] Guid PersonId, [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("tenant_id")] Guid? TenantId, [property: JsonPropertyName("sessions")] int Sessions,
        [property: JsonPropertyName("device_types")] IReadOnlyList<string> DeviceTypes, [property: JsonPropertyName("last_seen_at")] DateTimeOffset LastSeenAt);

    private static IdentityAccountService Service(HttpContext http) => IdentityAccountEndpoints.Service(http);

    private static async Task<IdentityAccountService.SessionCaller?> Caller(HttpContext http)
    {
        if (await IdentityAccountEndpoints.CurrentSession(http).ConfigureAwait(false) is not { } current) return null;
        return await Service(http).ResolveCallerAsync(current, http.RequestAborted).ConfigureAwait(false);
    }

    private static QianErrorResult Unauthenticated() => QianResults.Error(new QianError(PlatformErrors.Unauthenticated));

    private static QianErrorResult Denied() => QianResults.Error(new QianError(PlatformErrors.AuthzDenied));

    private static SessionBody Body(IdentityAccountService.SessionView view, Guid currentSession) =>
        new(view.Id, view.PersonId, view.MembershipId, view.TenantId, view.ClientId, view.DeviceType, view.RemoteAddress, view.UserAgent,
            view.CreatedAt, view.LastSeenAt, view.ExpiresAt, view.Id == currentSession);

    // GET /sessions?person=&tenant_id=：缺省 person 是本人。平台可看任何人（可按租户过滤），租户管理员只看本租户内的会话。
    private static async Task<IResult> List(HttpContext http)
    {
        if (await Caller(http).ConfigureAwait(false) is not { } caller) return Unauthenticated();
        Guid person = caller.PersonId;
        if (http.Request.Query.TryGetValue("person", out var raw) && !Guid.TryParseExact(raw.ToString(), "D", out person)) return QianResults.Error(new QianError(PlatformErrors.BadRequest));
        if (!Scope(http, caller, person, out var tenant, out var denied)) return denied!;
        var views = await Service(http).ListViewsAsync(person, tenant, http.RequestAborted).ConfigureAwait(false);
        return Results.Json(new { sessions = views.Select(view => Body(view, caller.SessionId)) }, IdentityAccountEndpoints.JsonOptions);
    }

    // POST /sessions/{sid}:revoke。
    private static async Task<IResult> Revoke(HttpContext http, Guid sid)
    {
        if (await IdentityAccountEndpoints.GuardAction(http).ConfigureAwait(false) is { } rejected) return rejected;
        if (await Caller(http).ConfigureAwait(false) is not { } caller) return Unauthenticated();
        var service = Service(http);
        var view = await service.FindViewAsync(sid, http.RequestAborted).ConfigureAwait(false);
        // 看不见的会话和不存在的会话同样回 404，不让调用者探测别人的会话。
        if (view is null || !(view.PersonId == caller.PersonId || caller.Platform || caller.TenantAdministrator && view.TenantId is { } tenant && tenant == caller.TenantId))
            return QianResults.Error(new QianError(PlatformErrors.NotFound));
        var revoked = await service.RevokeForAsync(caller.PersonId, view.PersonId, view.Id, null, view.PersonId == caller.PersonId ? "user_revoke" : "admin_revoke", http.RequestAborted).ConfigureAwait(false);
        if (view.Id == caller.SessionId) await SignOut(http).ConfigureAwait(false);
        return Results.Json(new { revoked }, IdentityAccountEndpoints.JsonOptions);
    }

    // POST /persons/{id}:revoke-all：本人吊销自己的全部会话（含当前）；平台吊销某人的全部；租户管理员只吊销该人在本租户内的会话。
    private static async Task<IResult> RevokeAll(HttpContext http, Guid id)
    {
        if (await IdentityAccountEndpoints.GuardAction(http).ConfigureAwait(false) is { } rejected) return rejected;
        if (await Caller(http).ConfigureAwait(false) is not { } caller) return Unauthenticated();
        if (!Scope(http, caller, id, out var tenant, out var denied)) return denied!;
        var self = id == caller.PersonId;
        var revoked = await Service(http).RevokeForAsync(caller.PersonId, id, null, tenant, self ? "user_revoke_all" : "admin_revoke_all", http.RequestAborted).ConfigureAwait(false);
        if (self && tenant is null) await SignOut(http).ConfigureAwait(false);
        return Results.Json(new { revoked }, IdentityAccountEndpoints.JsonOptions);
    }

    // GET /online-users?tenant_id=&limit=&offset=：平台视角看全部（可按租户过滤，含平台身份会话），租户管理员只看本租户。
    private static async Task<IResult> Online(HttpContext http)
    {
        if (await Caller(http).ConfigureAwait(false) is not { } caller) return Unauthenticated();
        Guid? tenant;
        if (caller.Platform)
        {
            tenant = null;
            if (http.Request.Query.TryGetValue("tenant_id", out var raw))
            {
                if (!Guid.TryParseExact(raw.ToString(), "D", out var parsed)) return QianResults.Error(new QianError(PlatformErrors.BadRequest));
                tenant = parsed;
            }
        }
        else if (caller.TenantAdministrator) tenant = caller.TenantId;
        else return Denied();
        if (!Page(http, "limit", 50, 1, MaxPage, out var limit) || !Page(http, "offset", 0, 0, int.MaxValue, out var offset)) return QianResults.Error(new QianError(PlatformErrors.BadRequest));
        var users = await Service(http).OnlineUsersAsync(tenant, limit, offset, http.RequestAborted).ConfigureAwait(false);
        return Results.Json(new { users = users.Select(user => new OnlineBody(user.PersonId, user.DisplayName, user.TenantId, user.Sessions, user.DeviceTypes, user.LastSeenAt)) }, IdentityAccountEndpoints.JsonOptions);
    }

    // 谁能管谁：本人（全部会话）；平台身份（任何人，可按 tenant_id 缩小）；租户管理员（只限本租户内的会话）。
    private static bool Scope(HttpContext http, IdentityAccountService.SessionCaller caller, Guid person, out Guid? tenant, out IResult? denied)
    {
        tenant = null; denied = null;
        if (person == caller.PersonId) return true;
        if (caller.Platform)
        {
            if (!http.Request.Query.TryGetValue("tenant_id", out var raw)) return true;
            if (Guid.TryParseExact(raw.ToString(), "D", out var parsed)) { tenant = parsed; return true; }
            denied = QianResults.Error(new QianError(PlatformErrors.BadRequest));
            return false;
        }
        if (caller.TenantAdministrator && caller.TenantId is { } own) { tenant = own; return true; }
        denied = Denied();
        return false;
    }

    private static bool Page(HttpContext http, string name, int fallback, int min, int max, out int value)
    {
        value = fallback;
        return !http.Request.Query.TryGetValue(name, out var raw) || int.TryParse(raw.ToString(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
    }

    private static Task SignOut(HttpContext http) =>
        Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(http, http.RequestServices.GetRequiredService<IdentityServerOptions>().SignInScheme);
}
