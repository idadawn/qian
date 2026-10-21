using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Identity.Server.Migrations
{
    /// <inheritdoc />
    internal partial class IdentityAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clients",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    device_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    redirect_uris = table.Column<string>(type: "jsonb", nullable: false),
                    scopes = table.Column<string>(type: "jsonb", nullable: false),
                    resources = table.Column<string>(type: "jsonb", nullable: false),
                    owner_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    plugin_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clients", x => x.client_id);
                    table.CheckConstraint("ck_clients_type", "type IN ('confidential', 'public', 'machine', 'agent')");
                });

            migrationBuilder.CreateTable(
                name: "login_audits",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    stage = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    identifier_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    identifier_masked = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    method = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    remote_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    session_hash = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_login_audits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "login_captchas",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_login_captchas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "login_tickets",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    purpose = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    choices = table.Column<string[]>(type: "text[]", nullable: false),
                    from_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amr = table.Column<string[]>(type: "text[]", nullable: false),
                    acr = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    authenticated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    remote_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_login_tickets", x => x.token_hash);
                    table.CheckConstraint("ck_login_tickets_purpose", "purpose IN ('selection', 'password_change')");
                });

            migrationBuilder.CreateTable(
                name: "persons",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    platform_access = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    password_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    last_failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    mfa_factors = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    identity_revision = table.Column<long>(type: "bigint", nullable: false),
                    tenant_ids = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_persons", x => x.id);
                    table.CheckConstraint("ck_persons_password_hash", "password_hash IS NULL OR password_hash LIKE '$argon2id$%'");
                    table.CheckConstraint("ck_persons_status", "status IN ('active', 'disabled', 'locked', 'purged')");
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_tenant_administrator = table.Column<bool>(type: "boolean", nullable: false),
                    identity_revision = table.Column<long>(type: "bigint", nullable: false),
                    profile = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memberships", x => x.id);
                    table.CheckConstraint("ck_memberships_status", "status IN ('active', 'disabled', 'locked', 'purged')");
                    table.ForeignKey(
                        name: "FK_memberships_persons_person_id",
                        column: x => x.person_id,
                        principalSchema: "plugin_qian_identity",
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "password_history",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_history_persons_person_id",
                        column: x => x.person_id,
                        principalSchema: "plugin_qian_identity",
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_identifiers",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    value = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_identifiers", x => x.id);
                    table.CheckConstraint("ck_person_identifiers_kind", "kind IN ('phone', 'email', 'username')");
                    table.ForeignKey(
                        name: "FK_person_identifiers_persons_person_id",
                        column: x => x.person_id,
                        principalSchema: "plugin_qian_identity",
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    amr = table.Column<string[]>(type: "text[]", nullable: false),
                    acr = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    identity_revision = table.Column<long>(type: "bigint", nullable: false),
                    remote_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    authenticated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_sessions_persons_person_id",
                        column: x => x.person_id,
                        principalSchema: "plugin_qian_identity",
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_identities",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    union_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_identities", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_identities_memberships_membership_id",
                        column: x => x.membership_id,
                        principalSchema: "plugin_qian_identity",
                        principalTable: "memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_identities_persons_person_id",
                        column: x => x.person_id,
                        principalSchema: "plugin_qian_identity",
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_external_identities_membership_id",
                schema: "plugin_qian_identity",
                table: "external_identities",
                column: "membership_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_identities_person_id",
                schema: "plugin_qian_identity",
                table: "external_identities",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_identities_tenant_id",
                schema: "plugin_qian_identity",
                table: "external_identities",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_identities_tenant_id_connector_id_subject",
                schema: "plugin_qian_identity",
                table: "external_identities",
                columns: new[] { "tenant_id", "connector_id", "subject" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_login_audits_occurred_at",
                schema: "plugin_qian_identity",
                table: "login_audits",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_login_audits_person_id_occurred_at",
                schema: "plugin_qian_identity",
                table: "login_audits",
                columns: new[] { "person_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_login_captchas_expires_at",
                schema: "plugin_qian_identity",
                table: "login_captchas",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_login_tickets_expires_at",
                schema: "plugin_qian_identity",
                table: "login_tickets",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_person_id",
                schema: "plugin_qian_identity",
                table: "memberships",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_tenant_id",
                schema: "plugin_qian_identity",
                table: "memberships",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_tenant_id_account",
                schema: "plugin_qian_identity",
                table: "memberships",
                columns: new[] { "tenant_id", "account" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_memberships_tenant_id_person_id",
                schema: "plugin_qian_identity",
                table: "memberships",
                columns: new[] { "tenant_id", "person_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_history_person_id_created_at",
                schema: "plugin_qian_identity",
                table: "password_history",
                columns: new[] { "person_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_person_identifiers_kind_value",
                schema: "plugin_qian_identity",
                table: "person_identifiers",
                columns: new[] { "kind", "value" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_person_identifiers_person_id",
                schema: "plugin_qian_identity",
                table: "person_identifiers",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_person_id",
                schema: "plugin_qian_identity",
                table: "sessions",
                column: "person_id",
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_tenant_id",
                schema: "plugin_qian_identity",
                table: "sessions",
                column: "tenant_id",
                filter: "revoked_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clients",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "external_identities",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "login_audits",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "login_captchas",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "login_tickets",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "password_history",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "person_identifiers",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "sessions",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "memberships",
                schema: "plugin_qian_identity");

            migrationBuilder.DropTable(
                name: "persons",
                schema: "plugin_qian_identity");
        }
    }
}
