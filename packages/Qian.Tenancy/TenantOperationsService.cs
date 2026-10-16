using Kun.Core.MultiTenancy;
using System.Data;
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
        => (await QueryAsync(skip, take, cancellationToken: cancellationToken).ConfigureAwait(false)).Items;
    public async ValueTask<(IReadOnlyList<TenantDescriptor> Items, int Total)> QueryAsync(int skip, int take, TenantStatus? status = null, string? keyword = null, CancellationToken cancellationToken = default)
    {
        Check();
        if (skip < 0 || take is < 1 or > 100) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Pointer = "#/status" });
        if (keyword?.Length > 200) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Pointer = "#/keyword" });
        await using var session = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        var query = session.Context.Tenants.AsNoTracking();
        if (status.HasValue) { var value = status.Value.ToString().ToLowerInvariant(); query = query.Where(row => row.Status == value); }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var escaped = keyword.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
            var pattern = "%" + escaped + "%";
            query = query.Where(row => EF.Functions.ILike(row.Code, pattern, "\\") || EF.Functions.ILike(row.Name, pattern, "\\"));
        }
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query.OrderBy(row => row.Code).ThenBy(row => row.Id).Skip(skip).Take(take)
            .Select(row => new { Row = row, Revision = EF.Property<uint>(row, KunDataConventions.RowVersionPropertyName) }).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return (Array.AsReadOnly(rows.Select(row => PostgresTenantDirectory.Describe(row.Row) with { Revision = row.Revision }).ToArray()), total);
    }
    public async ValueTask<TenantDescriptor> UpdateAsync(Guid id, UpdateTenant request, CancellationToken cancellationToken = default)
    {
        Check();
        try { return await directory.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false); }
        catch (QianErrorException error) when (error.Error.Code == PlatformErrors.PreconditionFailed)
        { throw new QianErrorException(new QianError(PlatformErrors.Conflict)); }
    }
}
