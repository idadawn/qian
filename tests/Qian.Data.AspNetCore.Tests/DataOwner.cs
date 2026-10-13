using System.Collections.Concurrent;
using System.Security.Cryptography;
using Npgsql;
using Qian.Testing.Containers;

namespace Qian.Data.AspNetCore.Tests;

/// <summary>
/// 迁移属主角色：NOSUPERUSER / NOBYPASSRLS，能在库里建 schema；它建的 schema 与表经默认权限自动授予应用角色。
/// 迁移任务用它执行 DDL，在线副本仍用受 RLS 约束的应用角色。
/// </summary>
internal static class DataOwner
{
    public const string RoleName = "qian_owner";
    private static readonly ConcurrentDictionary<PostgresFixture, Lazy<Task<string>>> Created = new();

    public static Task<string> ConnectionStringAsync(PostgresFixture postgres) =>
        Created.GetOrAdd(postgres, fixture => new Lazy<Task<string>>(() => CreateAsync(fixture))).Value;

    private static async Task<string> CreateAsync(PostgresFixture postgres)
    {
        // 口令是本测试生成的十六进制串，可以安全地拼进 DDL（CREATE ROLE 不支持参数化）。
        var password = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        await using var admin = new NpgsqlConnection(postgres.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"""
            CREATE ROLE {RoleName} LOGIN PASSWORD '{password}' NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            GRANT CONNECT, CREATE, TEMPORARY ON DATABASE {PostgresFixture.DatabaseName} TO {RoleName};
            ALTER DEFAULT PRIVILEGES FOR ROLE {RoleName} GRANT USAGE ON SCHEMAS TO {PostgresFixture.AppRoleName};
            ALTER DEFAULT PRIVILEGES FOR ROLE {RoleName} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {PostgresFixture.AppRoleName};
            """, admin);
        await command.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Username = RoleName, Password = password }.ConnectionString;
    }
}
