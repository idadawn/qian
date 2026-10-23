using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Qian.Audit;
using Qian.Authz;
using Qian.Capabilities;
using Qian.Data;
using Qian.Lifecycle;
using Qian.Registry;
using Qian.Sample.Crm;
using Qian.Tenancy;

namespace Qian.Dev.TenantOps;

// 开发库：每次启动按集成测试的角色形状重建。管理员连接只做 DDL 与授权；运行期一律用 NOSUPERUSER / NOBYPASSRLS 的受限登录，
// 平台 Authz / Audit 各用一个专用登录（ADR-006 D6.11、Audit 平台链）。口令每次随机生成，只留在本进程内存里。
internal sealed class DevDatabase
{
    internal const string Name = "qian_dev";
    private const string AppRole = "qian_dev_app";
    private const string BusinessRole = "qian_dev_crm";
    private const string AuditOwnerRole = "qian_dev_audit_fn";
    private const string MigratorRole = "qian_dev_migrator";
    internal const string AuthzPlatformRole = "qian_dev_authz_platform";
    internal const string AuditPlatformRole = "qian_dev_audit_platform";

    private readonly string _admin;
    private DevDatabase(string admin, string app, string business, string migrations, string authzPlatform, string auditPlatform)
    { _admin = admin; App = app; Business = business; Migrations = migrations; AuthzPlatform = authzPlatform; AuditPlatform = auditPlatform; }

    // 通用受限应用角色（与测试夹具的 qian_app 同形）：Authz / Tenancy / Audit / Registry 的在线连接。
    internal string App { get; }
    // CRM 业务登录：CRM、Capabilities、Lifecycle 的在线连接，经受控入口写审计。
    internal string Business { get; }
    // 迁移属主：只多一项建 schema 的权限，执行 Capabilities / Lifecycle 的包内迁移。
    internal string Migrations { get; }
    internal string AuthzPlatform { get; }
    internal string AuditPlatform { get; }

    internal static async Task<DevDatabase> ResetAsync(string adminConnection, CancellationToken ct)
    {
        var maintenance = new NpgsqlConnectionStringBuilder(adminConnection) { Database = "postgres", Pooling = false }.ConnectionString;
        var passwords = new[] { AppRole, BusinessRole, MigratorRole, AuthzPlatformRole, AuditPlatformRole }
            .ToDictionary(role => role, _ => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), StringComparer.Ordinal);
        const string restricted = "NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION";
        // DROP / CREATE DATABASE 不能进事务块，各自单独执行。口令是本进程生成的十六进制串，可以安全地拼进 DDL（CREATE ROLE 不支持参数化）。
        await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {Name} WITH (FORCE);", ct).ConfigureAwait(false);
        await ExecuteAsync(maintenance, $"""
            DROP ROLE IF EXISTS {AppRole}; DROP ROLE IF EXISTS {BusinessRole}; DROP ROLE IF EXISTS {AuditOwnerRole};
            DROP ROLE IF EXISTS {MigratorRole}; DROP ROLE IF EXISTS {AuthzPlatformRole}; DROP ROLE IF EXISTS {AuditPlatformRole};
            CREATE ROLE {AppRole} LOGIN PASSWORD '{passwords[AppRole]}' NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            CREATE ROLE {BusinessRole} LOGIN {restricted} PASSWORD '{passwords[BusinessRole]}';
            CREATE ROLE {AuditOwnerRole} NOLOGIN {restricted};
            CREATE ROLE {MigratorRole} LOGIN {restricted} PASSWORD '{passwords[MigratorRole]}';
            CREATE ROLE {AuthzPlatformRole} LOGIN {restricted} PASSWORD '{passwords[AuthzPlatformRole]}';
            CREATE ROLE {AuditPlatformRole} LOGIN {restricted} PASSWORD '{passwords[AuditPlatformRole]}';
            """, ct).ConfigureAwait(false);
        await ExecuteAsync(maintenance, $"CREATE DATABASE {Name};", ct).ConfigureAwait(false);
        var admin = new NpgsqlConnectionStringBuilder(adminConnection) { Database = Name, Pooling = false }.ConnectionString;
        await ExecuteAsync(admin, $"""
            GRANT CONNECT, TEMPORARY ON DATABASE {Name} TO {AppRole};
            GRANT USAGE ON SCHEMA public TO {AppRole};
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {AppRole};
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO {AppRole};
            GRANT CONNECT, CREATE ON DATABASE {Name} TO {MigratorRole};
            """, ct).ConfigureAwait(false);
        return new DevDatabase(admin, Login(AppRole), Login(BusinessRole), Login(MigratorRole), Login(AuthzPlatformRole), Login(AuditPlatformRole));

