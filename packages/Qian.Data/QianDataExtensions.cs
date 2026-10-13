using Kun.EntityFrameworkCore.DependencyInjection;
using Kun.EntityFrameworkCore;
using Kun.EventBus;
using Kun.EventBus.Outbox;
using Kun.MultiTenancy.EntityFrameworkCore;
using Kun.MultiTenancy.EntityFrameworkCore.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        => AddCore<TContext>(services, configure, null);

    /// <summary>把业务上下文绑定到连接引用；每次创建上下文时由宿主读取凭据，值不进入组件清单。</summary>
    public static IServiceCollection AddQianData<TContext>(this IServiceCollection services, string connectionReference,
        Func<IServiceProvider, string> resolveConnection, Action<IServiceProvider, DbContextOptionsBuilder> configure)
        where TContext : QianDbContext
    {
        ArgumentNullException.ThrowIfNull(resolveConnection);
        ArgumentNullException.ThrowIfNull(configure);
        DataConfiguration.ValidateReference(connectionReference);
        return AddCore<TContext>(services, (sp, options) =>
        {
            var connection = resolveConnection(sp);
            if (string.IsNullOrWhiteSpace(connection)) throw new QianDataMisconfiguredException("连接引用未解析出数据库凭据。");
            options.UseNpgsql(connection);
            configure(sp, options);
            var relational = options.Options.Extensions.OfType<RelationalOptionsExtension>().Single();
            if (relational.ConnectionString != connection || relational.Connection is not null)
                throw new QianDataMisconfiguredException("上下文不能覆盖已绑定引用的数据库连接。");
        }, connectionReference);
    }

    private static IServiceCollection AddCore<TContext>(IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configure, string? connectionReference)
        where TContext : QianDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddLogging();
        // 独立库宿主可直接注入数据服务；插件模式使用自己的实例管理挂载生命周期。
        var existing = services.Where(service => service.ServiceType == typeof(DataConnectionBinding))
            .Select(service => (DataConnectionBinding)service.ImplementationInstance!).FirstOrDefault(binding => binding.ContextType == typeof(TContext));
        if (existing is not null && existing.Reference != connectionReference)
            throw new QianDataMisconfiguredException("同一上下文不能登记不同的连接引用。");
        if (existing is null) services.AddSingleton(new DataConnectionBinding(typeof(TContext), connectionReference));
        services.TryAddSingleton<IDataService>(sp => new QianDataService(sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetServices<DataConnectionBinding>().Select(binding => binding.ContextType).ToHashSet(), Qian.Runtime.ScopeKey.Global));
        services.TryAddSingleton<IDataContextCatalog>(sp => new DataContextCatalog(
            sp.GetServices<DataConnectionBinding>().Select(binding => binding.ContextType).Distinct().ToArray()));
        QianDataMigrator.Register(services);
        services.TryAddSingleton(ErrorCatalog.CreateDefault());
        services.TryAddScoped<IntegrationEventBuffer>();
        // 不登记非泛型 KunDbContext/IUnitOfWork；每个 Data 会话明确选择自己的业务上下文。
        services.TryAddScoped(sp => new IntegrationEventOutboxAccumulator(
            sp.GetRequiredService<IntegrationEventBuffer>(), sp.GetRequiredService<IEventTypeRegistry>(),
            sp.GetRequiredService<Kun.Core.MultiTenancy.ITenantContext>()));
        services.TryAddScoped<IIntegrationEventBus>(sp => sp.GetRequiredService<IntegrationEventOutboxAccumulator>());
        services.TryAddScoped<IOutboxWriter>(sp => sp.GetRequiredService<IntegrationEventOutboxAccumulator>());
        services.AddKunTenantRls<TContext>();
        services.TryAddScoped<QianSoftDeleteInterceptor>();
        services.ConfigureDbContext<TContext>((sp, options) => options.AddInterceptors(sp.GetRequiredService<QianSoftDeleteInterceptor>()));
        services.AddKunDataConventions<TContext>();
        services.AddKunMultiTenancyEntityFrameworkCore();
        services.TryAddSingleton<QianApplicationRoleInterceptor>();
        services.ConfigureDbContext<TContext>((sp, options) => options.UseQianDataModel()
            .ReplaceService<IMigrationsSqlGenerator, QianPostgresMigrationsSqlGenerator>()
            .AddInterceptors(sp.GetRequiredService<KunTenantSaveChangesInterceptor>(),
                sp.GetRequiredService<QianApplicationRoleInterceptor>()));
        services.AddDbContext<TContext>(configure);
        services.AddHostedService<DataStartupCheck<TContext>>();
        return services;
    }

    private sealed class DataStartupCheck<TContext>(IServiceScopeFactory scopes, ErrorCatalog catalog) : IHostedService, IDisposable
        where TContext : QianDbContext
    {
        private IDisposable? _registration;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _registration ??= catalog.Register(DataErrors.Misconfigured);
            using var scope = scopes.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            if (context.GetService<IModelCacheKeyFactory>() is not KunMultiTenantModelCacheKeyFactory)
                throw new QianDataMisconfiguredException("生效的 EF 模型缓存键被替换。");
            if (context.GetService<IMigrationsSqlGenerator>() is not QianPostgresMigrationsSqlGenerator)
                throw new QianDataMisconfiguredException("必须使用自动生成租户 RLS 策略的 Qian 迁移生成器。");
            var model = context.Model;
            var schema = context.GetService<IDbContextOptions>().Extensions
                .OfType<RelationalOptionsExtension>().Single().MigrationsHistoryTableSchema;
            if (model.GetDefaultSchema() != schema)
                throw new QianDataMisconfiguredException("生效模型的 schema 与上下文迁移 schema 不一致。");
            await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // 预打开连接不会再次触发 ConnectionOpened，启动时必须显式核验它。
                await QianApplicationRoleInterceptor.ValidateAsync(context.Database.GetDbConnection(), cancellationToken).ConfigureAwait(false);
                // 在线副本只校验不迁移：迁移由 --migrate-only 的迁移任务先行完成（IDataMigrator）。
                if (context.Database.GetMigrations().Any())
                {
                    var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToArray();
                    if (pending.Length > 0)
                        throw new QianDataMisconfiguredException("数据库还有未应用的迁移（" + string.Join(", ", pending) + "），先以 --migrate-only 运行迁移任务。");
                }
            }
            finally { await context.Database.CloseConnectionAsync().ConfigureAwait(false); }
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
