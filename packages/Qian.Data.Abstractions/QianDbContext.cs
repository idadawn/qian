using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore;
using Kun.EntityFrameworkCore.Interceptors;
using Kun.MultiTenancy.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Qian.Data;

/// <summary>带安全装配门禁的数据上下文。租户过滤与 Outbox 继承 Kun 的行为。</summary>
public abstract class QianDbContext : KunDbContext, IKunMultiTenantDbContext
{
    /// <summary>检查双拦截器与租户模型缓存键，缺少任一项即拒绝构造。</summary>
    protected QianDbContext(DbContextOptions options, ITenantContext tenantContext) : base(options, tenantContext)
    {
        ValidateOptions(options);
    }

    /// <summary>配置只通过 AddQianData 的 options 回调传入，防止构造后移除安全装配。</summary>
    protected sealed override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        ValidateOptions(optionsBuilder.Options);
    }

    private static void ValidateOptions(DbContextOptions options)
    {
        var core = options.FindExtension<CoreOptionsExtension>();
        if (core?.Interceptors?.OfType<TenantRlsConnectionInterceptor>().Any() != true ||
            core.Interceptors.OfType<TenantRlsTransactionInterceptor>().Any() != true)
            throw new QianDataMisconfiguredException("必须同时装配 RLS 连接版与事务版拦截器。");
        if (core.ReplacedServices?.TryGetValue((typeof(IModelCacheKeyFactory), null), out var factory) != true ||
            factory != typeof(KunMultiTenantModelCacheKeyFactory))
            throw new QianDataMisconfiguredException("必须使用 KunMultiTenantModelCacheKeyFactory。");
        if (core.InternalServiceProvider is not null || core.Model is not null)
            throw new QianDataMisconfiguredException("不允许外部 EF 服务容器或预编译模型绕过租户模型配置。");
    }
}
