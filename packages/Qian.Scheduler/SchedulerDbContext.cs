using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Scheduler;

/// <summary>调度器三表；普通应用角色始终受 tenant RLS 保护。</summary>
public class SchedulerDbContext : QianDbContext
{
    /// <summary>使用 Data 的安全装配。</summary>
    public SchedulerDbContext(DbContextOptions<SchedulerDbContext> options, ITenantContext tenant) : this((DbContextOptions)options, tenant) { }
    /// <summary>专用领取上下文复用同一模型。</summary>
    protected SchedulerDbContext(DbContextOptions options, ITenantContext tenant) : base(options, tenant) { }
    /// <summary>组件独占的 schema。</summary>
    public const string Schema = "plugin_qian_scheduler";
    internal DbSet<JobDefinitionRow> Definitions => Set<JobDefinitionRow>();
    internal DbSet<JobTriggerRow> Triggers => Set<JobTriggerRow>();
    internal DbSet<JobRunRow> Runs => Set<JobRunRow>();
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobDefinitionRow>(e =>
        {
            e.ToTable("job_definitions"); e.HasKey(x => new { x.TenantId, x.Id });
            e.HasIndex(x => new { x.TenantId, x.BusinessKey }).IsUnique();
            e.Property(x => x.TenantId).HasMaxLength(36); e.Property(x => x.BusinessKey).HasMaxLength(200);
            e.Property(x => x.Target).HasMaxLength(300); e.Property(x => x.Schedule).HasMaxLength(256);
            e.Property(x => x.Parameters).HasColumnType("jsonb"); e.Property(x => x.Options).HasColumnType("jsonb");
            e.Property(x => x.Fingerprint).HasMaxLength(64); e.Property(x => x.CreatedBy).HasMaxLength(200);
        });
        modelBuilder.Entity<JobTriggerRow>(e =>
        {
            e.ToTable("job_triggers", t => t.HasCheckConstraint("ck_job_trigger_state", "state IN ('ready','paused','archived')"));
            e.HasKey(x => new { x.TenantId, x.Id }); e.Property(x => x.TenantId).HasMaxLength(36);
            e.HasOne<JobDefinitionRow>().WithOne().HasForeignKey<JobTriggerRow>(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.State).HasMaxLength(16); e.Property(x => x.LeaseOwner).HasMaxLength(100);
            e.HasIndex(x => new { x.State, x.NextRunAt, x.AvailableAt });
        });
        modelBuilder.Entity<JobRunRow>(e =>
        {
            e.ToTable("job_runs"); e.HasKey(x => new { x.TenantId, x.Id }); e.Property(x => x.TenantId).HasMaxLength(36);
            e.HasIndex(x => new { x.TenantId, x.TriggerId, x.ScheduledAt }).IsUnique();
            e.HasOne<JobTriggerRow>().WithMany().HasForeignKey(x => new { x.TenantId, x.TriggerId }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Status).HasMaxLength(20); e.Property(x => x.ErrorClass).HasMaxLength(100); e.Property(x => x.Worker).HasMaxLength(100);
        });
    }
}
/// <summary>只用于领取元数据的独立 worker 登录；定义和运行记录不得经此连接访问。</summary>
public sealed class SchedulerWorkerDbContext(DbContextOptions<SchedulerWorkerDbContext> options, ITenantContext tenant) : SchedulerDbContext(options, tenant);

internal sealed class JobDefinitionRow : IMultiTenant
{
    public string TenantId { get; set; } = "";
    public Guid Id { get; set; }
    public string BusinessKey { get; set; } = "";
    public string Target { get; set; } = "";
    public string Parameters { get; set; } = "{}";
    public string Schedule { get; set; } = "";
    public string Options { get; set; } = "{}";
    public string Fingerprint { get; set; } = "";
    public string CreatedBy { get; set; } = "";
}
internal sealed class JobTriggerRow : IMultiTenant
{
    public string TenantId { get; set; } = "";
    public Guid Id { get; set; }
    public string State { get; set; } = "ready";
    public DateTimeOffset NextRunAt { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public long RunCount { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public long Generation { get; set; }
    public Guid? ActiveRunId { get; set; }
}
internal sealed class JobRunRow : IMultiTenant
{
    public string TenantId { get; set; } = "";
    public Guid Id { get; set; }
    public Guid TriggerId { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = "running";
    public int Attempts { get; set; }
    public string Worker { get; set; } = "";
    public long Generation { get; set; }
    public string? ErrorClass { get; set; }
}
