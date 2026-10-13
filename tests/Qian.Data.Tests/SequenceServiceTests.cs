using System.Collections.Concurrent;
using System.Globalization;
using Kun.Core.MultiTenancy;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;

namespace Qian.Data.Tests;

/// <summary>真实 PostgreSQL 与受 RLS 约束的应用角色：并发取号、规则修改、周期与作用域、租户隔离和用尽。</summary>
public sealed class SequenceServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string TenantA = "01900000-0000-7000-8000-00000000a001";
    private const string TenantB = "01900000-0000-7000-8000-00000000b001";
    private const string TenantC = "01900000-0000-7000-8000-00000000c001";
    private static readonly ConcurrentDictionary<PostgresFixture, Lazy<Task>> Migrated = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 同一规则100并发取号无重复无跳号_规则修改后从1重新开始()
    {
        await using var host = await StartAsync(new DateTimeOffset(2026, 10, 14, 1, 0, 0, TimeSpan.Zero));
        using (host.Tenant.BeginScope(TenantA))
        {
            var rule = await host.Sequences.DefineAsync("order", "'SO'+yyyyMMdd + seq(6)", Ct);
            Assert.Equal(new SequenceRule("order", "'SO' + yyyyMMdd + seq(6)", 1), rule);
            var numbers = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => host.Sequences.NextAsync("order", "default", Ct), Ct)));
            Assert.Equal(100, numbers.Distinct(StringComparer.Ordinal).Count());
            Assert.All(numbers, number => Assert.StartsWith("SO20261014", number, StringComparison.Ordinal));
            Assert.Equal(Enumerable.Range(1, 100), numbers.Select(number => int.Parse(number[^6..], CultureInfo.InvariantCulture)).Order());

            // 重复定义同一模式不改修订号，流水接着走。
            Assert.Equal(1, (await host.Sequences.DefineAsync("order", "'SO' + yyyyMMdd + seq(6)", Ct)).Revision);
            Assert.Equal("SO20261014000101", await host.Sequences.NextAsync("order", "default", Ct));

            // 修改模式：修订号加一，流水从 1 重新开始。
            Assert.Equal(2, (await host.Sequences.DefineAsync("order", "'SO-' + yyyyMMdd + seq(6)", Ct)).Revision);
            Assert.Equal("SO-20261014000001", await host.Sequences.NextAsync("order", "default", Ct));
            Assert.Equal("SO-20261014000002", await host.Sequences.NextAsync("order", "default", Ct));

            // 作用域彼此独立；日期周期变化时重新开始。
            Assert.Equal("SO-20261014000001", await host.Sequences.NextAsync("order", "app:crm", Ct));
            host.Clock.Now = host.Clock.Now.AddDays(1);
            Assert.Equal("SO-20261015000001", await host.Sequences.NextAsync("order", "default", Ct));
        }
        using (host.Tenant.BeginScope(TenantB))
        {
            // 另一租户看不到 A 的规则，定义后计数独立。
            Assert.Equal(DataErrors.SequenceRuleNotFound, (await Assert.ThrowsAsync<QianErrorException>(() => host.Sequences.NextAsync("order", "default", Ct))).Error.Code);
            await host.Sequences.DefineAsync("order", "'B' + seq(4)", Ct);
            Assert.Equal("B0001", await host.Sequences.NextAsync("order", "default", Ct));
        }
        // 计数器表受 RLS 约束：应用角色在租户 B 的会话里只看得到 B 的行。
        Assert.Equal(1L, await AppCountAsync(TenantB, "SELECT count(*) FROM plugin_qian_data.sequence_counters"));
        Assert.Equal(4L, await AppCountAsync(TenantA, "SELECT count(*) FROM plugin_qian_data.sequence_counters"));
    }

    [Fact]
    public async Task 非法规则_用尽与缺少租户都返回结构化错误()
    {
        await using var host = await StartAsync(new DateTimeOffset(2026, 10, 14, 1, 0, 0, TimeSpan.Zero));
        await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => host.Sequences.NextAsync("order", "default", Ct));
        using (host.Tenant.BeginScope(TenantC))
        {
            foreach (var pattern in new[] { "'SO' + yyyyMMdd", "seq(6) + seq(2)", "'SO' + yyyymmdd + seq(6)", "'unterminated + seq(3)", "seq(19)", "'A' + + seq(3)" })
                Assert.Equal(DataErrors.SequenceRuleInvalid, (await Assert.ThrowsAsync<QianErrorException>(() => host.Sequences.DefineAsync("bad", pattern, Ct))).Error.Code);
            Assert.Equal(DataErrors.SequenceRuleInvalid, (await Assert.ThrowsAsync<QianErrorException>(() => host.Sequences.DefineAsync("Bad Code", "seq(3)", Ct))).Error.Code);
            Assert.Equal(DataErrors.SequenceRuleInvalid, (await Assert.ThrowsAsync<QianErrorException>(() => host.Sequences.NextAsync("tiny", "bad scope", Ct))).Error.Code);
            await host.Sequences.DefineAsync("tiny", "seq(1)", Ct);
            for (var expected = 1; expected <= 9; expected++)
                Assert.Equal(expected.ToString(CultureInfo.InvariantCulture), await host.Sequences.NextAsync("tiny", "default", Ct));
            Assert.Equal(DataErrors.SequenceExhausted, (await Assert.ThrowsAsync<QianErrorException>(() => host.Sequences.NextAsync("tiny", "default", Ct))).Error.Code);
        }
    }

    [Fact]
    public void 模式解析_规范化_周期与格式()
    {
        var pattern = SequencePattern.Parse("  'PO' +yyMM+ '-' + seq(4) ");
        Assert.Equal("'PO' + yyMM + '-' + seq(4)", pattern.Text);
        Assert.Equal((4, 9999L), (pattern.Digits, pattern.MaxValue));
        var at = new DateTime(2026, 10, 14, 9, 30, 0);
        Assert.Equal("2610", pattern.Period(at));
        Assert.Equal("PO2610-0042", pattern.Format(at, 42));
        Assert.Equal("", SequencePattern.Parse("seq(3)").Period(at));
        Assert.Equal("2026|1014", SequencePattern.Parse("yyyy + 'X' + MMdd + seq(2)").Period(at));
        Assert.Throws<ArgumentOutOfRangeException>(() => pattern.Format(at, 10000));
        Assert.False(SequencePattern.TryParse("'" + new string('x', 33) + "' + seq(2)", out _, out var error));
        Assert.Contains("32", error, StringComparison.Ordinal);
    }

    private async Task<SequenceHost> StartAsync(DateTimeOffset now)
    {
        var owner = await DataOwner.ConnectionStringAsync(postgres);
        var clock = new ManualClock { Now = now };
        var builder = Host.CreateApplicationBuilder();
        var tenant = new AsyncLocalTenantContext();
        builder.Services.AddSingleton<ITenantContext>(tenant);
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddKunEventBus();
        // 100 个并发调用方共用 20 条连接，排队取连接而不是各占一条（PG 默认最多 100 个连接）。
        var app = new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { MaxPoolSize = 20 }.ConnectionString;
        builder.Services.AddQianSequences((_, options) => options.UseNpgsql(app));
        builder.Services.AddQianDataMigrations(options => options.ResolveConnection = (_, _) => owner);
        var host = builder.Build();
        await Migrated.GetOrAdd(postgres, _ => new Lazy<Task>(() => host.Services.GetRequiredService<IDataMigrator>().MigrateAsync(Ct))).Value;
        await host.StartAsync(Ct);
        return new SequenceHost(host, tenant, clock, host.Services.GetRequiredService<ISequenceService>());
    }

    private async Task<long> AppCountAsync(string tenant, string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.AppConnectionString);
        await connection.OpenAsync(Ct);
        await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, false)", connection))
        {
            set.Parameters.AddWithValue("tenant", tenant);
            await set.ExecuteScalarAsync(Ct);
        }
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private sealed record SequenceHost(IHost Host, AsyncLocalTenantContext Tenant, ManualClock Clock, ISequenceService Sequences) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Host.StopAsync(Ct);
            Host.Dispose();
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
