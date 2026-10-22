using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Identity.Server.Migrations
{
    /// <inheritdoc />
    internal partial class IdentitySessionCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "revocation_processed_at",
                schema: "plugin_qian_identity",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revoked_by",
                schema: "plugin_qian_identity",
                table: "sessions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            // S1-IDP-003 阶段吊销的会话没有待处理的旁路：协议授权族由当时的登出路径或会话校验兜住，不为历史补发事件。
            migrationBuilder.Sql("UPDATE plugin_qian_identity.sessions SET revocation_processed_at = revoked_at WHERE revoked_at IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_pending_revocation",
                schema: "plugin_qian_identity",
                table: "sessions",
                column: "revoked_at",
                filter: "revoked_at IS NOT NULL AND revocation_processed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sessions_pending_revocation",
                schema: "plugin_qian_identity",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "revocation_processed_at",
                schema: "plugin_qian_identity",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "revoked_by",
                schema: "plugin_qian_identity",
                table: "sessions");
        }
    }
}
