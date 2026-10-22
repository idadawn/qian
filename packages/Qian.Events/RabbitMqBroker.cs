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

    /// <summary>
    /// 竞争队列的 broker 侧投递上限，取默认的最多投递次数（首投 + 5 次重试）。RabbitMQ 4 的 quorum 队列只对「消费者消失」（连接或通道断开时消息未确认）计次，
    /// basic.nack 放回不计；消费端的重试一律经重试队列并确认原消息，停机时的放回也不计，所以只有崩溃循环（处理中途进程退出）会触到它：
    /// 第 7 次投递后仍未确认即由 broker 按死信队列名转入本订阅的死信队列。broker 侧死信不经过消费端的计数与日志，由死信队列深度告警兜底。
    /// 取常量而不随宿主选项变化：同一队列的各个声明方参数必须一致。
    /// </summary>
    public const int DeliveryLimit = 6;

    public static async Task DeclareExchangesAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(QianEventTopology.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.ExchangeDeclareAsync(QianEventTopology.DeadLetterExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 竞争消费：每订阅一个 quorum 队列，并声明该订阅的死信队列。broker 侧的死信（投递上限、拒绝）以死信队列名为路由键转
    /// <see cref="QianEventTopology.DeadLetterExchange"/>；at-least-once 策略下死信目标不可达时消息滞留在源队列，所以死信队列先于主队列声明。
    /// </summary>
    public static async Task<string> DeclareSubscriptionAsync(IChannel channel, string service, string subscription, IEnumerable<string> bindingKeys, CancellationToken cancellationToken)
    {
        var queue = QianEventTopology.SubscriptionQueue(service, subscription);
        var deadLetter = await DeclareDeadLetterQueueAsync(channel, service, subscription, cancellationToken).ConfigureAwait(false);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-exchange"] = QianEventTopology.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = deadLetter,
            ["x-dead-letter-strategy"] = "at-least-once",
            ["x-overflow"] = "reject-publish",
            ["x-delivery-limit"] = DeliveryLimit,
        }, cancellationToken: cancellationToken).ConfigureAwait(false);
        await BindAsync(channel, queue, bindingKeys, cancellationToken).ConfigureAwait(false);
        return queue;
    }

    /// <summary>订阅的死信队列：quorum、跨实例共享，在 <see cref="QianEventTopology.DeadLetterExchange"/> 上按自己的队列名精确绑定。</summary>
    public static async Task<string> DeclareDeadLetterQueueAsync(IChannel channel, string service, string subscription, CancellationToken cancellationToken)
    {
        var queue = QianEventTopology.DeadLetterQueue(service, subscription);
        await DeclareExchangesAsync(channel, cancellationToken).ConfigureAwait(false);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.QueueBindAsync(queue, QianEventTopology.DeadLetterExchange, queue, cancellationToken: cancellationToken).ConfigureAwait(false);
        return queue;
    }

    /// <summary>
    /// 退避用的重试队列：没有消费者，消息停留 <paramref name="delay"/> 后经默认 exchange 回到 <paramref name="target"/>，
    /// 绝不经 <see cref="QianEventTopology.Exchange"/> 回投，否则会扇出到绑定同一 subject 的其他订阅。
    /// 竞争订阅的重试队列是 quorum + at-least-once，退避状态在 broker 上持久、跨副本有效；广播实例的重试队列是带闲置过期的经典队列，
    /// 实例下线、主队列删除后回流的消息无处可投即被丢弃。
    /// </summary>
    public static async Task DeclareRetryQueueAsync(IChannel channel, string queue, string target, TimeSpan delay, bool broadcast, CancellationToken cancellationToken)
    {
        var milliseconds = (long)delay.TotalMilliseconds;
        var arguments = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = milliseconds,
            ["x-dead-letter-exchange"] = "",
            ["x-dead-letter-routing-key"] = target,
        };
        if (broadcast)
        {
            arguments["x-queue-type"] = "classic";
            arguments["x-expires"] = milliseconds + BroadcastIdleExpiryMilliseconds;
        }
        else
        {
            arguments["x-queue-type"] = "quorum";
            arguments["x-dead-letter-strategy"] = "at-least-once";
            arguments["x-overflow"] = "reject-publish";
        }
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments, cancellationToken: cancellationToken).ConfigureAwait(false);
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
