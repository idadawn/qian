using System.Data.Common;
using Kun.Core.MultiTenancy;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Qian.Testing.Containers;

namespace Qian.Data.Tests;

/// <summary>真实 PostgreSQL：两个副本同时迁移，只有拿到 advisory lock 的一个执行 DDL；另一个等锁后只校验。</summary>
public sealed class MigrationCoordinationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 两个副本同时启动并执行迁移_只有一个执行DDL_另一个等锁后校验通过()
    {
        var owner = await DataOwner.ConnectionStringAsync(postgres);
        // 迁移前直接启动的副本只校验，发现未应用的迁移即拒绝启动。
        using (var early = Replica(owner, new DdlCounter()))
        {
            var refused = await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => early.StartAsync(Ct));
            Assert.Contains("未应用的迁移", refused.Message, StringComparison.Ordinal);
        }
        Assert.False(await SchemaExistsAsync());

        var counterA = new DdlCounter(); var counterB = new DdlCounter();
        using var a = Replica(owner, counterA);
        using var b = Replica(owner, counterB);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var migrateA = Task.Run(async () => { await gate.Task; return await a.Services.GetRequiredService<IDataMigrator>().MigrateAsync(Ct); }, Ct);
        var migrateB = Task.Run(async () => { await gate.Task; return await b.Services.GetRequiredService<IDataMigrator>().MigrateAsync(Ct); }, Ct);
        gate.SetResult();
        var reports = (await Task.WhenAll(migrateA, migrateB)).Select(Assert.Single).ToArray();

        var winner = Assert.Single(reports, report => report.Applied.Count > 0);
        var waiter = Assert.Single(reports, report => report.Applied.Count == 0);
        Assert.Equal("plugin_qian_data", winner.Schema);
        Assert.Equal(["20261013172723_Sequences"], winner.Applied);
        // 执行 DDL 的只有一个副本；另一个在锁上等到对方迁移完成（每条建表语句被拖慢 200 毫秒）。
        Assert.Equal(1, new[] { counterA.Ddl, counterB.Ddl }.Count(count => count > 0));
        Assert.True(waiter.LockWait >= TimeSpan.FromMilliseconds(300), "等锁 " + waiter.LockWait);
        Assert.Equal(1L, await ScalarAsync("SELECT count(*) FROM plugin_qian_data.\"__EFMigrationsHistory\""));

        // 两个副本随后正常启动：启动检查确认已无待应用迁移。
        await a.StartAsync(Ct);
        await b.StartAsync(Ct);
        await a.StopAsync(Ct);
        await b.StopAsync(Ct);

        // 迁移经 Qian 生成器建表：租户表强制 RLS，应用角色只拿到 DML 权限。
        Assert.Equal(true, await ScalarAsync("SELECT relrowsecurity AND relforcerowsecurity FROM pg_class WHERE oid = 'plugin_qian_data.sequence_counters'::regclass"));
        Assert.Equal(DataOwner.RoleName, await ScalarAsync("SELECT tableowner FROM pg_tables WHERE schemaname = 'plugin_qian_data' AND tablename = 'sequence_rules'"));
        Assert.Equal(false, await ScalarAsync($"SELECT has_table_privilege('{PostgresFixture.AppRoleName}', 'plugin_qian_data.sequence_rules', 'TRUNCATE')"));

        // 再迁移一次：无事可做，也不必等锁。
        var again = Assert.Single(await a.Services.GetRequiredService<IDataMigrator>().MigrateAsync(Ct));
        Assert.Empty(again.Applied);
    }

    [Fact]
    public async Task 迁移连接同样拒绝超级用户()
    {
        using var host = Replica(postgres.ConnectionString, new DdlCounter());
        await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => host.Services.GetRequiredService<IDataMigrator>().MigrateAsync(Ct));
    }

    private IHost Replica(string migrationConnection, DdlCounter counter)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddKunEventBus();
        builder.Services.AddQianSequences((_, options) => options.UseNpgsql(postgres.AppConnectionString).AddInterceptors(counter));
        builder.Services.AddQianDataMigrations(options => options.ResolveConnection = (_, _) => migrationConnection);
        return builder.Build();
    }

    private async Task<bool> SchemaExistsAsync() =>
        (bool)(await ScalarAsync("SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'plugin_qian_data')"))!;

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(Ct);
    }

    // 统计本副本发出的 DDL，并把每条建表语句拖慢，让同时启动的另一个副本确实在锁上等待。
    private sealed class DdlCounter : DbCommandInterceptor
    {
        private int _ddl;
        public int Ddl => Volatile.Read(ref _ddl);

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _ddl);
                await Task.Delay(200, cancellationToken);
            }
            return result;
        }
    }
}
