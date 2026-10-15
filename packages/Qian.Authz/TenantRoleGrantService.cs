using Qian.Authz.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Authz;

internal sealed class TenantRoleGrantService(PostgresTenantRoleStore store) : ITenantRoleGrant
{
    public async ValueTask AssignAsync(string roleCode, string subjectType, string subjectId, string actor, CancellationToken cancellationToken = default)
    {
        if (roleCode is not ("tenant.owner" or "tenant.admin" or "tenant.developer" or "tenant.user"))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        await store.AssignAsync(roleCode, subjectType, subjectId, actor, cancellationToken).ConfigureAwait(false);
    }
}
