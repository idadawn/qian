using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore;
using Kun.EntityFrameworkCore.Conventions;
using Kun.EntityFrameworkCore.Interceptors;
using Kun.MultiTenancy.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

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

    internal void CompleteDataModel(ModelBuilder modelBuilder)
    {
        var options = this.GetService<IDbContextOptions>();
        var schema = options.Extensions.OfType<RelationalOptionsExtension>().Single().MigrationsHistoryTableSchema!;
        if (DefaultSchema is not null && DefaultSchema != schema)
            throw new QianDataMisconfiguredException("上下文 schema 与迁移历史 schema 不一致。");
        var originalFilters = modelBuilder.Model.GetEntityTypes().ToDictionary(entity => entity.Name,
            entity => entity.GetDeclaredQueryFilters().Select(filter => filter.Expression).OfType<LambdaExpression>().ToArray());
        // 即使派生上下文漏调 base，租户/软删过滤与 Outbox 也必须存在。
        base.OnModelCreating(modelBuilder);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (!originalFilters.TryGetValue(entity.Name, out var original) || original.Length == 0) continue;
            var active = entity.GetDeclaredQueryFilters().ToArray();
            var expressions = original.Concat(active.Select(filter => filter.Expression).OfType<LambdaExpression>()).Distinct().ToArray();
            var parameter = Expression.Parameter(entity.ClrType, "entity");
            var bodies = expressions.Select(filter => new ReplaceParameter(filter.Parameters[0], parameter).Visit(filter.Body)!);
            var combined = bodies.Aggregate(Expression.AndAlso);
            foreach (var filter in active)
                if (filter.IsAnonymous) entity.SetQueryFilter(null);
                else entity.SetQueryFilter(filter.Key, null);
            entity.SetQueryFilter("Qian:combined", Expression.Lambda(combined, parameter));
        }
        modelBuilder.HasDefaultSchema(schema);
        modelBuilder.ApplyKunDataConventions();
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.GetSchema() != schema)
                throw new QianDataMisconfiguredException("数据上下文的表必须属于同一个插件 schema。");
            if (!typeof(Kun.Core.Domain.IMultiTenant).IsAssignableFrom(entity.ClrType)) continue;
            entity.SetAnnotation("Qian:MultiTenant", true);
            var tenant = entity.FindProperty(nameof(Kun.Core.Domain.IMultiTenant.TenantId))
                ?? throw new QianDataMisconfiguredException("租户实体缺少 TenantId 映射。");
            tenant.IsNullable = false;
            if (entity.FindIndex([tenant]) is null)
                entity.AddIndex(tenant).SetDatabaseName("ix_" + entity.GetTableName() + "_tenant_id");
        }
    }

    private sealed class ReplaceParameter(ParameterExpression before, ParameterExpression after) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == before ? after : base.VisitParameter(node);
    }

    private static void ValidateOptions(DbContextOptions options)
    {
        var core = options.FindExtension<CoreOptionsExtension>();
        if (core?.Interceptors?.OfType<TenantRlsConnectionInterceptor>().Any() != true ||
            core.Interceptors.OfType<TenantRlsTransactionInterceptor>().Any() != true)
            throw new QianDataMisconfiguredException("必须同时装配 RLS 连接版与事务版拦截器。");
        if (!core.Interceptors.OfType<AuditFieldsSaveChangesInterceptor>().Any())
            throw new QianDataMisconfiguredException("必须装配 Kun 的审计字段拦截器。");
        if (!core.Interceptors.OfType<KunTenantSaveChangesInterceptor>().Any())
            throw new QianDataMisconfiguredException("必须装配 Kun 的租户字段拦截器。");
        if (core.ReplacedServices?.TryGetValue((typeof(IModelCacheKeyFactory), null), out var factory) != true ||
            factory != typeof(KunMultiTenantModelCacheKeyFactory))
            throw new QianDataMisconfiguredException("必须使用 KunMultiTenantModelCacheKeyFactory。");
        if (core.ReplacedServices.TryGetValue((typeof(IModelCustomizer), null), out var customizer) != true ||
            customizer != typeof(QianModelCustomizer))
            throw new QianDataMisconfiguredException("必须装配 Qian 数据模型约定。");
        var relational = options.Extensions.OfType<RelationalOptionsExtension>().SingleOrDefault();
        if (relational?.MigrationsHistoryTableSchema is not { } schema ||
            !Regex.IsMatch(schema, "^plugin_[a-z][a-z0-9_]{0,55}$", RegexOptions.CultureInvariant))
            throw new QianDataMisconfiguredException("必须通过 UseKunSchema 指定 plugin_ 前缀的插件 schema。");
        if (core.InternalServiceProvider is not null || core.Model is not null)
            throw new QianDataMisconfiguredException("不允许外部 EF 服务容器或预编译模型绕过租户模型配置。");
    }
}
