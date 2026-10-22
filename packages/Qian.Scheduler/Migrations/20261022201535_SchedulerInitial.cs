using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Scheduler.Migrations
{
    /// <inheritdoc />
    internal partial class SchedulerInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "plugin_qian_scheduler");

            migrationBuilder.CreateTable(
                name: "job_definitions",
                schema: "plugin_qian_scheduler",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    target = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    schedule = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    options = table.Column<string>(type: "jsonb", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_definitions", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "plugin_qian_scheduler",
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
                name: "job_triggers",
                schema: "plugin_qian_scheduler",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    next_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    run_count = table.Column<long>(type: "bigint", nullable: false),
                    lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    generation = table.Column<long>(type: "bigint", nullable: false),
                    active_run_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_triggers", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_job_trigger_state", "state IN ('ready','paused','archived')");
                    table.ForeignKey(
                        name: "FK_job_triggers_job_definitions_tenant_id_id",
                        columns: x => new { x.tenant_id, x.id },
                        principalSchema: "plugin_qian_scheduler",
                        principalTable: "job_definitions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_runs",
                schema: "plugin_qian_scheduler",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trigger_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    worker = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    generation = table.Column<long>(type: "bigint", nullable: false),
                    error_class = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_runs", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "FK_job_runs_job_triggers_tenant_id_trigger_id",
                        columns: x => new { x.tenant_id, x.trigger_id },
                        principalSchema: "plugin_qian_scheduler",
                        principalTable: "job_triggers",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_definitions_tenant_id",
                schema: "plugin_qian_scheduler",
                table: "job_definitions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_definitions_tenant_id_business_key",
                schema: "plugin_qian_scheduler",
                table: "job_definitions",
                columns: new[] { "tenant_id", "business_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_tenant_id",
                schema: "plugin_qian_scheduler",
                table: "job_runs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_tenant_id_trigger_id_scheduled_at",
                schema: "plugin_qian_scheduler",
                table: "job_runs",
                columns: new[] { "tenant_id", "trigger_id", "scheduled_at" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_triggers_state_next_run_at_available_at",
                schema: "plugin_qian_scheduler",
                table: "job_triggers",
                columns: new[] { "state", "next_run_at", "available_at" });

            migrationBuilder.CreateIndex(
                name: "ix_job_triggers_tenant_id",
                schema: "plugin_qian_scheduler",
                table: "job_triggers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_occurred_at",
                schema: "plugin_qian_scheduler",
                table: "outbox_messages",
                columns: new[] { "status", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_runs",
                schema: "plugin_qian_scheduler");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "plugin_qian_scheduler");

            migrationBuilder.DropTable(
                name: "job_triggers",
                schema: "plugin_qian_scheduler");

            migrationBuilder.DropTable(
                name: "job_definitions",
                schema: "plugin_qian_scheduler");
        }
    }
}
