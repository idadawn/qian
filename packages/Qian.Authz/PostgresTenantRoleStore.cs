using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Authz;

// 供已授权的租户开通/角色管理流程使用；不对 HTTP 直接暴露，也不读取令牌中的权限集合。
internal sealed class PostgresTenantRoleStore(IDataService data, ITenantContext tenant)
{
    private static readonly string[] Builtins = ["tenant.owner", "tenant.admin", "tenant.developer", "tenant.user"];

    internal Task EnsureBuiltinsAsync(string actor, CancellationToken ct) => Write(actor, async (db, owner) =>
    {
        var existing = await db.Roles.Select(role => role.Code).ToListAsync(ct).ConfigureAwait(false);
        foreach (var code in Builtins.Where(code => !existing.Contains(code, StringComparer.Ordinal)))
            db.Roles.Add(new AuthzRoleRow { TenantId = owner, Code = code, Builtin = true });
    }, ct);

    internal Task AssignAsync(string roleCode, string subjectType, string subjectId, string actor, CancellationToken ct,
        DateTimeOffset? validFrom = null, DateTimeOffset? validTo = null)
    {
        Identifier(subjectId);
        if (subjectType is not ("user" or "organization" or "position" or "group" or "service" or "agent") || (validFrom is not null && validTo <= validFrom))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        return Write(actor, async (db, owner) =>
        {
            var role = await db.Roles.SingleOrDefaultAsync(row => row.Code == roleCode, ct).ConfigureAwait(false)
                ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            var existing = await db.RoleAssignments.SingleOrDefaultAsync(row => row.RoleId == role.Id && row.SubjectType == subjectType && row.SubjectId == subjectId, ct).ConfigureAwait(false);
            if (existing is null)
            {
                existing = new RoleAssignmentRow { TenantId = owner, RoleId = role.Id, SubjectType = subjectType, SubjectId = subjectId };
                db.RoleAssignments.Add(existing);
            }
            existing.ValidFrom = validFrom?.ToUniversalTime(); existing.ValidTo = validTo?.ToUniversalTime(); existing.AssignedBy = actor;
        }, ct);
    }

    // 撤销直接主体的角色绑定；同一事务递增租户授权修订，各宿主的判定快照随之失效。不存在的绑定返回 false，不泄露其它租户信息。
    internal async Task<bool> RevokeAsync(string roleCode, string subjectType, string subjectId, string actor, CancellationToken ct)
    {
        Identifier(subjectId);
        if (subjectType is not ("user" or "organization" or "position" or "group" or "service" or "agent")) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var removed = false;
        await Write(actor, async (db, _) =>
        {
            var assignment = await (from row in db.RoleAssignments
                                    join role in db.Roles on row.RoleId equals role.Id
                                    where role.Code == roleCode && row.SubjectType == subjectType && row.SubjectId == subjectId
                                    select row).SingleOrDefaultAsync(ct).ConfigureAwait(false);
            if (assignment is null) return;
            db.RoleAssignments.Remove(assignment); removed = true;
        }, ct).ConfigureAwait(false);
        return removed;
    }

