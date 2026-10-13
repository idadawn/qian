using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Data.Migrations
{
    /// <inheritdoc />
    internal partial class DataConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_connections",
                schema: "plugin_qian_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dialect = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    host = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    database = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    user_name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    secret_ref = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    schema = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: true),
                    encoding = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_connections", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_data_connections_tenant_id",
                schema: "plugin_qian_data",
                table: "data_connections",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_data_connections_tenant_id_name",
                schema: "plugin_qian_data",
                table: "data_connections",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "NOT is_deleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_connections",
                schema: "plugin_qian_data");
        }
    }
}
