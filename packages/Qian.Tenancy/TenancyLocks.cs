using Microsoft.EntityFrameworkCore;
using Qian.Data;
using Qian.Runtime;

namespace Qian.Tenancy;

// 会话级 advisory lock：同一键的开户、开通续跑与管理员替换串行（键形如 tenant:<id>）；进程退出即释放。
internal static class TenancyLocks
{
    internal static async Task<T> RunAsync<T>(IPluginContext context, string key, Func<Task<T>> work, CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
        await session.Context.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        var connection = session.Context.Database.GetDbConnection();
        var lockKey = "qian.tenancy:" + key;
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_lock(hashtextextended(@key, 0))";
        var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.Value = lockKey; command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        try { return await work().ConfigureAwait(false); }
        finally
        {
            command.CommandText = "SELECT pg_advisory_unlock(hashtextextended(@key, 0))";
            await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
