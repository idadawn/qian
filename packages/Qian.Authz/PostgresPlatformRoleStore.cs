using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Authz;

// 平台角色行（tenant_id 为 null）只经专用平台授权登录读写：普通租户凭据在 FORCE RLS 下看不到、也写不进这些行。
// 授予平台超管属 humanOnly 管理操作（PRD 7.5 Agent 契约），本类只供平台运营流程与测试引导，不对 HTTP 直接暴露。
internal sealed class PostgresPlatformRoleStore(IDataService data, ITenantContext tenant, string platformRole)
{
    internal async Task ValidateAsync(CancellationToken ct)
    {
        using var neutral = tenant.BeginScope(null);
        await using var session = await data.OpenAsync<PlatformAuthzDbContext>(ct).ConfigureAwait(false);
        await session.Context.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await AuthzPlatformAccess.ValidateConnectionAsync(session.Context.Database.GetDbConnection(), platformRole, ct).ConfigureAwait(false);
    }

    // 平台超管只授予人类用户；写入必须在无租户上下文的平台运营流程中进行，不能从某个租户的请求里顺手提升。
    internal Task AssignSuperadminAsync(string userId, string actor, CancellationToken ct, DateTimeOffset? validFrom = null, DateTimeOffset? validTo = null)
    {
        Identifier(userId);
        if (validFrom is not null && validTo <= validFrom) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        return Write(actor, async db =>
        {
            var role = await db.Roles.SingleOrDefaultAsync(row => row.Code == PlatformRoles.Superadmin, ct).ConfigureAwait(false);
            if (role is null) db.Roles.Add(role = new AuthzRoleRow { TenantId = null, Code = PlatformRoles.Superadmin, Builtin = true });
            var existing = await db.RoleAssignments.SingleOrDefaultAsync(row => row.RoleId == role.Id && row.SubjectType == "user" && row.SubjectId == userId, ct).ConfigureAwait(false);
            if (existing is null) db.RoleAssignments.Add(existing = new RoleAssignmentRow { TenantId = null, RoleId = role.Id, SubjectType = "user", SubjectId = userId });
            existing.ValidFrom = validFrom?.ToUniversalTime(); existing.ValidTo = validTo?.ToUniversalTime(); existing.AssignedBy = actor;
        }, ct);
    }

    internal async Task<bool> RevokeSuperadminAsync(string userId, string actor, CancellationToken ct)
    {
        Identifier(userId); var removed = false;
        await Write(actor, async db =>
        {
            var assignment = await (from row in db.RoleAssignments
                                    join role in db.Roles on row.RoleId equals role.Id
                                    where role.Code == PlatformRoles.Superadmin && row.SubjectType == "user" && row.SubjectId == userId
                                    select row).SingleOrDefaultAsync(ct).ConfigureAwait(false);
            if (assignment is null) return;
            db.RoleAssignments.Remove(assignment); removed = true;
        }, ct).ConfigureAwait(false);
        return removed;
    }

    // 旁路判定时调用方可能处在目标租户的上下文中；读取平台行须临时退到无租户上下文，退出后恢复原上下文。
    internal async Task<bool> IsSuperadminAsync(string userId, CancellationToken ct)
    {
        Identifier(userId); var now = DateTimeOffset.UtcNow;
        using var neutral = tenant.BeginScope(null);
        await using var session = await data.OpenAsync<PlatformAuthzDbContext>(ct).ConfigureAwait(false);
        var db = session.Context;
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await AuthzPlatformAccess.ValidateConnectionAsync(db.Database.GetDbConnection(), platformRole, ct).ConfigureAwait(false);
        return await (from assignment in db.RoleAssignments.AsNoTracking()
                      join role in db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
                      where role.Code == PlatformRoles.Superadmin && assignment.SubjectType == "user" && assignment.SubjectId == userId &&
                          (assignment.ValidFrom == null || assignment.ValidFrom <= now) && (assignment.ValidTo == null || assignment.ValidTo > now)
                      select assignment.Id).AnyAsync(ct).ConfigureAwait(false);
    }

    private async Task Write(string actor, Func<PlatformAuthzDbContext, Task> update, CancellationToken ct)
    {
        Identifier(actor);
        if (!string.IsNullOrEmpty(tenant.TenantId)) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
        await using var session = await data.OpenAsync<PlatformAuthzDbContext>(ct).ConfigureAwait(false);
        var db = session.Context;
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await AuthzPlatformAccess.ValidateConnectionAsync(db.Database.GetDbConnection(), platformRole, ct).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"authz-platform-roles"}, 0))", ct).ConfigureAwait(false);
        await update(db).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(tenant.TenantId)) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    private static void Identifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(char.IsControl)) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
    }
}
