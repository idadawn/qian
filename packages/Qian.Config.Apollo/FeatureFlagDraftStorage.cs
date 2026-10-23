using System.Data.Common;

namespace Qian.Config.Apollo;

/// <summary>平台草稿回执的离线迁移与固定 RLS 声明；在线组件不执行 DDL。</summary>
public static class FeatureFlagDraftStorage
{
    /// <summary>独立 schema。</summary>
    public const string Schema = "plugin_qian_config_flags";
    /// <summary>回执表。</summary>
    public const string Table = "draft_receipts";
    /// <summary>固定 restrictive 策略名。</summary>
    public const string GuardPolicy = "flags_platform_guard";
    /// <summary>PostgreSQL 16 的固定规范表达式；不能从当前数据库自动学习预期值。</summary>
    public static string ExpectedGuard(string platformRole)
    {
        CheckRole(platformRole);
        return "((CURRENT_USER = '" + platformRole + "'::name) AND (tenant_id IS NULL) AND (NULLIF(current_setting('app.current_tenant_id'::text, true), ''::text) IS NULL))";
    }
    /// <summary>用独立迁移连接建表与授予受限登录角色；不创建角色、不读取或设置口令。</summary>
    public static async Task MigrateAsync(DbConnection connection, string platformRole, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection); CheckRole(platformRole);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = $$"""
            SELECT pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended('qian.config.flags:migration',0));
            CREATE SCHEMA IF NOT EXISTS plugin_qian_config_flags;
            REVOKE ALL ON SCHEMA plugin_qian_config_flags FROM PUBLIC;
            CREATE TABLE IF NOT EXISTS plugin_qian_config_flags.draft_receipts (
              target_hash text NOT NULL CHECK(length(target_hash)=64), idempotency_key uuid NOT NULL,
              tenant_id uuid CHECK(tenant_id IS NULL),
              actor text NOT NULL, request_hash text NOT NULL CHECK(length(request_hash)=64), flag_name text NOT NULL,
              rule_hash text NOT NULL CHECK(length(rule_hash)=64), state text NOT NULL CHECK(state IN ('started','pending-publish','uncertain')),
              created_at timestamptz NOT NULL DEFAULT now(), completed_at timestamptz,
              PRIMARY KEY(target_hash,idempotency_key));
            ALTER TABLE plugin_qian_config_flags.draft_receipts ENABLE ROW LEVEL SECURITY;
            ALTER TABLE plugin_qian_config_flags.draft_receipts FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS flags_platform_guard ON plugin_qian_config_flags.draft_receipts;
            CREATE POLICY flags_platform_guard ON plugin_qian_config_flags.draft_receipts AS RESTRICTIVE FOR ALL TO PUBLIC
              USING ({{ExpectedGuard(platformRole)}}) WITH CHECK ({{ExpectedGuard(platformRole)}});
            DROP POLICY IF EXISTS flags_platform_access ON plugin_qian_config_flags.draft_receipts;
            CREATE POLICY flags_platform_access ON plugin_qian_config_flags.draft_receipts FOR ALL TO "{{platformRole}}" USING (true) WITH CHECK (true);
            REVOKE ALL ON plugin_qian_config_flags.draft_receipts FROM PUBLIC;
            GRANT USAGE ON SCHEMA plugin_qian_config_flags TO "{{platformRole}}";
            GRANT SELECT,INSERT ON plugin_qian_config_flags.draft_receipts TO "{{platformRole}}";
            GRANT UPDATE(state,completed_at) ON plugin_qian_config_flags.draft_receipts TO "{{platformRole}}";
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
    private static void CheckRole(string role)
    {
        if (role is not { Length: > 0 and <= 63 } || role[0] is not (>= 'a' and <= 'z') ||
            !role.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
            throw new ArgumentException("平台回执角色名称无效。", nameof(role));
    }
}
