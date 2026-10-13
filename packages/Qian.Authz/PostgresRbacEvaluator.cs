using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Authz.Abstractions;
using Qian.Data;

namespace Qian.Authz;

// 真正从角色/授权表求值的基础，不是从 token roles/permissions 推导；完整策略层仍需主体目录、审计等接入。
internal sealed class PostgresRbacEvaluator(IDataService data, ITenantContext tenant, IPermissionRegistry registry, IAuthzExecutionConstraints? constraints)
{
    internal async ValueTask<AuthorizationExplanation> EvaluateAsync(PermissionRequest request, CancellationToken ct)
    {
        var invalid = Validate(request.Subject);
        if (invalid is not null) return Deny(invalid, request.Permission);
        if (!PermissionCode.IsValid(request.Permission)) return Deny("invalid_permission", request.Permission);
        var owner = tenant.TenantId!;
        var parts = request.Permission.Split(':');
        await using var session = await data.OpenAsync<AuthzDbContext>(ct).ConfigureAwait(false);
        var db = session.Context;
        var install = await db.PolicyInstalls.AsNoTracking().SingleOrDefaultAsync(row => row.PluginId == parts[0], ct).ConfigureAwait(false);
        if (install is null) return Deny("plugin_not_installed", request.Permission);
        var definition = registry.Find(request.Permission, install.Version);
        if (definition is null) return Deny("permission_not_registered", request.Permission);
        var now = DateTimeOffset.UtcNow;
        var roles = await (from assignment in db.RoleAssignments.AsNoTracking()
                           join role in db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
                           where assignment.SubjectType == request.Subject.Kind && assignment.SubjectId == request.Subject.Id &&
                            (assignment.ValidFrom == null || assignment.ValidFrom <= now) && (assignment.ValidTo == null || assignment.ValidTo > now)
                           select new { role.Id, role.Code }).Distinct().ToArrayAsync(ct).ConfigureAwait(false);
        var roleIds = roles.Select(role => role.Id).ToArray();
        var grants = await (from grant in db.Grants.AsNoTracking()
                            join permission in db.Permissions.AsNoTracking() on new { grant.PluginId, Version = grant.PluginVersion, Code = grant.PermissionCode }
                                equals new { permission.PluginId, permission.Version, permission.Code }
                            where roleIds.Contains(grant.RoleId) && grant.PluginId == parts[0] && grant.PluginVersion == install.Version &&
                                grant.PermissionCode == request.Permission && grant.ResourceRef == null
                            select new { grant.Id, grant.RoleId, grant.Source }).ToArrayAsync(ct).ConfigureAwait(false);
        if (tenant.TenantId != owner) return Deny("tenant_context_changed", request.Permission);
        if (!ReferenceEquals(registry.Find(request.Permission, install.Version), definition)) return Deny("permission_registration_changed", request.Permission);
        var roleCodes = roles.Select(role => role.Code).Order(StringComparer.Ordinal).ToArray();
        if (grants.Length == 0) return new AuthorizationExplanation(new PermissionDecision(false, "no_matching_grant", []), roleCodes, [], [request.Permission]);
        var matchedRoles = roles.Where(role => grants.Any(grant => grant.RoleId == role.Id)).Select(role => role.Code).Order(StringComparer.Ordinal).ToArray();
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
        return new AuthorizationExplanation(new PermissionDecision(true, "role_grant", Array.AsReadOnly(policies)), matchedRoles, matchedGrants, []);
    }

    internal async ValueTask<IReadOnlyList<string>> ListPermissionsAsync(AuthorizationSubject subject, CancellationToken ct)
    {
        if (Validate(subject) is not null) return [];
        var owner = tenant.TenantId!;
        await using var session = await data.OpenAsync<AuthzDbContext>(ct).ConfigureAwait(false);
        var installs = await session.Context.PolicyInstalls.AsNoTracking().ToArrayAsync(ct).ConfigureAwait(false);
        var candidates = installs.SelectMany(install => registry.List(install.PluginId, install.Version)).Select(permission => permission.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var allowed = new List<string>();
        foreach (var permission in candidates)
            if ((await EvaluateAsync(new PermissionRequest(subject, permission), ct).ConfigureAwait(false)).Decision.Allow) allowed.Add(permission);
        return tenant.TenantId == owner ? allowed.AsReadOnly() : [];
    }

    private string? Validate(AuthorizationSubject subject)
    {
        if (string.IsNullOrWhiteSpace(tenant.TenantId)) return "tenant_context_required";
        if (string.IsNullOrWhiteSpace(subject.Id) || subject.Id.Length > 200 || subject.Id.Any(char.IsControl) || subject.Kind is not ("user" or "service")) return "invalid_subject";
        if (subject.TenantId != tenant.TenantId) return "subject_tenant_mismatch";
        return null;
    }
    private static AuthorizationExplanation Deny(string reason, string permission) => new(new PermissionDecision(false, reason, []), [], [],
        PermissionCode.IsValid(permission) ? [permission] : []);
}
