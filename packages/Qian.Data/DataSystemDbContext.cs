using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Qian.Data;

/// <summary>qian.data 自身的表（plugin_qian_data）：序号规则与计数器、数据连接档案；结构随包内 EF 迁移交付，由迁移任务建表。</summary>
internal sealed class DataSystemDbContext(DbContextOptions<DataSystemDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    public const string Schema = "plugin_qian_data";
    /// <summary>租户内连接名唯一索引。</summary>
    public const string NameIndex = "ux_data_connections_tenant_id_name";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SequenceRuleRecord>(entity =>
        {
            entity.ToTable("sequence_rules");
            entity.HasKey(rule => new { rule.TenantId, rule.Code });
            entity.Property(rule => rule.TenantId).HasColumnName("tenant_id").HasMaxLength(64);
            entity.Property(rule => rule.Code).HasColumnName("code").HasMaxLength(64);
            entity.Property(rule => rule.Pattern).HasColumnName("pattern").HasMaxLength(256);
            entity.Property(rule => rule.Revision).HasColumnName("revision");
            entity.Property(rule => rule.UpdatedAt).HasColumnName("updated_at");
        });
        modelBuilder.Entity<SequenceCounterRecord>(entity =>
        {
            entity.ToTable("sequence_counters");
            entity.HasKey(counter => new { counter.TenantId, counter.RuleCode, counter.Revision, counter.ScopeKey, counter.Period });
            entity.Property(counter => counter.TenantId).HasColumnName("tenant_id").HasMaxLength(64);
            entity.Property(counter => counter.RuleCode).HasColumnName("rule_code").HasMaxLength(64);
            entity.Property(counter => counter.Revision).HasColumnName("revision");
            entity.Property(counter => counter.ScopeKey).HasColumnName("scope_key").HasMaxLength(128);
            entity.Property(counter => counter.Period).HasColumnName("period").HasMaxLength(64);
            entity.Property(counter => counter.Value).HasColumnName("value");
        });
        modelBuilder.Entity<DataConnectionRecord>(entity =>
        {
            entity.ToTable("data_connections");
            entity.Property(connection => connection.TenantId).HasMaxLength(64);
            entity.Property(connection => connection.Name).HasMaxLength(64).IsRequired();
            entity.Property(connection => connection.Dialect).HasMaxLength(32).IsRequired();
            entity.Property(connection => connection.Host).HasMaxLength(253).IsRequired();
            entity.Property(connection => connection.Database).HasMaxLength(63).IsRequired();
            entity.Property(connection => connection.UserName).HasMaxLength(63).IsRequired();
            entity.Property(connection => connection.SecretRef).HasMaxLength(256).IsRequired();
            entity.Property(connection => connection.Schema).HasMaxLength(63);
            entity.Property(connection => connection.Encoding).HasMaxLength(32);
            // 软删后名称可以复用，唯一性只约束未删除的档案。
            entity.HasIndex(connection => new { connection.TenantId, connection.Name }).IsUnique()
                .HasFilter("NOT is_deleted").HasDatabaseName(NameIndex);
        });
    }
}

/// <summary>
/// 序号服务与数据连接档案共用的系统上下文登记：只登记一次；后续调用的连接配置追加在后，且必须解析出同一个连接串，
/// 防止两处装配悄悄指向不同的库。
/// </summary>
internal static class DataSystemContext
{
    internal static void Add(IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (services.Any(service => service.ServiceType == typeof(Marker)))
        {
            services.ConfigureDbContext<DataSystemDbContext>((sp, builder) =>
            {
                var before = ConnectionString(builder);
                Apply(sp, builder, configure);
                if (before is not null && ConnectionString(builder) != before)
                    throw new QianDataMisconfiguredException("序号服务与数据连接档案必须使用同一个 qian.data 系统库连接。");
            });
            return;
        }
        services.AddSingleton<Marker>();
        services.AddQianData<DataSystemDbContext>((sp, builder) => Apply(sp, builder, configure));
    }

    private static void Apply(IServiceProvider services, DbContextOptionsBuilder builder, Action<IServiceProvider, DbContextOptionsBuilder> configure)
    {
        configure(services, builder);
        builder.UseKunSchema(DataSystemDbContext.Schema);
    }

    private static string? ConnectionString(DbContextOptionsBuilder builder) =>
        builder.Options.Extensions.OfType<RelationalOptionsExtension>().SingleOrDefault()?.ConnectionString;

    private sealed class Marker;
}

/// <summary>租户内的一条序号规则；模式变化时修订号加一。</summary>
internal sealed class SequenceRuleRecord : IMultiTenant
{
    public string? TenantId { get; set; }
    public string Code { get; set; } = "";
    public string Pattern { get; set; } = "";
    public int Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>规则某一修订、作用域与周期的当前流水；只经 INSERT … ON CONFLICT DO UPDATE … RETURNING 原子递增。</summary>
internal sealed class SequenceCounterRecord : IMultiTenant
{
    public string? TenantId { get; set; }
    public string RuleCode { get; set; } = "";
    public int Revision { get; set; }
    public string ScopeKey { get; set; } = "";
    public string Period { get; set; } = "";
    public long Value { get; set; }
}

/// <summary>租户的数据连接档案；口令只以 secretRef 保存，在连通测试时于使用点解析。</summary>
internal sealed class DataConnectionRecord : AggregateRoot<Guid, string>, IMultiTenant
{
    public DataConnectionRecord() { }
    public DataConnectionRecord(Guid id) { Id = id; }
    public string? TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Dialect { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Database { get; set; } = "";
    public string UserName { get; set; } = "";
    public string SecretRef { get; set; } = "";
    public string? Schema { get; set; }
    public string? Encoding { get; set; }
}
