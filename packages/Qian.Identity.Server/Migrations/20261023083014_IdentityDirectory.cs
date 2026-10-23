using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Identity.Server.Migrations
{
    /// <inheritdoc />
    internal partial class IdentityDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "directory_deleted",
                schema: "plugin_qian_identity",
                table: "memberships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_memberships_tenant_id_id",
                schema: "plugin_qian_identity",
                table: "memberships",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "groups",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groups", x => x.id);
                    table.UniqueConstraint("ak_groups_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "org_types",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_types", x => new { x.tenant_id, x.code });
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pinyin = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    extensions = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_profiles", x => new { x.tenant_id, x.membership_id });
                    table.ForeignKey(
                        name: "FK_user_profiles_memberships_tenant_id_membership_id",
                        columns: x => new { x.tenant_id, x.membership_id },
                        principalSchema: "plugin_qian_identity",
                        principalTable: "memberships",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_relations",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position_id = table.Column<Guid>(type: "uuid", nullable: true),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_relations", x => x.id);
                    table.CheckConstraint("ck_member_relations_target", "(target_type = 'organization' AND organization_id IS NOT NULL AND position_id IS NULL AND group_id IS NULL) OR (target_type = 'position' AND organization_id IS NULL AND position_id IS NOT NULL AND group_id IS NULL) OR (target_type = 'group' AND organization_id IS NULL AND position_id IS NULL AND group_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_member_relations_groups_tenant_id_group_id",
                        columns: x => new { x.tenant_id, x.group_id },
                        principalSchema: "plugin_qian_identity",
                        principalTable: "groups",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_member_relations_memberships_tenant_id_membership_id",
                        columns: x => new { x.tenant_id, x.membership_id },
                        principalSchema: "plugin_qian_identity",
                        principalTable: "memberships",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    path = table.Column<string>(type: "character varying(334)", maxLength: 334, nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    manager_membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responsible_position_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                    table.UniqueConstraint("ak_organizations_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_organizations_depth", "depth BETWEEN 1 AND 9");
                    table.ForeignKey(
                        name: "FK_organizations_memberships_tenant_id_manager_membership_id",
                        columns: x => new { x.tenant_id, x.manager_membership_id },
                        principalSchema: "plugin_qian_identity",
                        principalTable: "memberships",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organizations_org_types_tenant_id_type_code",
                        columns: x => new { x.tenant_id, x.type_code },
                        principalSchema: "plugin_qian_identity",
                        principalTable: "org_types",
                        principalColumns: new[] { "tenant_id", "code" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organizations_organizations_tenant_id_parent_id",
                        columns: x => new { x.tenant_id, x.parent_id },
                        principalSchema: "plugin_qian_identity",
                        principalTable: "organizations",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    path = table.Column<string>(type: "text", nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_responsible = table.Column<bool>(type: "boolean", nullable: false),
                    responsible_membership_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions", x => x.id);
                    table.UniqueConstraint("ak_positions_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_positions_depth", "depth >= 1");
                    table.ForeignKey(
                        name: "FK_positions_memberships_tenant_id_responsible_membership_id",
                        columns: x => new { x.tenant_id, x.responsible_membership_id },
                        principalSchema: "plugin_qian_identity",
                        principalTable: "memberships",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_positions_organizations_tenant_id_organization_id",
                        columns: x => new { x.tenant_id, x.organization_id },
                        principalSchema: "plugin_qian_identity",
                        principalTable: "organizations",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_positions_positions_tenant_id_parent_id",
                        columns: x => new { x.tenant_id, x.parent_id },
                        principalSchema: "plugin_qian_identity",
                        principalTable: "positions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_groups_tenant_id",
                schema: "plugin_qian_identity",
                table: "groups",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_groups_tenant_id_name",
                schema: "plugin_qian_identity",
                table: "groups",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_member_relations_primary",
                schema: "plugin_qian_identity",
                table: "member_relations",
                columns: new[] { "tenant_id", "membership_id", "target_type" },
                unique: true,
                filter: "is_primary");

            migrationBuilder.CreateIndex(
                name: "ix_member_relations_tenant_id",
                schema: "plugin_qian_identity",
                table: "member_relations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_member_relations_tenant_id_group_id",
                schema: "plugin_qian_identity",
                table: "member_relations",
                columns: new[] { "tenant_id", "group_id" });

            migrationBuilder.CreateIndex(
                name: "ix_member_relations_tenant_id_membership_id_group_id",
                schema: "plugin_qian_identity",
                table: "member_relations",
                columns: new[] { "tenant_id", "membership_id", "group_id" },
                unique: true,
                filter: "group_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_member_relations_tenant_id_membership_id_organization_id",
                schema: "plugin_qian_identity",
                table: "member_relations",
                columns: new[] { "tenant_id", "membership_id", "organization_id" },
                unique: true,
                filter: "organization_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_member_relations_tenant_id_membership_id_position_id",
                schema: "plugin_qian_identity",
                table: "member_relations",
                columns: new[] { "tenant_id", "membership_id", "position_id" },
                unique: true,
                filter: "position_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_member_relations_tenant_id_organization_id",
                schema: "plugin_qian_identity",
                table: "member_relations",
                columns: new[] { "tenant_id", "organization_id" });

            migrationBuilder.CreateIndex(
                name: "ix_member_relations_tenant_id_position_id",
                schema: "plugin_qian_identity",
                table: "member_relations",
                columns: new[] { "tenant_id", "position_id" });

            migrationBuilder.CreateIndex(
                name: "ix_org_types_tenant_id",
                schema: "plugin_qian_identity",
                table: "org_types",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_single_root",
                schema: "plugin_qian_identity",
                table: "organizations",
                column: "tenant_id",
                unique: true,
                filter: "parent_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_tenant_id_manager_membership_id",
                schema: "plugin_qian_identity",
                table: "organizations",
                columns: new[] { "tenant_id", "manager_membership_id" });

            migrationBuilder.CreateIndex(
                name: "ix_organizations_tenant_id_parent_id",
                schema: "plugin_qian_identity",
                table: "organizations",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_organizations_tenant_id_path",
                schema: "plugin_qian_identity",
                table: "organizations",
                columns: new[] { "tenant_id", "path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_tenant_id_responsible_position_id",
                schema: "plugin_qian_identity",
                table: "organizations",
                columns: new[] { "tenant_id", "responsible_position_id" });

            migrationBuilder.CreateIndex(
                name: "ix_organizations_tenant_id_type_code",
                schema: "plugin_qian_identity",
                table: "organizations",
                columns: new[] { "tenant_id", "type_code" });

            migrationBuilder.CreateIndex(
                name: "ix_positions_default",
                schema: "plugin_qian_identity",
                table: "positions",
                columns: new[] { "tenant_id", "organization_id" },
                unique: true,
                filter: "is_default");

            migrationBuilder.CreateIndex(
                name: "ix_positions_tenant_id",
                schema: "plugin_qian_identity",
                table: "positions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_positions_tenant_id_parent_id",
                schema: "plugin_qian_identity",
                table: "positions",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_positions_tenant_id_path",
                schema: "plugin_qian_identity",
                table: "positions",
                columns: new[] { "tenant_id", "path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_positions_tenant_id_responsible_membership_id",
                schema: "plugin_qian_identity",
                table: "positions",
                columns: new[] { "tenant_id", "responsible_membership_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_tenant_id",
                schema: "plugin_qian_identity",
                table: "user_profiles",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_member_relations_organizations_tenant_id_organization_id",
                schema: "plugin_qian_identity",
                table: "member_relations",
                columns: new[] { "tenant_id", "organization_id" },
                principalSchema: "plugin_qian_identity",
                principalTable: "organizations",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_member_relations_positions_tenant_id_position_id",
                schema: "plugin_qian_identity",
                table: "member_relations",
                columns: new[] { "tenant_id", "position_id" },
                principalSchema: "plugin_qian_identity",
                principalTable: "positions",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_organizations_positions_tenant_id_responsible_position_id",
                schema: "plugin_qian_identity",
                table: "organizations",
                columns: new[] { "tenant_id", "responsible_position_id" },
                principalSchema: "plugin_qian_identity",
                principalTable: "positions",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_positions_organizations_tenant_id_organization_id",
                schema: "plugin_qian_identity",
                table: "positions");

            migrationBuilder.DropTable(
                name: "member_relations",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "user_profiles",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "groups",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "org_types",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "positions",
                schema: "plugin_qian_identity");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_memberships_tenant_id_id",
                schema: "plugin_qian_identity",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "directory_deleted",
                schema: "plugin_qian_identity",
                table: "memberships");
        }
    }
}
