using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Authz.Abstractions;
using Qian.Data;

namespace Qian.Authz;

// identity_revision 失效传播的服务端比对（PRD 7.4 / 7.5）：令牌只带 identity_revision，身份组件在授权变更后递增它并推送客户端换新令牌；
// 服务端记住每个主体见过的最高修订（本组件表 subject_identity_revisions，按租户 RLS），比它低的令牌即「旧令牌」，拒绝。
// 修订由身份签发方保证单调，所以抬高下限只需要看到更新的已验证令牌，不需要身份组件回调。
// 下限每次判定都读库（主键查找）而不进 Kun.Caching：内存后端不跨实例，靠 TTL 失效会让旧令牌在别的实例上多活 10 分钟，违反 R7 的 3 秒口径；
// 判定快照仍按 (租户, 主体, identity_revision, 租户授权修订) 缓存。平台主体（无租户）不在租户表里，S1 不比对。
internal sealed class IdentityRevisionGuard(IDataService data, ITenantContext tenant)
{
    internal const string StaleReason = "identity_revision_stale";

    // 返回 null 表示通过；否则为稳定拒绝原因。
    internal async ValueTask<string?> CheckAsync(EnforcementSubject subject, CancellationToken ct)
    {
        if (subject.IdentityRevision is not { } presented || subject.TenantId is null) return null;
        if (presented < 0) return StaleReason;
        var owner = tenant.TenantId!;
        await using var session = await data.OpenAsync<AuthzDbContext>(ct).ConfigureAwait(false);
        var db = session.Context;
        var row = await db.SubjectRevisions.SingleOrDefaultAsync(r => r.TenantId == owner && r.SubjectType == subject.Kind && r.SubjectId == subject.Id, ct).ConfigureAwait(false);
        if (row is not null && presented < row.IdentityRevision) return StaleReason;
        // 只抬高、不降低；两个宿主同时首次见到同一主体或同时抬高时，并发写入失败的一方忽略（对方写的值 ≥ 自己见到的，下一次请求再比对）。
        if (row is null) db.SubjectRevisions.Add(new SubjectIdentityRevisionRow { TenantId = owner, SubjectType = subject.Kind, SubjectId = subject.Id, IdentityRevision = presented, UpdatedAt = DateTimeOffset.UtcNow });
        else if (presented > row.IdentityRevision) { row.IdentityRevision = presented; row.UpdatedAt = DateTimeOffset.UtcNow; }
        else return null;
        try { await session.SaveChangesAsync(ct).ConfigureAwait(false); }
        catch (DbUpdateException) { }
        return null;
    }
}
