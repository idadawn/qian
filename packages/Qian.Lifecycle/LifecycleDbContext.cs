using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Lifecycle;

/// <summary>生命周期上下文：平台修订（平台级目录）、租户安装、钩子执行记录、挂载失败诊断与反向权限批准（租户表，强制 RLS）；表结构随包内迁移交付。</summary>
public sealed class LifecycleDbContext(DbContextOptions<LifecycleDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    /// <summary>组件规格 §7.6 的唯一数据库 schema。</summary>
    public const string Schema = "plugin_qian_lifecycle";
    internal DbSet<PluginRevisionRow> Revisions => Set<PluginRevisionRow>();
    internal DbSet<TenantInstallRow> Installs => Set<TenantInstallRow>();
    internal DbSet<LifecycleRunRow> Runs => Set<LifecycleRunRow>();
    internal DbSet<MountFailureRow> MountFailures => Set<MountFailureRow>();
    internal DbSet<PermissionApprovalRow> PermissionApprovals => Set<PermissionApprovalRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 平台修订是平台级目录，不带租户；租户是否可用由安装记录与 scope patch 决定。
        modelBuilder.Entity<PluginRevisionRow>(entity =>
        {
            entity.ToTable("plugin_revisions");
            entity.HasKey(row => new { row.PluginId, row.Revision });
            entity.Property(row => row.PluginId).HasMaxLength(128);
            entity.Property(row => row.Version).HasMaxLength(200).IsRequired();
            entity.Property(row => row.ArtifactDigest).HasMaxLength(71).IsRequired();
            entity.Property(row => row.Form).HasMaxLength(16).IsRequired();
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.SignedBy).HasMaxLength(71).IsRequired();
            entity.Property(row => row.Workload).HasMaxLength(63);
            entity.Property(row => row.DeploymentReference).HasMaxLength(200);
            entity.Property(row => row.HealthPath).HasMaxLength(200);
            entity.Property(row => row.Health).HasMaxLength(16);
            entity.Property(row => row.HealthDetail).HasMaxLength(500);
            entity.Property(row => row.InstalledBy).HasMaxLength(200).IsRequired();
            entity.Property(row => row.InstalledAt).HasColumnType("timestamp with time zone");
            entity.Property(row => row.ActivatedAt).HasColumnType("timestamp with time zone");
            entity.Property(row => row.HealthCheckedAt).HasColumnType("timestamp with time zone");
            entity.Property(row => row.RowVersion).IsConcurrencyToken();
            entity.HasIndex(row => new { row.PluginId, row.Version }).IsUnique();
            entity.HasIndex(row => new { row.PluginId, row.ArtifactDigest }).IsUnique();
            // 同一组件至多一个 Active 修订，由数据库兜住并发激活。
            entity.HasIndex(row => row.PluginId).IsUnique().HasFilter("state = 'Active'").HasDatabaseName("ux_plugin_revisions_active");
            entity.Ignore(row => row.DomainEvents);
        });
        // 安装状态只有授权 / 开通 / 升级 / 卸载 / 清除；挂载与否只在 Registry 的 scope_patches（R34）。
        modelBuilder.Entity<TenantInstallRow>(entity =>
        {
            entity.ToTable("tenant_plugin_installs");
            entity.HasKey(row => new { row.TenantId, row.PluginId });
            entity.Property(row => row.TenantId).HasMaxLength(200);
            entity.Property(row => row.PluginId).HasMaxLength(128);
            entity.Property(row => row.State).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Version).HasMaxLength(200);
            entity.Property(row => row.TargetVersion).HasMaxLength(200);
            entity.Property(row => row.UpdatedBy).HasMaxLength(200).IsRequired();
            entity.Property(row => row.UpdatedAt).HasColumnType("timestamp with time zone");
            entity.Property(row => row.RowVersion).IsConcurrencyToken();
            entity.Ignore(row => row.DomainEvents);
        });
        // 主键即钩子幂等键 (tenant, plugin, hook, toVersion)：成功后不再执行，失败可重试。
        modelBuilder.Entity<LifecycleRunRow>(entity =>
        {
            entity.ToTable("lifecycle_runs");
            entity.HasKey(row => new { row.TenantId, row.PluginId, row.Hook, row.ToVersion });
            entity.Property(row => row.TenantId).HasMaxLength(200);
            entity.Property(row => row.PluginId).HasMaxLength(128);
            entity.Property(row => row.Hook).HasMaxLength(32);
            entity.Property(row => row.ToVersion).HasMaxLength(200);
            entity.Property(row => row.FromVersion).HasMaxLength(200);
            entity.Property(row => row.Status).HasMaxLength(16).IsRequired();
            entity.Property(row => row.ReasonCode).HasMaxLength(64);
            entity.Property(row => row.Result).HasColumnType("jsonb");
            entity.Property(row => row.StartedAt).HasColumnType("timestamp with time zone");
            entity.Property(row => row.FinishedAt).HasColumnType("timestamp with time zone");
        });
        // 经 Lifecycle 挂载或升级失败、已撤回的条目：Registry 的运行时诊断随撤回消失，原因在这里留到同一条目挂载成功或卸载。
        modelBuilder.Entity<MountFailureRow>(entity =>
        {
            entity.ToTable("mount_failures");
            entity.HasKey(row => new { row.TenantId, row.Scope, row.EntryId });
            entity.Property(row => row.TenantId).HasMaxLength(200);
            entity.Property(row => row.Scope).HasMaxLength(256);
            entity.Property(row => row.EntryId).HasMaxLength(128);
            entity.Property(row => row.Plugin).HasMaxLength(330);
            entity.Property(row => row.ReasonCode).HasMaxLength(64).IsRequired();
            entity.Property(row => row.OccurredAt).HasColumnType("timestamp with time zone");
        });
        // 租户对组件版本的反向权限批准（D7 逐项批准、升级新增即重批）：一个版本一行，整份替换；与权益（Entitled）是两道门，不合并。
        modelBuilder.Entity<PermissionApprovalRow>(entity =>
        {
            entity.ToTable("permission_approvals");
            entity.HasKey(row => new { row.TenantId, row.PluginId, row.Version });
            entity.Property(row => row.TenantId).HasMaxLength(200);
            entity.Property(row => row.PluginId).HasMaxLength(128);
            entity.Property(row => row.Version).HasMaxLength(200);
            entity.Property(row => row.Approved).HasColumnType("jsonb").IsRequired();
            entity.Property(row => row.ApprovedBy).HasMaxLength(200).IsRequired();
            entity.Property(row => row.ApprovedVia).HasMaxLength(16).IsRequired();
            entity.Property(row => row.ApprovedAt).HasColumnType("timestamp with time zone");
            entity.Property(row => row.RowVersion).IsConcurrencyToken();
        });
    }
}

