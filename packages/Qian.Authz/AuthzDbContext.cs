using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Authz;

/// <summary>权限目录上下文；连接、迁移和应用角色校验由 Data 的强制装配提供。</summary>
public sealed class AuthzDbContext(DbContextOptions<AuthzDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    /// <summary>Authz 拥有的独立 schema。</summary>
    public const string Schema = "plugin_qian_authz";
    internal DbSet<PermissionCatalogRow> Catalogs => Set<PermissionCatalogRow>();
    internal DbSet<PermissionRow> Permissions => Set<PermissionRow>();
    internal DbSet<ResourceTypeRow> ResourceTypes => Set<ResourceTypeRow>();
    internal DbSet<PolicyPackRow> PolicyPacks => Set<PolicyPackRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PermissionCatalogRow>(entity =>
        {
            entity.ToTable("permission_catalogs"); entity.HasKey(row => new { row.PluginId, row.Version });
            entity.Property(row => row.PluginId).HasMaxLength(128); entity.Property(row => row.Version).HasMaxLength(200);
            entity.Property(row => row.Snapshot).HasColumnType("jsonb");
        });
        modelBuilder.Entity<PermissionRow>(entity =>
        {
            entity.ToTable("permission_definitions"); entity.HasKey(row => new { row.PluginId, row.Version, row.Code });
            entity.Property(row => row.PluginId).HasMaxLength(128); entity.Property(row => row.Version).HasMaxLength(200);
            entity.Property(row => row.Code).HasMaxLength(512); entity.Property(row => row.Resource).HasMaxLength(128); entity.Property(row => row.Action).HasMaxLength(128);
            entity.Property(row => row.Risk).HasMaxLength(16); entity.Property(row => row.Terminals).HasColumnType("jsonb");
            entity.HasOne<PermissionCatalogRow>().WithMany().HasForeignKey(row => new { row.PluginId, row.Version }).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ResourceTypeRow>(entity =>
        {
            entity.ToTable("resource_types"); entity.HasKey(row => new { row.PluginId, row.Version, row.Code });
            entity.Property(row => row.PluginId).HasMaxLength(128); entity.Property(row => row.Version).HasMaxLength(200);
            entity.Property(row => row.Code).HasMaxLength(384); entity.Property(row => row.Name).HasMaxLength(128);
            entity.HasOne<PermissionCatalogRow>().WithMany().HasForeignKey(row => new { row.PluginId, row.Version }).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PolicyPackRow>(entity =>
        {
            entity.ToTable("policy_packs"); entity.HasKey(row => new { row.PluginId, row.Version, row.Role });
            entity.Property(row => row.PluginId).HasMaxLength(128); entity.Property(row => row.Version).HasMaxLength(200); entity.Property(row => row.Role).HasMaxLength(128);
            entity.Property(row => row.Patterns).HasColumnType("jsonb"); entity.Property(row => row.Grants).HasColumnType("jsonb");
            entity.HasOne<PermissionCatalogRow>().WithMany().HasForeignKey(row => new { row.PluginId, row.Version }).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class PermissionCatalogRow
{
    public string PluginId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Snapshot { get; set; } = "";
}
internal sealed class PermissionRow
{
    public string PluginId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Code { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Action { get; set; } = "";
    public string Risk { get; set; } = "";
    public bool StepUp { get; set; }
    public string Terminals { get; set; } = "";
}
internal sealed class ResourceTypeRow
{
    public string PluginId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
internal sealed class PolicyPackRow
{
    public string PluginId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Role { get; set; } = "";
    public string Patterns { get; set; } = "";
    public string Grants { get; set; } = "";
}
