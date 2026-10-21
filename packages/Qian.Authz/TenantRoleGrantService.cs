using Qian.Authz.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Authz;

internal sealed class TenantRoleGrantService(PostgresTenantRoleStore store) : ITenantRoleGrant
{
    public async ValueTask AssignAsync(string roleCode, string subjectType, string subjectId, string actor, CancellationToken cancellationToken = default)
    {
        Builtin(roleCode);
        await store.AssignAsync(roleCode, subjectType, subjectId, actor, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask BindAsync(RoleBinding binding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding); Builtin(binding.RoleCode);
        await store.AssignAsync(binding.RoleCode, binding.SubjectType, binding.SubjectId, binding.Actor, cancellationToken,
            binding.ValidFrom, binding.ValidTo, binding.Scope).ConfigureAwait(false);
    }

    private static void Builtin(string roleCode)
    {
        if (roleCode is not ("tenant.owner" or "tenant.admin" or "tenant.developer" or "tenant.user"))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
    }
}
