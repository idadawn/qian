using Kun.EntityFrameworkCore.DependencyInjection;
using Kun.MultiTenancy.EntityFrameworkCore;
using Kun.MultiTenancy.EntityFrameworkCore.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Qian.Runtime.Errors;

namespace Qian.Data;

/// <summary>数据 Provider 的宿主安全装配；组件安装入口由 qian.data 的后续任务交付。</summary>
public static class QianDataExtensions
{
    /// <summary>装配租户隔离并在 Host.StartAsync 时验证上下文。</summary>
    public static IServiceCollection AddQianData<TContext>(this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configure)
        where TContext : QianDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddLogging();
        services.TryAddSingleton(ErrorCatalog.CreateDefault());
        services.AddKunTenantRls<TContext>();
        services.AddKunDataConventions<TContext>();
        services.AddKunMultiTenancyEntityFrameworkCore();
        services.ConfigureDbContext<TContext>((sp, options) => options.UseQianDataModel()
            .AddInterceptors(sp.GetRequiredService<KunTenantSaveChangesInterceptor>()));
        services.AddDbContext<TContext>(configure);
        services.AddHostedService<DataStartupCheck<TContext>>();
        return services;
    }

    private sealed class DataStartupCheck<TContext>(IServiceScopeFactory scopes, ErrorCatalog catalog) : IHostedService, IDisposable
        where TContext : QianDbContext
    {
        private IDisposable? _registration;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _registration ??= catalog.Register(DataErrors.Misconfigured);
            using var scope = scopes.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            if (context.GetService<IModelCacheKeyFactory>() is not KunMultiTenantModelCacheKeyFactory)
                throw new QianDataMisconfiguredException("生效的 EF 模型缓存键被替换。");
            var model = context.Model;
            var schema = context.GetService<IDbContextOptions>().Extensions
                .OfType<RelationalOptionsExtension>().Single().MigrationsHistoryTableSchema;
            if (model.GetDefaultSchema() != schema)
                throw new QianDataMisconfiguredException("生效模型的 schema 与上下文迁移 schema 不一致。");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _registration?.Dispose();
            _registration = null;
        }
    }
}
