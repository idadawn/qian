using Xunit;

namespace Qian.Testing.Containers;

/// <summary>
/// 一次拉起全部四个依赖（PostgreSQL / Redis / RabbitMQ / Apollo 替身），并行启动。
/// 平台插件的集成测试通常同时需要它们，作为 collection 或 assembly fixture 使用，整组测试共享一套容器。
/// </summary>
public sealed class InfrastructureFixture : IAsyncLifetime
{
    /// <summary>PostgreSQL 16。</summary>
    public PostgresFixture Postgres { get; } = new();

    /// <summary>Redis 7。</summary>
    public RedisFixture Redis { get; } = new();

    /// <summary>RabbitMQ。</summary>
    public RabbitMqFixture RabbitMq { get; } = new();

    /// <summary>Apollo 协议替身。</summary>
    public ApolloFixture Apollo { get; } = new();

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        try
        {
            await Task.WhenAll(
                Postgres.InitializeAsync().AsTask(),
                Redis.InitializeAsync().AsTask(),
                RabbitMq.InitializeAsync().AsTask(),
                Apollo.InitializeAsync().AsTask()).ConfigureAwait(false);
        }
        catch
        {
            // 有一个没起来就整体失败（或整体跳过），先把已经起来的释放掉。
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(
            Apollo.DisposeAsync().AsTask(),
            RabbitMq.DisposeAsync().AsTask(),
            Redis.DisposeAsync().AsTask(),
            Postgres.DisposeAsync().AsTask()).ConfigureAwait(false);
    }
}