        string Login(string role) => new NpgsqlConnectionStringBuilder(adminConnection) { Database = Name, Username = role, Password = passwords[role] }.ConnectionString;
    }

    // 与集成测试的建库顺序一致：无包内迁移的上下文用 EF 建表脚本，Capabilities / Lifecycle 经迁移器在 advisory lock 内迁移。
    internal async Task CreateSchemasAsync(IServiceProvider services, CancellationToken ct)
    {
        using (var scope = services.CreateScope())
        {
            var sql = scope.ServiceProvider.GetRequiredService<CrmDbContext>().Database.GenerateCreateScript()
                + scope.ServiceProvider.GetRequiredService<AuthzDbContext>().Database.GenerateCreateScript()
                + scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Database.GenerateCreateScript()
                + scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.GenerateCreateScript()
                + scope.ServiceProvider.GetRequiredService<RegistryDbContext>().Database.GenerateCreateScript();
            await ExecuteAsync(_admin, sql, ct).ConfigureAwait(false);
        }
        await ExecuteAsync(_admin, $"""
            GRANT USAGE ON SCHEMA {CrmDbContext.Schema} TO {BusinessRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {CrmDbContext.Schema} TO {BusinessRole};
            GRANT USAGE ON SCHEMA {AuthzDbContext.Schema}, {TenancyDbContext.Schema}, {AuditDbContext.Schema}, {RegistryDbContext.Schema} TO {AppRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {AuthzDbContext.Schema}, {TenancyDbContext.Schema}, {AuditDbContext.Schema}, {RegistryDbContext.Schema} TO {AppRole};
            """, ct).ConfigureAwait(false);
        await ExecuteAsync(_admin, "BEGIN;" + AuditTransactionAccess.CreateMigrationSql(AuditOwnerRole, BusinessRole) + "COMMIT;", ct).ConfigureAwait(false);
        // 平台审计链：三参重载另建平台链的同事务函数（属主同上），Lifecycle 的业务登录只获执行权，平台级生命周期操作与状态变更同一事务入链（S1-AUD-009）。
        await ExecuteAsync(_admin, "BEGIN;" + AuthzPlatformAccess.CreateMigrationSql(AuthzPlatformRole) + AuditPlatformAccess.CreateMigrationSql(AuditPlatformRole, AuditOwnerRole, BusinessRole) + "COMMIT;", ct).ConfigureAwait(false);
        var reports = await services.GetRequiredService<IDataMigrator>().MigrateAsync(ct).ConfigureAwait(false);
        foreach (var context in new[] { typeof(CapabilitiesDbContext), typeof(LifecycleDbContext) })
            if (!reports.Any(report => report.ContextType == context && report.Applied.Count > 0))
                throw new InvalidOperationException("包内迁移没有执行：" + context.Name);
        await ExecuteAsync(_admin, $"""
            GRANT USAGE ON SCHEMA {CapabilitiesDbContext.Schema}, {LifecycleDbContext.Schema} TO {BusinessRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {CapabilitiesDbContext.Schema}, {LifecycleDbContext.Schema} TO {BusinessRole};
            """, ct).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
