using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Capabilities.Migrations
{
    /// <inheritdoc />
    internal partial class NavigationAndInvokeReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "plugin_qian_capabilities");

            migrationBuilder.CreateTable(
                name: "invoke_receipts",
                schema: "plugin_qian_capabilities",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    capability_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoke_receipts", x => new { x.tenant_id, x.subject_key, x.capability_id, x.idempotency_key });
                });

            migrationBuilder.CreateTable(
                name: "navigation_preferences",
                schema: "plugin_qian_capabilities",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    resource_ref = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    hidden = table.Column<bool>(type: "boolean", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_navigation_preferences", x => new { x.tenant_id, x.subject_key, x.resource_key });
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "plugin_qian_capabilities",
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
                name: "ix_invoke_receipts_tenant_id",
                schema: "plugin_qian_capabilities",
                table: "invoke_receipts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_invoke_receipts_tenant_id_expires_at",
                schema: "plugin_qian_capabilities",
                table: "invoke_receipts",
                columns: new[] { "tenant_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_navigation_preferences_tenant_id",
                schema: "plugin_qian_capabilities",
                table: "navigation_preferences",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_occurred_at",
                schema: "plugin_qian_capabilities",
                table: "outbox_messages",
                columns: new[] { "status", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoke_receipts",
                schema: "plugin_qian_capabilities");

            migrationBuilder.DropTable(
                name: "navigation_preferences",
                schema: "plugin_qian_capabilities");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "plugin_qian_capabilities");
        }
    }
}
