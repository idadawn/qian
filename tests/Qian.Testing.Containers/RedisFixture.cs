using Testcontainers.Redis;

namespace Qian.Testing.Containers;

/// <summary>Redis 7 容器。</summary>
public sealed class RedisFixture : ContainerFixture<RedisContainer>
{
    /// <summary>StackExchange.Redis 格式的连接串（<c>host:port</c>）。</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <inheritdoc />
    protected override RedisContainer CreateContainer() =>
        new RedisBuilder(ContainerImages.Redis).Build();

    /// <inheritdoc />
    protected override ValueTask OnStartedAsync(RedisContainer container, CancellationToken cancellationToken)
    {
        ConnectionString = container.GetConnectionString();
        return ValueTask.CompletedTask;
    }
}
