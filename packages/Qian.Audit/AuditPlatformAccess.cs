using System.Data.Common;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Audit;

/// <summary>平台审计链的显式存储权限；不授予租户业务操作权限，也不替代 Authz。</summary>
public static class AuditPlatformAccess
{
    /// <summary>生成迁移阶段的 SQL：角色须已独立创建为受限 NOINHERIT LOGIN，无角色成员关系。只改 Audit 自有表。</summary>
    public static string CreateMigrationSql(string platformRole)
    {
        ValidateRoleName(platformRole);
        var predicate = $"""
            ((tenant_id = NULLIF(pg_catalog.current_setting('app.current_tenant_id', true), '')
              AND current_user::text <> '{platformRole}' AND session_user::text <> '{platformRole}')
             OR (tenant_id IS NULL AND NULLIF(pg_catalog.current_setting('app.current_tenant_id', true), '') IS NULL
              AND current_user::text = '{platformRole}' AND session_user::text = '{platformRole}'))
            """;
        return $"""
            DO $qian_audit$
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r WHERE r.rolname = '{platformRole}'
                AND r.rolcanlogin AND NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb
                AND NOT r.rolcreaterole AND NOT r.rolreplication AND NOT r.rolinherit
                AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members m WHERE m.roleid = r.oid OR m.member = r.oid))
              THEN RAISE EXCEPTION 'audit platform role must be a dedicated restricted login'; END IF;
              IF (SELECT count(*) FROM pg_catalog.pg_policy p JOIN pg_catalog.pg_class c ON c.oid = p.polrelid
                JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'plugin_qian_audit'
                AND c.relname IN ('audit_entries', 'audit_chain_state') AND p.polname = 'qian_tenant_guard'
                AND NOT p.polpermissive AND c.relrowsecurity AND c.relforcerowsecurity) <> 2
              THEN RAISE EXCEPTION 'audit tenant guards must already exist'; END IF;
            END $qian_audit$;
            ALTER POLICY qian_tenant_guard ON plugin_qian_audit.audit_entries USING ({predicate}) WITH CHECK ({predicate});
            ALTER POLICY qian_tenant_guard ON plugin_qian_audit.audit_chain_state USING ({predicate}) WITH CHECK ({predicate});
            GRANT USAGE ON SCHEMA plugin_qian_audit TO "{platformRole}";
            GRANT SELECT, INSERT ON plugin_qian_audit.audit_entries TO "{platformRole}";
            GRANT SELECT, INSERT, UPDATE ON plugin_qian_audit.audit_chain_state TO "{platformRole}";
            REVOKE UPDATE, DELETE, TRUNCATE ON plugin_qian_audit.audit_entries FROM "{platformRole}";
            REVOKE DELETE, TRUNCATE ON plugin_qian_audit.audit_chain_state FROM "{platformRole}";
            """;
    }
    internal static async Task ValidateConnectionAsync(DbConnection connection, string role, CancellationToken ct)
    {
        ValidateRoleName(role); await QianApplicationRoleInterceptor.ValidateAsync(connection, ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r WHERE r.rolname = @role
              AND current_user::text = @role AND session_user::text = @role
              AND r.rolcanlogin AND NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb
              AND NOT r.rolcreaterole AND NOT r.rolreplication AND NOT r.rolinherit
              AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members m WHERE m.roleid = r.oid OR m.member = r.oid))
              AND pg_catalog.has_schema_privilege(@role, 'plugin_qian_audit', 'USAGE')
              AND pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_entries', 'SELECT')
              AND pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_entries', 'INSERT')
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_entries', 'UPDATE')
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_entries', 'DELETE')
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_entries', 'TRUNCATE')
              AND pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_chain_state', 'SELECT')
              AND pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_chain_state', 'INSERT')
              AND pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_chain_state', 'UPDATE')
            """;
        var parameter = command.CreateParameter(); parameter.ParameterName = "role"; parameter.Value = role; command.Parameters.Add(parameter);
        if (await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not true)
            throw new QianDataMisconfiguredException("平台审计必须使用独立的受限登录角色；禁止角色切换、继承或复用普通租户凭据。");
    }
    internal static void ValidateRoleName(string role)
    {
        if (role is not { Length: > 0 and <= 63 } || role[0] is < 'a' or > 'z' || role.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
    }
}
