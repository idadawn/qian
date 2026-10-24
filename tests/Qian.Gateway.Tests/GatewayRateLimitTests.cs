using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Kun.Gateway.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;
using StackExchange.Redis;

namespace Qian.Gateway.Tests;

public sealed class GatewayRateLimitTests
{
    private const string TenantA = "01900000-0000-7000-8000-000000000001";
    private const string TenantB = "01900000-0000-7000-8000-000000000002";
    private static GatewayRateLimitPolicy Policy(string policy, IRedisRateLimitStore? store = null) => new(new(
        "https://id.test", "api", "https://gateway.test/s2s", "/private.pem", "/public.pem", [],
        [new("one", "/one", "api") { RateLimiterPolicy = policy }, new("two", "/two", "api") { RateLimiterPolicy = policy }], [new("api", "http://api/")])
    { RateLimits = new() { IpPermitLimit = 3, TenantPermitLimit = 5, UserPermitLimit = 7 } }, store ?? new Counter());

    private static DefaultHttpContext Context(string? tenant = TenantA, string subject = "alice", bool authenticated = true)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.User = new(new ClaimsIdentity([
            new("sub", subject), new("principal_kind", "user"),
            tenant is null ? new("tenant_id", "null", JsonClaimValueTypes.JsonNull) : new("tenant_id", tenant)
        ], authenticated ? "verified" : null));
        return context;
    }

    [Fact]
    public void 三种策略按可信IP_租户_租户内主体隔离_且按路由隔离()
    {
        var ip = Policy("kun-default"); var tenant = Policy("tenant"); var user = Policy("user");
        var alice = Context(); var bob = Context(subject: "bob"); var other = Context(TenantB);
        Assert.Equal(ip.Partition(alice, "one"), ip.Partition(other, "one"));
        Assert.Equal(3, ip.Partition(alice, "one").Limit);
        Assert.Equal(tenant.Partition(alice, "one"), tenant.Partition(bob, "one"));
        Assert.NotEqual(tenant.Partition(alice, "one").Key, tenant.Partition(other, "one").Key);
        Assert.Equal(5, tenant.Partition(alice, "one").Limit);
        Assert.NotEqual(user.Partition(alice, "one").Key, user.Partition(bob, "one").Key);
        Assert.NotEqual(user.Partition(alice, "one").Key, user.Partition(other, "one").Key);
        Assert.Equal(7, user.Partition(alice, "one").Limit);
        Assert.NotEqual(user.Partition(alice, "one").Key, user.Partition(alice, "two").Key);
        Assert.Matches("^[0-9a-f]{64}$", user.Partition(alice, "one").Key);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("user")]
    public void 匿名及不规范身份退回IP_请求头不能选择身份(string policyName)
    {
        var policy = Policy(policyName); var anonymous = Context(authenticated: false);
        var expected = policy.Partition(anonymous, "one");
        Assert.Equal(3, expected.Limit);
        anonymous.Request.Headers["X-Tenant-Id"] = TenantB; anonymous.Request.Headers["X-Forwarded-For"] = "198.51.100.1";
        Assert.Equal(expected, policy.Partition(anonymous, "one"));
        Assert.Equal(expected, policy.Partition(Context("invalid"), "one"));
        var duplicate = Context(); ((ClaimsIdentity)duplicate.User.Identity!).AddClaim(new("tenant_id", TenantB));
        Assert.Equal(expected, policy.Partition(duplicate, "one"));
    }

    [Fact]
    public void IPv4映射地址与原地址同桶_平台用户使用独立主体桶()
    {
        var ip = Policy("kun-default"); var first = Context(); var mapped = Context();
        mapped.Connection.RemoteIpAddress = IPAddress.Loopback.MapToIPv6();
        Assert.Equal(ip.Partition(first, "one"), ip.Partition(mapped, "one"));
        var user = Policy("user"); var platform = Context(null);
        Assert.Equal(7, user.Partition(platform, "one").Limit);
        Assert.NotEqual(user.Partition(platform, "one").Key, user.Partition(first, "one").Key);
    }

    [Fact]
    public async Task 同步探测不执行Redis_异步请求只记一次并提供重试时长()
    {
        var store = new Counter(); using var limiter = new GatewayRedisRateLimiter(store, "key", 1, TimeSpan.FromSeconds(20));
        using var probe = limiter.AttemptAcquire(); Assert.False(probe.IsAcquired); Assert.Equal(0, store.Hits);
        using var first = await limiter.AcquireAsync(cancellationToken: TestContext.Current.CancellationToken); Assert.True(first.IsAcquired);
        using var second = await limiter.AcquireAsync(cancellationToken: TestContext.Current.CancellationToken); Assert.False(second.IsAcquired);
        Assert.Equal(2, store.Hits); Assert.True(second.TryGetMetadata(MetadataName.RetryAfter, out var retry)); Assert.Equal(TimeSpan.FromSeconds(20), retry);
    }

    [Fact]
    public async Task Redis故障必须失败关闭且不伪装为超限()
    {
        using var limiter = new GatewayRedisRateLimiter(new Unavailable(), "key", 100, TimeSpan.FromMinutes(1));
        var failure = await Assert.ThrowsAsync<QianErrorException>(() => limiter.AcquireAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(PlatformErrors.Unavailable, failure.Error.Code);
    }

    private sealed class Counter : IRedisRateLimitStore
    {
        public int Hits;
        public Task<RateLimitHitResult> HitAsync(string partitionKey, TimeSpan window, CancellationToken cancellationToken = default)
            => Task.FromResult(new RateLimitHitResult(Interlocked.Increment(ref Hits), window));
    }
    private sealed class Unavailable : IRedisRateLimitStore
    {
        public Task<RateLimitHitResult> HitAsync(string partitionKey, TimeSpan window, CancellationToken cancellationToken = default)
            => Task.FromException<RateLimitHitResult>(new RedisConnectionException(ConnectionFailureType.SocketFailure, "测试连接故障"));
    }
}

