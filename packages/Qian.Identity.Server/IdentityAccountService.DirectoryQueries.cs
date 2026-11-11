using Microsoft.EntityFrameworkCore;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Identity.Server;

// 目录派生查询（PRD 7.4.3，S1-IDP-010）：成员检索、按岗位链的上级责任人、直属主管、部门负责人与 @下属 / @子孙。
// 口径由产品负责人 2026-11-11 确认；派生查询一次读出本租户的组织与岗位结构在内存里推导（两条查询，S1 规模下是几百到几千行），
// 不做逐级往返查询。所有读取都在当前已验证租户内，FORCE RLS 与 EF 租户过滤照常生效。
internal sealed partial class IdentityAccountService
{
    private static string? MemberKeyword(string? value)
    {
        if (value is null) return null;
        var keyword = value.Trim();
        return keyword.Length is >= 1 and <= DirectoryMemberQuery.MaximumKeywordLength && !keyword.Any(char.IsControl) ? keyword : throw IdentifierRules.Invalid("#/q");
    }

    private static Guid[]? MemberIds(IReadOnlyList<Guid>? ids, string pointer)
    {
        if (ids is null) return null;
        return ids.Count is >= 1 and <= DirectoryMemberQuery.MaximumIds && !ids.Contains(Guid.Empty) && ids.Distinct().Count() == ids.Count ? [.. ids] : throw IdentifierRules.Invalid(pointer);
    }

    // ILIKE 的通配符按字面量处理：关键字里的 % _ \ 都转义，不能被用来写模式。
    private static string LikePattern(string keyword) => "%" + keyword.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";

