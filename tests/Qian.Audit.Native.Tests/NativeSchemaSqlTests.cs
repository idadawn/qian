using System.Security.Cryptography;
using Npgsql;
using Qian.Testing.Containers;

namespace Qian.Audit.Native.Tests;

public sealed class NativeSchemaSqlTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task 原生迁移幂等_固定RLS表达式与函数归属可核查()
    {
        var ct = TestContext.Current.CancellationToken;
        var roles = new NativeAuditRoles("audit_native_template_tenant", "audit_native_template_owner", "audit_native_template_platform");
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await using var db = new NpgsqlConnection(postgres.ConnectionString); await db.OpenAsync(ct);
        await using (var create = new NpgsqlCommand($"""
            CREATE ROLE audit_native_template_tenant LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}';
            CREATE ROLE audit_native_template_platform LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}';
            CREATE ROLE audit_native_template_owner NOLOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;
            """, db)) await create.ExecuteNonQueryAsync(ct);
        await NativeAuditStorage.MigrateAsync(db, roles, ct);
        await using var query = new NpgsqlCommand("SELECT pg_get_expr(p.polqual,p.polrelid) FROM pg_policy p JOIN pg_class c ON c.oid=p.polrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='plugin_qian_audit' AND c.relname='audit_entries' AND p.polname='qian_audit_native_guard'", db);
        var guard = (string)(await query.ExecuteScalarAsync(ct))!;
        Assert.Equal(NativeAuditStorage.ExpectedGuard(roles), guard);
        await NativeAuditStorage.MigrateAsync(db, roles, ct);
        Assert.Equal(guard, await query.ExecuteScalarAsync(ct));
        if (Environment.GetEnvironmentVariable("QIAN_AUDIT_GUARD_CAPTURE") is { Length: > 0 } destination) await File.WriteAllTextAsync(destination, guard, ct);
        await using var owners = new NpgsqlCommand("SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace JOIN pg_roles r ON r.oid=p.proowner WHERE n.nspname='plugin_qian_audit' AND p.proname IN ('native_context','native_tip','native_append') AND r.rolname='audit_native_template_owner'", db);
        Assert.Equal(3L, await owners.ExecuteScalarAsync(ct));
    }
}
