using StackExchange.Redis;

namespace Qian.Testing.Containers.IntegrationTests;

[Collection(SharedInfrastructure.Name)]
public sealed class RedisFixtureTests(InfrastructureFixture infrastructure)
{
    [Fact]
    public async Task Server_is_Redis_7_and_round_trips_values()
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(infrastructure.Redis.ConnectionString);
        var server = redis.GetServer(redis.GetEndPoints()[0]);
        Assert.Equal(7, server.Version.Major);

        var key = $"qian:test:{Guid.NewGuid():N}";
        var db = redis.GetDatabase();
        Assert.True(await db.StringSetAsync(key, "乾", TimeSpan.FromMinutes(1)));
        Assert.Equal("乾", (string?)await db.StringGetAsync(key));
    }
}
