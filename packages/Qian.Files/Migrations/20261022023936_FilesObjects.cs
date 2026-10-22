using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Files.Migrations
{
    /// <inheritdoc />
    internal partial class FilesObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "plugin_qian_files");

            migrationBuilder.CreateTable(
                name: "file_objects",
                schema: "plugin_qian_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    plugin_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    purpose = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    upload_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    scan_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quarantine_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    upload_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_file_objects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "plugin_qian_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    trace_parent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    trace_state = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_objects_tenant_id",
                schema: "plugin_qian_files",
                table: "file_objects",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_file_objects_tenant_id_purpose_status",
                schema: "plugin_qian_files",
                table: "file_objects",
                columns: new[] { "tenant_id", "purpose", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_file_objects_storage_key",
                schema: "plugin_qian_files",
                table: "file_objects",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_file_objects_tenant_id_idempotency_key",
                schema: "plugin_qian_files",
                table: "file_objects",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_occurred_at",
                schema: "plugin_qian_files",
                table: "outbox_messages",
                columns: new[] { "status", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_objects",
                schema: "plugin_qian_files");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "plugin_qian_files");
        }
    }
}
