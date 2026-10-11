using Kun.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace Qian.Data;

// Npgsql 的公开 SQL 生成器构造器要求此 options 接口；升级 Npgsql 时由真库生成测试复验。
#pragma warning disable EF1001
internal sealed class QianPostgresMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies,
    INpgsqlSingletonOptions options) : NpgsqlMigrationsSqlGenerator(dependencies, options)
#pragma warning restore EF1001
{
    protected override void Generate(CreateTableOperation operation, IModel? model,
        MigrationCommandListBuilder builder, bool terminate = true)
    {
        var table = model?.GetRelationalModel().FindTable(operation.Name, operation.Schema);
        var entities = table?.EntityTypeMappings.Select(mapping => mapping.TypeBase).OfType<IEntityType>()
            .Where(entity => entity.FindAnnotation("Qian:MultiTenant")?.Value is true).ToArray() ?? [];
        if (entities.Length == 0)
        {
            if (table is null || operation.Columns.Any(column => column.Name == "tenant_id" && !column.IsNullable))
                throw new QianDataMisconfiguredException("生成租户表迁移时必须提供带 Qian 租户标记的目标模型。");
            base.Generate(operation, model, builder, terminate);
            return;
        }

        var store = StoreObjectIdentifier.Table(operation.Name, operation.Schema);
        var columns = entities.Select(entity => entity.FindProperty(nameof(IMultiTenant.TenantId))?.GetColumnName(store))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (columns.Length != 1 || columns[0] is null)
            throw new QianDataMisconfiguredException("租户表必须具有一致的 TenantId 列映射。");
        base.Generate(operation, model, builder, terminate);
        if (!terminate) builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        var name = Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name, operation.Schema);
        var tenant = Dependencies.SqlGenerationHelper.DelimitIdentifier(columns[0]!);
        var predicate = tenant + " = NULLIF(current_setting('app.current_tenant_id', true), '')";
        // 紧随 CREATE TABLE，先装隔离策略再执行后续可能抑制事务的迁移操作。
        // restrictive guard 防止后续 permissive policy 通过 OR 放宽租户边界。
        builder.AppendLine($"ALTER TABLE {name} ENABLE ROW LEVEL SECURITY;")
            .AppendLine($"ALTER TABLE {name} FORCE ROW LEVEL SECURITY;")
            .AppendLine($"CREATE POLICY qian_tenant_access ON {name} FOR ALL USING (true) WITH CHECK (true);")
            .AppendLine($"CREATE POLICY qian_tenant_guard ON {name} AS RESTRICTIVE FOR ALL USING ({predicate}) WITH CHECK ({predicate});");
        if (terminate) builder.EndCommand();
    }
}
