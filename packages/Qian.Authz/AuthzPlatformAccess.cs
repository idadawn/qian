using System.Data.Common;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Authz;

/// <summary>平台角色（tenant_id 为 null 的 roles / role_assignments 行）的显式存储权限；不授予任何租户行或授权表的访问。</summary>
public static class AuthzPlatformAccess
{
    /// <summary>
    /// 生成迁移阶段的 SQL：角色须已独立创建为受限 NOINHERIT LOGIN，无角色成员关系；只改 Authz 自有的 roles 与 role_assignments。
    /// 普通租户凭据仍只能读写当前租户的行，平台登录只能在无租户上下文时读写平台行。
    /// </summary>
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
            DO $qian_authz$
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r WHERE r.rolname = '{platformRole}'
                AND r.rolcanlogin AND NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb
                AND NOT r.rolcreaterole AND NOT r.rolreplication AND NOT r.rolinherit
                AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members m WHERE m.roleid = r.oid OR m.member = r.oid))
              THEN RAISE EXCEPTION 'authz platform role must be a dedicated restricted login'; END IF;
              IF (SELECT count(*) FROM pg_catalog.pg_policy p JOIN pg_catalog.pg_class c ON c.oid = p.polrelid
                JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'plugin_qian_authz'
                AND c.relname IN ('roles', 'role_assignments') AND p.polname = 'qian_tenant_guard'
                AND NOT p.polpermissive AND c.relrowsecurity AND c.relforcerowsecurity) <> 2
              THEN RAISE EXCEPTION 'authz tenant guards must already exist'; END IF;
            END $qian_authz$;
            ALTER POLICY qian_tenant_guard ON plugin_qian_authz.roles USING ({predicate}) WITH CHECK ({predicate});
            ALTER POLICY qian_tenant_guard ON plugin_qian_authz.role_assignments USING ({predicate}) WITH CHECK ({predicate});
            GRANT USAGE ON SCHEMA plugin_qian_authz TO "{platformRole}";
            GRANT SELECT, INSERT, UPDATE, DELETE ON plugin_qian_authz.roles, plugin_qian_authz.role_assignments TO "{platformRole}";
            REVOKE TRUNCATE ON plugin_qian_authz.roles, plugin_qian_authz.role_assignments FROM "{platformRole}";
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
              AND pg_catalog.has_schema_privilege(@role, 'plugin_qian_authz', 'USAGE')
              AND (SELECT bool_and(pg_catalog.has_table_privilege(@role, t.name, p.name))
                FROM (VALUES ('plugin_qian_authz.roles'), ('plugin_qian_authz.role_assignments')) AS t(name)
                CROSS JOIN (VALUES ('SELECT'), ('INSERT'), ('UPDATE'), ('DELETE')) AS p(name))
              -- has_table_privilege 列出多个权限时任一即真，所以逐表单查：租户授权表不得对平台登录开放任何读写。
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_authz.grants', 'SELECT')
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_authz.grants', 'INSERT')
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_authz.tenant_authz_revisions', 'SELECT')
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_authz.tenant_authz_revisions', 'UPDATE')
            """;
        var parameter = command.CreateParameter(); parameter.ParameterName = "role"; parameter.Value = role; command.Parameters.Add(parameter);
        if (await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not true)
            throw new QianDataMisconfiguredException("平台授权必须使用独立的受限登录角色；禁止角色切换、继承、复用普通租户凭据或访问租户授权表。");
    }

    internal static void ValidateRoleName(string role)
    {
        if (role is not { Length: > 0 and <= 63 } || role[0] is < 'a' or > 'z' || role.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
    }
}
