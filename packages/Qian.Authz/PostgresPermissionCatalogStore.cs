using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Authz;

// 目录只存声明；这里既不授予主体角色，也不进行权限 Check。
internal sealed class PostgresPermissionCatalogStore(IDataService data)
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    internal async Task PutAsync(PermissionCatalog catalog, CancellationToken ct)
    {
        var normalized = Normalize(catalog);
        var snapshot = JsonSerializer.Serialize(normalized, Json);
        await using var session = await data.OpenAsync<AuthzDbContext>(ct).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var identity = JsonSerializer.Serialize(new[] { normalized.PluginId, normalized.Version });
        await session.Context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({identity}, 0))", ct).ConfigureAwait(false);
        var existing = await session.Context.Catalogs.AsNoTracking().SingleOrDefaultAsync(row => row.PluginId == normalized.PluginId && row.Version == normalized.Version, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(existing.Snapshot), JsonSerializer.Deserialize<JsonElement>(snapshot)))
                throw new QianErrorException(new QianError(PlatformErrors.Conflict) { Pointer = "metadata#/version" });
            return;
        }
        session.Context.Catalogs.Add(new PermissionCatalogRow { PluginId = normalized.PluginId, Version = normalized.Version, Snapshot = snapshot });
        session.Context.Permissions.AddRange(normalized.Permissions.Select(permission => new PermissionRow
        {
            PluginId = normalized.PluginId, Version = normalized.Version, Code = permission.Code, Resource = permission.Resource, Action = permission.Action,
            Risk = permission.Risk.ToString().ToLowerInvariant(), StepUp = permission.RequiresStepUp, Terminals = JsonSerializer.Serialize(permission.Terminals)
        }));
        session.Context.ResourceTypes.AddRange(normalized.Permissions.Select(permission => permission.Resource).Distinct(StringComparer.Ordinal).Select(resource => new ResourceTypeRow
        { PluginId = normalized.PluginId, Version = normalized.Version, Name = resource, Code = normalized.PluginId + ":" + resource }));
        session.Context.PolicyPacks.AddRange(normalized.Roles.Select(role => new PolicyPackRow
        { PluginId = normalized.PluginId, Version = normalized.Version, Role = role.Role, Patterns = JsonSerializer.Serialize(role.Patterns), Grants = JsonSerializer.Serialize(role.Grants) }));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    internal async Task<PermissionCatalog?> GetAsync(string pluginId, string version, CancellationToken ct)
    {
        await using var session = await data.OpenAsync<AuthzDbContext>(ct).ConfigureAwait(false);
        var row = await session.Context.Catalogs.AsNoTracking().SingleOrDefaultAsync(row => row.PluginId == pluginId && row.Version == version, ct).ConfigureAwait(false);
        return row is null ? null : JsonSerializer.Deserialize<PermissionCatalog>(row.Snapshot, Json);
    }

    private static PermissionCatalog Normalize(PermissionCatalog catalog)
    {
        if (catalog.PluginId.Length > 128 || catalog.Version.Length > 200 || catalog.Permissions.Any(permission => permission.PluginId != catalog.PluginId ||
            permission.Code.Length > 512 || permission.Resource.Length > 128 || permission.Action.Length > 128) || catalog.Roles.Any(role => role.Role.Length > 128))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var codes = catalog.Permissions.Select(permission => permission.Code).ToHashSet(StringComparer.Ordinal);
        if (codes.Count != catalog.Permissions.Count || catalog.Roles.Select(role => role.Role).Distinct(StringComparer.Ordinal).Count() != catalog.Roles.Count ||
            catalog.Roles.Any(role => role.Grants.Any(grant => !codes.Contains(grant))))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        return new PermissionCatalog(catalog.PluginId, catalog.Version,
            catalog.Permissions.OrderBy(permission => permission.Code, StringComparer.Ordinal).Select(permission => new PermissionDefinition(permission.PluginId,
                permission.Resource, permission.Action, permission.Risk, permission.RequiresStepUp, permission.Terminals.Order(StringComparer.Ordinal).ToArray())).ToArray(),
            catalog.Roles.OrderBy(role => role.Role, StringComparer.Ordinal).Select(role => new RoleTemplate(role.Role,
                role.Patterns.Order(StringComparer.Ordinal).ToArray(), role.Grants.Order(StringComparer.Ordinal).ToArray())).ToArray());
    }
}
