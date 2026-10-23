using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Identity.Server;

internal sealed partial class IdentityAccountService
{
    private static void DirectoryPage(int offset, int limit)
    { if (offset < 0 || limit is < 1 or > 200) throw IdentifierRules.Invalid("#/page"); }
    private static IQueryable<MembershipRow> VisibleMembers(IdentityDbContext db) => db.Memberships.AsNoTracking().Where(row => !row.DirectoryDeleted && row.Status != AccountStatuses.Purged);
    private static async Task<IReadOnlyList<DirectoryMember>> MembersPage(IQueryable<MembershipRow> query, int offset, int limit, CancellationToken ct) =>
        (await query.OrderBy(row => row.Account).ThenBy(row => row.Id).Skip(offset).Take(limit).ToArrayAsync(ct).ConfigureAwait(false)).Select(row => new DirectoryMember(ToMembership(row), row.DirectoryDeleted)).ToArray();

    public ValueTask<IReadOnlyList<DirectoryMember>> ListMembersAsync(int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
    {
        DirectoryPage(offset, limit); return DirectoryRead((db, ct) => MembersPage(VisibleMembers(db), offset, limit, ct), cancellationToken);
    }

    public ValueTask<IReadOnlyList<DirectoryMember>> GetOrganizationMembersAsync(Guid organizationId, bool descendants = false, int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
    {
        DirectoryPage(offset, limit);
        return DirectoryRead(async (db, ct) =>
        {
            var root = await db.Set<OrganizationRow>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == organizationId, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
            var orgIds = descendants ? await db.Set<OrganizationRow>().Where(row => row.Path.StartsWith(root.Path)).Select(row => row.Id).ToArrayAsync(ct).ConfigureAwait(false) : [organizationId];
            var relationIds = db.Set<MemberRelationRow>().Where(row => row.OrganizationId != null && orgIds.Contains(row.OrganizationId.Value) ||
                row.PositionId != null && db.Set<PositionRow>().Any(position => position.Id == row.PositionId && orgIds.Contains(position.OrganizationId))).Select(row => row.MembershipId);
            return await MembersPage(VisibleMembers(db).Where(row => relationIds.Contains(row.Id)), offset, limit, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    public ValueTask<IReadOnlyList<DirectoryMember>> GetPositionHoldersAsync(Guid positionId, int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
    {
        DirectoryPage(offset, limit);
        return DirectoryRead(async (db, ct) =>
        {
            if (!await db.Set<PositionRow>().AnyAsync(row => row.Id == positionId, ct).ConfigureAwait(false)) throw Error(PlatformErrors.NotFound);
            var ids = db.Set<MemberRelationRow>().Where(row => row.PositionId == positionId).Select(row => row.MembershipId);
            return await MembersPage(VisibleMembers(db).Where(row => ids.Contains(row.Id)), offset, limit, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    public ValueTask<IReadOnlyList<DirectoryGroup>> ListGroupsAsync(CancellationToken cancellationToken = default) => DirectoryRead<IReadOnlyList<DirectoryGroup>>(
        async (db, ct) => await db.Set<DirectoryGroupRow>().OrderBy(row => row.Name).Select(row => new DirectoryGroup(row.Id, row.Name)).ToArrayAsync(ct).ConfigureAwait(false), cancellationToken);
    public ValueTask<DirectoryGroup> CreateGroupAsync(string name, CancellationToken cancellationToken = default)
    {
        name = DirectoryName(name);
        return DirectoryWrite((db, _) => { var row = new DirectoryGroupRow { Id = Guid.CreateVersion7(), Name = name }; db.Add(row); return Task.FromResult(new DirectoryGroup(row.Id, row.Name)); }, cancellationToken);
    }
    public async ValueTask DeleteGroupAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await DirectoryWrite(async (db, ct) =>
        {
            var row = await db.Set<DirectoryGroupRow>().SingleOrDefaultAsync(row => row.Id == id, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
            if (await db.Set<MemberRelationRow>().AnyAsync(relation => relation.GroupId == id, ct).ConfigureAwait(false)) throw Error(IdentityErrors.DirectoryHasMembers);
            db.Remove(row); return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<IReadOnlyList<MemberRelation>> GetRelationsAsync(Guid membershipId, CancellationToken cancellationToken = default) => DirectoryRead<IReadOnlyList<MemberRelation>>(async (db, ct) =>
    {
        await DirectoryMemberRow(db, membershipId, ct).ConfigureAwait(false);
        return (await db.Set<MemberRelationRow>().AsNoTracking().Where(row => row.MembershipId == membershipId).OrderBy(row => row.TargetType).ThenBy(row => row.Id).ToArrayAsync(ct).ConfigureAwait(false))
            .Select(row => new MemberRelation(row.TargetType, row.OrganizationId ?? row.PositionId ?? row.GroupId!.Value, row.IsPrimary)).ToArray();
    }, cancellationToken);

    public ValueTask<IReadOnlyList<MemberRelation>> SetRelationsAsync(Guid membershipId, IReadOnlyList<MemberRelation> relations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relations);
        if (relations.Count > 100 || relations.Any(row => row is null || row.TargetId == Guid.Empty || row.TargetType is not ("organization" or "position" or "group")) ||
            relations.Select(row => (row.TargetType, row.TargetId)).Distinct().Count() != relations.Count || relations.Where(row => row.IsPrimary).GroupBy(row => row.TargetType).Any(group => group.Count() > 1)) throw IdentifierRules.Invalid("#/relations");
        var snapshot = relations.ToArray();
        return DirectoryWrite<IReadOnlyList<MemberRelation>>(async (db, ct) =>
        {
            // 标记删除后的成员仍允许解除历史归属，不能再新增归属或恢复账号。
            var member = await db.Memberships.SingleOrDefaultAsync(row => row.Id == membershipId && row.Status != AccountStatuses.Purged, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
            if (member.DirectoryDeleted && snapshot.Length != 0) throw Error(PlatformErrors.NotFound);
            foreach (var relation in snapshot)
            {
                var exists = relation.TargetType switch
                {
                    "organization" => await db.Set<OrganizationRow>().AnyAsync(row => row.Id == relation.TargetId, ct).ConfigureAwait(false),
                    "position" => await db.Set<PositionRow>().AnyAsync(row => row.Id == relation.TargetId, ct).ConfigureAwait(false),
                    _ => await db.Set<DirectoryGroupRow>().AnyAsync(row => row.Id == relation.TargetId, ct).ConfigureAwait(false)
                };
                if (!exists) throw Error(PlatformErrors.NotFound);
            }
            var previous = await db.Set<MemberRelationRow>().Where(row => row.MembershipId == membershipId).ToArrayAsync(ct).ConfigureAwait(false);
            // 先删除旧主归属再写新归属，避免唯一索引受 EF INSERT/DELETE 排序影响；同一事务失败即全部回滚。
            db.RemoveRange(previous); await db.SaveChangesAsync(ct).ConfigureAwait(false);
            foreach (var relation in snapshot) db.Add(new MemberRelationRow
            {
                Id = Guid.CreateVersion7(), MembershipId = membershipId, TargetType = relation.TargetType, IsPrimary = relation.IsPrimary,
                OrganizationId = relation.TargetType == "organization" ? relation.TargetId : null,
                PositionId = relation.TargetType == "position" ? relation.TargetId : null,
                GroupId = relation.TargetType == "group" ? relation.TargetId : null
            });
            member.IdentityRevision++; return snapshot;
        }, cancellationToken);
    }

    private static UserProfile Profile(UserProfileRow row)
    {
        using var extensions = JsonDocument.Parse(row.Extensions);
        return new(row.MembershipId, row.Name, row.Pinyin, row.Phone is null ? null : IdentifierRules.Mask(IdentifierKind.Phone, row.Phone),
            row.Email is null ? null : IdentifierRules.Mask(IdentifierKind.Email, row.Email), extensions.RootElement.Clone());
    }

    public ValueTask<UserProfile?> GetProfileAsync(Guid membershipId, CancellationToken cancellationToken = default) => DirectoryRead(async (db, ct) =>
    {
        await DirectoryMemberRow(db, membershipId, ct).ConfigureAwait(false);
        var row = await db.Set<UserProfileRow>().AsNoTracking().SingleOrDefaultAsync(row => row.MembershipId == membershipId, ct).ConfigureAwait(false);
        return row is null ? null : Profile(row);
    }, cancellationToken);

    public ValueTask<UserProfile> SetProfileAsync(Guid membershipId, UserProfileInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); var name = DirectoryName(request.Name);
        var pinyin = request.Pinyin is null ? null : DirectoryName(request.Pinyin);
        var phone = request.Phone is null ? null : IdentifierRules.Normalize(IdentifierKind.Phone, request.Phone, _account.DefaultPhoneCountryCode) ?? throw IdentifierRules.Invalid("#/phone");
        var email = request.Email is null ? null : IdentifierRules.Normalize(IdentifierKind.Email, request.Email, _account.DefaultPhoneCountryCode) ?? throw IdentifierRules.Invalid("#/email");
        var extra = request.Extensions?.GetRawText() ?? "{}";
        if (request.Extensions is { ValueKind: not JsonValueKind.Object } || System.Text.Encoding.UTF8.GetByteCount(extra) > 16384) throw IdentifierRules.Invalid("#/extensions");
        return DirectoryWrite(async (db, ct) =>
        {
            var member = await DirectoryMemberRow(db, membershipId, ct).ConfigureAwait(false);
            var row = await db.Set<UserProfileRow>().SingleOrDefaultAsync(row => row.MembershipId == membershipId, ct).ConfigureAwait(false);
            if (row is null) db.Add(row = new UserProfileRow { MembershipId = membershipId });
            row.Name = name; row.Pinyin = pinyin; row.Phone = phone; row.Email = email; row.Extensions = extra;
            member.DisplayName = name; return Profile(row);
        }, cancellationToken);
    }

    public ValueTask<Membership> SetDirectoryMemberStatusAsync(Guid membershipId, string status, string reason, CancellationToken cancellationToken = default) =>
        SetMembershipStatusCoreAsync(membershipId, status, reason, true, false, cancellationToken);

    public async ValueTask<DirectoryBatchResult> DisableMembersAsync(IReadOnlyList<Guid> membershipIds, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membershipIds);
        if (membershipIds.Count is < 1 or > 100 || membershipIds.Any(id => id == Guid.Empty) || membershipIds.Distinct().Count() != membershipIds.Count) throw IdentifierRules.Invalid("#/membership_ids");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 32) throw IdentifierRules.Invalid("#/reason");
        var results = new List<DirectoryBatchItem>();
        foreach (var id in membershipIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { await SetDirectoryMemberStatusAsync(id, AccountStatuses.Disabled, reason, cancellationToken).ConfigureAwait(false); results.Add(new(id, true, null)); }
            catch (QianErrorException error) { results.Add(new(id, false, error.Error.Code.Code)); }
        }
        var success = results.Count(row => row.Succeeded);
        return new(success == results.Count ? "SUCCESS" : success == 0 ? "FAILED" : "PARTIAL_SUCCESS", results.AsReadOnly());
    }

    public async ValueTask DeleteMemberAsync(Guid membershipId, string reason, CancellationToken cancellationToken = default) =>
        _ = await SetMembershipStatusCoreAsync(membershipId, AccountStatuses.Disabled, reason, true, true, cancellationToken).ConfigureAwait(false);
}
