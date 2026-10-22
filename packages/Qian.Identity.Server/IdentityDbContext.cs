using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Identity.Server;

/// <summary>唯一发行方的协议与账号存储；必须经 Qian.Data 安全装配并使用独立受限数据库角色。</summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    /// <summary>遵循组件的数据 schema 命名规则。</summary>
    public const string Schema = "plugin_qian_identity";
    internal DbSet<LogoutDelivery> LogoutDeliveries => Set<LogoutDelivery>();
    internal DbSet<PersonRow> Persons => Set<PersonRow>();
    internal DbSet<PersonIdentifierRow> Identifiers => Set<PersonIdentifierRow>();
    internal DbSet<PasswordHistoryRow> PasswordHistory => Set<PasswordHistoryRow>();
    internal DbSet<MembershipRow> Memberships => Set<MembershipRow>();
    internal DbSet<ExternalIdentityRow> ExternalIdentities => Set<ExternalIdentityRow>();
    internal DbSet<ClientRow> Clients => Set<ClientRow>();
    internal DbSet<SessionRow> Sessions => Set<SessionRow>();
    internal DbSet<LoginAuditRow> LoginAudits => Set<LoginAuditRow>();
    internal DbSet<LoginTicketRow> LoginTickets => Set<LoginTicketRow>();
    internal DbSet<CaptchaRow> Captchas => Set<CaptchaRow>();
    internal DbSet<ConnectorFlowRow> ConnectorFlows => Set<ConnectorFlowRow>();
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
        modelBuilder.Entity<PersonRow>(entity =>
        {
            entity.ToTable("persons", table =>
            {
                table.HasCheckConstraint("ck_persons_status", "status IN ('active', 'disabled', 'locked', 'purged')");
                table.HasCheckConstraint("ck_persons_password_hash", "password_hash IS NULL OR password_hash LIKE '$argon2id$%'");
            });
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(row => row.Status).HasMaxLength(16).IsRequired();
            entity.Property(row => row.PasswordHash).HasMaxLength(512);
            entity.Property(row => row.MfaFactors).HasColumnType("jsonb").HasDefaultValue("[]");
            entity.Property(row => row.TenantIds).HasColumnType("text[]");
            entity.Property(row => row.CreatedBy).HasMaxLength(200); entity.Property(row => row.UpdatedBy).HasMaxLength(200);
        });
        modelBuilder.Entity<PersonIdentifierRow>(entity =>
        {
            entity.ToTable("person_identifiers", table => table.HasCheckConstraint("ck_person_identifiers_kind", "kind IN ('phone', 'email', 'username')"));
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Kind).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Value).HasMaxLength(320).IsRequired();
            entity.Property(row => row.CreatedAt).HasDefaultValueSql("now()");
            // 同一标识同一时刻只属于一个有效 Person；解绑（软删除）后释放。
            entity.HasIndex(row => new { row.Kind, row.Value }).IsUnique().HasFilter("is_deleted = false");
            entity.HasIndex(row => row.PersonId);
            entity.HasOne<PersonRow>().WithMany().HasForeignKey(row => row.PersonId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PasswordHistoryRow>(entity =>
        {
            entity.ToTable("password_history"); entity.HasKey(row => row.Id);
            entity.Property(row => row.Hash).HasMaxLength(512).IsRequired();
            entity.HasIndex(row => new { row.PersonId, row.CreatedAt });
            entity.HasOne<PersonRow>().WithMany().HasForeignKey(row => row.PersonId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<MembershipRow>(entity =>
        {
            entity.ToTable("memberships", table => table.HasCheckConstraint("ck_memberships_status", "status IN ('active', 'disabled', 'locked', 'purged')"));
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.TenantId).HasMaxLength(36);
            entity.Property(row => row.Account).HasMaxLength(64).IsRequired();
            entity.Property(row => row.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(row => row.Status).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Profile).HasMaxLength(64);
            entity.Property(row => row.CreatedBy).HasMaxLength(200); entity.Property(row => row.UpdatedBy).HasMaxLength(200);
            entity.HasIndex(row => new { row.TenantId, row.PersonId }).IsUnique();
            entity.HasIndex(row => new { row.TenantId, row.Account }).IsUnique();
            entity.HasOne<PersonRow>().WithMany().HasForeignKey(row => row.PersonId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ExternalIdentityRow>(entity =>
        {
            entity.ToTable("external_identities"); entity.HasKey(row => row.Id);
            entity.Property(row => row.TenantId).HasMaxLength(36);
            entity.Property(row => row.ConnectorId).HasMaxLength(100).IsRequired();
            entity.Property(row => row.Subject).HasMaxLength(256).IsRequired();
            entity.Property(row => row.UnionId).HasMaxLength(256);
            entity.Property(row => row.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(row => new { row.TenantId, row.ConnectorId, row.Subject }).IsUnique().HasFilter("is_deleted = false");
            entity.HasIndex(row => row.MembershipId);
            entity.HasOne<MembershipRow>().WithMany().HasForeignKey(row => row.MembershipId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PersonRow>().WithMany().HasForeignKey(row => row.PersonId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ClientRow>(entity =>
        {
            entity.ToTable("clients", table => table.HasCheckConstraint("ck_clients_type", "type IN ('confidential', 'public', 'machine', 'agent')"));
            entity.HasKey(row => row.ClientId);
            entity.Property(row => row.ClientId).HasMaxLength(100);
            entity.Property(row => row.Type).HasMaxLength(16).IsRequired();
            entity.Property(row => row.DeviceType).HasMaxLength(16).IsRequired();
            entity.Property(row => row.RedirectUris).HasColumnType("jsonb"); entity.Property(row => row.Scopes).HasColumnType("jsonb"); entity.Property(row => row.Resources).HasColumnType("jsonb");
            entity.Property(row => row.TenantId).HasMaxLength(36).IsRequired(false);
            entity.Property(row => row.PluginId).HasMaxLength(128);
            entity.Property(row => row.Status).HasMaxLength(16).IsRequired();
        });
        modelBuilder.Entity<SessionRow>(entity =>
        {
            entity.ToTable("sessions"); entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.TenantId).HasMaxLength(36).IsRequired(false);
            entity.Property(row => row.ClientId).HasMaxLength(100).IsRequired();
            entity.Property(row => row.DeviceType).HasMaxLength(16).IsRequired();
            entity.Property(row => row.Acr).HasMaxLength(16);
            entity.Property(row => row.RemoteAddress).HasMaxLength(64); entity.Property(row => row.UserAgent).HasMaxLength(256);
            entity.Property(row => row.RevokeReason).HasMaxLength(32);
            entity.Property(row => row.RevokedBy).HasMaxLength(300);
            entity.HasIndex(row => row.PersonId).HasFilter("revoked_at IS NULL");
            entity.HasIndex(row => row.TenantId).HasFilter("revoked_at IS NULL");
            // 待处理的吊销旁路：处理器按吊销时间顺序领取，处理完成后不再出现在索引里。
            entity.HasIndex(row => row.RevokedAt).HasFilter("revoked_at IS NOT NULL AND revocation_processed_at IS NULL").HasDatabaseName("ix_sessions_pending_revocation");
            entity.HasOne<PersonRow>().WithMany().HasForeignKey(row => row.PersonId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LoginAuditRow>(entity =>
        {
            entity.ToTable("login_audits"); entity.HasKey(row => row.Id);
            entity.Property(row => row.Stage).HasMaxLength(24).IsRequired();
            entity.Property(row => row.Outcome).HasMaxLength(32).IsRequired();
            entity.Property(row => row.Reason).HasMaxLength(64);
            entity.Property(row => row.TenantId).HasMaxLength(36).IsRequired(false);
            entity.Property(row => row.IdentifierKind).HasMaxLength(16); entity.Property(row => row.IdentifierMasked).HasMaxLength(64);
            entity.Property(row => row.ClientId).HasMaxLength(100); entity.Property(row => row.Method).HasMaxLength(64);
            entity.Property(row => row.RemoteAddress).HasMaxLength(64); entity.Property(row => row.UserAgent).HasMaxLength(256);
            entity.Property(row => row.SessionHash).HasMaxLength(16); entity.Property(row => row.TraceId).HasMaxLength(32);
            entity.HasIndex(row => new { row.PersonId, row.OccurredAt }); entity.HasIndex(row => row.OccurredAt);
        });
        modelBuilder.Entity<LoginTicketRow>(entity =>
        {
            entity.ToTable("login_tickets", table => table.HasCheckConstraint("ck_login_tickets_purpose", "purpose IN ('selection', 'password_change', 'connector')"));
            entity.HasKey(row => row.TokenHash);
            entity.Property(row => row.Purpose).HasMaxLength(16).IsRequired();
            entity.Property(row => row.ClientId).HasMaxLength(100).IsRequired();
            entity.Property(row => row.DeviceType).HasMaxLength(16).IsRequired();
            entity.Property(row => row.TenantId).HasMaxLength(36).IsRequired(false);
            entity.Property(row => row.Choices).HasColumnType("text[]");
            entity.Property(row => row.Acr).HasMaxLength(16);
            entity.Property(row => row.Reason).HasMaxLength(64);
            entity.Property(row => row.RemoteAddress).HasMaxLength(64); entity.Property(row => row.UserAgent).HasMaxLength(256);
            entity.HasIndex(row => row.ExpiresAt);
        });
        modelBuilder.Entity<CaptchaRow>(entity =>
        {
            entity.ToTable("login_captchas"); entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.HasIndex(row => row.ExpiresAt);
        });
        modelBuilder.Entity<ConnectorFlowRow>(entity =>
        {
            entity.ToTable("connector_flows", table => table.HasCheckConstraint("ck_connector_flows_status",
                "status IN ('pending', 'processing', 'bind', 'completed', 'failed', 'cancelled')"));
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.ConnectorId).HasMaxLength(64).IsRequired();
            entity.Property(row => row.TenantId).HasMaxLength(36);
            entity.Property(row => row.ClientId).HasMaxLength(100).IsRequired();
            entity.Property(row => row.DeviceType).HasMaxLength(16).IsRequired();
            entity.Property(row => row.ReturnUrl).HasMaxLength(2048);
            entity.Property(row => row.Nonce).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Status).HasMaxLength(16).IsRequired();
            entity.Property(row => row.ErrorCode).HasMaxLength(64); entity.Property(row => row.ErrorDetail).HasMaxLength(200);
            entity.Property(row => row.ExternalIssuer).HasMaxLength(512); entity.Property(row => row.ExternalSubject).HasMaxLength(256);
            entity.Property(row => row.ExternalUnionId).HasMaxLength(256); entity.Property(row => row.ExternalProfile).HasColumnType("jsonb");
            entity.Property(row => row.RemoteAddress).HasMaxLength(64); entity.Property(row => row.UserAgent).HasMaxLength(256);
            entity.HasIndex(row => row.StateHash).IsUnique();
            entity.HasIndex(row => row.ExpiresAt);
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
