using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Data.Migrations
{
    /// <inheritdoc />
    internal partial class Sequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "plugin_qian_data");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "plugin_qian_data",
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
                name: "sequence_counters",
                schema: "plugin_qian_data",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rule_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    scope_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    period = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sequence_counters", x => new { x.tenant_id, x.rule_code, x.revision, x.scope_key, x.period });
                });

            migrationBuilder.CreateTable(
                name: "sequence_rules",
                schema: "plugin_qian_data",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    pattern = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sequence_rules", x => new { x.tenant_id, x.code });
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_occurred_at",
                schema: "plugin_qian_data",
                table: "outbox_messages",
                columns: new[] { "status", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sequence_counters_tenant_id",
                schema: "plugin_qian_data",
                table: "sequence_counters",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sequence_rules_tenant_id",
                schema: "plugin_qian_data",
                table: "sequence_rules",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "plugin_qian_data");

            migrationBuilder.DropTable(
                name: "sequence_counters",
                schema: "plugin_qian_data");

            migrationBuilder.DropTable(
                name: "sequence_rules",
                schema: "plugin_qian_data");
        }
    }
}
