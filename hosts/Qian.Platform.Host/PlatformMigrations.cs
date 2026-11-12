using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Qian.Audit;
using Qian.Audit.Native;
using Qian.Authz;
using Qian.Data;

namespace Qian.Platform.Host;

/// <summary>
/// 迁移 Job（<c>--migrate-only</c>，S1-DEP-011）：在线副本启动之前执行，跑完退出。分三步：
/// <list type="number">
/// <item>离线 DDL（配置了 <c>ConnectionFiles:MigrationAdmin</c> 才做，用数据库管理员连接）：没有包内迁移的上下文（Tenancy、Authz、Audit、Registry）按 EF 模型建 schema 与表
/// （含 Qian 迁移生成器补出的 RLS），schema 已在时只核对表都在；再执行两个平台登录与审计同事务函数的授权脚本（<see cref="AuditTransactionAccess"/>、
/// <see cref="AuthzPlatformAccess"/>、<see cref="AuditPlatformAccess"/>），配置了网关审计角色时执行网关原生审计的离线迁移（<see cref="NativeAuditStorage"/>）。
/// 审计函数的属主必须是没有任何成员关系的 NOLOGIN 角色，改函数属主只能由管理员做，所以这一步不能交给迁移属主。</item>
/// <item>包内迁移（迁移属主登录，<see cref="IDataMigrator"/>）：Capabilities、Lifecycle、身份与开通申请等随包带迁移的上下文。</item>
/// <item>授权（管理员）：全部业务 schema 的表与序列授给应用登录，迁移历史表只读。</item>
/// </list>
/// 三步都可重复执行。离线 DDL 不做跨版本的表结构升级：schema 已在而缺表时拒绝执行，审计租户守卫带原生审计标记后不再重放 Audit 的平台访问脚本，正式的迁移链归 S1-DEP-005。
/// 角色本身（应用登录、迁移属主、两个平台登录、审计函数属主、网关审计角色）由部署侧在此之前建好，这里只按名字使用、由脚本核对其属性。
/// </summary>
internal static class PlatformMigrations
{
    // 两张审计表的租户守卫都带原生审计的迁移标记（策略注释以 qian-audit-native-v1: 开头）。
    private const string NativeGuardInstalled = """
        SELECT count(*) = 2 FROM pg_catalog.pg_policy p JOIN pg_catalog.pg_class c ON c.oid = p.polrelid JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'plugin_qian_audit' AND c.relname IN ('audit_entries', 'audit_chain_state') AND p.polname = 'qian_tenant_guard'
          AND pg_catalog.obj_description(p.oid, 'pg_policy') LIKE @value
        """;

    internal static async Task RunAsync(WebApplication app, CancellationToken ct = default)
    {
        var services = app.Services;
        var settings = services.GetRequiredService<PlatformHostSettings>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var runtime = services.GetRequiredService<PlatformRuntime>();
        var admin = settings.HasConnection("MigrationAdmin") ? settings.Connection("MigrationAdmin") : null;
        var appRole = admin is null ? null : configuration["Migrations:AppRole"] is { Length: > 0 } role ? Role(role)
            : throw new InvalidOperationException("配置了 ConnectionFiles:MigrationAdmin 时必须给出 Migrations:AppRole（应用登录的角色名）。");
        var catalog = services.GetRequiredService<IDataContextCatalog>().ContextTypes;
        if (admin is not null) await OfflineAsync(services, runtime, configuration, catalog, admin, appRole!, ct).ConfigureAwait(false);
        await services.GetRequiredService<IDataMigrator>().MigrateAsync(ct).ConfigureAwait(false);
        if (admin is not null) await GrantAsync(services, catalog, admin, appRole!, ct).ConfigureAwait(false);
    }

