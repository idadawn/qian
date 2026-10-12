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

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
        });
    }
}

// 撤销保留内部墓碑和修订，避免删除/重建后接受旧客户端的相同修订（ABA）。
internal sealed class ScopePatchRow : IMultiTenant
{
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
}
