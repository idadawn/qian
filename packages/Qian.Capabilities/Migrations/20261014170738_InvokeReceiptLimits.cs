using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qian.Capabilities.Migrations
{
    /// <inheritdoc />
    internal partial class InvokeReceiptLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "withheld",
                schema: "plugin_qian_capabilities",
                table: "invoke_receipts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoke_receipts_response_size",
                schema: "plugin_qian_capabilities",
                table: "invoke_receipts",
                sql: "octet_length(response) <= 1048576");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoke_receipts_withheld",
                schema: "plugin_qian_capabilities",
                table: "invoke_receipts",
                sql: "withheld IS NULL OR (response IS NULL AND withheld IN ('response_too_large', 'data_classification'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_invoke_receipts_response_size",
                schema: "plugin_qian_capabilities",
                table: "invoke_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoke_receipts_withheld",
                schema: "plugin_qian_capabilities",
                table: "invoke_receipts");

            migrationBuilder.DropColumn(
                name: "withheld",
                schema: "plugin_qian_capabilities",
                table: "invoke_receipts");
        }
    }
}
