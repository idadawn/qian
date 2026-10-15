using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Authz;

/// <summary>权限目录上下文；连接、迁移和应用角色校验由 Data 的强制装配提供。</summary>
public class AuthzDbContext : QianDbContext
{
    /// <summary>普通租户上下文：角色、主体绑定与授权只读写当前租户的行，平台行（tenant_id 为 null）不可见。</summary>
    public AuthzDbContext(DbContextOptions<AuthzDbContext> options, ITenantContext tenant) : this(options, tenant, false) { }
    /// <summary>显式平台模式只改变查询投影；实际平台权限由独立数据库登录角色与 RLS 约束（<see cref="AuthzPlatformAccess"/>）。</summary>
    protected AuthzDbContext(DbContextOptions options, ITenantContext tenant, bool platform) : base(options, tenant) => PlatformMode = platform;
    private bool PlatformMode { get; }

    /// <summary>Authz 拥有的独立 schema。</summary>
    public const string Schema = "plugin_qian_authz";
    internal DbSet<PermissionCatalogRow> Catalogs => Set<PermissionCatalogRow>();
    internal DbSet<PermissionRow> Permissions => Set<PermissionRow>();
    internal DbSet<ResourceTypeRow> ResourceTypes => Set<ResourceTypeRow>();
    internal DbSet<PolicyPackRow> PolicyPacks => Set<PolicyPackRow>();
    internal DbSet<AuthzRoleRow> Roles => Set<AuthzRoleRow>();
    internal DbSet<RoleAssignmentRow> RoleAssignments => Set<RoleAssignmentRow>();
    internal DbSet<RoleGrantRow> Grants => Set<RoleGrantRow>();
    internal DbSet<TenantPolicyInstallRow> PolicyInstalls => Set<TenantPolicyInstallRow>();
    internal DbSet<TenantAuthzRevisionRow> Revisions => Set<TenantAuthzRevisionRow>();
    // 查询过滤器读取当前 DbContext 成员，不捕获首次建模时的访问器。
    private string? ScopeTenant => TenantIdForModelCacheKey;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthzRoleRow>(entity =>
        {
            entity.ToTable("roles"); entity.HasKey(row => row.Id);
            entity.Property(row => row.TenantId).HasMaxLength(200).IsRequired(false);
            entity.Property(row => row.Code).HasMaxLength(128).IsRequired(); entity.Property(row => row.SourcePack).HasMaxLength(384);
            // 平台角色的 tenant_id 为 null，唯一性须把 null 视为同一值，否则平台角色可重复建出。
            entity.HasIndex(row => new { row.TenantId, row.Code }).IsUnique().AreNullsDistinct(false);
            entity.HasAnnotation("Qian:MultiTenant", true);
            entity.HasQueryFilter(row => PlatformMode ? row.TenantId == null : row.TenantId != null && row.TenantId == ScopeTenant);
        });
        modelBuilder.Entity<RoleAssignmentRow>(entity =>
        {
            entity.ToTable("role_assignments"); entity.HasKey(row => row.Id);
            entity.Property(row => row.TenantId).HasMaxLength(200).IsRequired(false);
            entity.Property(row => row.SubjectType).HasMaxLength(32).IsRequired(); entity.Property(row => row.SubjectId).HasMaxLength(200).IsRequired();
            entity.Property(row => row.AssignedBy).HasMaxLength(200).IsRequired();
            entity.HasIndex(row => new { row.TenantId, row.RoleId, row.SubjectType, row.SubjectId }).IsUnique().AreNullsDistinct(false);
            entity.HasOne<AuthzRoleRow>().WithMany().HasForeignKey(row => row.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasAnnotation("Qian:MultiTenant", true);
            entity.HasQueryFilter(row => PlatformMode ? row.TenantId == null : row.TenantId != null && row.TenantId == ScopeTenant);
        });
        modelBuilder.Entity<RoleGrantRow>(entity =>
        {
            entity.ToTable("grants"); entity.HasKey(row => row.Id);
            entity.Property(row => row.TenantId).HasMaxLength(200).IsRequired(false);
            entity.Property(row => row.PluginId).HasMaxLength(128).IsRequired(); entity.Property(row => row.PluginVersion).HasMaxLength(200).IsRequired();
            entity.Property(row => row.PermissionCode).HasMaxLength(512).IsRequired(); entity.Property(row => row.Source).HasMaxLength(32).IsRequired();
            entity.Property(row => row.GrantedBy).HasMaxLength(200).IsRequired(); entity.Property(row => row.ResourceRef).HasMaxLength(512);
            entity.HasIndex(row => new { row.TenantId, row.RoleId, row.PluginId, row.PermissionCode, row.Source }).IsUnique();
            entity.HasOne<AuthzRoleRow>().WithMany().HasForeignKey(row => row.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasAnnotation("Qian:MultiTenant", true);
            // 平台角色不经授权表放行（超管只走显式旁路），平台模式下授权表恒为空。
            entity.HasQueryFilter(row => !PlatformMode && row.TenantId != null && row.TenantId == ScopeTenant);
        });
        modelBuilder.Entity<TenantPolicyInstallRow>(entity =>
        {
            entity.ToTable("tenant_policy_installs"); entity.HasKey(row => new { row.TenantId, row.PluginId });
            entity.Property(row => row.TenantId).HasMaxLength(200); entity.Property(row => row.PluginId).HasMaxLength(128);
            entity.Property(row => row.Version).HasMaxLength(200).IsRequired(); entity.Property(row => row.UpdatedBy).HasMaxLength(200).IsRequired();
            entity.Property(row => row.Revision).IsConcurrencyToken();
        });
        modelBuilder.Entity<TenantAuthzRevisionRow>(entity =>
        {
            // 租户内任一角色、绑定、授权或策略安装变更都在同一事务里递增；判定快照缓存以它为键，跨宿主立即失效。
            entity.ToTable("tenant_authz_revisions"); entity.HasKey(row => row.TenantId);
            entity.Property(row => row.TenantId).HasMaxLength(200); entity.Property(row => row.UpdatedBy).HasMaxLength(200).IsRequired();
        });
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

// nullable 是平台角色的既有存储契约；当前租户过滤/RLS 不因 null 放行平台行。
internal sealed class AuthzRoleRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string? TenantId { get; set; }
    public string Code { get; set; } = "";
    public bool Builtin { get; set; }
    public string? SourcePack { get; set; }
}
internal sealed class RoleAssignmentRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string? TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string SubjectType { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public string AssignedBy { get; set; } = "";
}
internal sealed class RoleGrantRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string? TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string PluginId { get; set; } = "";
    public string PluginVersion { get; set; } = "";
    public string PermissionCode { get; set; } = "";
    public string Source { get; set; } = "";
    public string GrantedBy { get; set; } = "";
    public string? ResourceRef { get; set; }
}
internal sealed class TenantPolicyInstallRow : Kun.Core.Domain.IMultiTenant
{
    public string TenantId { get; set; } = "";
    public string PluginId { get; set; } = "";
    public string Version { get; set; } = "";
    public int Revision { get; set; }
    public string UpdatedBy { get; set; } = "";
}
internal sealed class TenantAuthzRevisionRow : Kun.Core.Domain.IMultiTenant
{
    public string TenantId { get; set; } = "";
    public long Revision { get; set; }
    public string UpdatedBy { get; set; } = "";
}

/// <summary>仅绑定专用平台授权登录角色的上下文：只看平台行（tenant_id 为 null）；普通租户上下文不会自动切换到本类型。</summary>
public sealed class PlatformAuthzDbContext(DbContextOptions<PlatformAuthzDbContext> options, ITenantContext tenant)
    : AuthzDbContext(options, tenant, true);
