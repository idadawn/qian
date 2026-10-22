using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Authz.Abstractions;
using Qian.Data;

namespace Qian.Authz;

// identity_revision 失效传播的服务端比对（PRD 7.4 / 7.5）：令牌只带 identity_revision，身份组件在授权变更后递增它并推送客户端换新令牌；
// 服务端记住每个主体见过的最高修订（本组件表 subject_identity_revisions，按租户 RLS），比它低的令牌即「旧令牌」，拒绝（401，客户端据此换令牌）。
// 下限有两个来源，都只增不减：判定时见到更高修订的已验证令牌（懒抬高，修订由身份签发方保证单调）；同进程宿主里授权变更提交后，
// 身份端口返回的新修订（IdentityRevisionPropagation，S1-DEC-027）。
// 下限每次判定都读库（主键查找）而不进 Kun.Caching：内存后端不跨实例，靠 TTL 失效会让旧令牌在别的实例上多活 10 分钟，违反 R7 的 3 秒口径；
// 判定快照仍按 (租户, 主体, identity_revision, 租户授权修订) 缓存。平台主体（无租户）不在租户表里，S1 不比对。
internal sealed class IdentityRevisionGuard(IDataService data, ITenantContext tenant)
{
    // 返回 null 表示通过；否则为稳定拒绝原因。
    internal async ValueTask<string?> CheckAsync(EnforcementSubject subject, CancellationToken ct)
    {
        if (subject.IdentityRevision is not { } presented || subject.TenantId is null) return null;
        if (presented < 0) return AuthzEnforcement.StaleIdentityRevision;
        var owner = tenant.TenantId!;
        await using var session = await data.OpenAsync<AuthzDbContext>(ct).ConfigureAwait(false);
        var db = session.Context;
        var row = await db.SubjectRevisions.SingleOrDefaultAsync(r => r.TenantId == owner && r.SubjectType == subject.Kind && r.SubjectId == subject.Id, ct).ConfigureAwait(false);
        if (row is not null && presented < row.IdentityRevision) return AuthzEnforcement.StaleIdentityRevision;
        // 只抬高、不降低；两个宿主同时首次见到同一主体或同时抬高时，并发写入失败的一方忽略（修订列是并发令牌，不会把别人写的更高值压低；下一次请求再比对）。
        if (row is null) db.SubjectRevisions.Add(new SubjectIdentityRevisionRow { TenantId = owner, SubjectType = subject.Kind, SubjectId = subject.Id, IdentityRevision = presented, UpdatedAt = DateTimeOffset.UtcNow });
        else if (presented > row.IdentityRevision) { row.IdentityRevision = presented; row.UpdatedAt = DateTimeOffset.UtcNow; }
        else return null;
        try { await session.SaveChangesAsync(ct).ConfigureAwait(false); }
        catch (DbUpdateException) { }
        return null;
    }

    // 把下限抬到至少 revision（身份侧刚递增的新修订），只增不减：已有更低的行就条件更新，没有行就插入；
    // 插入撞上并发首写时清掉跟踪重来，所以正常返回时下限一定 ≥ revision。与懒抬高不同，这里不能「失败的一方忽略」。
    internal static async Task RaiseAsync(IDataService data, string owner, string kind, string id, long revision, CancellationToken ct)
    {
        await using var session = await data.OpenAsync<AuthzDbContext>(ct).ConfigureAwait(false);
        var db = session.Context;
        for (var attempt = 1; ; attempt++)
        {
            var rows = db.SubjectRevisions.Where(r => r.TenantId == owner && r.SubjectType == kind && r.SubjectId == id);
            var now = DateTimeOffset.UtcNow;
            if (await rows.Where(r => r.IdentityRevision < revision)
                    .ExecuteUpdateAsync(set => set.SetProperty(r => r.IdentityRevision, revision).SetProperty(r => r.UpdatedAt, now), ct).ConfigureAwait(false) > 0) return;
            if (await rows.AnyAsync(ct).ConfigureAwait(false)) return;
            db.SubjectRevisions.Add(new SubjectIdentityRevisionRow { TenantId = owner, SubjectType = kind, SubjectId = id, IdentityRevision = revision, UpdatedAt = now });
            try { await session.SaveChangesAsync(ct).ConfigureAwait(false); return; }
            catch (DbUpdateException) when (attempt < 3) { db.ChangeTracker.Clear(); }
        }
    }
}