    // Authz 视角的租户成员（持有本租户角色绑定的主体）；平台超管的绑定是平台行，按 RLS 与查询投影都不会出现在这里。
    internal async Task<IReadOnlyList<TenantRoleMember>> ListMembersAsync(CancellationToken ct)
    {
        var owner = Owner();
        await using var session = await data.OpenAsync<AuthzDbContext>(ct).ConfigureAwait(false);
        var db = session.Context;
        var rows = await (from assignment in db.RoleAssignments.AsNoTracking()
                          join role in db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
                          select new { assignment.SubjectType, assignment.SubjectId, role.Code }).ToArrayAsync(ct).ConfigureAwait(false);
        CheckOwner(owner);
        return Array.AsReadOnly(rows.GroupBy(row => (row.SubjectType, row.SubjectId))
            .OrderBy(group => group.Key.SubjectType, StringComparer.Ordinal).ThenBy(group => group.Key.SubjectId, StringComparer.Ordinal)
            .Select(group => new TenantRoleMember(group.Key.SubjectType, group.Key.SubjectId,
                Array.AsReadOnly(group.Select(row => row.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()))).ToArray());
    }

    internal Task InstallDefaultsAsync(string pluginId, string version, string? expectedVersion, string actor, CancellationToken ct) => Write(actor, async (db, owner) =>
    {
        var source = await db.Catalogs.AsNoTracking().SingleOrDefaultAsync(row => row.PluginId == pluginId && row.Version == version, ct).ConfigureAwait(false)
            ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        var catalog = JsonSerializer.Deserialize<PermissionCatalog>(source.Snapshot, JsonSerializerOptions.Web)!;
        var install = await db.PolicyInstalls.SingleOrDefaultAsync(row => row.PluginId == pluginId, ct).ConfigureAwait(false);
        if (install?.Version == version) return;
        if (install?.Version != expectedVersion) throw new QianErrorException(new QianError(PlatformErrors.Conflict));
        if (catalog.Roles.Any(role => role.Role.StartsWith("platform.", StringComparison.Ordinal))) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var permissions = catalog.Permissions.Select(permission => permission.Code).ToHashSet(StringComparer.Ordinal);
        if (catalog.Roles.Any(role => role.Grants.Any(grant => !permissions.Contains(grant)))) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var roles = await db.Roles.ToDictionaryAsync(role => role.Code, StringComparer.Ordinal, ct).ConfigureAwait(false);
        // 只更新此组件管理的默认授权；其他组件和人工授权不受影响。
        await db.Grants.Where(grant => grant.PluginId == pluginId && grant.Source == "default").ExecuteDeleteAsync(ct).ConfigureAwait(false);
        foreach (var template in catalog.Roles)
        {
            if (!roles.TryGetValue(template.Role, out var role))
            {
                role = new AuthzRoleRow { TenantId = owner, Code = template.Role, Builtin = Builtins.Contains(template.Role, StringComparer.Ordinal), SourcePack = pluginId };
                db.Roles.Add(role); roles.Add(role.Code, role);
            }
            foreach (var permission in template.Grants)
                db.Grants.Add(new RoleGrantRow { TenantId = owner, RoleId = role.Id, PluginId = pluginId, PluginVersion = version,
                    PermissionCode = permission, Source = "default", GrantedBy = actor });
        }
        if (install is null)
        {
            install = new TenantPolicyInstallRow { TenantId = owner, PluginId = pluginId };
            db.PolicyInstalls.Add(install);
        }
        if (install.Revision == int.MaxValue) throw new QianErrorException(new QianError(PlatformErrors.Conflict));
        install.Version = version; install.Revision++; install.UpdatedBy = actor;
    }, ct);

    internal async Task<IReadOnlyList<string>> ReadDirectUserPermissionsAsync(string subjectId, CancellationToken ct)
    {
        Identifier(subjectId); var owner = Owner(); var now = DateTimeOffset.UtcNow;
        await using var session = await data.OpenAsync<AuthzDbContext>(ct).ConfigureAwait(false);
        var db = session.Context;
        var result = await (from assignment in db.RoleAssignments.AsNoTracking()
                            join role in db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
                            join grant in db.Grants.AsNoTracking() on role.Id equals grant.RoleId
                            join active in db.PolicyInstalls.AsNoTracking() on new { grant.PluginId, Version = grant.PluginVersion } equals new { active.PluginId, active.Version }
                            join permission in db.Permissions.AsNoTracking() on new { grant.PluginId, Version = grant.PluginVersion, Code = grant.PermissionCode }
                                equals new { permission.PluginId, permission.Version, permission.Code }
                            where assignment.SubjectType == "user" && assignment.SubjectId == subjectId &&
                                (assignment.ValidFrom == null || assignment.ValidFrom <= now) && (assignment.ValidTo == null || assignment.ValidTo > now) && grant.ResourceRef == null
                            select permission.Code).Distinct().OrderBy(code => code).ToArrayAsync(ct).ConfigureAwait(false);
        CheckOwner(owner);
        return result;
    }

    private async Task Write(string actor, Func<AuthzDbContext, string, Task> update, CancellationToken ct)
    {
        Identifier(actor); var owner = Owner();
        await using var session = await data.OpenAsync<AuthzDbContext>(ct).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var key = "authz-tenant-roles:" + owner;
        await session.Context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct).ConfigureAwait(false);
        await update(session.Context, owner).ConfigureAwait(false);
        CheckOwner(owner);
        // 修订号与变更同事务提交：快照缓存以 (租户, 主体, 修订) 为键，提交即对所有宿主失效；没有变更（重复开通、撤销不存在的绑定）不递增。
        if (session.Context.ChangeTracker.HasChanges())
        {
            var revision = await session.Context.Revisions.SingleOrDefaultAsync(row => row.TenantId == owner, ct).ConfigureAwait(false);
            if (revision is null) session.Context.Revisions.Add(revision = new TenantAuthzRevisionRow { TenantId = owner });
            revision.Revision++; revision.UpdatedBy = actor;
        }
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        CheckOwner(owner);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }
    private string Owner()
    {
        if (string.IsNullOrWhiteSpace(tenant.TenantId)) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        Identifier(tenant.TenantId); return tenant.TenantId;
    }
    private void CheckOwner(string owner)
    {
        if (tenant.TenantId != owner) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
    }
    private static void Identifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value.Any(char.IsControl)) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
    }
}

/// <summary>租户内持有角色绑定的主体及其角色编码。</summary>
internal sealed record TenantRoleMember(string SubjectType, string SubjectId, IReadOnlyList<string> Roles);
