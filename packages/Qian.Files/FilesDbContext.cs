using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Files;

/// <summary>qian.files 的业务上下文：file_objects 一张租户表（RLS 由 Qian 迁移生成器随迁移输出）。宿主以 AddQianData&lt;FilesDbContext&gt; 装配。</summary>
public sealed class FilesDbContext(DbContextOptions<FilesDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    /// <summary>组件 schema（plugin_ 前缀，UseKunSchema 与迁移历史表共用）。</summary>
    public const string Schema = "plugin_qian_files";
    internal DbSet<FileObject> Files => Set<FileObject>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileObject>(entity =>
        {
            entity.ToTable("file_objects");
            entity.Property(row => row.TenantId).HasMaxLength(36);
            entity.Property(row => row.PluginId).HasMaxLength(128).IsRequired();
            entity.Property(row => row.Purpose).HasMaxLength(128).IsRequired();
            entity.Property(row => row.Name).HasMaxLength(255).IsRequired();
            entity.Property(row => row.ContentType).HasMaxLength(255).IsRequired();
            entity.Property(row => row.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(row => row.StorageKey).HasMaxLength(512).IsRequired();
            entity.Property(row => row.UploadKey).HasMaxLength(512).IsRequired();
            entity.Property(row => row.Provider).HasMaxLength(32).IsRequired();
            entity.Property(row => row.Status).HasMaxLength(16).IsRequired();
            entity.Property(row => row.ScanStatus).HasMaxLength(16).IsRequired();
            entity.Property(row => row.QuarantineReason).HasMaxLength(32);
            entity.Property(row => row.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(row => row.StorageKey).IsUnique().HasDatabaseName("ux_file_objects_storage_key");
            entity.HasIndex(row => new { row.TenantId, row.IdempotencyKey }).IsUnique().HasDatabaseName(IdempotencyIndex);
            entity.HasIndex(row => new { row.TenantId, row.Purpose, row.Status }).HasDatabaseName("ix_file_objects_tenant_id_purpose_status");
        });
    }

    internal const string IdempotencyIndex = "ux_file_objects_tenant_id_idempotency_key";
}

// 元数据行：存储键与上传暂存键都由服务端生成；size / sha256 在完成前是客户端声明，完成时以服务端自算结果核对。
internal sealed class FileObject : AggregateRoot<Guid, string>, IMultiTenant
{
    public string? TenantId { get; set; }
    public string PluginId { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public string ContentType { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string UploadKey { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Status { get; set; } = FileStatus.Uploading;
    public string ScanStatus { get; set; } = FileScanStatus.Unscanned;
    public string? QuarantineReason { get; set; }
    public DateTime UploadExpiresAt { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string RequestHash { get; set; } = "";

    public FileObject() { }
    public FileObject(Guid id) { Id = id; }

    internal void SoftDelete()
    {
        Status = FileStatus.Deleted;
        Delete();
    }

    internal FileDescriptor Describe() => new(Id, TenantId!, PluginId, Purpose, Name, Size, ContentType, Sha256, StorageKey, Provider, Status, ScanStatus,
        new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc)))
    {
        QuarantineReason = QuarantineReason,
        DeletedAt = DeletedAt is { } deleted ? new DateTimeOffset(DateTime.SpecifyKind(deleted, DateTimeKind.Utc)) : null
    };
}

// 扫描本身归 S2；S1 只落字段，所有文件为 unscanned。
internal static class FileScanStatus
{
    internal const string Unscanned = "unscanned";
}
