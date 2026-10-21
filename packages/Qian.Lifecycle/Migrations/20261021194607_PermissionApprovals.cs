using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Lifecycle.Migrations
{
    /// <inheritdoc />
    internal partial class PermissionApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permission_approvals",
                schema: "plugin_qian_lifecycle",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    plugin_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    approved = table.Column<string>(type: "jsonb", nullable: false),
                    approved_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    approved_via = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_approvals", x => new { x.tenant_id, x.plugin_id, x.version });
                });

            migrationBuilder.CreateIndex(
                name: "ix_permission_approvals_tenant_id",
                schema: "plugin_qian_lifecycle",
                table: "permission_approvals",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permission_approvals",
                schema: "plugin_qian_lifecycle");
        }
    }
}
