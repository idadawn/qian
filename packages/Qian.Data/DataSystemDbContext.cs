using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Qian.Data;

/// <summary>qian.data 自身的表（plugin_qian_data）：序号规则与计数器；结构随包内 EF 迁移交付，由迁移任务建表。</summary>
internal sealed class DataSystemDbContext(DbContextOptions<DataSystemDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    public const string Schema = "plugin_qian_data";

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
    }
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
