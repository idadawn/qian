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

            // 此前回执没有上限，存量行可能超过 1 MiB：这些行会让带校验的约束失败，而迁移属主在 FORCE RLS 下删不掉它们；
            // DDL 校验本身不经 RLS，照样会扫到这些行。NOT VALID 只约束新写入；存量行过期后不再回放，物理删除靠本租户后续
            // 带键写调用登记时的惰性清理（每次最多 100 行）。模型快照里仍是同一条约束。
            migrationBuilder.Sql(
                "ALTER TABLE plugin_qian_capabilities.invoke_receipts ADD CONSTRAINT ck_invoke_receipts_response_size CHECK (octet_length(response) <= 1048576) NOT VALID;");

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
