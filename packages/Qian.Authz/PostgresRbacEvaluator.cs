using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kun.Authorization;
using Kun.Caching.Abstractions;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Authz.Abstractions;
using Qian.Data;

namespace Qian.Authz;

// 真正从角色/授权表求值，不是从 token roles/permissions 推导。主体的角色与授权读成快照，按 (租户, 主体, 租户授权修订) 缓存；
// 有效期、活动权限登记与 step-up / 终端约束仍在每次判定时求值。组织/岗位/组继承与 identity_revision 失效分属 S1-AZ-005 / S1-AZ-004。
internal sealed class PostgresRbacEvaluator(IDataService data, ITenantContext tenant, IPermissionRegistry registry, IAuthzExecutionConstraints? constraints,
    IKunCache? cache = null)
{
    // PRD 7.5：Redis，TTL 10 分钟；授予、撤销与策略安装递增修订号，旧快照不再被读取，TTL 只负责回收。
    internal static readonly TimeSpan SnapshotTtl = TimeSpan.FromMinutes(10);
    private const string KeyPrefix = "qian.authz/rbac/v1:";

    internal async ValueTask<AuthorizationExplanation> EvaluateAsync(PermissionRequest request, CancellationToken ct)
    {
        var invalid = Validate(request.Subject);
        if (invalid is not null) return Deny(invalid, request.Permission);
        if (!PermissionCode.IsValid(request.Permission)) return Deny("invalid_permission", request.Permission);
        var owner = tenant.TenantId!;
        var snapshot = await LoadAsync(owner, request.Subject, ct).ConfigureAwait(false);
        if (tenant.TenantId != owner) return Deny("tenant_context_changed", request.Permission);
        return await EvaluateAsync(snapshot, owner, request, ct).ConfigureAwait(false);
    }

    internal async ValueTask<IReadOnlyList<string>> ListPermissionsAsync(AuthorizationSubject subject, CancellationToken ct)
    {
        if (Validate(subject) is not null) return [];
        var owner = tenant.TenantId!;
        var snapshot = await LoadAsync(owner, subject, ct).ConfigureAwait(false);
        var candidates = snapshot.Installs.SelectMany(install => registry.List(install.PluginId, install.Version)).Select(permission => permission.Code)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var allowed = new List<string>();
        foreach (var permission in candidates)
            if ((await EvaluateAsync(snapshot, owner, new PermissionRequest(subject, permission), ct).ConfigureAwait(false)).Decision.Allow) allowed.Add(permission);
        return tenant.TenantId == owner ? allowed.AsReadOnly() : [];
    }

    private async ValueTask<AuthorizationExplanation> EvaluateAsync(RbacSnapshot snapshot, string owner, PermissionRequest request, CancellationToken ct)
    {
        var plugin = request.Permission.Split(':')[0];
        var install = snapshot.Installs.SingleOrDefault(row => row.PluginId == plugin);
        if (install is null) return Deny("plugin_not_installed", request.Permission);
        var definition = registry.Find(request.Permission, install.Version);
        if (definition is null) return Deny("permission_not_registered", request.Permission);
        var now = DateTimeOffset.UtcNow;
        var roles = snapshot.Roles.Where(role => (role.ValidFrom is null || role.ValidFrom <= now) && (role.ValidTo is null || role.ValidTo > now)).ToArray();
        var roleIds = roles.Select(role => role.Id).ToHashSet();
        var grants = snapshot.Grants.Where(grant => roleIds.Contains(grant.RoleId) && grant.PluginId == plugin && grant.Version == install.Version &&
            grant.Permission == request.Permission).ToArray();
        var roleCodes = roles.Select(role => role.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (grants.Length == 0) return new AuthorizationExplanation(new PermissionDecision(false, "no_matching_grant", []), roleCodes, [], [request.Permission]);
        var matchedRoles = roles.Where(role => grants.Any(grant => grant.RoleId == role.Id)).Select(role => role.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var matchedGrants = grants.Select(grant => "grant:" + grant.Id.ToString("D")).Order(StringComparer.Ordinal).ToArray();
        var policies = matchedRoles.Select(role => "role:" + role).Concat(matchedGrants).ToArray();
        if (definition.RequiresStepUp || definition.Terminals.Count != 0)
        {
            if (constraints is null) return new AuthorizationExplanation(new PermissionDecision(false,
                definition.RequiresStepUp ? "step_up_required" : "terminal_context_required", Array.AsReadOnly(policies)), matchedRoles, matchedGrants, []);
            var constrained = await constraints.CheckAsync(request.Subject, definition, ct).ConfigureAwait(false);
            if (tenant.TenantId != owner || !ReferenceEquals(registry.Find(request.Permission, install.Version), definition)) return Deny("evaluation_context_changed", request.Permission);
            policies = policies.Concat(constrained.MatchedPolicies).Distinct(StringComparer.Ordinal).ToArray();
            if (!constrained.Allow) return new AuthorizationExplanation(new PermissionDecision(false, constrained.Reason, Array.AsReadOnly(policies)), matchedRoles, matchedGrants, []);
        }
        if (tenant.TenantId != owner || !ReferenceEquals(registry.Find(request.Permission, install.Version), definition)) return Deny("permission_registration_changed", request.Permission);
        return new AuthorizationExplanation(new PermissionDecision(true, "role_grant", Array.AsReadOnly(policies)), matchedRoles, matchedGrants, []);
    }

    // 先读修订号再读数据：读到旧修订配新数据只会让旧键多一份更新的快照，读到新修订必然对应提交后的数据。
    private async ValueTask<RbacSnapshot> LoadAsync(string owner, AuthorizationSubject subject, CancellationToken ct)
    {
        await using var session = await data.OpenAsync<AuthzDbContext>(ct).ConfigureAwait(false);
        var db = session.Context;
        var revision = await db.Revisions.AsNoTracking().Where(row => row.TenantId == owner).Select(row => (long?)row.Revision).SingleOrDefaultAsync(ct).ConfigureAwait(false) ?? 0;
        var key = cache is null ? null : Key(owner, subject, revision);
        if (key is not null && await cache!.GetBytesAsync(key, ct).ConfigureAwait(false) is { Length: > 0 } bytes && Read(bytes) is { } cached &&
            cached.Tenant == owner && cached.Kind == subject.Kind && cached.Subject == subject.Id && cached.Revision == revision)
            return cached;
        var installs = await db.PolicyInstalls.AsNoTracking().Select(row => new SnapshotInstall(row.PluginId, row.Version)).ToArrayAsync(ct).ConfigureAwait(false);
        var roles = await (from assignment in db.RoleAssignments.AsNoTracking()
                           join role in db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
                           where assignment.SubjectType == subject.Kind && assignment.SubjectId == subject.Id
                           select new SnapshotRole(role.Id, role.Code, assignment.ValidFrom, assignment.ValidTo)).ToArrayAsync(ct).ConfigureAwait(false);
        var roleIds = roles.Select(role => role.Id).Distinct().ToArray();
        var grants = await (from grant in db.Grants.AsNoTracking()
                            join active in db.PolicyInstalls.AsNoTracking() on new { grant.PluginId, Version = grant.PluginVersion } equals new { active.PluginId, active.Version }
                            join permission in db.Permissions.AsNoTracking() on new { grant.PluginId, Version = grant.PluginVersion, Code = grant.PermissionCode }
                                equals new { permission.PluginId, permission.Version, permission.Code }
                            where roleIds.Contains(grant.RoleId) && grant.ResourceRef == null
                            select new SnapshotGrant(grant.Id, grant.RoleId, grant.PluginId, grant.PluginVersion, grant.PermissionCode)).ToArrayAsync(ct).ConfigureAwait(false);
        var snapshot = new RbacSnapshot(owner, subject.Kind, subject.Id, revision, installs, roles, grants);
        if (key is not null && tenant.TenantId == owner)
            await cache!.SetBytesAsync(key, JsonSerializer.SerializeToUtf8Bytes(snapshot, AuthzJson.Default.RbacSnapshot), SnapshotTtl, ct).ConfigureAwait(false);
        return snapshot;
    }

    // 键用 JSON 数组编码各段，主体或租户标识里的分隔符不会拼出另一个主体的键；读取时再比对快照内的身份与修订。
    internal static string Key(string owner, AuthorizationSubject subject, long revision) => KeyPrefix +
        JsonSerializer.Serialize(new[] { owner, subject.Kind, subject.Id, revision.ToString(CultureInfo.InvariantCulture) }, AuthzJson.Default.StringArray);

    private static RbacSnapshot? Read(byte[] bytes)
    {
        try { return JsonSerializer.Deserialize(bytes, AuthzJson.Default.RbacSnapshot); }
        catch (JsonException) { return null; }
    }

    private string? Validate(AuthorizationSubject subject)
    {
        if (string.IsNullOrWhiteSpace(tenant.TenantId)) return "tenant_context_required";
        if (string.IsNullOrWhiteSpace(subject.Id) || subject.Id.Length > 200 || subject.Id.Any(char.IsControl) || subject.Kind is not ("user" or "service")) return "invalid_subject";
        // 平台主体（无租户）不进入租户 RBAC；平台超管只能经显式旁路 ISuperadminBypass 放行并审计。
        if (subject.TenantId is null) return "platform_subject_requires_bypass";
        if (subject.TenantId != tenant.TenantId) return "subject_tenant_mismatch";
        return null;
    }
    private static AuthorizationExplanation Deny(string reason, string permission) => new(new PermissionDecision(false, reason, []), [], [],
        PermissionCode.IsValid(permission) ? [permission] : []);
}

internal sealed record RbacSnapshot(string Tenant, string Kind, string Subject, long Revision,
    IReadOnlyList<SnapshotInstall> Installs, IReadOnlyList<SnapshotRole> Roles, IReadOnlyList<SnapshotGrant> Grants);
internal sealed record SnapshotInstall(string PluginId, string Version);
internal sealed record SnapshotRole(Guid Id, string Code, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo);
internal sealed record SnapshotGrant(Guid Id, Guid RoleId, string PluginId, string Version, string Permission);

[JsonSerializable(typeof(RbacSnapshot))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class AuthzJson : JsonSerializerContext;
