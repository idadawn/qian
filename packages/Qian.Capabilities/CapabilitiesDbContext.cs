using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Capabilities;

/// <summary>菜单展示偏好上下文，使用 Data 的受限角色、过滤器与强制 RLS。</summary>
public sealed class CapabilitiesDbContext(DbContextOptions<CapabilitiesDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    /// <summary>本组件的数据 schema。</summary>
    public const string Schema = "plugin_qian_capabilities";
    internal DbSet<NavigationPreferenceRow> NavigationPreferences => Set<NavigationPreferenceRow>();
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
