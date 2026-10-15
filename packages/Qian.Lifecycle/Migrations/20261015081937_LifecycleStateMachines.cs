using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Lifecycle.Migrations
{
    /// <inheritdoc />
    internal partial class LifecycleStateMachines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "plugin_qian_lifecycle");

            migrationBuilder.CreateTable(
                name: "lifecycle_runs",
                schema: "plugin_qian_lifecycle",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    plugin_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    hook = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    to_version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    from_version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lifecycle_runs", x => new { x.tenant_id, x.plugin_id, x.hook, x.to_version });
                });

            migrationBuilder.CreateTable(
                name: "mount_failures",
                schema: "plugin_qian_lifecycle",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scope = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    entry_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    plugin = table.Column<string>(type: "character varying(330)", maxLength: 330, nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mount_failures", x => new { x.tenant_id, x.scope, x.entry_id });
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "plugin_qian_lifecycle",
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

            migrationBuilder.CreateTable(
                name: "plugin_revisions",
                schema: "plugin_qian_lifecycle",
                columns: table => new
                {
                    plugin_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    artifact_digest = table.Column<string>(type: "character varying(71)", maxLength: 71, nullable: false),
                    form = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    signed_by = table.Column<string>(type: "character varying(71)", maxLength: 71, nullable: false),
                    required = table.Column<bool>(type: "boolean", nullable: false),
                    workload = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: true),
                    deployment_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    service_port = table.Column<int>(type: "integer", nullable: true),
                    health_path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    health = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    health_detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    health_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    installed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    installed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plugin_revisions", x => new { x.plugin_id, x.revision });
                });

            migrationBuilder.CreateTable(
                name: "tenant_plugin_installs",
                schema: "plugin_qian_lifecycle",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    plugin_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    target_version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_plugin_installs", x => new { x.tenant_id, x.plugin_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_lifecycle_runs_tenant_id",
                schema: "plugin_qian_lifecycle",
                table: "lifecycle_runs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_mount_failures_tenant_id",
                schema: "plugin_qian_lifecycle",
                table: "mount_failures",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_occurred_at",
                schema: "plugin_qian_lifecycle",
                table: "outbox_messages",
                columns: new[] { "status", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_plugin_revisions_plugin_id_artifact_digest",
                schema: "plugin_qian_lifecycle",
                table: "plugin_revisions",
                columns: new[] { "plugin_id", "artifact_digest" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plugin_revisions_plugin_id_version",
                schema: "plugin_qian_lifecycle",
                table: "plugin_revisions",
                columns: new[] { "plugin_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_plugin_revisions_active",
                schema: "plugin_qian_lifecycle",
                table: "plugin_revisions",
                column: "plugin_id",
                unique: true,
                filter: "state = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_plugin_installs_tenant_id",
                schema: "plugin_qian_lifecycle",
                table: "tenant_plugin_installs",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lifecycle_runs",
                schema: "plugin_qian_lifecycle");

            migrationBuilder.DropTable(
                name: "mount_failures",
                schema: "plugin_qian_lifecycle");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "plugin_qian_lifecycle");

            migrationBuilder.DropTable(
                name: "plugin_revisions",
                schema: "plugin_qian_lifecycle");

            migrationBuilder.DropTable(
                name: "tenant_plugin_installs",
                schema: "plugin_qian_lifecycle");
        }
    }
}
