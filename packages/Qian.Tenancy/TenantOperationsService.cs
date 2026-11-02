using Kun.Core.MultiTenancy;
using System.Data;
using System.Text.Json;
using Kun.EntityFrameworkCore.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Tenancy;

internal sealed class TenantOperationsService(IPluginContext context, PostgresTenantDirectory directory, TenantAdministrationService administration) : ITenantOperations
{
    private readonly ITenantContext _tenant = context.Services.GetRequiredService<ITenantContext>();
    private void Check()
    {
        if (_tenant.TenantId is not null) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
    }
    public async ValueTask<IReadOnlyList<TenantDescriptor>> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
        => (await QueryAsync(skip, take, cancellationToken: cancellationToken).ConfigureAwait(false)).Items;
    public async ValueTask<(IReadOnlyList<TenantDescriptor> Items, int Total)> QueryAsync(int skip, int take, TenantStatus? status = null, string? keyword = null, CancellationToken cancellationToken = default)
    {
        Check(); Validate(skip, take, status, keyword);
        await using var session = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        var query = WithStatus(WithKeyword(session.Context.Tenants.AsNoTracking(), keyword), status);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await Page(query, skip, take).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return (Array.AsReadOnly(rows.Select(row => PostgresTenantDirectory.Describe(row.Row) with { Revision = row.Revision }).ToArray()), total);
    }
    // 列表页的计数、当前页与总数在同一个可重复读快照里读；用量与管理员名逐行经身份侧端口取，单页至多 100 行。
    public async ValueTask<TenantOverviewPage> OverviewAsync(int skip, int take, TenantStatus? status = null, string? keyword = null, CancellationToken cancellationToken = default)
    {
        Check(); Validate(skip, take, status, keyword);
        PagedRow[] rows; int total; Dictionary<string, int> counts;
        await using (var session = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false))
        {
            await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
            var matched = WithKeyword(session.Context.Tenants.AsNoTracking(), keyword);
            counts = await matched.GroupBy(row => row.Status).Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Key, item => item.Count, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
            var query = WithStatus(matched, status);
            total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
            rows = await Page(query, skip, take).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        var items = new List<TenantOverview>(rows.Length);
        foreach (var row in rows)
        {
            var tenant = PostgresTenantDirectory.Describe(row.Row);
            items.Add(new TenantOverview(tenant.Id, tenant.Code, tenant.Name, tenant.Isolation, tenant.Status)
            {
                Revision = row.Revision, CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(row.Row.CreatedAt, DateTimeKind.Utc)), ValidUntil = row.Row.ValidUntil,
                UsersLimit = JsonSerializer.Deserialize<TenantQuotas>(row.Row.Quotas, JsonSerializerOptions.Web)?.Users,
                UsersUsed = await UsersAsync(tenant.Id, cancellationToken).ConfigureAwait(false),
                AdministratorName = await administration.AdministratorNameAsync(tenant.Id, cancellationToken).ConfigureAwait(false),
            });
        }
        int Count(string value) => counts.GetValueOrDefault(value);
        return new TenantOverviewPage(items.AsReadOnly(), total,
            new TenantStatusCounts(counts.Values.Sum(), Count("pending"), Count("trial"), Count("active"), Count("grace"), Count("suspended")));
    }
    private static void Validate(int skip, int take, TenantStatus? status, string? keyword)
    {
        if (skip < 0 || take is < 1 or > 100) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Pointer = "#/status" });
        if (keyword?.Length > 200) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Pointer = "#/keyword" });
    }
    private static IQueryable<TenantRow> WithKeyword(IQueryable<TenantRow> query, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return query;
        var escaped = keyword.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
        var pattern = "%" + escaped + "%";
        return query.Where(row => EF.Functions.ILike(row.Code, pattern, "\\") || EF.Functions.ILike(row.Name, pattern, "\\"));
    }
    private static IQueryable<TenantRow> WithStatus(IQueryable<TenantRow> query, TenantStatus? status)
    {
        if (!status.HasValue) return query;
        var value = status.Value.ToString().ToLowerInvariant();
        return query.Where(row => row.Status == value);
    }
    private static IQueryable<PagedRow> Page(IQueryable<TenantRow> query, int skip, int take) => query.OrderBy(row => row.Code).ThenBy(row => row.Id).Skip(skip).Take(take)
        .Select(row => new PagedRow(row, EF.Property<uint>(row, KunDataConventions.RowVersionPropertyName)));
    // 用户数由身份侧在目标租户上下文里统计；端口缺席时返回 null，页面显示「—」而不是 0。
    private async Task<int?> UsersAsync(Guid id, CancellationToken ct)
    {
        using var tenant = _tenant.BeginScope(id.ToString());
        var counter = context.Scoped(new ScopeKey("tenant", id.ToString())).TryInject<ITenantUserCounter>(TenantSettings.UserCounterKey);
        return counter is null ? null : await counter.CountUsersAsync(ct).ConfigureAwait(false);
    }
    private sealed record PagedRow(TenantRow Row, uint Revision);
    public async ValueTask<TenantDescriptor> UpdateAsync(Guid id, UpdateTenant request, CancellationToken cancellationToken = default)
    {
        Check();
        try { return await directory.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false); }
        catch (QianErrorException error) when (error.Error.Code == PlatformErrors.PreconditionFailed)
        { throw new QianErrorException(new QianError(PlatformErrors.Conflict)); }
    }
}
