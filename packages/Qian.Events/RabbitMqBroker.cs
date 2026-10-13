using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Qian.Events;

/// <summary>发布端独占的 RabbitMQ 连接与确认通道；连接按需建立并自动恢复，通道出错即丢弃重建。</summary>
internal sealed partial class RabbitMqBroker(Func<string> amqpUri, ILogger<RabbitMqBroker> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed;

    /// <summary>发往 <see cref="QianEventTopology.Exchange"/> 并等待 broker 确认；未确认或被拒即抛出。</summary>
    public async ValueTask PublishAsync(string routingKey, BasicProperties properties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var channel = await ChannelAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // 无订阅者的事件按发布订阅语义丢弃（mandatory = false）；开启确认跟踪后，await 完成即 broker 已确认。
                await channel.BasicPublishAsync(QianEventTopology.Exchange, routingKey, mandatory: false, properties, body, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await ResetChannelAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    private async ValueTask<IChannel> ChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true }) return _channel;
        await ResetChannelAsync().ConfigureAwait(false);
        if (_connection is not { IsOpen: true })
        {
            if (_connection is not null)
            {
                // 自动恢复中的连接不能替换，否则恢复后的连接无人释放；本轮失败，由调用方按退避重试。
                throw new IOException("RabbitMQ 连接正在恢复。");
            }
            var factory = new ConnectionFactory
            {
                Uri = new Uri(amqpUri()),
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                ClientProvidedName = "qian.events.publisher",
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
            };
            _connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            Log.Connected(logger);
        }
        var channel = await _connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken).ConfigureAwait(false);
        try { await RabbitTopology.DeclareExchangesAsync(channel, cancellationToken).ConfigureAwait(false); }
        catch { await channel.DisposeAsync().ConfigureAwait(false); throw; }
        _channel = channel;
        return channel;
    }

    private async ValueTask ResetChannelAsync()
    {
        var channel = _channel;
        _channel = null;
        if (channel is null) return;
        try { await channel.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) when (error is not OutOfMemoryException) { Log.ChannelDisposeFailed(logger, error); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            await ResetChannelAsync().ConfigureAwait(false);
            if (_connection is not null) await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
        finally { _gate.Release(); }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "已连接事件 broker。")]
        public static partial void Connected(ILogger logger);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "释放发布通道失败。")]
        public static partial void ChannelDisposeFailed(ILogger logger, Exception error);
    }
}

/// <summary>PRD 7.10 的 RabbitMQ 拓扑声明；所有声明幂等，参数与已有对象不一致时 broker 会拒绝。</summary>
internal static class RabbitTopology
{
    private const int BroadcastIdleExpiryMilliseconds = 30 * 60 * 1000;

    public static async Task DeclareExchangesAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(QianEventTopology.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.ExchangeDeclareAsync(QianEventTopology.DeadLetterExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>竞争消费：每服务一个 quorum 队列，死信转 <see cref="QianEventTopology.DeadLetterExchange"/>。</summary>
    public static async Task<string> DeclareSubscriptionAsync(IChannel channel, string service, string subscription, IEnumerable<string> bindingKeys, CancellationToken cancellationToken)
    {
        var queue = QianEventTopology.SubscriptionQueue(service, subscription);
        await DeclareExchangesAsync(channel, cancellationToken).ConfigureAwait(false);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-exchange"] = QianEventTopology.DeadLetterExchange,
            ["x-dead-letter-strategy"] = "at-least-once",
            ["x-overflow"] = "reject-publish",
        }, cancellationToken: cancellationToken).ConfigureAwait(false);
        await BindAsync(channel, queue, bindingKeys, cancellationToken).ConfigureAwait(false);
        return queue;
    }

    /// <summary>
    /// 广播：每实例一个经典自动删除队列，实例的最后一个消费者断开即删除（quorum 队列不支持自动删除）。
    /// RabbitMQ 4 默认禁止非持久的非独占队列，所以声明为持久；声明后从未被消费的队列闲置 30 分钟由 broker 过期删除。
    /// </summary>
    public static async Task<string> DeclareBroadcastAsync(IChannel channel, string service, string instance, IEnumerable<string> bindingKeys, CancellationToken cancellationToken)
    {
        var queue = QianEventTopology.BroadcastQueue(service, instance);
        await DeclareExchangesAsync(channel, cancellationToken).ConfigureAwait(false);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: true, new Dictionary<string, object?>
        {
            ["x-queue-type"] = "classic",
            ["x-expires"] = BroadcastIdleExpiryMilliseconds,
        }, cancellationToken: cancellationToken).ConfigureAwait(false);
        await BindAsync(channel, queue, bindingKeys, cancellationToken).ConfigureAwait(false);
        return queue;
    }

    private static async Task BindAsync(IChannel channel, string queue, IEnumerable<string> bindingKeys, CancellationToken cancellationToken)
    {
        var keys = bindingKeys.ToArray();
        if (keys.Length == 0) throw new ArgumentException("至少需要一个绑定键。", nameof(bindingKeys));
        foreach (var key in keys)
            await channel.QueueBindAsync(queue, QianEventTopology.Exchange, key, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