public sealed class GatewayRateLimitStoreTests(RedisFixture redis) : IClassFixture<RedisFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 两个计数器原子共享额度并设置过期_后续命中不续期()
    {
        using var connectionA = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        using var connectionB = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var prefix = "qian:gw-counter:" + Guid.NewGuid().ToString("N");
        var first = new GatewayRedisRateLimitStore(connectionA, prefix); var second = new GatewayRedisRateLimitStore(connectionB, prefix);
        var hits = await Task.WhenAll(Enumerable.Range(0, 200).Select(index => (index % 2 == 0 ? first : second).HitAsync("shared", TimeSpan.FromSeconds(5), Ct)));
        Assert.Equal(Enumerable.Range(1, 200).Select(value => (long)value), hits.Select(hit => hit.Count).Order());
        var before = await connectionA.GetDatabase().KeyTimeToLiveAsync(prefix + ":shared");
        Assert.NotNull(before); Assert.True(before > TimeSpan.Zero);
        await Task.Delay(150, Ct);
        var next = await first.HitAsync("shared", TimeSpan.FromSeconds(5), Ct);
        Assert.Equal(201, next.Count); Assert.True(next.ResetsIn < before);
    }

    [Fact]
    public async Task 窗口结束自动重新计数_没有TTL的遗留桶被修复()
    {
        using var connection = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var prefix = "qian:gw-counter:" + Guid.NewGuid().ToString("N");
        var store = new GatewayRedisRateLimitStore(connection, prefix);
        Assert.Equal(1, (await store.HitAsync("short", TimeSpan.FromMilliseconds(100), Ct)).Count);
        await Task.Delay(200, Ct);
        Assert.Equal(1, (await store.HitAsync("short", TimeSpan.FromMilliseconds(100), Ct)).Count);
        await connection.GetDatabase().StringSetAsync(prefix + ":orphan", 10);
        Assert.Equal(11, (await store.HitAsync("orphan", TimeSpan.FromSeconds(5), Ct)).Count);
        Assert.NotNull(await connection.GetDatabase().KeyTimeToLiveAsync(prefix + ":orphan"));
    }
}
