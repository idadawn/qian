using Npgsql;

namespace Qian.Data.Native.Postgres;

internal static class Security
{
    internal static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string? tenant, bool local, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT pg_catalog.set_config('app.current_tenant_id', @tenant, @local)", connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant ?? ""); command.Parameters.AddWithValue("local", local);
        await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }

    internal static async Task CheckAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, OpenBinding binding, string? tenant,
        bool checkTenant, CancellationToken ct, bool serializable = false)
    {
        await using (var command = new NpgsqlCommand("""
            SELECT current_user::text = @role AND session_user::text = @role AND current_database()::text = @database
              AND r.rolcanlogin AND NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb
              AND NOT r.rolcreaterole AND NOT r.rolreplication AND NOT r.rolinherit
              AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members m WHERE m.member = r.oid OR m.roleid = r.oid)
              AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_database d WHERE d.datname = current_database() AND d.datdba = r.oid)
              AND (NOT @tenant_check OR COALESCE(pg_catalog.current_setting('app.current_tenant_id', true), '') = @tenant)
              AND (NOT @serializable OR pg_catalog.current_setting('transaction_isolation') = 'serializable')
            FROM pg_catalog.pg_roles r WHERE r.rolname = @role
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("role", binding.Binding.Role); command.Parameters.AddWithValue("database", binding.Database);
            command.Parameters.AddWithValue("tenant_check", checkTenant); command.Parameters.AddWithValue("tenant", tenant ?? "");
            command.Parameters.AddWithValue("serializable", serializable);
            if (await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not true) throw NativeErrors.Denied();
        }
        foreach (var table in binding.Binding.Tables)
        {
            await using var command = new NpgsqlCommand("""
                SELECT c.relrowsecurity AND c.relforcerowsecurity AND c.relkind IN ('r', 'p')
                  AND c.relowner <> (SELECT oid FROM pg_catalog.pg_roles WHERE rolname = @role)
                  AND NOT pg_catalog.has_schema_privilege(@role, n.oid, 'CREATE')
                  AND NOT pg_catalog.has_table_privilege(@role, c.oid, 'TRUNCATE')
                  AND NOT pg_catalog.has_table_privilege(@role, c.oid, 'TRIGGER')
                  AND EXISTS (SELECT 1 FROM pg_catalog.pg_attribute a WHERE a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped)
                  AND NOT p.polpermissive AND p.polcmd = '*' AND p.polroles = ARRAY[0::oid]
                  AND pg_catalog.pg_get_expr(p.polqual, p.polrelid) = @using
                  AND pg_catalog.pg_get_expr(p.polwithcheck, p.polrelid) = @check
                FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                  JOIN pg_catalog.pg_policy p ON p.polrelid = c.oid AND p.polname = @policy
                WHERE n.nspname = @schema AND c.relname = @table
                """, connection, transaction);
            command.Parameters.AddWithValue("role", binding.Binding.Role); command.Parameters.AddWithValue("schema", table.Schema);
            command.Parameters.AddWithValue("table", table.Name); command.Parameters.AddWithValue("policy", table.GuardPolicy);
            command.Parameters.AddWithValue("using", table.ExpectedUsing); command.Parameters.AddWithValue("check", table.ExpectedCheck);
            if (await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not true) throw NativeErrors.Invalid();
        }
    }
}
