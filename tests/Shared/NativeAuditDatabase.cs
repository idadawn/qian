using System.Security.Cryptography;
using Npgsql;
using Qian.Audit.Native;
using Qian.Testing.Containers;

namespace Qian.Testing.NativeAudit;

internal sealed record NativeAuditDatabase(NativeAuditRoles Roles, string TenantConnection, string PlatformConnection)
{
    public override string ToString() => "NativeAuditDatabase(redacted)";
    internal static async Task<NativeAuditDatabase> Create(PostgresFixture postgres, CancellationToken ct, bool reset = true)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var roles = new NativeAuditRoles("audit_t_" + suffix, "audit_w_" + suffix, "audit_p_" + suffix);
        var tenantPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var platformPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await using var db = new NpgsqlConnection(postgres.ConnectionString); await db.OpenAsync(ct);
        await using (var command = new NpgsqlCommand($"""
            {(reset ? "DROP SCHEMA IF EXISTS plugin_qian_audit CASCADE;" : "")}
            CREATE ROLE {roles.TenantLogin} LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{tenantPassword}';
            CREATE ROLE {roles.PlatformLogin} LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{platformPassword}';
            CREATE ROLE {roles.FunctionOwner} NOLOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;
            """, db)) await command.ExecuteNonQueryAsync(ct);
        await NativeAuditStorage.MigrateAsync(db, roles, ct);
        string Connection(string role, string password) => new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        { Username = role, Password = password, MaxPoolSize = 20, Enlist = false }.ConnectionString;
        return new(roles, Connection(roles.TenantLogin, tenantPassword), Connection(roles.PlatformLogin!, platformPassword));
    }
}
