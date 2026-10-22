using System.Security.Cryptography;
using Npgsql;
using Qian.Testing.Containers;

namespace Qian.Testing.NativeData;

internal sealed record NativeDataDatabase(string Schema, string TenantRole, string PlatformRole,
    string TenantConnection, string PlatformConnection, string Predicate)
{
    public override string ToString() => "NativeDataDatabase(redacted)";
    internal static async Task<NativeDataDatabase> Create(PostgresFixture postgres, CancellationToken ct)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12]; var schema = "plugin_native_" + suffix;
        var tenantRole = "native_t_" + suffix; var platformRole = "native_p_" + suffix;
        var tenantPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var platformPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var predicate = $"((tenant_id = NULLIF(current_setting('app.current_tenant_id'::text, true), ''::text)) OR ((tenant_id IS NULL) AND (CURRENT_USER = '{platformRole}'::name)))";
        await Admin($"""
            CREATE ROLE {tenantRole} LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{tenantPassword}';
            CREATE ROLE {platformRole} LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{platformPassword}';
            CREATE SCHEMA {schema}; CREATE TABLE {schema}.items (tenant_id text, value text NOT NULL);
            ALTER TABLE {schema}.items ENABLE ROW LEVEL SECURITY; ALTER TABLE {schema}.items FORCE ROW LEVEL SECURITY;
            CREATE POLICY native_access ON {schema}.items AS PERMISSIVE FOR ALL TO PUBLIC USING (true) WITH CHECK (true);
            CREATE POLICY native_guard ON {schema}.items AS RESTRICTIVE FOR ALL TO PUBLIC USING ({predicate}) WITH CHECK ({predicate});
            GRANT USAGE ON SCHEMA {schema} TO {tenantRole}, {platformRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON {schema}.items TO {tenantRole}, {platformRole};
            """);
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Username = tenantRole, Password = tenantPassword, MaxPoolSize = 1, Enlist = false }.ConnectionString;
        var platform = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Username = platformRole, Password = platformPassword, MaxPoolSize = 1, Enlist = false }.ConnectionString;
        return new(schema, tenantRole, platformRole, connection, platform, predicate);
        async Task Admin(string sql)
        {
            await using var db = new NpgsqlConnection(postgres.ConnectionString); await db.OpenAsync(ct);
            await using var command = new NpgsqlCommand(sql, db); await command.ExecuteNonQueryAsync(ct);
        }
    }
}
