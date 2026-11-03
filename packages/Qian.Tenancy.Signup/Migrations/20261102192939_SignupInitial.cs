using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Tenancy.Signup.Migrations
{
    /// <inheritdoc />
    internal partial class SignupInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "plugin_qian_tenancy_signup");

            migrationBuilder.CreateTable(
                name: "applications",
                schema: "plugin_qian_tenancy_signup",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    industry = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    size = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    existing_account = table.Column<bool>(type: "boolean", nullable: false),
                    plan = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    users = table.Column<int>(type: "integer", nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    review_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reviewer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provisioned_tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                    table.CheckConstraint("ck_applications_code", "code ~ '^[a-z0-9]+$' AND length(code) <= 64");
                    table.CheckConstraint("ck_applications_plan", "plan IS NULL OR plan IN ('trial', 'formal')");
                    table.CheckConstraint("ck_applications_status", "status IN ('submitted', 'provisioning', 'approved', 'failed', 'rejected', 'withdrawn')");
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "plugin_qian_tenancy_signup",
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
                name: "sessions",
                schema: "plugin_qian_tenancy_signup",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "verifications",
                schema: "plugin_qian_tenancy_signup",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    code_salt = table.Column<byte[]>(type: "bytea", nullable: false),
                    code_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_verifications", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_applications_number",
                schema: "plugin_qian_tenancy_signup",
                table: "applications",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_person_id_submitted_at",
                schema: "plugin_qian_tenancy_signup",
                table: "applications",
                columns: new[] { "person_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_applications_status_submitted_at",
                schema: "plugin_qian_tenancy_signup",
                table: "applications",
                columns: new[] { "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ux_applications_open_code",
                schema: "plugin_qian_tenancy_signup",
                table: "applications",
                column: "code",
                unique: true,
                filter: "status IN ('submitted', 'provisioning', 'failed')");

            migrationBuilder.CreateIndex(
                name: "ux_applications_open_person",
                schema: "plugin_qian_tenancy_signup",
                table: "applications",
                column: "person_id",
                unique: true,
                filter: "status IN ('submitted', 'provisioning', 'failed')");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_occurred_at",
                schema: "plugin_qian_tenancy_signup",
                table: "outbox_messages",
                columns: new[] { "status", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sessions_expires_at",
                schema: "plugin_qian_tenancy_signup",
                table: "sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_token_hash",
                schema: "plugin_qian_tenancy_signup",
                table: "sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_verifications_phone_created_at",
                schema: "plugin_qian_tenancy_signup",
                table: "verifications",
                columns: new[] { "phone", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_verifications_source_created_at",
                schema: "plugin_qian_tenancy_signup",
                table: "verifications",
                columns: new[] { "source", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "applications",
                schema: "plugin_qian_tenancy_signup");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "plugin_qian_tenancy_signup");

            migrationBuilder.DropTable(
                name: "sessions",
                schema: "plugin_qian_tenancy_signup");

            migrationBuilder.DropTable(
                name: "verifications",
                schema: "plugin_qian_tenancy_signup");
        }
    }
}
