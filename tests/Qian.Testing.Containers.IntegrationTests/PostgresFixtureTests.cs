using Npgsql;

namespace Qian.Testing.Containers.IntegrationTests;

[Collection(SharedInfrastructure.Name)]
public sealed class PostgresFixtureTests(InfrastructureFixture infrastructure)
{
    private readonly PostgresFixture _postgres = infrastructure.Postgres;

    [Fact]
    public async Task Server_is_PostgreSQL_16()
    {
        var connection = await OpenAsync(_postgres.ConnectionString);
        await using (connection)
        {
            var major = await ScalarAsync<int>(connection, "SELECT current_setting('server_version_num')::int / 10000");
            Assert.Equal(16, major);
        }
    }

    [Fact]
    public async Task App_role_cannot_bypass_row_level_security()
    {
        var connection = await OpenAsync(_postgres.AppConnectionString);
        await using (connection)
        {
            Assert.Equal(PostgresFixture.AppRoleName, await ScalarAsync<string>(connection, "SELECT current_user"));
            Assert.False(await ScalarAsync<bool>(connection, "SELECT rolsuper FROM pg_roles WHERE rolname = current_user"));
            Assert.False(await ScalarAsync<bool>(connection, "SELECT rolbypassrls FROM pg_roles WHERE rolname = current_user"));
        }
    }

    [Fact]
    public async Task Rls_policy_filters_rows_for_app_role_by_tenant_setting()
    {
        var table = $"rls_probe_{Guid.NewGuid():N}";
        var admin = await OpenAsync(_postgres.ConnectionString);
        await using (admin)
        {
            await ExecuteAsync(admin, $"""
                CREATE TABLE {table} (id int PRIMARY KEY, tenant_id text NOT NULL);
                ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON {table} USING (tenant_id = current_setting('app.tenant_id', true));
                INSERT INTO {table} VALUES (1, 't-a'), (2, 't-a'), (3, 't-b');
                """);
        }

        var app = await OpenAsync(_postgres.AppConnectionString);
        await using (app)
        {
            // 未设置租户：fail-closed，一行也看不到（PRD §14.3 示例 1 的直连 DB 通道）。
            Assert.Equal(0L, await ScalarAsync<long>(app, $"SELECT count(*) FROM {table}"));

            await ExecuteAsync(app, "SET app.tenant_id = 't-a'");
            Assert.Equal(2L, await ScalarAsync<long>(app, $"SELECT count(*) FROM {table}"));

            await ExecuteAsync(app, "SET app.tenant_id = 't-b'");
            Assert.Equal(3, await ScalarAsync<int>(app, $"SELECT id FROM {table}"));

            // 应用角色不能建表：DDL 只走迁移角色。
            var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(app, "CREATE TABLE app_role_ddl (id int)"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
        }
    }

    private static async Task<NpgsqlConnection> OpenAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return (T)value!;
    }
}
