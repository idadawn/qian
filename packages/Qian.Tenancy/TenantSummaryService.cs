using Kun.Core.MultiTenancy;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Tenancy;

// 本租户摘要（S1-TEN-021）：租户只取自当前租户上下文（宿主按已验证令牌进入），没有就是未认证；字段口径与运营列表的一行相同。
internal sealed class TenantSummaryService(IPluginContext context, TenantAdministrationService administration) : ITenantSummaries
{
    private readonly ITenantContext _tenant = context.Services.GetRequiredService<ITenantContext>();
    public async ValueTask<TenantSummary> CurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParseExact(_tenant.TenantId, "D", out var id) || id == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        TenantRow row;
        await using (var session = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false))
            row = await session.Context.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken).ConfigureAwait(false)
                ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        var tenant = PostgresTenantDirectory.Describe(row);
        // 用户数由身份侧在本租户上下文里统计；端口缺席时为 null，页面显示「—」而不是 0。
        var counter = context.Scoped(new ScopeKey("tenant", id.ToString())).TryInject<ITenantUserCounter>(TenantSettings.UserCounterKey);
        return new TenantSummary(tenant.Id, tenant.Code, tenant.Name, tenant.Status)
        {
            ValidUntil = row.ValidUntil,
            UsersLimit = JsonSerializer.Deserialize<TenantQuotas>(row.Quotas, JsonSerializerOptions.Web)?.Users,
            UsersUsed = counter is null ? null : await counter.CountUsersAsync(cancellationToken).ConfigureAwait(false),
            AdministratorName = await administration.AdministratorNameAsync(id, cancellationToken).ConfigureAwait(false),
        };
    }
}