    public ValueTask<IReadOnlyList<DirectoryMember>> QueryMembersAsync(DirectoryMemberQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        DirectoryPage(query.Offset, query.Limit);
        var keyword = MemberKeyword(query.Keyword); var ids = MemberIds(query.MembershipIds, "#/ids"); var persons = MemberIds(query.PersonIds, "#/person_ids");
        if ((keyword is null ? 0 : 1) + (ids is null ? 0 : 1) + (persons is null ? 0 : 1) > 1) throw IdentifierRules.Invalid("#/q");
        return DirectoryRead(async (db, ct) =>
        {
            var members = VisibleMembers(db);
            if (query.OrganizationId is { } organizationId)
            {
                var root = await db.Set<OrganizationRow>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == organizationId, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
                var orgIds = query.Descendants ? await db.Set<OrganizationRow>().Where(row => row.Path.StartsWith(root.Path)).Select(row => row.Id).ToArrayAsync(ct).ConfigureAwait(false) : [organizationId];
                var relationIds = db.Set<MemberRelationRow>().Where(row => row.OrganizationId != null && orgIds.Contains(row.OrganizationId.Value) ||
                    row.PositionId != null && db.Set<PositionRow>().Any(position => position.Id == row.PositionId && orgIds.Contains(position.OrganizationId))).Select(row => row.MembershipId);
                members = members.Where(row => relationIds.Contains(row.Id));
            }
            if (keyword is not null)
            {
                // 显示名、账号与档案拼音；手机号与邮箱只对管理员可见，不参与检索，避免借检索探测联系方式。
                var pattern = LikePattern(keyword);
                members = members.Where(row => EF.Functions.ILike(row.DisplayName, pattern, "\\") || EF.Functions.ILike(row.Account, pattern, "\\") ||
                    db.Set<UserProfileRow>().Any(profile => profile.MembershipId == row.Id && profile.Pinyin != null && EF.Functions.ILike(profile.Pinyin, pattern, "\\")));
            }
            if (ids is not null) members = members.Where(row => ids.Contains(row.Id));
            if (persons is not null) members = members.Where(row => persons.Contains(row.PersonId));
            return await MembersPage(members, query.Offset, query.Limit, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    public ValueTask<DirectorySuperiors> GetSuperiorsAsync(Guid membershipId, Guid? positionId = null, CancellationToken cancellationToken = default) => DirectoryRead(async (db, ct) =>
    {
        await DirectoryMemberRow(db, membershipId, ct).ConfigureAwait(false);
        var graph = await DirectoryGraph.LoadAsync(db, ct).ConfigureAwait(false);
        var relations = await db.Set<MemberRelationRow>().AsNoTracking().Where(row => row.MembershipId == membershipId && row.TargetType != "group").ToArrayAsync(ct).ConfigureAwait(false);
        Guid? startPosition, startOrganization = null;
        if (positionId is { } requested)
        {
            // 只能从本人担任的岗位起算；不是本人岗位与岗位不存在同样 404，不泄露岗位是否存在。
            if (!relations.Any(row => row.PositionId == requested)) throw Error(PlatformErrors.NotFound);
            startPosition = requested;
        }
        else
        {
            startPosition = relations.FirstOrDefault(row => row.IsPrimary && row.PositionId is not null)?.PositionId;
            if (startPosition is null) startOrganization = relations.FirstOrDefault(row => row.IsPrimary && row.OrganizationId is not null)?.OrganizationId;
        }
        var chain = graph.Chain(startPosition, startOrganization);
        var responsible = await ActiveMembersAsync(db, chain.Select(row => row.ResponsibleMembershipId), ct).ConfigureAwait(false);
        var items = chain.Select((position, index) => position.ResponsibleMembershipId is not { } holder
                ? new DirectorySuperior(index + 1, position.Id, position.OrganizationId, null, DirectoryVacancies.Unassigned)
                : responsible.TryGetValue(holder, out var member)
                    ? new DirectorySuperior(index + 1, position.Id, position.OrganizationId, member, null)
                    : new DirectorySuperior(index + 1, position.Id, position.OrganizationId, null, DirectoryVacancies.Inactive)).ToArray();
        // 结构快照与归属分两条语句读；其间并发新建的岗位不在快照里，此时链为空、起点组织未知，而不是 500。
        var origin = startPosition is { } start ? graph.Positions.GetValueOrDefault(start)?.OrganizationId : startOrganization;
        return new DirectorySuperiors(membershipId, startPosition, origin, items);
    }, cancellationToken);

    public ValueTask<DirectoryManager> GetManagerAsync(Guid membershipId, CancellationToken cancellationToken = default) => DirectoryRead(async (db, ct) =>
    {
        await DirectoryMemberRow(db, membershipId, ct).ConfigureAwait(false);
        var graph = await DirectoryGraph.LoadAsync(db, ct).ConfigureAwait(false);
        var primaries = await db.Set<MemberRelationRow>().AsNoTracking().Where(row => row.MembershipId == membershipId && row.IsPrimary && row.TargetType != "group").ToArrayAsync(ct).ConfigureAwait(false);
        var start = primaries.FirstOrDefault(row => row.OrganizationId is not null)?.OrganizationId ??
            (primaries.FirstOrDefault(row => row.PositionId is not null)?.PositionId is { } position && graph.Positions.TryGetValue(position, out var held) ? held.OrganizationId : null);
        // 从起点组织逐级向上：本人就是主管、未设主管或主管已停用 / 删除时继续取上级组织的主管。
        var candidates = graph.OrganizationChain(start).Where(org => org.ManagerMembershipId is { } manager && manager != membershipId).ToArray();
        var managers = await ActiveMembersAsync(db, candidates.Select(org => org.ManagerMembershipId), ct).ConfigureAwait(false);
        foreach (var org in candidates)
            if (managers.TryGetValue(org.ManagerMembershipId!.Value, out var manager)) return new DirectoryManager(membershipId, manager, org.Id);
        return new DirectoryManager(membershipId, null, null);
    }, cancellationToken);

    public ValueTask<OrganizationHead> GetOrganizationHeadAsync(Guid organizationId, CancellationToken cancellationToken = default) => DirectoryRead(async (db, ct) =>
    {
        var org = await db.Set<OrganizationRow>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == organizationId, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
        var position = org.ResponsiblePositionId is { } responsibleId ? await db.Set<PositionRow>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == responsibleId, ct).ConfigureAwait(false) : null;
        var members = await ActiveMembersAsync(db, [position?.ResponsibleMembershipId, org.ManagerMembershipId], ct).ConfigureAwait(false);
        Membership? Active(Guid? id) => id is { } value && members.TryGetValue(value, out var member) ? member : null;
        var responsible = Active(position?.ResponsibleMembershipId);
        var vacancy = position is null ? null : position.ResponsibleMembershipId is null ? DirectoryVacancies.Unassigned : responsible is null ? DirectoryVacancies.Inactive : null;
        return new OrganizationHead(org.Id, position?.Id, responsible, vacancy, Active(org.ManagerMembershipId));
    }, cancellationToken);

    public ValueTask<IReadOnlyList<DirectoryMember>> GetSubordinatesAsync(Guid membershipId, int? depth = null, int offset = 0, int limit = 50, CancellationToken cancellationToken = default)
    {
        DirectoryPage(offset, limit);
        if (depth is < 1) throw IdentifierRules.Invalid("#/depth");
        return DirectoryRead(async (db, ct) =>
        {
            var me = await DirectoryMemberRow(db, membershipId, ct).ConfigureAwait(false);
            // 停用或锁定的成员在别人的上级链里是空缺（inactive），因此没有下属。
            if (me.Status != AccountStatuses.Active) return [];
            var graph = await DirectoryGraph.LoadAsync(db, ct).ConfigureAwait(false);
            var mine = graph.Positions.Values.Where(row => row.ResponsibleMembershipId == membershipId).Select(row => row.Id).ToHashSet();
            if (mine.Count == 0) return [];
            var primaries = await db.Set<MemberRelationRow>().AsNoTracking().Where(row => row.IsPrimary && row.TargetType != "group")
                .Select(row => new { row.MembershipId, row.PositionId, row.OrganizationId }).ToArrayAsync(ct).ConfigureAwait(false);
            // 上级链只取决于起点（主岗位，没有时主组织），同一起点的成员共用一次推导。
            var levels = new Dictionary<(Guid? Position, Guid? Organization), int?>();
            var matched = new List<Guid>();
            foreach (var member in primaries.GroupBy(row => row.MembershipId))
            {
                if (member.Key == membershipId) continue;
                var position = member.FirstOrDefault(row => row.PositionId is not null)?.PositionId;
                var key = position is not null ? (position, (Guid?)null) : ((Guid?)null, member.FirstOrDefault(row => row.OrganizationId is not null)?.OrganizationId);
                if (!levels.TryGetValue(key, out var level))
                {
                    var index = graph.Chain(key.Item1, key.Item2).Select((row, i) => (row.Id, Level: i + 1)).FirstOrDefault(item => mine.Contains(item.Id)).Level;
                    levels[key] = level = index == 0 ? null : index;
                }
                if (level is { } found && (depth is null || found <= depth)) matched.Add(member.Key);
            }
            return await MembersPage(VisibleMembers(db).Where(row => matched.Contains(row.Id)), offset, limit, ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    // 只返回启用且未从目录删除的成员；停用、锁定、删除或已清除的一律不在结果里（调用方据此判为空缺）。
    private static async Task<Dictionary<Guid, Membership>> ActiveMembersAsync(IdentityDbContext db, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var wanted = ids.OfType<Guid>().Distinct().ToArray();
        if (wanted.Length == 0) return [];
        return (await db.Memberships.AsNoTracking().Where(row => wanted.Contains(row.Id) && row.Status == AccountStatuses.Active && !row.DirectoryDeleted).ToArrayAsync(ct).ConfigureAwait(false))
            .ToDictionary(row => row.Id, ToMembership);
    }

    // 本租户的组织与岗位结构快照；写入路径已保证无环，遍历仍带访问集合兜底。
    private sealed class DirectoryGraph
    {
        private DirectoryGraph(Dictionary<Guid, PositionRow> positions, Dictionary<Guid, OrganizationRow> organizations) => (Positions, Organizations) = (positions, organizations);
        internal Dictionary<Guid, PositionRow> Positions { get; }
        internal Dictionary<Guid, OrganizationRow> Organizations { get; }

        internal static async Task<DirectoryGraph> LoadAsync(IdentityDbContext db, CancellationToken ct) => new(
            await db.Set<PositionRow>().AsNoTracking().ToDictionaryAsync(row => row.Id, ct).ConfigureAwait(false),
            await db.Set<OrganizationRow>().AsNoTracking().ToDictionaryAsync(row => row.Id, ct).ConfigureAwait(false));

        internal IEnumerable<OrganizationRow> OrganizationChain(Guid? start)
        {
            var seen = new HashSet<Guid>();
            for (var org = start is { } id ? Organizations.GetValueOrDefault(id) : null; org is not null && seen.Add(org.Id); org = org.ParentId is { } parent ? Organizations.GetValueOrDefault(parent) : null)
                yield return org;
        }

        // 岗位链：先沿岗位父链向上（不含起点；父岗位在别的组织也照走），每个责任岗位算一级；
        // 岗位链到顶后，从最上层岗位所属组织起逐级取各组织的责任岗位，已出现过的岗位跳过。没有岗位起点时直接从起点组织取。
        internal List<PositionRow> Chain(Guid? startPosition, Guid? startOrganization)
        {
            var chain = new List<PositionRow>(); var visited = new HashSet<Guid>();
            var organization = startOrganization;
            if (startPosition is { } id && Positions.TryGetValue(id, out var start))
            {
                visited.Add(start.Id); var top = start;
                for (var parent = Parent(start); parent is not null && visited.Add(parent.Id); parent = Parent(parent))
                {
                    if (parent.IsResponsible) chain.Add(parent);
                    top = parent;
                }
                organization = top.OrganizationId;
            }
            foreach (var org in OrganizationChain(organization))
                if (org.ResponsiblePositionId is { } responsible && Positions.TryGetValue(responsible, out var position) && visited.Add(position.Id)) chain.Add(position);
            return chain;
        }

        private PositionRow? Parent(PositionRow row) => row.ParentId is { } parent ? Positions.GetValueOrDefault(parent) : null;
    }
}