    private static async Task OfflineAsync(IServiceProvider services, PlatformRuntime runtime, IConfiguration configuration, IReadOnlyCollection<Type> catalog, string admin, string appRole,
        CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var contexts = catalog.Select(type => (QianDbContext)scope.ServiceProvider.GetRequiredService(type)).ToArray();
        // 同一 schema 可有多个上下文（平台授权、平台审计登录的上下文与业务上下文共用表）：取表最多的那个建表，其余上下文的表都必须在它的模型里。
        foreach (var group in contexts.Where(context => !context.Database.GetMigrations().Any()).GroupBy(Schema))
        {
            var owner = group.MaxBy(context => Tables(context).Count)!;
            var tables = Tables(owner);
            foreach (var other in group)
                if (Tables(other).Except(tables, StringComparer.Ordinal).FirstOrDefault() is { } missing)
                    throw new InvalidOperationException("schema " + group.Key + " 的上下文 " + other.GetType().Name + " 有建表上下文 " + owner.GetType().Name + " 之外的表 " + missing + "。");
            if (!await ExistsAsync(admin, "SELECT to_regnamespace(@value) IS NOT NULL", group.Key, ct).ConfigureAwait(false))
            {
                await ExecuteAsync(admin, owner.Database.GenerateCreateScript(), ct).ConfigureAwait(false);
                continue;
            }
            foreach (var table in tables)
                if (!await ExistsAsync(admin, "SELECT to_regclass(@value) IS NOT NULL", Quote(group.Key) + "." + Quote(table), ct).ConfigureAwait(false))
                    throw new InvalidOperationException("schema " + group.Key + " 已存在但缺少表 " + table + "：离线迁移不做表结构升级（正式迁移链归 S1-DEP-005）。");
        }
        await GrantAsync(admin, contexts.Where(context => !context.Database.GetMigrations().Any()).Select(Schema).Distinct(StringComparer.Ordinal), appRole, ct).ConfigureAwait(false);
        // 两个平台登录与审计同事务函数（R103 / S1-AUD-009）：角色名取自合成后的 profile（环境 overlay），审计函数属主取自 Migrations:AuditWriterRole。
        var authz = runtime.Profile.Entries.SingleOrDefault(entry => entry.Id == "authz");
        var audit = runtime.Profile.Entries.SingleOrDefault(entry => entry.Id == "audit");
        var authzRole = authz is null ? null : Text(authz.Config, "platformRole");
        var auditRole = audit is null ? null : Text(audit.Config, "chain", "platformRole");
        var sql = new List<string>();
        if (audit is not null)
        {
            var writer = configuration["Migrations:AuditWriterRole"] is { Length: > 0 } value ? Role(value)
                : throw new InvalidOperationException("迁移审计同事务函数要求 Migrations:AuditWriterRole（审计函数属主，独立的 NOLOGIN 角色）。");
            sql.Add(AuditTransactionAccess.CreateMigrationSql(writer, appRole));
            // 平台访问脚本会把两张审计表的租户守卫改回自己的谓词；网关原生审计迁移之后守卫带 qian-audit-native-v1 标记并拼了原生分支，再改一次就被原生迁移判为漂移
            // （「explicit migration required」）。离线迁移不做升级：标记在时说明首装已执行过平台访问脚本，这次跳过；改审计角色走正式迁移链（S1-DEP-005）。
            if (auditRole is not null && !await ExistsAsync(admin, NativeGuardInstalled, "qian-audit-native-v1:%", ct).ConfigureAwait(false))
                sql.Add(AuditPlatformAccess.CreateMigrationSql(auditRole, writer, appRole));
        }
        if (authzRole is not null) sql.Add(AuthzPlatformAccess.CreateMigrationSql(authzRole));
        if (sql.Count > 0) await ExecuteAsync(admin, "BEGIN;" + string.Concat(sql) + "COMMIT;", ct).ConfigureAwait(false);
        // 网关原生审计（与本宿主的 Audit 共用 v1 审计 schema）：网关宿主没有迁移入口，它的受限角色、守卫策略与函数在这里一并迁移。
        var gateway = configuration.GetSection("Migrations:GatewayAudit");
        if (gateway.Exists())
        {
            var roles = new NativeAuditRoles(Role(Required(gateway, "TenantRole")), Role(Required(gateway, "FunctionOwner")), Role(Required(gateway, "PlatformRole")));
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await NativeAuditStorage.MigrateAsync(connection, roles, ct).ConfigureAwait(false);
        }
    }

    // 全部业务 schema（含随包迁移的）授给应用登录：表的增删改查与序列；迁移历史表只读（在线副本启动时核对没有待执行的迁移）。
    private static async Task GrantAsync(IServiceProvider services, IReadOnlyCollection<Type> catalog, string admin, string appRole, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        await GrantAsync(admin, catalog.Select(type => Schema((QianDbContext)scope.ServiceProvider.GetRequiredService(type))).Distinct(StringComparer.Ordinal), appRole, ct)
            .ConfigureAwait(false);
    }

    private static async Task GrantAsync(string admin, IEnumerable<string> schemas, string appRole, CancellationToken ct)
    {
        foreach (var schema in schemas)
        {
            var name = Quote(schema);
            var history = await ExistsAsync(admin, "SELECT to_regclass(@value) IS NOT NULL", name + ".\"__EFMigrationsHistory\"", ct).ConfigureAwait(false);
            await ExecuteAsync(admin, $"""
                GRANT USAGE ON SCHEMA {name} TO {Quote(appRole)};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {name} TO {Quote(appRole)};
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA {name} TO {Quote(appRole)};
                {(history ? $"REVOKE INSERT, UPDATE, DELETE ON {name}.\"__EFMigrationsHistory\" FROM {Quote(appRole)};" : "")}
                """, ct).ConfigureAwait(false);
        }
    }

    private static string Schema(QianDbContext context) => context.Model.GetDefaultSchema() ?? throw new InvalidOperationException("上下文缺少插件 schema：" + context.GetType().Name);

    private static IReadOnlyList<string> Tables(QianDbContext context) =>
        [.. context.Model.GetEntityTypes().Select(entity => entity.GetTableName()).OfType<string>().Distinct(StringComparer.Ordinal)];

    private static string? Text(JsonElement config, params string[] path)
    {
        foreach (var name in path)
            if (config.ValueKind != JsonValueKind.Object || !config.TryGetProperty(name, out config)) return null;
        return config.ValueKind == JsonValueKind.String ? config.GetString() : null;
    }

    private static string Required(IConfigurationSection section, string name) =>
        section[name] is { Length: > 0 } value ? value : throw new InvalidOperationException("缺少迁移配置 " + section.Path + ":" + name + "。");

    // 角色名进 DDL：只收小写字母开头的小写字母、数字与下划线（与 Authz / Audit 的角色名校验同一口径）。
    private static string Role(string value) =>
        value is { Length: > 0 and <= 63 } && value[0] is >= 'a' and <= 'z' && value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_') ? value
            : throw new InvalidOperationException("角色名不合法：" + value);

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static async Task<bool> ExistsAsync(string connectionString, string sql, string value, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("value", value);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is true;
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 0 };
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
