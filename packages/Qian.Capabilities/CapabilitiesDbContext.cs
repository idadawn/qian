using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Capabilities;

/// <summary>菜单展示偏好与调用幂等回执的上下文，使用 Data 的受限角色、过滤器与强制 RLS；表结构随包内迁移交付。</summary>
public sealed class CapabilitiesDbContext(DbContextOptions<CapabilitiesDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    /// <summary>本组件的数据 schema。</summary>
    public const string Schema = "plugin_qian_capabilities";
    internal const string ReceiptsTable = "invoke_receipts";
    // 回执体的硬上限（UTF-8 字节），与组件配置 receiptMaxBytes 的最大值一致。
    internal const int ReceiptResponseLimit = 1048576;
    // 不保存响应体的原因，也是 CAPABILITY_REPLAY_UNAVAILABLE 的 reason 参数。
    internal const string WithheldTooLarge = "response_too_large", WithheldClassification = "data_classification";
    internal DbSet<NavigationPreferenceRow> NavigationPreferences => Set<NavigationPreferenceRow>();
    internal DbSet<InvokeReceiptRow> InvokeReceipts => Set<InvokeReceiptRow>();
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NavigationPreferenceRow>(entity =>
        {
            entity.ToTable("navigation_preferences"); entity.HasKey(row => new { row.TenantId, row.SubjectKey, row.ResourceKey });
            entity.Property(row => row.TenantId).HasMaxLength(200);
            entity.Property(row => row.SubjectKey).HasMaxLength(64); entity.Property(row => row.ResourceKey).HasMaxLength(64);
            entity.Property(row => row.SubjectType).HasMaxLength(16).IsRequired(); entity.Property(row => row.SubjectId).HasMaxLength(200).IsRequired();
            entity.Property(row => row.ResourceRef).HasMaxLength(4096).IsRequired();
        });
        modelBuilder.Entity<InvokeReceiptRow>(entity =>
        {
            // 回执体的硬上限：组件配置 receiptMaxBytes 在写入前执行（默认 64 KiB），这里兜住配置之外的写入，明文响应不会无界增长。
            entity.ToTable(ReceiptsTable, table =>
            {
                table.HasCheckConstraint("ck_invoke_receipts_response_size", "octet_length(response) <= " + ReceiptResponseLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
                table.HasCheckConstraint("ck_invoke_receipts_withheld", "withheld IS NULL OR (response IS NULL AND withheld IN ('" + WithheldTooLarge + "', '" + WithheldClassification + "'))");
            });
            entity.HasKey(row => new { row.TenantId, row.SubjectKey, row.CapabilityId, row.IdempotencyKey });
            entity.Property(row => row.TenantId).HasMaxLength(200);
            entity.Property(row => row.SubjectKey).HasMaxLength(64); entity.Property(row => row.CapabilityId).HasMaxLength(300);
            entity.Property(row => row.RequestSha256).HasMaxLength(64).IsRequired();
            // 原样保存响应文本，回放与首次响应逐字节一致（jsonb 会重排成员顺序）；与 Withheld 同时为空表示执行中。
            entity.Property(row => row.Response);
            entity.Property(row => row.Withheld).HasMaxLength(32);
            // 登记时按租户清理过期行。
            entity.HasIndex(row => new { row.TenantId, row.ExpiresAt });
        });
    }
}
internal sealed class NavigationPreferenceRow : IMultiTenant
{
    public string TenantId { get; set; } = "";
    public string SubjectKey { get; set; } = "";
    public string ResourceKey { get; set; } = "";
    public string SubjectType { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public string ResourceRef { get; set; } = "";
    public bool Hidden { get; set; }
    public int? Order { get; set; }
}
// 幂等键的登记与回执：执行中只有输入摘要（Response 与 Withheld 都为空，ExpiresAt 是租约到期时间）；成功后保存原样响应，或在响应超过
// 回执上限、能力数据分级不允许保存时只在 Withheld 记原因，ExpiresAt 都延到回放窗口末尾。失败调用撤销登记，不留回执。
internal sealed class InvokeReceiptRow : IMultiTenant
{
    public string TenantId { get; set; } = "";
    public string SubjectKey { get; set; } = "";
    public string CapabilityId { get; set; } = "";
    public Guid IdempotencyKey { get; set; }
    public string RequestSha256 { get; set; } = "";
    public string? Response { get; set; }
    public string? Withheld { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
