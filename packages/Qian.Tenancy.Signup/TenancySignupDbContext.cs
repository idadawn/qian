using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Tenancy.Signup;

/// <summary>
/// 开通申请、验证码挑战与申请人会话（规格 §4）。三张都是全局表，没有租户列：申请发生在租户存在之前，读写只在无租户上下文中进行。
/// 手机号只为通知保存原值，接口一律返回掩码；验证码与会话令牌只存哈希。
/// </summary>
public sealed class TenancySignupDbContext(DbContextOptions<TenancySignupDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    /// <summary>组件独占的 schema。</summary>
    public const string Schema = "plugin_qian_tenancy_signup";
    internal DbSet<SignupApplicationRow> Applications => Set<SignupApplicationRow>();
    internal DbSet<SignupVerificationRow> Verifications => Set<SignupVerificationRow>();
    internal DbSet<SignupSessionRow> Sessions => Set<SignupSessionRow>();
    // 进行中的申请：租户号预留、同一自然人只能有一条（部分唯一索引的过滤条件，与 SignupStatuses.Open 一致）。
    internal const string OpenFilter = "status IN ('submitted', 'provisioning', 'failed')";

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SignupApplicationRow>(entity =>
        {
            entity.ToTable("applications", table =>
            {
                table.HasCheckConstraint("ck_applications_status", "status IN ('submitted', 'provisioning', 'approved', 'failed', 'rejected', 'withdrawn')");
                table.HasCheckConstraint("ck_applications_code", "code ~ '^[a-z0-9]+$' AND length(code) <= 64");
                table.HasCheckConstraint("ck_applications_plan", "plan IS NULL OR plan IN ('trial', 'formal')");
            });
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => row.Number).IsUnique();
            entity.HasIndex(row => row.Code).IsUnique().HasFilter(OpenFilter).HasDatabaseName("ux_applications_open_code");
            entity.HasIndex(row => row.PersonId).IsUnique().HasFilter(OpenFilter).HasDatabaseName("ux_applications_open_person");
            entity.HasIndex(row => new { row.PersonId, row.SubmittedAt });
            entity.HasIndex(row => new { row.Status, row.SubmittedAt });
            entity.Property(row => row.Number).HasMaxLength(32);
            entity.Property(row => row.Status).HasMaxLength(16);
            entity.Property(row => row.CompanyName).HasMaxLength(200);
            entity.Property(row => row.Code).HasMaxLength(64);
            entity.Property(row => row.Industry).HasMaxLength(64);
            entity.Property(row => row.Size).HasMaxLength(16);
            entity.Property(row => row.Description).HasMaxLength(2000);
            entity.Property(row => row.ContactName).HasMaxLength(200);
            entity.Property(row => row.Phone).HasMaxLength(32);
            entity.Property(row => row.Email).HasMaxLength(254);
            entity.Property(row => row.Plan).HasMaxLength(16);
            entity.Property(row => row.RejectReason).HasMaxLength(500);
            entity.Property(row => row.ReviewNote).HasMaxLength(2000);
            entity.Property(row => row.Reviewer).HasMaxLength(200);
            entity.Property(row => row.FailureReason).HasMaxLength(64);
            entity.Property(row => row.Version).IsConcurrencyToken();
        });
        modelBuilder.Entity<SignupVerificationRow>(entity =>
        {
            entity.ToTable("verifications");
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => new { row.Phone, row.CreatedAt });
            entity.HasIndex(row => new { row.Source, row.CreatedAt });
            entity.Property(row => row.Phone).HasMaxLength(32);
            entity.Property(row => row.Source).HasMaxLength(64);
        });
        modelBuilder.Entity<SignupSessionRow>(entity =>
        {
            entity.ToTable("sessions");
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => row.TokenHash).IsUnique();
            entity.HasIndex(row => row.ExpiresAt);
            entity.Property(row => row.Phone).HasMaxLength(32);
        });
    }
}

internal sealed class SignupApplicationRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Number { get; set; } = "";
    public Guid PersonId { get; set; }
    public string Status { get; set; } = SignupStatuses.Submitted;
    public string CompanyName { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Industry { get; set; }
    public string Size { get; set; } = "";
    public string? Description { get; set; }
    public string ContactName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public bool ExistingAccount { get; set; }
    public string? Plan { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    public int? Users { get; set; }
    public string? RejectReason { get; set; }
    public string? ReviewNote { get; set; }
    public string? Reviewer { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ProvisionedTenantId { get; set; }
    public Guid? MembershipId { get; set; }
    public string? FailureReason { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int Version { get; set; }
}

internal sealed class SignupVerificationRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Phone { get; set; } = "";
    public byte[] CodeSalt { get; set; } = [];
    public byte[] CodeHash { get; set; } = [];
    public int Attempts { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public string Source { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class SignupSessionRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public byte[] TokenHash { get; set; } = [];
    public string Phone { get; set; } = "";
    public Guid? PersonId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal static class SignupStatuses
{
    internal const string Submitted = "submitted";
    internal const string Provisioning = "provisioning";
    internal const string Approved = "approved";
    internal const string Failed = "failed";
    internal const string Rejected = "rejected";
    internal const string Withdrawn = "withdrawn";
    internal static readonly string[] Open = [Submitted, Provisioning, Failed];

    internal static string Name(SignupApplicationStatus status) => status switch
    {
        SignupApplicationStatus.Submitted => Submitted, SignupApplicationStatus.Provisioning => Provisioning, SignupApplicationStatus.Approved => Approved,
        SignupApplicationStatus.Failed => Failed, SignupApplicationStatus.Rejected => Rejected, SignupApplicationStatus.Withdrawn => Withdrawn,
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    internal static SignupApplicationStatus Parse(string status) => status switch
    {
        Submitted => SignupApplicationStatus.Submitted, Provisioning => SignupApplicationStatus.Provisioning, Approved => SignupApplicationStatus.Approved,
        Failed => SignupApplicationStatus.Failed, Rejected => SignupApplicationStatus.Rejected, Withdrawn => SignupApplicationStatus.Withdrawn,
        _ => throw new InvalidOperationException("未知申请状态。")
    };
}
