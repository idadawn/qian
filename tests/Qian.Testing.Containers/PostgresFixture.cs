using Npgsql;
using Testcontainers.PostgreSql;

namespace Qian.Testing.Containers;

/// <summary>
/// PostgreSQL 16 容器。提供两条连接串：
/// <see cref="ConnectionString"/> 是超级用户，用于建表、迁移与造数；
/// <see cref="AppConnectionString"/> 是 <c>NOSUPERUSER NOBYPASSRLS</c> 的应用角色，验证 RLS 的测试必须用它——
/// 超级用户与 <c>BYPASSRLS</c> 角色会绕过 <c>FORCE ROW LEVEL SECURITY</c>，策略对它们不可见（ADR-004）。
/// </summary>
public sealed class PostgresFixture : ContainerFixture<PostgreSqlContainer>
{
    /// <summary>测试库名。</summary>
    public const string DatabaseName = "qian_test";

    /// <summary>应用角色名：只有 DML 权限，不能建表，受 RLS 约束。</summary>
    public const string AppRoleName = "qian_app";

    private readonly string _appPassword = NewSecret();

    /// <summary>超级用户连接串（DDL、迁移、造数）。</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>应用角色连接串（受 RLS 约束的读写）。</summary>
    public string AppConnectionString { get; private set; } = string.Empty;

    /// <inheritdoc />
    protected override PostgreSqlContainer CreateContainer() =>
        new PostgreSqlBuilder(ContainerImages.Postgres)
            .WithDatabase(DatabaseName)
            .WithUsername("qian")
            .WithPassword(NewSecret())
            .Build();

    /// <inheritdoc />
    protected override async ValueTask OnStartedAsync(PostgreSqlContainer container, CancellationToken cancellationToken)
    {
        ConnectionString = container.GetConnectionString();

        // 口令是本 fixture 生成的十六进制串，可以安全地拼进 DDL（CREATE ROLE 不支持参数化）。
        var ddl = $"""
            CREATE ROLE {AppRoleName} LOGIN PASSWORD '{_appPassword}' NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            GRANT CONNECT, TEMPORARY ON DATABASE {DatabaseName} TO {AppRoleName};
            GRANT USAGE ON SCHEMA public TO {AppRoleName};
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {AppRoleName};
            ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO {AppRoleName};
            """;

        var connection = new NpgsqlConnection(ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var command = new NpgsqlCommand(ddl, connection);
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        AppConnectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = AppRoleName,
            Password = _appPassword,
        }.ConnectionString;
    }
}