internal sealed class PluginRevisionRow : IAggregateRoot
{
    private List<IDomainEvent>? _changes;
    public string PluginId { get; set; } = "";
    public int Revision { get; set; }
    public string Version { get; set; } = "";
    public string ArtifactDigest { get; set; } = "";
    public string Form { get; set; } = "";
    public string State { get; set; } = PluginRevisionStates.Installed;
    public string SignedBy { get; set; } = "";
    public bool Required { get; set; }
    public string? Workload { get; set; }
    public string? DeploymentReference { get; set; }
    public int? ServicePort { get; set; }
    public string? HealthPath { get; set; }
    public string? Health { get; set; }
    public string? HealthDetail { get; set; }
    public DateTimeOffset? HealthCheckedAt { get; set; }
    public string InstalledBy { get; set; } = "";
    public DateTimeOffset InstalledAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public int RowVersion { get; set; }
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _changes ?? [];
    public void ClearDomainEvents() => _changes?.Clear();
    internal void Raise(IDomainEvent change) => (_changes ??= []).Add(change);
    internal PluginRevisionView View() => new(PluginId, Revision, Version, ArtifactDigest, Form, State, SignedBy, Health, DeploymentReference, InstalledAt, ActivatedAt);
}

internal sealed class TenantInstallRow : IMultiTenant, IAggregateRoot
{
    private List<IDomainEvent>? _changes;
    public string TenantId { get; set; } = "";
    public string PluginId { get; set; } = "";
    public string State { get; set; } = TenantInstallStates.Entitled;
    public string? Version { get; set; }
    public string? TargetVersion { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public int RowVersion { get; set; }
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _changes ?? [];
    public void ClearDomainEvents() => _changes?.Clear();
    internal void Raise(IDomainEvent change) => (_changes ??= []).Add(change);
    internal TenantPluginInstallView View() => new(TenantId, PluginId, State, Version, TargetVersion, UpdatedBy, UpdatedAt);
}

internal sealed class LifecycleRunRow : IMultiTenant
{
    public string TenantId { get; set; } = "";
    public string PluginId { get; set; } = "";
    public string Hook { get; set; } = "";
    public string ToVersion { get; set; } = "";
    public string? FromVersion { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string Status { get; set; } = LifecycleRunStates.Running;
    public int Attempts { get; set; }
    public string? ReasonCode { get; set; }
    public string? Result { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    internal LifecycleRunView View() => new(TenantId, PluginId, Hook, FromVersion, ToVersion, Status, Attempts, ReasonCode, StartedAt, FinishedAt);
}

internal sealed class MountFailureRow : IMultiTenant
{
    public string TenantId { get; set; } = "";
    public string Scope { get; set; } = "";
    public string EntryId { get; set; } = "";
    public string? Plugin { get; set; }
    public int? Revision { get; set; }
    public string ReasonCode { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    internal LifecycleDiagnosticView View() => new(Scope, EntryId, Plugin, Revision, ReasonCode, OccurredAt);
}

internal sealed class PermissionApprovalRow : IMultiTenant
{
    public string TenantId { get; set; } = "";
    public string PluginId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Approved { get; set; } = "";
    public string ApprovedBy { get; set; } = "";
    public string ApprovedVia { get; set; } = "";
    public DateTimeOffset ApprovedAt { get; set; }
    public int RowVersion { get; set; }
    internal PermissionApprovalView View() => new(TenantId, PluginId, Version, PermissionSet.Parse(Approved).ToGrants(), ApprovedBy, ApprovedVia, ApprovedAt);
}
