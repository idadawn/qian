using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Registry;

/// <summary>注册表上下文；由宿主通过 AddQianData 装配并迁移，运行时不自动建表。</summary>
public sealed class RegistryDbContext(DbContextOptions<RegistryDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    /// <summary>组件规格 §7.6 的唯一数据库 schema。</summary>
    public const string Schema = "plugin_qian_registry";
    internal DbSet<ScopePatchRow> ScopePatches => Set<ScopePatchRow>();
    internal DbSet<PluginArtifactRow> PluginArtifacts => Set<PluginArtifactRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 制品是平台级目录；没有租户启用状态，不能用目录可见性替代 scope/权益检查。
        modelBuilder.Entity<PluginArtifactRow>(entity =>
        {
            entity.ToTable("plugin_artifacts");
            entity.HasKey(row => row.Digest).HasName("pk_plugin_artifacts");
            entity.Property(row => row.Digest).HasMaxLength(71);
            entity.Property(row => row.PluginId).HasMaxLength(128).IsRequired();
            entity.Property(row => row.Version).HasMaxLength(200).IsRequired();
            entity.HasIndex(row => new { row.PluginId, row.Version });
            entity.Property(row => row.Manifest).HasColumnType("jsonb").IsRequired();
            entity.Property(row => row.Documents).HasColumnType("jsonb").IsRequired();
            entity.Property(row => row.Files).HasColumnType("jsonb").IsRequired();
            entity.Property(row => row.Metadata).HasColumnType("jsonb").IsRequired();
            entity.Property(row => row.State).HasMaxLength(32).IsRequired();
            entity.Property(row => row.SignatureVerification).HasColumnType("jsonb");
            entity.Property(row => row.Profile).HasMaxLength(128).IsRequired();
            entity.Property(row => row.SubmittedBy).HasMaxLength(200).IsRequired();
            entity.Property(row => row.SubmittedAt).HasColumnType("timestamp with time zone");
        });
        modelBuilder.Entity<ScopePatchRow>(entity =>
        {
            entity.ToTable("scope_patches");
            entity.HasKey(row => new { row.TenantId, row.Scope, row.EntryId }).HasName("pk_scope_patches");
            entity.Property(row => row.TenantId).HasMaxLength(200).IsRequired();
            entity.Property(row => row.Scope).HasMaxLength(256).IsRequired();
            entity.Property(row => row.EntryId).HasMaxLength(128).IsRequired();
            entity.Property(row => row.Plugin).HasMaxLength(128);
            entity.Property(row => row.PluginVersion).HasMaxLength(200);
            entity.Property(row => row.Config).HasColumnType("jsonb");
            entity.Property(row => row.Extensions).HasColumnType("jsonb");
            entity.Property(row => row.Note).HasMaxLength(4000);
            entity.Property(row => row.Revision).IsConcurrencyToken();
            entity.Property(row => row.UpdatedBy).HasMaxLength(200).IsRequired();
            entity.Property(row => row.UpdatedAt).HasColumnType("timestamp with time zone");
            entity.Ignore(row => row.DomainEvents);
        });
    }
}

// 撤销保留内部墓碑和修订，避免删除/重建后接受旧客户端的相同修订（ABA）。
// 公开挂载 / 卸载经领域事件把集成事件放进同一事务的 Outbox；内部持久化原语不登记事件。
internal sealed class ScopePatchRow : IMultiTenant, IAggregateRoot
{
    private List<IDomainEvent>? _changes;
    public string TenantId { get; set; } = "";
    public string Scope { get; set; } = "";
    public string EntryId { get; set; } = "";
    public string? Plugin { get; set; }
    public string? PluginVersion { get; set; }
    public string? Config { get; set; }
    public bool Disabled { get; set; }
    public bool DisabledSpecified { get; set; }
    public string? Extensions { get; set; }
    public string? Note { get; set; }
    public int Revision { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _changes ?? [];
    public void ClearDomainEvents() => _changes?.Clear();
    internal void Raise(IDomainEvent change) => (_changes ??= []).Add(change);
}

// 注册表的全局候选目录；公开管理写入必须另经平台权限，Submitted 不可作为挂载依据。
internal sealed class PluginArtifactRow
{
    public string Digest { get; set; } = "";
    public string PluginId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Manifest { get; set; } = "";
    public string Documents { get; set; } = "";
    public string Files { get; set; } = "";
    public string Metadata { get; set; } = "";
    public string State { get; set; } = "Submitted";
    public string? SignatureVerification { get; set; }
    public string Profile { get; set; } = "";
    public string SubmittedBy { get; set; } = "";
    public DateTimeOffset SubmittedAt { get; set; }
}
