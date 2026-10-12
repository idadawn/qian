using Microsoft.EntityFrameworkCore;
using Kun.EntityFrameworkCore.Conventions;
using Npgsql;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Tenancy;

internal sealed class PostgresTenantDirectory(IDataService data) : ITenantDirectory, IDisposable
{
    private int _disposed;
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
    private void EnsureActive() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    public async ValueTask<TenantDescriptor?> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        EnsureActive(); ValidateCode(code);
        await using var session = await data.OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false);
        var row = await session.Context.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code, cancellationToken).ConfigureAwait(false);
        EnsureActive(); return row is null ? null : Describe(row);
    }
    internal async ValueTask<TenantDescriptor?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        EnsureActive();
        await using var session = await data.OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
        var row = await session.Context.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        EnsureActive(); return row is null ? null : Describe(row);
    }
    // 仅供已授权控制面/生命周期接线；不发布未经权限检查的 HTTP 写入入口。
    internal async Task<TenantDescriptor> CreateAsync(CreateTenant request, CancellationToken ct)
    {
        EnsureActive(); ArgumentNullException.ThrowIfNull(request); ValidateCode(request.Code); ValidateName(request.Name);
        if (request.Isolation != TenantIsolation.Row) throw Error(PlatformErrors.InputInvalid);
        await using var session = await data.OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
        var row = new TenantRow { Code = request.Code, Name = request.Name };
        session.Context.Tenants.Add(row);
        try { await session.SaveChangesAsync(ct).ConfigureAwait(false); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw Error(PlatformErrors.Conflict); }
        return Describe(row);
    }
    internal async Task<TenantDescriptor> UpdateAsync(Guid id, UpdateTenant request, CancellationToken ct)
    {
        EnsureActive(); ArgumentNullException.ThrowIfNull(request); ValidateName(request.Name);
        await using var session = await data.OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
        var row = await session.Context.Tenants.SingleOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
        if (request.ExpectedRevision != session.Context.Entry(row).Property<uint>(KunDataConventions.RowVersionPropertyName).CurrentValue) throw Error(PlatformErrors.PreconditionFailed);
        row.Name = request.Name;
        try { await session.SaveChangesAsync(ct).ConfigureAwait(false); }
        catch (DbUpdateConcurrencyException) { throw Error(PlatformErrors.PreconditionFailed); }
        return Describe(row);
    }
    private static TenantDescriptor Describe(TenantRow row) => new(row.Id, row.Code, row.Name,
        row.Isolation == "row" ? TenantIsolation.Row : throw new InvalidOperationException("未装配此隔离策略。"),
        row.Status switch { "pending" => TenantStatus.Pending, "trial" => TenantStatus.Trial, "active" => TenantStatus.Active,
            "grace" => TenantStatus.Grace, "suspended" => TenantStatus.Suspended, _ => throw new InvalidOperationException("未知租户状态。") });
    private static void ValidateCode(string code)
    { if (code is not { Length: > 0 and <= 64 } || code.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9'))) throw Error(PlatformErrors.InputInvalid); }
    private static void ValidateName(string name)
    { if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || name.Any(char.IsControl)) throw Error(PlatformErrors.InputInvalid); }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}
