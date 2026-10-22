using System.Data.Common;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Scheduler;

/// <summary>跨租户领取的专用登录权限；普通租户仍受三表的强制 RLS 保护。</summary>
public static class SchedulerWorkerAccess
{
    /// <summary>生成迁移阶段的 SQL：角色须已独立创建为受限 NOINHERIT LOGIN，无角色成员关系。只改变 Scheduler 自有触发器表，不授予参数、定义或运行结果访问权。</summary>
    public static string CreateMigrationSql(string workerRole)
    {
        ValidateRoleName(workerRole);
        var predicate = $"""
            ((tenant_id = NULLIF(pg_catalog.current_setting('app.current_tenant_id', true), '')
              AND current_user::text <> '{workerRole}' AND session_user::text <> '{workerRole}')
             OR (NULLIF(pg_catalog.current_setting('app.current_tenant_id', true), '') IS NULL
              AND current_user::text = '{workerRole}' AND session_user::text = '{workerRole}'))
            """;
        return $"""
            DO $qian_scheduler$
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r WHERE r.rolname = '{workerRole}'
                AND r.rolcanlogin AND NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb
                AND NOT r.rolcreaterole AND NOT r.rolreplication AND NOT r.rolinherit
                AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members m WHERE m.roleid = r.oid OR m.member = r.oid))
              THEN RAISE EXCEPTION 'scheduler worker role must be a dedicated restricted login'; END IF;
              IF (SELECT count(*) FROM pg_catalog.pg_policy p JOIN pg_catalog.pg_class c ON c.oid = p.polrelid
                JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'plugin_qian_scheduler'
                AND c.relname = 'job_triggers' AND p.polname = 'qian_tenant_guard'
                AND NOT p.polpermissive AND c.relrowsecurity AND c.relforcerowsecurity) <> 1
              THEN RAISE EXCEPTION 'scheduler tenant guards must already exist'; END IF;
            END $qian_scheduler$;
            ALTER POLICY qian_tenant_guard ON plugin_qian_scheduler.job_triggers USING ({predicate}) WITH CHECK ({predicate});
            GRANT USAGE ON SCHEMA plugin_qian_scheduler TO "{workerRole}";
            REVOKE ALL ON ALL TABLES IN SCHEMA plugin_qian_scheduler FROM "{workerRole}";
            GRANT SELECT ON plugin_qian_scheduler.job_triggers TO "{workerRole}";
            GRANT UPDATE (lease_owner, lease_until, generation, active_run_id) ON plugin_qian_scheduler.job_triggers TO "{workerRole}";
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
              AND NULLIF(pg_catalog.current_setting('app.current_tenant_id', true), '') IS NULL
              AND pg_catalog.has_schema_privilege(@role, 'plugin_qian_scheduler', 'USAGE')
              AND pg_catalog.has_table_privilege(@role, 'plugin_qian_scheduler.job_triggers', 'SELECT')
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_scheduler.job_triggers', 'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
              AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_attribute a
                JOIN pg_catalog.pg_class c ON c.oid=a.attrelid JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='plugin_qian_scheduler' AND c.relname='job_triggers' AND a.attnum>0 AND NOT a.attisdropped
                AND (pg_catalog.has_column_privilege(@role,c.oid,a.attnum,'UPDATE') <> (a.attname IN ('lease_owner','lease_until','generation','active_run_id'))
                  OR pg_catalog.has_column_privilege(@role,c.oid,a.attnum,'INSERT,REFERENCES')))
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_scheduler.job_definitions', 'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_scheduler.job_runs', 'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
              AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_attribute a
                JOIN pg_catalog.pg_class c ON c.oid=a.attrelid JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname='plugin_qian_scheduler' AND c.relname IN ('job_definitions','job_runs') AND a.attnum>0 AND NOT a.attisdropped
                AND pg_catalog.has_column_privilege(@role,c.oid,a.attnum,'SELECT,INSERT,UPDATE,REFERENCES'))
            """;
        var parameter = command.CreateParameter(); parameter.ParameterName = "role"; parameter.Value = role; command.Parameters.Add(parameter);
        if (await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not true)
            throw new QianDataMisconfiguredException("调度 worker 必须使用独立的受限登录角色；禁止角色切换、继承或复用普通租户凭据。");
    }
    internal static void ValidateRoleName(string role)
    {
        if (role is not { Length: > 0 and <= 63 } || role[0] is < 'a' or > 'z' || role.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
    }
}
