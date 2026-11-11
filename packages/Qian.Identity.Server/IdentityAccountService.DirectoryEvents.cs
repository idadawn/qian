using Microsoft.EntityFrameworkCore;
using Qian.Identity.Abstractions;

namespace Qian.Identity.Server;

internal sealed partial class IdentityAccountService
{
    // 一次目录写入产生的事件。载荷在写入完成后才生成：修订号要等写入成功、在同一事务里递增后才知道，行上的值也已是最终值。
    private sealed class DirectoryEventBatch
    {
        private readonly List<Func<long, DirectoryIntegrationEvent>> _pending = [];
        internal void Add(Func<long, DirectoryIntegrationEvent> create) => _pending.Add(create);
        internal bool IsEmpty => _pending.Count == 0;
        internal IEnumerable<DirectoryIntegrationEvent> Create(long revision) => _pending.Select(create => create(revision));
    }

    // 在当前事务里递增租户目录修订（原子 upsert，行锁持有到事务结束，并发写入因此按修订号排队），把事件挂到修订行上；
    // 随后的会话保存派发领域通知，DirectoryChangeHandler 把载荷写进同一事务的 Outbox。没有事件的写入不动修订。
    private static async Task<IReadOnlyList<DirectoryChange>> RaiseDirectoryEventsAsync(IdentityDbContext db, Guid tenant, DirectoryEventBatch events, CancellationToken ct)
    {
        if (events.IsEmpty) return [];
        var revision = (await IdentitySql.QueryAsync(db, $"""
            INSERT INTO {IdentitySql.DirectoryRevisions} (tenant_id, revision) VALUES (@tenant, 1)
            ON CONFLICT (tenant_id) DO UPDATE SET revision = {IdentitySql.DirectoryRevisions}.revision + 1 RETURNING revision
            """, reader => reader.GetInt64(0), ct, ("tenant", tenant.ToString("D"))).ConfigureAwait(false)).Single();
        // 修订行只作事件载体：不经 EF 改写，已跟踪的实例（同一上下文里第二次写入）直接复用。
        var row = db.Set<DirectoryRevisionRow>().Local.SingleOrDefault() ?? await db.Set<DirectoryRevisionRow>().SingleAsync(ct).ConfigureAwait(false);
        var changes = events.Create(revision).Select(payload => new DirectoryChange(payload)).ToArray();
        foreach (var change in changes) row.Raise(change);
        return changes;
    }

    // 会话保存之后、提交之前核对：宿主漏装 AddQianIdentityEvents 时抛错回滚，不会静默丢目录事件。
    private static void EnsurePublished(IReadOnlyList<DirectoryChange> changes)
    {
        if (changes.Any(change => !change.Published)) throw new InvalidOperationException("目录事件未进入 Outbox：宿主须登记 AddQianIdentityEvents。");
    }

    private static DirectoryOrganizationCreatedEvent OrganizationCreated(long revision, OrganizationRow row) =>
        new(revision, row.Id, row.ParentId, row.TypeCode, row.Path, row.Depth, row.ManagerMembershipId, row.ResponsiblePositionId);
    private static DirectoryOrganizationUpdatedEvent OrganizationUpdated(long revision, OrganizationRow row) =>
        new(revision, row.Id, row.ParentId, row.TypeCode, row.Path, row.Depth, row.ManagerMembershipId, row.ResponsiblePositionId);
    private static DirectoryPositionCreatedEvent PositionCreated(long revision, PositionRow row) =>
        new(revision, row.Id, row.OrganizationId, row.ParentId, row.Path, row.Depth, row.IsDefault, row.IsResponsible, row.ResponsibleMembershipId);
    private static DirectoryPositionUpdatedEvent PositionUpdated(long revision, PositionRow row) =>
        new(revision, row.Id, row.OrganizationId, row.ParentId, row.Path, row.Depth, row.IsDefault, row.IsResponsible, row.ResponsibleMembershipId);
}
