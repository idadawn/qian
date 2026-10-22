using System.Data;
using System.Data.Common;

namespace Qian.Audit.Native;

/// <summary>原生审计的离线迁移和固定安全声明。在线 Provider 只校验，不调用迁移。</summary>
public static class NativeAuditStorage
{
    /// <summary>与既有 EF Provider 共用的 v1 审计 schema。</summary>
    public const string Schema = "plugin_qian_audit";
    /// <summary>仅收窄 Native 角色的独立 restrictive 策略。</summary>
    public const string GuardPolicy = "qian_audit_native_guard";
    /// <summary>由受信迁移模板冻结的 PostgreSQL 16 规范表达式，供 NativeDataTable 的两项期待值使用。</summary>
    public static string ExpectedGuard(NativeAuditRoles roles) => Replace(Resource("guard-canonical.sql"), roles);

    /// <summary>在独立迁移连接上以事务和 schema 锁执行；连接不应来自在线 NativeData。</summary>
    public static async Task MigrateAsync(DbConnection migrationConnection, NativeAuditRoles roles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migrationConnection); ArgumentNullException.ThrowIfNull(roles);
        if (migrationConnection.State != ConnectionState.Open) throw new ArgumentException("迁移连接必须已打开。", nameof(migrationConnection));
        await using var transaction = await migrationConnection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        await using var command = migrationConnection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SET LOCAL search_path=pg_catalog; SELECT pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended('qian.audit.native:migration',0));\n" + CreateMigrationSql(roles);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>生成原生专用授权和函数；必须在离线迁移事务中运行。既有 EF 角色的授权不被改变。</summary>
    public static string CreateMigrationSql(NativeAuditRoles roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        return Resource("schema.sql") + "\n" + Replace(Resource("roles-policies.sql"), roles)
            .Replace("$GUARD$", Replace(Resource("guard-expression.sql").Trim(), roles), StringComparison.Ordinal)
            + "\n" + Replace(Resource("functions.sql"), roles) + "\n" + Replace(Resource("function-grants.sql"), roles);
    }
    internal static string Replace(string value, NativeAuditRoles roles) => value.Replace("$TENANT$", roles.TenantLogin, StringComparison.Ordinal)
        .Replace("$PLATFORM$", roles.PlatformLogin ?? "", StringComparison.Ordinal).Replace("$OWNER$", roles.FunctionOwner, StringComparison.Ordinal);
    internal static string Resource(string name)
    {
        using var stream = typeof(NativeAuditStorage).Assembly.GetManifestResourceStream("audit-native/" + name) ?? throw new InvalidOperationException("缺少审计协议资源：" + name);
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
}
