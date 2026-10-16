using Microsoft.EntityFrameworkCore;
using Kun.EntityFrameworkCore.Conventions;
using Npgsql;
using Qian.Data;
using Kun.Core.MultiTenancy;
using Qian.Events;
using Qian.Runtime.Errors;

namespace Qian.Tenancy;

internal sealed class PostgresTenantDirectory(IDataService data, ITenantContext tenant) : ITenantDirectory, IDisposable
{
    private int _disposed;
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
    private void EnsureActive() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    public async ValueTask<TenantDescriptor?> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        EnsureActive(); ValidateCode(code);
        await using var session = await data.OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false);
        var result = await session.Context.Tenants.AsNoTracking().Where(x => x.Code == code)
            .Select(row => new { Row = row, Revision = EF.Property<uint>(row, KunDataConventions.RowVersionPropertyName) }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        EnsureActive(); return result is null ? null : Describe(result.Row) with { Revision = result.Revision };
    }
    public async ValueTask<TenantDescriptor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureActive();
        await using var session = await data.OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false);
        var result = await session.Context.Tenants.AsNoTracking().Where(x => x.Id == id)
            .Select(row => new { Row = row, Revision = EF.Property<uint>(row, KunDataConventions.RowVersionPropertyName) }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        EnsureActive(); return result is null ? null : Describe(result.Row) with { Revision = result.Revision };
    }
    // 仅供已授权控制面/生命周期接线；不发布未经权限检查的 HTTP 写入入口。
    internal async Task<TenantDescriptor> CreateAsync(CreateTenant request, CancellationToken ct)
    {
        EnsureActive(); ArgumentNullException.ThrowIfNull(request); ValidateCode(request.Code); ValidateName(request.Name);
        if (request.Isolation != TenantIsolation.Row) throw Error(PlatformErrors.InputInvalid);
        if (tenant.TenantId is not null) throw Error(PlatformErrors.AuthzDenied);
        var row = new TenantRow { Code = request.Code, Name = request.Name, Quotas = TenantQuotaRules.Serialize(request.Quotas) };
        using var scope = tenant.BeginScope(row.Id.ToString());
        await using var session = await data.OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        using var origin = EventEnvelopeScope.Begin(new EventOrigin("qian.tenancy", EventEnvelopeScope.Current?.Actor ?? EventActor.Service("qian.tenancy")));
        var created = new TenantChange(TenancyEvents.Created, row); row.Raise(created);
        session.Context.Tenants.Add(row);
        try { await TenantChange.Save(session, created, ct).ConfigureAwait(false); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw Error(PlatformErrors.Conflict); }
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return Describe(row) with { Revision = session.Context.Entry(row).Property<uint>(KunDataConventions.RowVersionPropertyName).CurrentValue };
    }
    internal async Task<TenantDescriptor> UpdateAsync(Guid id, UpdateTenant request, CancellationToken ct)
    {
        EnsureActive(); ArgumentNullException.ThrowIfNull(request); ValidateName(request.Name);
        await using var session = await data.OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
        var row = await session.Context.Tenants.SingleOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
        if (request.ExpectedRevision != session.Context.Entry(row).Property<uint>(KunDataConventions.RowVersionPropertyName).CurrentValue) throw Error(PlatformErrors.PreconditionFailed);
        row.Name = request.Name;
        if (request.Quotas is not null) row.Quotas = TenantQuotaRules.Serialize(request.Quotas);
        try { await session.SaveChangesAsync(ct).ConfigureAwait(false); }
        catch (DbUpdateConcurrencyException) { throw Error(PlatformErrors.PreconditionFailed); }
        return Describe(row) with { Revision = session.Context.Entry(row).Property<uint>(KunDataConventions.RowVersionPropertyName).CurrentValue };
    }
    internal static TenantDescriptor Describe(TenantRow row) => new(row.Id, row.Code, row.Name,
        row.Isolation == "row" ? TenantIsolation.Row : throw new InvalidOperationException("未装配此隔离策略。"),
        row.Status switch { "pending" => TenantStatus.Pending, "trial" => TenantStatus.Trial, "active" => TenantStatus.Active,
            "grace" => TenantStatus.Grace, "suspended" => TenantStatus.Suspended, _ => throw new InvalidOperationException("未知租户状态。") });
    internal static void ValidateCode(string code)
    {
        if (string.IsNullOrEmpty(code)) throw Invalid(PlatformErrors.Required, "#/code");
        if (code.Length > 64) throw Invalid(PlatformErrors.OutOfRange, "#/code");
        if (code.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9'))) throw Invalid(PlatformErrors.PatternMismatch, "#/code");
    }
    internal static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw Invalid(PlatformErrors.Required, "#/name");
        if (name.Length > 200) throw Invalid(PlatformErrors.OutOfRange, "#/name");
        if (name.Any(char.IsControl)) throw Invalid(PlatformErrors.PatternMismatch, "#/name");
    }
    private static QianErrorException Invalid(ErrorCode code, string pointer) => new(new QianError(PlatformErrors.InputInvalid) { Errors = [new QianFieldError(code, pointer)] });
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}
