using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Qian.Data;

namespace Qian.Tenancy;

/// <summary>租户控制面拥有的主档与管理员指针；Membership 内容由 Identity 拥有。</summary>
public sealed class TenancyDbContext(DbContextOptions<TenancyDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    /// <summary>与 Data 的插件 schema 约定一致。</summary>
    public const string Schema = "plugin_qian_tenancy";
    internal DbSet<TenantRow> Tenants => Set<TenantRow>();
    internal DbSet<TenantAdminRow> Administrators => Set<TenantAdminRow>();
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantRow>(entity =>
        {
            entity.ToTable("tenants", table =>
            {
                table.HasCheckConstraint("ck_tenants_code", "code ~ '^[a-z0-9]+$' AND length(code) <= 64");
                table.HasCheckConstraint("ck_tenants_isolation", "isolation = 'row'");
            });
            entity.HasKey(x => x.Id); entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.Property(x => x.Isolation).HasMaxLength(16).IsRequired().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
        });
        modelBuilder.Entity<TenantAdminRow>(entity =>
        {
            entity.ToTable("tenant_admins"); entity.HasKey(x => x.TenantId);
            entity.Property(x => x.TenantId).HasMaxLength(36);
            entity.Property(x => x.MembershipId).IsRequired();
        });
    }
}

internal sealed class TenantRow : IAuditedObject<string>
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Isolation { get; set; } = "row";
    public string Status { get; set; } = "pending";
}
internal sealed class TenantAdminRow : IMultiTenant, IAuditedObject<string>
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string TenantId { get; set; } = "";
    public Guid MembershipId { get; set; }
}
