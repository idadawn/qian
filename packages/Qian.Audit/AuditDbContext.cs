using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Audit;

/// <summary>审计拥有的数据库模型，租户记录受 Data 双层隔离；null 平台行当前不自动开放。</summary>
public class AuditDbContext : QianDbContext
{
    /// <summary>普通租户审计上下文；无租户不读取平台记录。</summary>
    public AuditDbContext(DbContextOptions<AuditDbContext> options, ITenantContext tenant) : this(options, tenant, false) { }
    /// <summary>显式模式只改变查询投影，实际平台权限仍由独立数据库角色与 RLS 约束。</summary>
    protected AuditDbContext(DbContextOptions options, ITenantContext tenant, bool platform) : base(options, tenant) => PlatformMode = platform;
    private bool PlatformMode { get; }

    /// <summary>独立审计 schema。</summary>
    public const string Schema = "plugin_qian_audit";
    internal DbSet<AuditEntryRow> Entries => Set<AuditEntryRow>();
    internal DbSet<AuditChainRow> Chains => Set<AuditChainRow>();
    private string? CurrentTenant => TenantIdForModelCacheKey;
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEntryRow>(entity =>
        {
            entity.ToTable("audit_entries"); entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantId).HasMaxLength(36).IsRequired(false);
            entity.Property(x => x.ChainKey).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.ChainKey, x.Sequence }).IsUnique(); entity.HasIndex(x => x.ContentHash).IsUnique();
            entity.HasIndex(x => new { x.ChainKey, x.PreviousHash }).IsUnique().AreNullsDistinct(false);
            entity.Property(x => x.Sequence).HasColumnName("seq");
            entity.Property(x => x.PreviousHash).HasColumnName("prev_hash");
            entity.HasIndex(x => new { x.TenantId, x.OnBehalfOf, x.Sequence });
            entity.Property(x => x.PreviousHash).HasMaxLength(64); entity.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.ActorType).HasMaxLength(16); entity.Property(x => x.ActorId).HasMaxLength(200); entity.Property(x => x.OnBehalfOf).HasMaxLength(200);
            entity.Property(x => x.CapabilityId).HasMaxLength(384); entity.Property(x => x.ContractVersion).HasMaxLength(100);
            entity.Property(x => x.InputHash).HasMaxLength(64); entity.Property(x => x.Channel).HasMaxLength(16); entity.Property(x => x.Resource).HasMaxLength(512);
            entity.Property(x => x.Result).HasMaxLength(64); entity.Property(x => x.TraceId).HasMaxLength(32);
            entity.HasAnnotation("Qian:MultiTenant", true); entity.HasQueryFilter(x => PlatformMode ? x.TenantId == null : x.TenantId != null && x.TenantId == CurrentTenant);
        });
        modelBuilder.Entity<AuditChainRow>(entity =>
        {
            entity.ToTable("audit_chain_state"); entity.HasKey(x => x.ChainKey);
            entity.Property(x => x.ChainKey).HasMaxLength(64); entity.Property(x => x.TenantId).HasMaxLength(36).IsRequired(false);
            entity.Property(x => x.TipHash).HasMaxLength(64); entity.Property(x => x.TipSequence).HasColumnName("tip_seq");
            entity.HasAnnotation("Qian:MultiTenant", true); entity.HasQueryFilter(x => PlatformMode ? x.TenantId == null : x.TenantId != null && x.TenantId == CurrentTenant);
        });
    }
}
/// <summary>仅绑定专用平台审计登录角色的上下文；普通租户上下文不会自动切换到本类型。</summary>
public sealed class PlatformAuditDbContext(DbContextOptions<PlatformAuditDbContext> options, ITenantContext tenant)
    : AuditDbContext(options, tenant, true);

internal sealed class AuditChainRow
{
    public string ChainKey { get; set; } = ""; public string? TenantId { get; set; }
    public long TipSequence { get; set; } public string? TipHash { get; set; }
}
internal sealed class AuditEntryRow
{
    public Guid Id { get; set; } public string? TenantId { get; set; } public string ChainKey { get; set; } = "";
    public long Sequence { get; set; } public string? PreviousHash { get; set; } public string ContentHash { get; set; } = "";
    public string ActorType { get; set; } = ""; public string ActorId { get; set; } = ""; public string? OnBehalfOf { get; set; }
    public string CapabilityId { get; set; } = ""; public string ContractVersion { get; set; } = ""; public string InputHash { get; set; } = "";
    public string Channel { get; set; } = ""; public string? Resource { get; set; } public string Result { get; set; } = "";
    public string TraceId { get; set; } = ""; public DateTimeOffset OccurredAt { get; set; } public string Payload { get; set; } = "{}";
}
