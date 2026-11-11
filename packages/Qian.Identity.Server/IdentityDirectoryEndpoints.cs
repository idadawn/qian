using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Identity.Server;

// 目录 API 属于 Identity Server。读者必须持本租户有效成员会话；写入与档案读取要求租户管理员。
// 平台会话没有租户 Membership，因此不合成成员、不接受 query/header/body 提供的租户来旁路。
internal static class IdentityDirectoryEndpoints
{
    internal const string Prefix = "/api/directory";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Prefix + "/org-types", (Delegate)((HttpContext http) => Run(http, false, async (directory, ct) => new { items = await directory.ListOrganizationTypesAsync(ct).ConfigureAwait(false) })));
        endpoints.MapPut(Prefix + "/org-types", (Delegate)((HttpContext http) => Write<OrganizationType>(http, async (directory, request, ct) => await directory.SetOrganizationTypeAsync(request, ct).ConfigureAwait(false))));
        endpoints.MapDelete(Prefix + "/org-types/{code}", (Delegate)((HttpContext http, string code) => Delete(http, (directory, ct) => directory.DeleteOrganizationTypeAsync(code, ct).AsTask())));
        endpoints.MapGet(Prefix + "/orgs/tree", (Delegate)((HttpContext http) => Run(http, false, async (directory, ct) => new { items = await directory.GetOrganizationTreeAsync(ct).ConfigureAwait(false) })));
        endpoints.MapPost(Prefix + "/orgs", (Delegate)((HttpContext http) => Write<OrganizationInput>(http, async (directory, request, ct) => await directory.CreateOrganizationAsync(request, ct).ConfigureAwait(false))));
        endpoints.MapPut(Prefix + "/orgs/{id:guid}", (Delegate)((HttpContext http, Guid id) => Write<OrganizationInput>(http, async (directory, request, ct) => await directory.UpdateOrganizationAsync(id, request, ct).ConfigureAwait(false))));
        endpoints.MapDelete(Prefix + "/orgs/{id:guid}", (Delegate)((HttpContext http, Guid id) => Delete(http, (directory, ct) => directory.DeleteOrganizationAsync(id, ct).AsTask())));
        endpoints.MapGet(Prefix + "/positions", (Delegate)((HttpContext http) => Run(http, false, async (directory, ct) => new { items = await directory.ListPositionsAsync(ct).ConfigureAwait(false) })));
        endpoints.MapPost(Prefix + "/positions", (Delegate)((HttpContext http) => Write<PositionInput>(http, async (directory, request, ct) => await directory.CreatePositionAsync(request, ct).ConfigureAwait(false))));
        endpoints.MapPut(Prefix + "/positions/{id:guid}", (Delegate)((HttpContext http, Guid id) => Write<PositionInput>(http, async (directory, request, ct) => await directory.UpdatePositionAsync(id, request, ct).ConfigureAwait(false))));
        endpoints.MapDelete(Prefix + "/positions/{id:guid}", (Delegate)((HttpContext http, Guid id) => Delete(http, (directory, ct) => directory.DeletePositionAsync(id, ct).AsTask())));
        endpoints.MapGet(Prefix + "/groups", (Delegate)((HttpContext http) => Run(http, false, async (directory, ct) => new { items = await directory.ListGroupsAsync(ct).ConfigureAwait(false) })));
        endpoints.MapPost(Prefix + "/groups", (Delegate)((HttpContext http) => Write<GroupBody>(http, async (directory, request, ct) => await directory.CreateGroupAsync(request.Name, ct).ConfigureAwait(false))));
        endpoints.MapDelete(Prefix + "/groups/{id:guid}", (Delegate)((HttpContext http, Guid id) => Delete(http, (directory, ct) => directory.DeleteGroupAsync(id, ct).AsTask())));
        // 成员检索（S1-IDP-010）：q 为关键字，ids / person_ids 为按 id 批量；三者至多给一个，规则见 DirectoryMemberQuery。
        endpoints.MapGet(Prefix + "/members", (Delegate)((HttpContext http) => Run(http, false, async (directory, ct) => new
        {
            items = await directory.QueryMembersAsync(new DirectoryMemberQuery
            { Keyword = One(http, "q"), MembershipIds = Ids(http, "ids"), PersonIds = Ids(http, "person_ids"), Offset = Page(http, "offset", 0), Limit = Page(http, "limit", 50) }, ct).ConfigureAwait(false)
        })));
        endpoints.MapGet(Prefix + "/orgs/{id:guid}/members", (Delegate)((HttpContext http, Guid id) => Run(http, false, async (directory, ct) => new
        {
            items = await directory.QueryMembersAsync(new DirectoryMemberQuery
            { OrganizationId = id, Descendants = Descendants(http), Keyword = One(http, "q"), Offset = Page(http, "offset", 0), Limit = Page(http, "limit", 50) }, ct).ConfigureAwait(false)
        })));
        // 派生查询（S1-IDP-010）：上级链、直属主管、部门负责人、@下属 / @子孙；口径见 IIdentityDirectory 与 Identity Server README。
        endpoints.MapGet(Prefix + "/members/{id:guid}/superiors", (Delegate)((HttpContext http, Guid id) => Run(http, false, async (directory, ct) =>
            await directory.GetSuperiorsAsync(id, Uuid(http, "position_id"), ct).ConfigureAwait(false))));
        endpoints.MapGet(Prefix + "/members/{id:guid}/manager", (Delegate)((HttpContext http, Guid id) => Run(http, false, async (directory, ct) =>
            await directory.GetManagerAsync(id, ct).ConfigureAwait(false))));
        endpoints.MapGet(Prefix + "/orgs/{id:guid}/head", (Delegate)((HttpContext http, Guid id) => Run(http, false, async (directory, ct) =>
            await directory.GetOrganizationHeadAsync(id, ct).ConfigureAwait(false))));
        endpoints.MapGet(Prefix + "/me/subordinates", (Delegate)((HttpContext http) => Run(http, false, async (directory, caller, ct) => new
        { items = await directory.GetSubordinatesAsync(caller.MembershipId!.Value, Depth(http), Page(http, "offset", 0), Page(http, "limit", 50), ct).ConfigureAwait(false) })));
        endpoints.MapGet(Prefix + "/positions/{id:guid}/holders", (Delegate)((HttpContext http, Guid id) => Run(http, false, async (directory, ct) => new
        { items = await directory.GetPositionHoldersAsync(id, Page(http, "offset", 0), Page(http, "limit", 50), ct).ConfigureAwait(false) })));
        endpoints.MapGet(Prefix + "/members/{id:guid}/relations", (Delegate)((HttpContext http, Guid id) => Run(http, false, async (directory, ct) => new { items = await directory.GetRelationsAsync(id, ct).ConfigureAwait(false) })));
        endpoints.MapPut(Prefix + "/members/{id:guid}/relations", (Delegate)((HttpContext http, Guid id) => Write<RelationsBody>(http, async (directory, request, ct) => new { items = await directory.SetRelationsAsync(id, request.Items, ct).ConfigureAwait(false) })));
        endpoints.MapGet(Prefix + "/members/{id:guid}/profile", (Delegate)((HttpContext http, Guid id) => Run(http, true, async (directory, ct) =>
            await directory.GetProfileAsync(id, ct).ConfigureAwait(false) ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound)))));
        endpoints.MapPut(Prefix + "/members/{id:guid}/profile", (Delegate)((HttpContext http, Guid id) => Write<UserProfileInput>(http, async (directory, request, ct) => await directory.SetProfileAsync(id, request, ct).ConfigureAwait(false))));
        endpoints.MapPost(Prefix + "/members/{id:guid}/status", (Delegate)((HttpContext http, Guid id) => Write<StatusBody>(http, async (directory, request, ct) => await directory.SetDirectoryMemberStatusAsync(id, request.Status, request.Reason, ct).ConfigureAwait(false))));
        endpoints.MapPost(Prefix + "/members:disable", (Delegate)((HttpContext http) => Write<DisableBody>(http, async (directory, request, ct) => await directory.DisableMembersAsync(request.MembershipIds, request.Reason, ct).ConfigureAwait(false))));
        endpoints.MapDelete(Prefix + "/members/{id:guid}", (Delegate)((HttpContext http, Guid id) => Delete(http, (directory, ct) => directory.DeleteMemberAsync(id, "directory_delete", ct).AsTask())));
    }

    private sealed record GroupBody(string Name);
    private sealed record RelationsBody(IReadOnlyList<MemberRelation> Items);
    private sealed record StatusBody(string Status, string Reason);
    private sealed record DisableBody(IReadOnlyList<Guid> MembershipIds, string Reason);

    private static int Page(HttpContext http, string key, int fallback) => !http.Request.Query.TryGetValue(key, out var value) ? fallback :
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
    private static bool Descendants(HttpContext http) => !http.Request.Query.TryGetValue("descendants", out var value) ? false :
        bool.TryParse(value, out var parsed) ? parsed : throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
    // 单值参数重复出现即 400，不猜取哪一个。
    private static string? One(HttpContext http, string key) => !http.Request.Query.TryGetValue(key, out var value) ? null :
        value.Count == 1 ? value[0] : throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
    private static Guid? Uuid(HttpContext http, string key) => One(http, key) is not { } value ? null :
        Guid.TryParseExact(value, "D", out var parsed) ? parsed : throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
    // 可重复的 id 参数（?ids=a&ids=b）；每项必须是规范 UUID，数量与重复由 DirectoryMemberQuery 的规则判定。
    private static Guid[]? Ids(HttpContext http, string key) => !http.Request.Query.TryGetValue(key, out var values) ? null :
        [.. values.Select(value => Guid.TryParseExact(value, "D", out var parsed) ? parsed : throw new QianErrorException(new QianError(PlatformErrors.BadRequest)))];
    private static int? Depth(HttpContext http) => One(http, "depth") is not { } value ? null :
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : throw new QianErrorException(new QianError(PlatformErrors.BadRequest));

    private static Task<IResult> Run(HttpContext http, bool administrator, Func<IIdentityDirectory, CancellationToken, Task<object>> action) =>
        Run(http, administrator, (directory, _, ct) => action(directory, ct));

    private static async Task<IResult> Run(HttpContext http, bool administrator, Func<IIdentityDirectory, IdentityAccountService.SessionCaller, CancellationToken, Task<object>> action)
    {
        var service = IdentityAccountEndpoints.Service(http);
        if (service is null) return Results.NotFound();
        if (await IdentityAccountEndpoints.CurrentSession(http).ConfigureAwait(false) is not { } current ||
            await service.ResolveCallerAsync(current, http.RequestAborted).ConfigureAwait(false) is not { } caller)
            return QianResults.Error(new QianError(PlatformErrors.Unauthenticated));
        if (caller.TenantId is not { } tenant || caller.MembershipId is null || administrator && !caller.TenantAdministrator)
            return QianResults.Error(new QianError(PlatformErrors.AuthzDenied));
        using var scope = http.RequestServices.GetRequiredService<ITenantContext>().BeginScope(tenant.ToString("D"));
        using var origin = EventEnvelopeScope.Begin(new EventOrigin("qian.identity", EventActor.User(caller.PersonId.ToString("D"))));
        return Results.Json(await action(service, caller, http.RequestAborted).ConfigureAwait(false), Json);
    }

    private static async Task<IResult> Write<T>(HttpContext http, Func<IIdentityDirectory, T, CancellationToken, Task<object>> action) where T : class
    {
        if (await IdentityAccountEndpoints.Guard(http).ConfigureAwait(false) is { } rejected) return rejected;
        return await Run(http, true, async (IIdentityDirectory directory, CancellationToken ct) =>
        {
            var body = await IdentityAccountEndpoints.Read<T>(http, Json).ConfigureAwait(false) ?? throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
            try { return await action(directory, body, ct).ConfigureAwait(false); }
            catch (ArgumentNullException) { throw new QianErrorException(new QianError(PlatformErrors.BadRequest)); }
        }).ConfigureAwait(false);
    }

    private static async Task<IResult> Delete(HttpContext http, Func<IIdentityDirectory, CancellationToken, Task> action)
    {
        if (await IdentityAccountEndpoints.GuardAction(http).ConfigureAwait(false) is { } rejected) return rejected;
        return await Run(http, true, async (IIdentityDirectory directory, CancellationToken ct) => { await action(directory, ct).ConfigureAwait(false); return new { deleted = true }; }).ConfigureAwait(false);
    }
}
