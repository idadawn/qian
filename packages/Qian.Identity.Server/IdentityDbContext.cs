using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Identity.Server;

/// <summary>唯一发行方的协议存储；必须经 Qian.Data 安全装配并使用独立受限数据库角色。</summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    /// <summary>遵循组件的数据 schema 命名规则。</summary>
    public const string Schema = "plugin_qian_identity";
    internal DbSet<LogoutDelivery> LogoutDeliveries => Set<LogoutDelivery>();
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseOpenIddict();
        modelBuilder.Entity<LogoutDelivery>(entity =>
        {
            entity.ToTable("logout_deliveries"); entity.HasKey(row => row.Id);
            entity.Property(row => row.ClientId).HasMaxLength(100); entity.Property(row => row.Subject).HasMaxLength(200);
            entity.Property(row => row.SessionId).HasMaxLength(36);
            entity.HasIndex(row => new { row.ClientId, row.SessionId }).IsUnique();
        });
    }
}

internal sealed class LogoutDelivery
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string ClientId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string SessionId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt { get; set; }
    public int Attempts { get; set; }
}
