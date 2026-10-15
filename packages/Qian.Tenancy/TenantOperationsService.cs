using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Tenancy;

internal sealed class TenantOperationsService(IPluginContext context, PostgresTenantDirectory directory) : ITenantOperations
{
    private void Check()
    {
        if (context.Services.GetRequiredService<ITenantContext>().TenantId is not null) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
    }
    public async ValueTask<IReadOnlyList<TenantDescriptor>> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        Check();
        if (skip < 0 || take is < 1 or > 100) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        await using var session = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false);
        var rows = await session.Context.Tenants.AsNoTracking().OrderBy(row => row.Code).Skip(skip).Take(take)
            .Select(row => new { Row = row, Revision = EF.Property<uint>(row, KunDataConventions.RowVersionPropertyName) }).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return Array.AsReadOnly(rows.Select(row => PostgresTenantDirectory.Describe(row.Row) with { Revision = row.Revision }).ToArray());
    }
    public async ValueTask<TenantDescriptor> UpdateAsync(Guid id, UpdateTenant request, CancellationToken cancellationToken = default)
    {
        Check();
        try { return await directory.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false); }
        catch (QianErrorException error) when (error.Error.Code == PlatformErrors.PreconditionFailed)
        { throw new QianErrorException(new QianError(PlatformErrors.Conflict)); }
    }
}
