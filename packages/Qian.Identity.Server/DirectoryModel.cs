using Microsoft.EntityFrameworkCore;

namespace Qian.Identity.Server;

internal static class DirectoryModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<MembershipRow>().HasAlternateKey(row => new { row.TenantId, row.Id });
        model.Entity<OrganizationTypeRow>(entity =>
        {
            entity.ToTable("org_types"); entity.HasKey(row => new { row.TenantId, row.Code });
            entity.Property(row => row.TenantId).HasMaxLength(36); entity.Property(row => row.Code).HasMaxLength(64); entity.Property(row => row.Name).HasMaxLength(200);
        });
        model.Entity<OrganizationRow>(entity =>
        {
            entity.ToTable("organizations", table => table.HasCheckConstraint("ck_organizations_depth", "depth BETWEEN 1 AND 9"));
            entity.HasKey(row => row.Id); entity.HasAlternateKey(row => new { row.TenantId, row.Id });
            entity.Property(row => row.TenantId).HasMaxLength(36); entity.Property(row => row.Name).HasMaxLength(200);
            entity.Property(row => row.TypeCode).HasMaxLength(64); entity.Property(row => row.Path).HasMaxLength(334);
            entity.HasIndex(row => new { row.TenantId, row.Path }).IsUnique();
            entity.HasIndex(row => row.TenantId).IsUnique().HasFilter("parent_id IS NULL").HasDatabaseName("ix_organizations_single_root");
            entity.HasOne<OrganizationTypeRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.TypeCode }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OrganizationRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.ParentId }).HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MembershipRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.ManagerMembershipId }).HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PositionRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.ResponsiblePositionId }).HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PositionRow>(entity =>
        {
            entity.ToTable("positions", table => table.HasCheckConstraint("ck_positions_depth", "depth >= 1"));
            entity.HasKey(row => row.Id); entity.HasAlternateKey(row => new { row.TenantId, row.Id });
            entity.Property(row => row.TenantId).HasMaxLength(36); entity.Property(row => row.Name).HasMaxLength(200); entity.Property(row => row.Path).HasColumnType("text");
            entity.HasIndex(row => new { row.TenantId, row.Path }).IsUnique();
            entity.HasIndex(row => new { row.TenantId, row.OrganizationId }).IsUnique().HasFilter("is_default").HasDatabaseName("ix_positions_default");
            entity.HasOne<OrganizationRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.OrganizationId }).HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PositionRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.ParentId }).HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MembershipRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.ResponsibleMembershipId }).HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DirectoryGroupRow>(entity =>
        {
            entity.ToTable("groups"); entity.HasKey(row => row.Id); entity.HasAlternateKey(row => new { row.TenantId, row.Id });
            entity.Property(row => row.TenantId).HasMaxLength(36); entity.Property(row => row.Name).HasMaxLength(200); entity.HasIndex(row => new { row.TenantId, row.Name }).IsUnique();
        });
        model.Entity<MemberRelationRow>(entity =>
        {
            entity.ToTable("member_relations", table => table.HasCheckConstraint("ck_member_relations_target", "(target_type = 'organization' AND organization_id IS NOT NULL AND position_id IS NULL AND group_id IS NULL) OR (target_type = 'position' AND organization_id IS NULL AND position_id IS NOT NULL AND group_id IS NULL) OR (target_type = 'group' AND organization_id IS NULL AND position_id IS NULL AND group_id IS NOT NULL)"));
            entity.HasKey(row => row.Id); entity.Property(row => row.TenantId).HasMaxLength(36); entity.Property(row => row.TargetType).HasMaxLength(16);
            entity.HasIndex(row => new { row.TenantId, row.MembershipId, row.TargetType }).IsUnique().HasFilter("is_primary").HasDatabaseName("ix_member_relations_primary");
            entity.HasIndex(row => new { row.TenantId, row.MembershipId, row.OrganizationId }).IsUnique().HasFilter("organization_id IS NOT NULL");
            entity.HasIndex(row => new { row.TenantId, row.MembershipId, row.PositionId }).IsUnique().HasFilter("position_id IS NOT NULL");
            entity.HasIndex(row => new { row.TenantId, row.MembershipId, row.GroupId }).IsUnique().HasFilter("group_id IS NOT NULL");
            entity.HasOne<MembershipRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.MembershipId }).HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OrganizationRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.OrganizationId }).HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PositionRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.PositionId }).HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DirectoryGroupRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.GroupId }).HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<UserProfileRow>(entity =>
        {
            entity.ToTable("user_profiles"); entity.HasKey(row => new { row.TenantId, row.MembershipId });
            entity.Property(row => row.TenantId).HasMaxLength(36); entity.Property(row => row.Name).HasMaxLength(200); entity.Property(row => row.Pinyin).HasMaxLength(200);
            entity.Property(row => row.Phone).HasMaxLength(32); entity.Property(row => row.Email).HasMaxLength(320); entity.Property(row => row.Extensions).HasColumnType("jsonb");
            entity.HasOne<MembershipRow>().WithMany().HasForeignKey(row => new { row.TenantId, row.MembershipId }).HasPrincipalKey(row => new { row.TenantId, row.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
