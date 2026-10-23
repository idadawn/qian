using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Identity.Server;

internal sealed partial class IdentityAccountService
{
    private static readonly Regex DirectoryCode = new("\\A[a-z][a-z0-9_-]{0,63}\\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static string DirectoryName(string? value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 200 && !value.Any(char.IsControl)
        ? value.Trim() : throw IdentifierRules.Invalid("#/name");
    private static string TypeCode(string? value) => value is not null && DirectoryCode.IsMatch(value) ? value : throw IdentifierRules.Invalid("#/type_code");
    private static string PathFor(string? parentPath, Guid id) => (parentPath ?? "/") + id.ToString("D") + "/";
    private static Organization Org(OrganizationRow row) => new(row.Id, row.Name, row.TypeCode, row.ParentId, row.Path, row.Depth, row.ManagerMembershipId, row.ResponsiblePositionId);
    private static DirectoryPosition Position(PositionRow row) => new(row.Id, row.Name, row.OrganizationId, row.ParentId, row.Path, row.Depth, row.IsDefault, row.IsResponsible, row.ResponsibleMembershipId);

    // 所有目录写入共用租户事务锁，树移动、成员归属与删除不会相互穿插。锁内查询始终保留 EF 过滤与 FORCE RLS。
    private async ValueTask<T> DirectoryWrite<T>(Func<IdentityDbContext, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var tenant = RequireTenant(); using var scope = TenantScope(tenant);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await IdentitySql.LockAsync(session.Context, "directory:" + tenant.ToString("D"), ct).ConfigureAwait(false);
        try
        {
            var result = await action(session.Context, ct).ConfigureAwait(false);
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return result;
        }
        catch (DbUpdateException error) when (IsUniqueViolation(error)) { throw Error(PlatformErrors.Conflict); }
    }

    private async ValueTask<T> DirectoryRead<T>(Func<IdentityDbContext, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var tenant = RequireTenant(); using var scope = TenantScope(tenant);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        return await action(session.Context, ct).ConfigureAwait(false);
    }

    private static async Task<MembershipRow> DirectoryMemberRow(IdentityDbContext db, Guid id, CancellationToken ct) =>
        await db.Memberships.SingleOrDefaultAsync(row => row.Id == id && !row.DirectoryDeleted && row.Status != AccountStatuses.Purged, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);

    public ValueTask<IReadOnlyList<OrganizationType>> ListOrganizationTypesAsync(CancellationToken cancellationToken = default) => DirectoryRead<IReadOnlyList<OrganizationType>>(
        async (db, ct) => await db.Set<OrganizationTypeRow>().AsNoTracking().OrderBy(row => row.Code).Select(row => new OrganizationType(row.Code, row.Name)).ToArrayAsync(ct).ConfigureAwait(false), cancellationToken);

    public ValueTask<OrganizationType> SetOrganizationTypeAsync(OrganizationType request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); var code = TypeCode(request.Code); var name = DirectoryName(request.Name);
        return DirectoryWrite(async (db, ct) =>
        {
            var row = await db.Set<OrganizationTypeRow>().SingleOrDefaultAsync(row => row.Code == code, ct).ConfigureAwait(false);
            if (row is null) db.Add(row = new OrganizationTypeRow { Code = code }); row.Name = name;
            return new OrganizationType(code, name);
        }, cancellationToken);
    }

