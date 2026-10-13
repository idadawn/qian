using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;
using StackExchange.Redis;

namespace Qian.Data.Tests;

/// <summary>真实 Redis：WorkerId 租约在副本间互斥，正常停止与崩溃后的编号都能回收复用，复用后不产生重复 ID。</summary>
public sealed class SnowflakeIdTests(RedisFixture redis) : IClassFixture<RedisFixture>, IAsyncLifetime
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(2);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private ConnectionMultiplexer _admin = null!;

    public async ValueTask InitializeAsync()
    {
        _admin = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        // 每个用例从空的编号空间开始。
        foreach (var key in _admin.GetServers().Single().Keys(pattern: "qian:snowflake:worker:*")) await _admin.GetDatabase().KeyDeleteAsync(key);
    }

    public async ValueTask DisposeAsync() => await _admin.DisposeAsync();

    [Fact]
    public async Task 三个副本WorkerId互不相同_重启后租约回收复用且不产生重复ID()
    {
        var a = await StartAsync(); var b = await StartAsync(); var c = await StartAsync();
        Assert.Equal([0, 1, 2], new[] { a, b, c }.Select(replica => replica.Ids.WorkerId).Order());
        // 占位带 TTL，持有者标识是「机器/进程/随机串」，不是空值。
        Assert.InRange((await _admin.GetDatabase().KeyTimeToLiveAsync("qian:snowflake:worker:1"))!.Value, TimeSpan.FromMilliseconds(1), Lease);
        Assert.Equal(3, (await _admin.GetDatabase().StringGetAsync("qian:snowflake:worker:1")).ToString().Split('/').Length);
        var batches = await Task.WhenAll(new[] { a, b, c }.Select(replica => Task.Run(() => Generate(replica.Ids, 5000), Ct)));
        Assert.Equal(15000, batches.SelectMany(batch => batch).Distinct().Count());
        foreach (var (replica, batch) in new[] { a, b, c }.Zip(batches))
        {
            Assert.All(batch, id => Assert.Equal(replica.Ids.WorkerId, (int)((id >> 12) & 1023)));
            Assert.True(batch.SequenceEqual(batch.Order()), "同一副本的 ID 必须严格递增");
        }

        // 正常停止：释放编号并把高水位收紧到实际用到的毫秒；重启的副本拿回最小空闲编号，ID 接在其后。
        var worker = b.Ids.WorkerId;
        var last = batches[1].Max();
        await b.Host.StopAsync(Ct);
        b.Host.Dispose();
        Assert.False(await _admin.GetDatabase().KeyExistsAsync("qian:snowflake:worker:" + worker));
        var restarted = await StartAsync();
        Assert.Equal(worker, restarted.Ids.WorkerId);
        Assert.True(restarted.Ids.NextId() > last);

        // 崩溃：不释放，编号在租约到期前不能被别人取得，到期后被回收，新持有者从高水位之后发号。
        var crashedWorker = c.Ids.WorkerId;
        var crashedLast = c.Ids.NextId();
        c.Host.Dispose();
        var bystander = await StartAsync();
        Assert.NotEqual(crashedWorker, bystander.Ids.WorkerId);
        await Task.Delay(Lease + TimeSpan.FromMilliseconds(500), Ct);
        var successor = await StartAsync();
        Assert.Equal(crashedWorker, successor.Ids.WorkerId);
        Assert.True(successor.Ids.NextId() > crashedLast);

        foreach (var replica in new[] { a, restarted, bystander, successor })
        {
            await replica.Host.StopAsync(Ct);
            replica.Host.Dispose();
        }
        // 全部正常停止后只剩各编号的高水位，没有残留占位。
        Assert.DoesNotContain(_admin.GetServers().Single().Keys(pattern: "qian:snowflake:worker:*"), key => !key.ToString().EndsWith(":hwm", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 租约被他人持有时停发并改取空闲编号_停止后不再发号()
    {
        var replica = await StartAsync();
        var worker = replica.Ids.WorkerId;
        var before = replica.Ids.NextId();
        // 模拟租约过期后被别人取得：占位换成他人的持有者标识。
        await _admin.GetDatabase().StringSetAsync("qian:snowflake:worker:" + worker, "intruder", TimeSpan.FromMinutes(1));
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        // 发现失租后先停发（WorkerId 不可用），随后取得另一个空闲编号。
        while (CurrentWorker(replica.Ids) is var current && (current == worker || current < 0))
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "续租没有发现租约已被他人持有");
            await Task.Delay(50, Ct);
        }
        var moved = replica.Ids.WorkerId;
        Assert.NotEqual(worker, moved);
        Assert.True(replica.Ids.NextId() > before);
        Assert.Equal("intruder", (await _admin.GetDatabase().StringGetAsync("qian:snowflake:worker:" + worker)).ToString());

        await replica.Host.StopAsync(Ct);
        Assert.Equal(DataErrors.IdWorkerUnavailable, Assert.Throws<QianErrorException>(() => replica.Ids.NextId()).Error.Code);
        replica.Host.Dispose();
    }

    [Fact]
    public async Task 时钟回拨与同一毫秒大量取号仍严格递增()
    {
        var clock = new ManualClock { Now = DateTimeOffset.UtcNow };
        var replica = await StartAsync(clock);
        var first = Generate(replica.Ids, 10000);
        Assert.True(first.SequenceEqual(first.Order()) && first.Distinct().Count() == first.Length);
        clock.Now = clock.Now.AddSeconds(-30);
        var second = Generate(replica.Ids, 100);
        Assert.True(second[0] > first[^1] && second.SequenceEqual(second.Order()));
        await replica.Host.StopAsync(Ct);
        replica.Host.Dispose();
    }

    private async Task<Replica> StartAsync(TimeProvider? clock = null)
    {
        var builder = Host.CreateApplicationBuilder();
        if (clock is not null) builder.Services.AddSingleton(clock);
        builder.Services.AddSingleton<IConnectionMultiplexer>(await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString));
        builder.Services.AddQianSnowflakeIds(options =>
        {
            options.LeaseDuration = Lease;
            options.RenewInterval = TimeSpan.FromMilliseconds(200);
        });
        var host = builder.Build();
        await host.StartAsync(Ct);
        return new Replica(host, host.Services.GetRequiredService<ISnowflakeIdGenerator>());
    }

    private static long[] Generate(ISnowflakeIdGenerator ids, int count) => Enumerable.Range(0, count).Select(_ => ids.NextId()).ToArray();

    private static int CurrentWorker(ISnowflakeIdGenerator ids)
    {
        try { return ids.WorkerId; }
        catch (QianErrorException) { return -1; }
    }

    private sealed record Replica(IHost Host, ISnowflakeIdGenerator Ids);

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
