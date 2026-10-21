using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Qian.Identity.Server;

// 少数必须原子完成、且要读回结果的语句（错误计数、一次性票据、登录索引）用原生 SQL；
// 命令沿用 EF 的连接与当前事务，所以 Kun 的 RLS 拦截器照常生效。EF 的 FromSql 会把 UPDATE … RETURNING 包进子查询而失败，故不用它。
internal static class IdentitySql
{
    // 表名取自模型的固定 schema（Qian.Data 要求 plugin_ 前缀的唯一 schema），编译期常量，不拼接任何外部输入。
    internal const string Persons = $"{IdentityDbContext.Schema}.persons";
    internal const string PasswordHistory = $"{IdentityDbContext.Schema}.password_history";
    internal const string Sessions = $"{IdentityDbContext.Schema}.sessions";
    internal const string Memberships = $"{IdentityDbContext.Schema}.memberships";
    internal const string Tickets = $"{IdentityDbContext.Schema}.login_tickets";
    internal const string Captchas = $"{IdentityDbContext.Schema}.login_captchas";

    internal static async Task<int> ExecuteAsync(DbContext db, string sql, CancellationToken ct, params (string Name, object? Value)[] args)
    {
        await using var command = await CreateAsync(db, sql, args, ct).ConfigureAwait(false);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    internal static async Task<List<T>> QueryAsync<T>(DbContext db, string sql, Func<DbDataReader, T> map, CancellationToken ct, params (string Name, object? Value)[] args)
    {
        await using var command = await CreateAsync(db, sql, args, ct).ConfigureAwait(false);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var rows = new List<T>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) rows.Add(map(reader));
        return rows;
    }

    /// <summary>事务级咨询锁，随事务结束释放；同一键的并发事务串行。</summary>
    internal static Task LockAsync(DbContext db, string key, CancellationToken ct) =>
        ExecuteAsync(db, "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", ct, ("key", "qian.identity:" + key));

    private static async Task<DbCommand> CreateAsync(DbContext db, string sql, (string Name, object? Value)[] args, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        foreach (var (name, value) in args)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}