    public async ValueTask DeleteOrganizationTypeAsync(string code, CancellationToken cancellationToken = default)
    {
        TypeCode(code);
        await DirectoryWrite(async (db, ct) =>
        {
            var row = await db.Set<OrganizationTypeRow>().SingleOrDefaultAsync(row => row.Code == code, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
            if (await db.Set<OrganizationRow>().AnyAsync(org => org.TypeCode == code, ct).ConfigureAwait(false)) throw Error(PlatformErrors.Conflict);
            db.Remove(row); return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<IReadOnlyList<Organization>> GetOrganizationTreeAsync(CancellationToken cancellationToken = default) => DirectoryRead<IReadOnlyList<Organization>>(
        async (db, ct) => (await db.Set<OrganizationRow>().AsNoTracking().OrderBy(row => row.Path).ToArrayAsync(ct).ConfigureAwait(false)).Select(Org).ToArray(), cancellationToken);
    public ValueTask<Organization> CreateOrganizationAsync(OrganizationInput request, CancellationToken cancellationToken = default) => SaveOrganization(null, request, cancellationToken);
    public ValueTask<Organization> UpdateOrganizationAsync(Guid id, OrganizationInput request, CancellationToken cancellationToken = default) => SaveOrganization(id, request, cancellationToken);

    private ValueTask<Organization> SaveOrganization(Guid? id, OrganizationInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request); var name = DirectoryName(request.Name); var code = TypeCode(request.TypeCode);
        return DirectoryWrite(async (db, ct) =>
        {
            if (!await db.Set<OrganizationTypeRow>().AnyAsync(row => row.Code == code, ct).ConfigureAwait(false)) throw Error(PlatformErrors.NotFound);
            OrganizationRow? parent = null;
            if (request.ParentId is { } parentId) parent = await db.Set<OrganizationRow>().SingleOrDefaultAsync(row => row.Id == parentId, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
            var row = id is { } existing ? await db.Set<OrganizationRow>().SingleOrDefaultAsync(row => row.Id == existing, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound)
                : new OrganizationRow { Id = Guid.CreateVersion7() };
            if (parent is not null && id is not null && parent.Path.StartsWith(row.Path, StringComparison.Ordinal)) throw Error(IdentityErrors.DirectoryCycle);
            if (parent is null && await db.Set<OrganizationRow>().AnyAsync(other => other.ParentId == null && other.Id != row.Id, ct).ConfigureAwait(false)) throw Error(PlatformErrors.Conflict);
            var descendants = id is null ? [] : await db.Set<OrganizationRow>().Where(other => other.Id != row.Id && other.Path.StartsWith(row.Path)).ToListAsync(ct).ConfigureAwait(false);
            DirectoryRules.ValidateDepth(parent?.Depth ?? 0, descendants.Count == 0 ? 1 : descendants.Max(other => other.Depth) - row.Depth + 1);
            if (request.ManagerMembershipId is { } manager) await DirectoryMemberRow(db, manager, ct).ConfigureAwait(false);
            if (request.ResponsiblePositionId is { } responsible && !await db.Set<PositionRow>().AnyAsync(position => position.Id == responsible && position.OrganizationId == row.Id && position.IsResponsible, ct).ConfigureAwait(false)) throw Error(PlatformErrors.NotFound);
            var oldPath = row.Path; var path = PathFor(parent?.Path, row.Id); var depth = (parent?.Depth ?? 0) + 1; var shift = depth - row.Depth;
            foreach (var child in descendants) { child.Path = path + child.Path[oldPath.Length..]; child.Depth += shift; }
            row.Name = name; row.TypeCode = code; row.ParentId = parent?.Id; row.Path = path; row.Depth = depth;
            row.ManagerMembershipId = request.ManagerMembershipId; row.ResponsiblePositionId = request.ResponsiblePositionId;
            if (id is null) db.Add(row); return Org(row);
        }, cancellationToken);
    }

    public async ValueTask DeleteOrganizationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await DirectoryWrite(async (db, ct) =>
        {
            var root = await db.Set<OrganizationRow>().SingleOrDefaultAsync(row => row.Id == id, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
            var organizations = await db.Set<OrganizationRow>().Where(row => row.Path.StartsWith(root.Path)).ToListAsync(ct).ConfigureAwait(false);
            var ids = organizations.Select(row => row.Id).ToArray();
            var positions = await db.Set<PositionRow>().Where(row => ids.Contains(row.OrganizationId)).ToListAsync(ct).ConfigureAwait(false);
            var positionIds = positions.Select(row => row.Id).ToArray();
            // 组织删除不跨组织级联岗位：外部组织的岗位若仍依赖本子树岗位，先调整其父链。
            if (await db.Set<PositionRow>().AnyAsync(row => row.ParentId != null && positionIds.Contains(row.ParentId.Value) && !ids.Contains(row.OrganizationId), ct).ConfigureAwait(false)) throw Error(PlatformErrors.Conflict);
            if (organizations.Any(row => row.ManagerMembershipId is not null) || positions.Any(row => row.ResponsibleMembershipId is not null) ||
                await db.Set<MemberRelationRow>().AnyAsync(row => row.OrganizationId != null && ids.Contains(row.OrganizationId.Value) || row.PositionId != null && positionIds.Contains(row.PositionId.Value), ct).ConfigureAwait(false))
                throw Error(IdentityErrors.DirectoryHasMembers);
            foreach (var org in organizations) org.ResponsiblePositionId = null;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            // 分层删除避免自引用外键依赖数据库对 DELETE 顺序的猜测；全程在同一事务内。
            foreach (var depth in positions.Select(row => row.Depth).Distinct().OrderDescending())
            { db.RemoveRange(positions.Where(row => row.Depth == depth)); await db.SaveChangesAsync(ct).ConfigureAwait(false); }
            foreach (var depth in organizations.Select(row => row.Depth).Distinct().OrderDescending())
            { db.RemoveRange(organizations.Where(row => row.Depth == depth)); await db.SaveChangesAsync(ct).ConfigureAwait(false); }
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<IReadOnlyList<DirectoryPosition>> ListPositionsAsync(CancellationToken cancellationToken = default) => DirectoryRead<IReadOnlyList<DirectoryPosition>>(
        async (db, ct) => (await db.Set<PositionRow>().AsNoTracking().OrderBy(row => row.Path).ToArrayAsync(ct).ConfigureAwait(false)).Select(Position).ToArray(), cancellationToken);
    public ValueTask<DirectoryPosition> CreatePositionAsync(PositionInput request, CancellationToken cancellationToken = default) => SavePosition(null, request, cancellationToken);
    public ValueTask<DirectoryPosition> UpdatePositionAsync(Guid id, PositionInput request, CancellationToken cancellationToken = default) => SavePosition(id, request, cancellationToken);

    private ValueTask<DirectoryPosition> SavePosition(Guid? id, PositionInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request); var name = DirectoryName(request.Name);
        return DirectoryWrite(async (db, ct) =>
        {
            if (!await db.Set<OrganizationRow>().AnyAsync(row => row.Id == request.OrganizationId, ct).ConfigureAwait(false)) throw Error(PlatformErrors.NotFound);
            var parent = request.ParentId is { } parentId ? await db.Set<PositionRow>().SingleOrDefaultAsync(row => row.Id == parentId, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound) : null;
            var row = id is { } existing ? await db.Set<PositionRow>().SingleOrDefaultAsync(row => row.Id == existing, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound) : new PositionRow { Id = Guid.CreateVersion7() };
            // 岗位父链可跨同租户的组织。迁出仍被原组织引用的责任岗位前，须先显式解除该引用。
            if (id is not null && row.OrganizationId != request.OrganizationId && await db.Set<OrganizationRow>().AnyAsync(org => org.ResponsiblePositionId == row.Id, ct).ConfigureAwait(false)) throw Error(PlatformErrors.Conflict);
            if (parent is not null && id is not null && parent.Path.StartsWith(row.Path, StringComparison.Ordinal)) throw Error(IdentityErrors.DirectoryCycle);
            var children = id is null ? [] : await db.Set<PositionRow>().Where(other => other.Id != row.Id && other.Path.StartsWith(row.Path)).ToListAsync(ct).ConfigureAwait(false);
            if (request.IsDefault && await db.Set<PositionRow>().AnyAsync(other => other.OrganizationId == request.OrganizationId && other.IsDefault && other.Id != row.Id, ct).ConfigureAwait(false)) throw Error(PlatformErrors.Conflict);
            if (request.ResponsibleMembershipId is { } member) await DirectoryMemberRow(db, member, ct).ConfigureAwait(false);
            if (!request.IsResponsible && (request.ResponsibleMembershipId is not null || await db.Set<OrganizationRow>().AnyAsync(org => org.ResponsiblePositionId == row.Id, ct).ConfigureAwait(false))) throw Error(PlatformErrors.Conflict);
            var oldPath = row.Path; var path = PathFor(parent?.Path, row.Id); var depth = (parent?.Depth ?? 0) + 1; var shift = depth - row.Depth;
            foreach (var child in children) { child.Path = path + child.Path[oldPath.Length..]; child.Depth += shift; }
            row.Name = name; row.OrganizationId = request.OrganizationId; row.ParentId = parent?.Id; row.Path = path; row.Depth = depth;
            row.IsDefault = request.IsDefault; row.IsResponsible = request.IsResponsible; row.ResponsibleMembershipId = request.ResponsibleMembershipId;
            if (id is null) db.Add(row); return Position(row);
        }, cancellationToken);
    }

    public async ValueTask DeletePositionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await DirectoryWrite(async (db, ct) =>
        {
            var root = await db.Set<PositionRow>().SingleOrDefaultAsync(row => row.Id == id, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
            var rows = await db.Set<PositionRow>().Where(row => row.Path.StartsWith(root.Path)).ToListAsync(ct).ConfigureAwait(false); var ids = rows.Select(row => row.Id).ToArray();
            if (rows.Any(row => row.ResponsibleMembershipId is not null) || await db.Set<MemberRelationRow>().AnyAsync(row => row.PositionId != null && ids.Contains(row.PositionId.Value), ct).ConfigureAwait(false)) throw Error(IdentityErrors.DirectoryHasMembers);
            foreach (var org in await db.Set<OrganizationRow>().Where(row => row.ResponsiblePositionId != null && ids.Contains(row.ResponsiblePositionId.Value)).ToListAsync(ct).ConfigureAwait(false)) org.ResponsiblePositionId = null;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            foreach (var depth in rows.Select(row => row.Depth).Distinct().OrderDescending())
            { db.RemoveRange(rows.Where(row => row.Depth == depth)); await db.SaveChangesAsync(ct).ConfigureAwait(false); }
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }
}
