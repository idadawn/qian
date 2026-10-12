using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Qian.Data;

/// <summary>在线应用连接只能使用受 RLS 约束的 PostgreSQL 角色；迁移管理员通过独立连接执行 DDL。</summary>
public sealed class QianApplicationRoleInterceptor : DbConnectionInterceptor
{
    private const string RoleQuery = "SELECT coalesce(bool_or(rolsuper OR rolbypassrls), true) FROM pg_roles WHERE rolname IN (current_user, session_user)";

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        try { Validate(connection); }
        catch { connection.Close(); throw; }
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        try { await ValidateAsync(connection, cancellationToken).ConfigureAwait(false); }
        catch { await connection.CloseAsync().ConfigureAwait(false); throw; }
    }

    /// <summary>检查已经打开的连接，包括宿主提供的预打开连接；同时校验登录角色和当前角色。</summary>
    public static void Validate(DbConnection connection)
    {
        EnsureOpen(connection);
        using var command = connection.CreateCommand();
        command.CommandText = RoleQuery;
        EnsureRestricted(command.ExecuteScalar());
    }

    /// <summary>异步检查已经打开的连接；不创建表、不迁移、不修改角色或租户会话。</summary>
    public static async Task ValidateAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        EnsureOpen(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = RoleQuery;
        EnsureRestricted(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private static void EnsureOpen(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State != ConnectionState.Open)
            throw new QianDataMisconfiguredException("应用角色校验要求数据库连接已经打开。");
    }

    private static void EnsureRestricted(object? privileged)
    {
        if (privileged is not false)
            throw new QianDataMisconfiguredException("在线应用禁止使用超级用户或 BYPASSRLS 角色；请使用独立的受限应用凭据。");
    }
}
