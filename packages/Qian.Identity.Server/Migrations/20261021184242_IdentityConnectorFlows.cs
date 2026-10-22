using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Identity.Server.Migrations
{
    /// <inheritdoc />
    internal partial class IdentityConnectorFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // S1-IDP-007：上游连接器登录流程（全局表，state 与浏览器密钥只存哈希），以及连接器登录的一次性票据（login_tickets 新用途与审计原因）。
            migrationBuilder.DropCheckConstraint(
                name: "ck_login_tickets_purpose",
                schema: "plugin_qian_identity",
                table: "login_tickets");

            migrationBuilder.AddColumn<string>(
                name: "reason",
                schema: "plugin_qian_identity",
                table: "login_tickets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "connector_flows",
                schema: "plugin_qian_identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    state_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    browser_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    connector_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    return_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    nonce = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    error_detail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    external_issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    external_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    external_union_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    external_profile = table.Column<string>(type: "jsonb", nullable: true),
                    person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verified_amr = table.Column<string[]>(type: "text[]", nullable: false),
                    remote_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    callback_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    binding_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_connector_flows", x => x.id);
                    table.CheckConstraint("ck_connector_flows_status", "status IN ('pending', 'processing', 'bind', 'completed', 'failed', 'cancelled')");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_login_tickets_purpose",
                schema: "plugin_qian_identity",
                table: "login_tickets",
                sql: "purpose IN ('selection', 'password_change', 'connector')");

            migrationBuilder.CreateIndex(
                name: "ix_connector_flows_expires_at",
                schema: "plugin_qian_identity",
                table: "connector_flows",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_connector_flows_state_hash",
                schema: "plugin_qian_identity",
                table: "connector_flows",
                column: "state_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "connector_flows",
                schema: "plugin_qian_identity");

            migrationBuilder.DropCheckConstraint(
                name: "ck_login_tickets_purpose",
                schema: "plugin_qian_identity",
                table: "login_tickets");

            migrationBuilder.DropColumn(
                name: "reason",
                schema: "plugin_qian_identity",
                table: "login_tickets");

            migrationBuilder.AddCheckConstraint(
                name: "ck_login_tickets_purpose",
                schema: "plugin_qian_identity",
                table: "login_tickets",
                sql: "purpose IN ('selection', 'password_change')");
        }
    }
}
