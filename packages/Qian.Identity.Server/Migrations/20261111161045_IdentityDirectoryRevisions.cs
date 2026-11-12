using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Identity.Server.Migrations
{
    /// <inheritdoc />
    internal partial class IdentityDirectoryRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "directory_revisions",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_directory_revisions", x => x.tenant_id);
                    table.CheckConstraint("ck_directory_revisions_revision", "revision >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_directory_revisions_tenant_id",
                schema: "plugin_qian_identity",
                table: "directory_revisions",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "directory_revisions",
                schema: "plugin_qian_identity");
        }
    }
}
