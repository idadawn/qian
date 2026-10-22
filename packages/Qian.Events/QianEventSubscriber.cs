using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EventBus;
using Kun.EventBus.Idempotency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qian.Runtime.Errors;
using Qian.Tenancy;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Qian.Events;

/// <summary>
/// 消费端后台服务（PRD 7.10、ADR-008 D8.3 / D8.4 / D8.6 / D8.9 / D8.12）：按 <see cref="QianEventTopology"/> 声明队列，手动确认。
/// 每条消息先解析 v1 信封并与 AMQP 属性核对，再查幂等记录、在信封租户的作用域里执行 handler，成功并写入幂等记录后才确认。
/// 瞬时失败经重试队列按 <c>RetryBaseDelay × 2^(n-1)</c> 退避，用尽后与确定性失败一样转入订阅的死信队列；
/// 重试或死信副本转发失败时原地退避重发，连续失败后交 broker 按队列的死信参数处理，不放回原消息；
/// 宿主停机打断的消息放回队列，不计一次尝试。
/// </summary>
internal sealed partial class QianEventSubscriber(IServiceProvider services, EventSubscriptionRegistry registrations,
    IOptions<QianEventsSubscriberOptions> settings, ILogger<QianEventSubscriber> logger) : BackgroundService
{
    internal const string AttemptsHeader = "x-qian-attempts";
    private const string ReasonHeader = "x-qian-reason";
    private static readonly ActivitySource Source = new(EventsMetrics.MeterName);
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StableSession = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(10);

    /// <summary>同一条消息的重试或死信副本最多转发几次；连续失败到这个次数即不再放回原消息（见 <see cref="Settlement"/>）。</summary>
    internal const int ForwardAttempts = 3;

    private readonly QianEventsSubscriberOptions _options = settings.Value;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<ConsumerQueue> _queues = [];
    private IReadOnlyList<string> _deadLetters = [];
    private IProcessedMessageStore? _store;
    private ITenantContext? _tenant;
    private ITenantContextPropagator? _propagator;
    private Func<string>? _amqp;
    private int _inFlight;

    /// <summary>第一次声明完拓扑并开始消费后完成；测试据此在发布前等待订阅就绪。</summary>
    internal Task Ready => _ready.Task;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var service = registrations.Service;
        _options.Validate(service);
        var events = services.GetService<IEventTypeRegistry>() ?? throw Misconfigured("宿主必须以 AddKunEventBus 显式登记事件类型目录。");
        var address = services.GetService<EventsBrokerAddress>() ?? throw Misconfigured("订阅器要求宿主以 AddQianEventsBroker 登记事件 broker。");
        _amqp = () => address.Resolve(services);
        var subscriptions = registrations.Bind(events);
        if (subscriptions.Count == 0) throw Misconfigured("订阅器至少需要一个订阅。");
        var broadcast = subscriptions.Where(subscription => subscription.Mode == EventSubscriptionMode.Broadcast).ToArray();
        if (broadcast.Length > 0 && subscriptions.Any(subscription => subscription.Name == _options.Instance))
            throw Misconfigured("实例名 " + _options.Instance + " 与订阅名相同，广播队列会与竞争队列同名。");
        _queues = Plan(service, subscriptions, broadcast);
        _deadLetters = subscriptions.Select(subscription => subscription.Name).ToArray();
        _store = ResolveStore();
        _tenant = services.GetService<ITenantContext>();
        _propagator = services.GetService<ITenantContextPropagator>();
        if (_tenant is not { IsMultiTenant: true }) Log.SingleTenantHost(logger, service);
        else if (_propagator is null) Log.PropagatorMissing(logger, service);
        return base.StartAsync(cancellationToken);
    }

    // 竞争订阅各一个队列；全部广播订阅共用本实例的一个队列，按事件 ID 分派到所属订阅。
    private List<ConsumerQueue> Plan(string service, IReadOnlyList<SubscriptionSpec> subscriptions, SubscriptionSpec[] broadcast)
    {
        var queues = subscriptions.Where(subscription => subscription.Mode == EventSubscriptionMode.Competing).Select(subscription =>
        {
            var deadLetter = QianEventTopology.DeadLetterQueue(service, subscription.Name);
            var target = subscription.Routes.Values.ToDictionary(route => route.Type,
                route => new Target(subscription, route, service + "." + subscription.Name, deadLetter), StringComparer.Ordinal);
            return new ConsumerQueue(QianEventTopology.SubscriptionQueue(service, subscription.Name), subscription.Name, EventSubscriptionMode.Competing, subscription.Name,
                target.Values.Select(item => item.Route.Subject).Distinct(StringComparer.Ordinal).ToArray(), target, deadLetter);
        }).ToList();
        if (broadcast.Length > 0)
        {
            var targets = broadcast.SelectMany(subscription => subscription.Routes.Values.Select(route => new Target(subscription, route,
                service + "." + subscription.Name + "@" + _options.Instance, QianEventTopology.DeadLetterQueue(service, subscription.Name))))
                .ToDictionary(target => target.Route.Type, StringComparer.Ordinal);
            queues.Add(new ConsumerQueue(QianEventTopology.BroadcastQueue(service, _options.Instance), _options.Instance, EventSubscriptionMode.Broadcast,
                string.Join(',', broadcast.Select(subscription => subscription.Name)), targets.Values.Select(item => item.Route.Subject).Distinct(StringComparer.Ordinal).ToArray(),
                targets, DeadLetter: null));
        }
        return queues;
    }

    // 默认 Redis（R63）：宿主登记了 IProcessedMessageStore 就用它，否则用已登记的 IConnectionMultiplexer 建 Kun 的 Redis 实现。
    private IProcessedMessageStore ResolveStore()
    {
        IProcessedMessageStore? store;
        try { store = services.GetService<IProcessedMessageStore>(); }
        catch (InvalidOperationException error) { throw Misconfigured("幂等存储无法解析：" + error.Message); }
        if (store is null && services.GetService<StackExchange.Redis.IConnectionMultiplexer>() is { } redis) store = new RedisProcessedMessageStore(redis);
        if (store is null) throw Misconfigured("消费端幂等记录写 Redis：宿主须登记 IConnectionMultiplexer，或以 UseRedisIdempotency 登记 IProcessedMessageStore。");
        if (store is InMemoryProcessedMessageStore) Log.InMemoryIdempotency(logger, registrations.Service);
        return store;
    }

    // 消费循环在空执行上下文里运行：通道的派发线程从这里继承 AsyncLocal，宿主启动时的租户或事件作用域不能带进每条消息。
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (ExecutionContext.SuppressFlow()) return Task.Run(() => RunAsync(stoppingToken), CancellationToken.None);
    }

    // broker 不可达或会话中断不能让后台服务失败（宿主默认会随之停机），按封顶退避重连直到宿主停机。
    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var backoff = InitialBackoff;
        while (!stoppingToken.IsCancellationRequested)
        {
            var started = TimeProvider.System.GetTimestamp();
            try { await ConsumeAsync(stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                if (TimeProvider.System.GetElapsedTime(started) >= StableSession) backoff = InitialBackoff;
                Log.SessionFailed(logger, error, registrations.Service, backoff.TotalSeconds);
            }
            try { await Task.Delay(backoff, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
        }
        Log.Stopped(logger, registrations.Service, _options.Instance);
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_amqp!()),
            // 会话由本服务整体重建（重新声明拓扑、重新订阅），不与客户端的自动恢复叠加。
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false,
            ClientProvidedName = "qian.events.subscriber",
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
        };
        var session = new Session();
        await using var connection = await factory.CreateConnectionAsync(stoppingToken).ConfigureAwait(false);
        connection.ConnectionShutdownAsync += (_, args) => session.Break("连接关闭：" + args.ReplyText);
        await using var publisher = new SettlePublisher(connection);
        await using (var setup = await connection.CreateChannelAsync(cancellationToken: stoppingToken).ConfigureAwait(false))
        {
            foreach (var subscription in _deadLetters)
                await RabbitTopology.DeclareDeadLetterQueueAsync(setup, registrations.Service, subscription, stoppingToken).ConfigureAwait(false);
            foreach (var queue in _queues)
            {
                _ = queue.Mode == EventSubscriptionMode.Competing
                    ? await RabbitTopology.DeclareSubscriptionAsync(setup, registrations.Service, queue.RetryName, queue.Bindings, stoppingToken).ConfigureAwait(false)
                    : await RabbitTopology.DeclareBroadcastAsync(setup, registrations.Service, queue.RetryName, queue.Bindings, stoppingToken).ConfigureAwait(false);
            }
        }
        var consumers = new List<(IChannel Channel, string Tag)>();
        try
        {
            foreach (var queue in _queues)
            {
                var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
                consumers.Add((channel, ""));
                channel.ChannelShutdownAsync += (_, args) => session.Break("通道关闭：" + args.ReplyText);
                await channel.BasicQosAsync(0, _options.PrefetchCount, global: false, stoppingToken).ConfigureAwait(false);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, delivery) => DeliverAsync(queue, channel, publisher, session, delivery, stoppingToken);
                consumer.UnregisteredAsync += (_, _) => session.Break("队列 " + queue.Name + " 的消费者被 broker 取消");
                var tag = await channel.BasicConsumeAsync(queue.Name, autoAck: false, consumer, stoppingToken).ConfigureAwait(false);
                consumers[^1] = (channel, tag);
                Log.Subscribed(logger, queue.Name, queue.Subscriptions, queue.BindingList);
            }
            _ready.TrySetResult();
            await session.WaitAsync(stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            // 先停止接收，再等手上的消息处理完（停机时 handler 已收到取消），最后关闭通道；未确认的预取消息由 broker 放回队列。
            session.Close();
            foreach (var (channel, tag) in consumers)
            {
                try { if (tag.Length > 0 && channel.IsOpen) await channel.BasicCancelAsync(tag, noWait: true, CancellationToken.None).ConfigureAwait(false); }
                catch (Exception error) when (error is not OutOfMemoryException) { Log.CloseFailed(logger, error); }
            }
            var drain = TimeProvider.System.GetTimestamp();
            while (Volatile.Read(ref _inFlight) > 0 && TimeProvider.System.GetElapsedTime(drain) < DrainTimeout)
                await Task.Delay(20, CancellationToken.None).ConfigureAwait(false);
            foreach (var (channel, _) in consumers)
            {
                try { await channel.DisposeAsync().ConfigureAwait(false); }
                catch (Exception error) when (error is not OutOfMemoryException) { Log.CloseFailed(logger, error); }
            }
        }
    }

    private async Task DeliverAsync(ConsumerQueue queue, IChannel channel, SettlePublisher publisher, Session session, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        Interlocked.Increment(ref _inFlight);
        try
        {
            if (session.Closing || stoppingToken.IsCancellationRequested) await RequeueAsync(channel, delivery.DeliveryTag).ConfigureAwait(false);
            else await ProcessAsync(queue, channel, publisher, delivery, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Log.DeliveryFailed(logger, error, queue.Name);
            await RequeueAsync(channel, delivery.DeliveryTag).ConfigureAwait(false);
        }
        finally { Interlocked.Decrement(ref _inFlight); }
    }

    private async Task ProcessAsync(ConsumerQueue queue, IChannel channel, SettlePublisher publisher, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        var attempt = ReadAttempts(delivery) + 1;
        var settle = new Settlement(this, queue, channel, publisher, delivery, attempt, stoppingToken);
        QianEventEnvelope envelope;
        try { envelope = QianEventEnvelope.Parse(Encoding.UTF8.GetString(delivery.Body.Span)); }
        catch (QianEventsException error)
        {
            await settle.RejectAsync(null, null, "invalid_envelope", error).ConfigureAwait(false);
            return;
        }
        if (!queue.Routes.TryGetValue(envelope.Type, out var target))
        {
            await settle.RejectAsync(envelope, null, "unsubscribed_type", null).ConfigureAwait(false);
            return;
        }
        if ((Inconsistency(delivery, envelope) ?? (envelope.TenantId is null && !target.Subscription.AcceptPlatformEvents ? "platform_event" : null)) is { } reason)
        {
            await settle.RejectAsync(envelope, target, reason, null).ConfigureAwait(false);
            return;
        }
        if (attempt > _options.MaxRetries + 1)
        {
            await settle.DeadLetterAsync(envelope, target, "exhausted", null).ConfigureAwait(false);
            return;
        }
        bool duplicate;
        try { duplicate = await HandleAsync(queue, target, envelope, delivery, attempt, stoppingToken).ConfigureAwait(false); }
        catch (DeliveryRejectedException rejected)
        {
            await settle.RejectAsync(envelope, target, rejected.Reason, rejected.InnerException).ConfigureAwait(false);
            return;
        }
        catch (Exception error) when (error is not OutOfMemoryException && stoppingToken.IsCancellationRequested)
        {
            // 停机打断的处理不算一次失败：放回队列，由其他副本或重启后的本副本接手。
            Log.Requeued(logger, envelope.EventId, envelope.Type, target.Consumer);
            await RequeueAsync(channel, delivery.DeliveryTag).ConfigureAwait(false);
            return;
        }
        catch (Exception error) when (error is not OutOfMemoryException && IsDeterministic(error))
        {
            await settle.DeadLetterAsync(envelope, target, "handler_rejected", error).ConfigureAwait(false);
            return;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (attempt <= _options.MaxRetries) await settle.RetryAsync(envelope, target, error).ConfigureAwait(false);
            else await settle.DeadLetterAsync(envelope, target, "exhausted", error).ConfigureAwait(false);
            return;
        }
        await AckAsync(channel, delivery.DeliveryTag).ConfigureAwait(false);
        (duplicate ? EventsMetrics.ConsumerDuplicate : EventsMetrics.ConsumerProcessed).Add(1, Tags(target));
        if (duplicate) Log.Duplicate(logger, envelope.EventId, envelope.Type, target.Consumer);
    }

    // 返回 true 表示幂等记录命中、未执行 handler。handler 成功并写入幂等记录后才返回，调用方随即确认。
    private async Task<bool> HandleAsync(ConsumerQueue queue, Target target, QianEventEnvelope envelope, BasicDeliverEventArgs delivery, int attempt, CancellationToken stoppingToken)
    {
        if (await _store!.IsProcessedAsync(target.Consumer, envelope.EventId, stoppingToken).ConfigureAwait(false)) return true;
        await using (var scope = services.CreateAsyncScope())
        {
            using var activity = StartActivity(queue, target, envelope, ReadHeader(delivery.BasicProperties.Headers, "tracestate"));
            using (EnterTenant(envelope, delivery.Body))
            {
                object payload;
                try { payload = target.Route.Deserialize(envelope.Payload); }
                catch (JsonException error) { throw new DeliveryRejectedException("invalid_payload", error); }
                scope.ServiceProvider.GetRequiredService<EventDeliverySlot>().Current = new QianEventDelivery
                {
                    Envelope = envelope, Service = registrations.Service, Subscription = target.Subscription.Name, Mode = target.Subscription.Mode,
                    Consumer = target.Consumer, Attempt = attempt,
                };
                var context = new MessageContext
                {
                    MessageId = envelope.EventId, EventType = envelope.Type, TraceParent = envelope.TraceParent,
                    TenantId = envelope.TenantId?.ToString("D"), OccurredOn = envelope.OccurredAt,
                };
                await target.Route.InvokeAsync(scope.ServiceProvider, payload, context, stoppingToken).ConfigureAwait(false);
            }
        }
        // handler 已经成功：停机令牌不再打断写记录，否则重启后同一事件会再执行一次。
        // 写入返回 false 说明先查后写之间同一事件的另一次投递已写入记录：并发重复投递，handler 至少执行了两次，靠 handler 的业务幂等兜底（ADR-008 D8.4）。
        if (!await _store.TryMarkProcessedAsync(target.Consumer, envelope.EventId, _options.IdempotencyTtl, CancellationToken.None).ConfigureAwait(false))
        {
            EventsMetrics.ConsumerDuplicateExecuted.Add(1, Tags(target));
            Log.DuplicateExecuted(logger, envelope.EventId, envelope.Type, target.Consumer);
        }
        return false;
    }

    /// <summary>
    /// 租户只取已校验信封的 tenant_id（contracts/v0 §5 第 3 条），每条消息在全新作用域里进入。宿主登记了 <see cref="ITenantContextPropagator"/> 时经它进入：
    /// carrier 为信封原文，authorizedTenantId 为已与 AMQP 头核对一致的信封租户。消费端没有独立于消息的令牌，只能近似满足传播器契约（R122）：
    /// 它提供的是缺失、重复、不一致与租户冲突的 fail-closed 判定，信任根是 broker 的连接认证与发布端 Outbox 写入端。
    /// 未登记时退回 Kun ITenantContext，按同样规则先核对原文里恰有一个一致的 tenant_id。两条路径进入后都核对当前租户等于信封租户，
    /// 传播器或宿主上下文没有进入作用域、进入了别的租户，都按 tenant_rejected 拒绝。
    /// </summary>
    private IDisposable? EnterTenant(QianEventEnvelope envelope, ReadOnlyMemory<byte> body)
    {
        if (_tenant is not { IsMultiTenant: true } tenant) return null;
        if (tenant.TenantId is not null) throw new DeliveryRejectedException("tenant_rejected", Misconfigured("消费流里残留了租户上下文。"));
        if (envelope.TenantId is not { } id) return null;
        IDisposable scope;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (_propagator is not null) scope = _propagator.BeginScope(document.RootElement, id);
            else
            {
                var fields = document.RootElement.EnumerateObject().Where(field => field.NameEquals("tenant_id")).ToArray();
                if (fields.Length != 1 || fields[0].Value.ValueKind != JsonValueKind.String || fields[0].Value.GetString() != id.ToString("D"))
                    throw new QianEventsException(EventsErrors.ConsumeRejected, "信封原文必须恰有一个与授权租户一致的 tenant_id。");
                scope = tenant.BeginScope(id.ToString("D"));
            }
        }
        catch (Exception error) when (error is QianErrorException or QianEventsException or ArgumentException or JsonException)
        {
            throw new DeliveryRejectedException("tenant_rejected", error);
        }
        if (Guid.TryParseExact(tenant.TenantId, "D", out var current) && current == id) return scope;
        scope?.Dispose();
        throw new DeliveryRejectedException("tenant_rejected", new QianEventsException(EventsErrors.ConsumeRejected, "进入租户作用域后当前租户与信封租户不一致。"));
    }

    private static Activity? StartActivity(ConsumerQueue queue, Target target, QianEventEnvelope envelope, string? traceState)
    {
        _ = ActivityContext.TryParse(envelope.TraceParent, traceState, isRemote: true, out var parent);
        var activity = Source.StartActivity("consume " + envelope.Subject, ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", queue.Name);
        activity?.SetTag("messaging.message.id", envelope.EventId.ToString("D"));
        activity?.SetTag("qian.events.consumer", target.Consumer);
        return activity;
    }

    // 发布端从同一信封写出这些属性；任何一项对不上都说明消息不是经 Outbox 发布器发出的。
    private static string? Inconsistency(BasicDeliverEventArgs delivery, QianEventEnvelope envelope)
    {
        var properties = delivery.BasicProperties;
        if (properties.MessageId != envelope.EventId.ToString("D") || properties.Type != envelope.Type) return "property_mismatch";
        if (ReadHeader(properties.Headers, "tenant_id") != envelope.TenantId?.ToString("D")) return "tenant_mismatch";
        if (delivery.Exchange == QianEventTopology.Exchange && delivery.RoutingKey != envelope.Subject) return "routing_mismatch";
        return null;
    }

    // 计数头只认经重试队列回流（默认 exchange）的副本；直接发到 qian.events 的消息带的同名头一律忽略。
    private static int ReadAttempts(BasicDeliverEventArgs delivery)
    {
        if (delivery.Exchange.Length != 0 || delivery.BasicProperties.Headers?.TryGetValue(AttemptsHeader, out var value) != true) return 0;
        return value switch { int count => Math.Max(0, count), long count => (int)Math.Clamp(count, 0, int.MaxValue), _ => 0 };
    }

    private static string? ReadHeader(IDictionary<string, object?>? headers, string key) =>
        headers is not null && headers.TryGetValue(key, out var value) ? value switch { byte[] bytes => Encoding.UTF8.GetString(bytes), string text => text, _ => value?.ToString() } : null;

    // 重试也不会成功的失败：契约错误（QianEventsException；RecordChangedEvent.FromEnvelope 等派生失败抛的 InvalidOperationException 本身）、参数非法（含非法租户 id）、
    // 载荷不合法、不支持的操作，以及错误码声明不可重试的统一错误。其余异常按瞬时失败退避重试，其中包括：
    // InvalidOperationException 的其他子类（ObjectDisposedException 等，对象生命周期问题，换一次投递可能就好）；
    // 包着内层异常的 InvalidOperationException 按内层判定（Npgsql 的执行策略把瞬时数据库故障包成 InvalidOperationException 抛出）。
    internal static bool IsDeterministic(Exception error) => error switch
    {
        QianEventsException => true,
        QianErrorException { Error.Code.Retryable: var retryable } => !retryable,
        InvalidOperationException when error.GetType() == typeof(InvalidOperationException) => error.InnerException is null || IsDeterministic(error.InnerException),
        InvalidOperationException => false,
        ArgumentException or JsonException or NotSupportedException => true,
        _ => false,
    };

    private async Task AckAsync(IChannel channel, ulong deliveryTag)
    {
        try { await channel.BasicAckAsync(deliveryTag, multiple: false, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception error) when (error is not OutOfMemoryException) { Log.SettleFailed(logger, error); }
    }

    private async Task RequeueAsync(IChannel channel, ulong deliveryTag)
    {
        try { if (channel.IsOpen) await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception error) when (error is not OutOfMemoryException) { Log.SettleFailed(logger, error); }
    }

    // nack 不放回：竞争队列由 broker 按队列的死信参数（qian.dlx、本订阅的死信队列名、at-least-once）转入死信队列，广播队列没有死信参数，消息丢弃。
    private async Task DiscardAsync(IChannel channel, ulong deliveryTag)
    {
        try { if (channel.IsOpen) await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception error) when (error is not OutOfMemoryException) { Log.SettleFailed(logger, error); }
    }

    private static TagList Tags(Target? target, string? type = null, string? reason = null, string? consumer = null)
    {
        var tags = new TagList { { "qian.events.consumer", target?.Consumer ?? consumer } };
        if ((target?.Route.Type ?? type) is { } eventType) tags.Add("qian.events.type", eventType);
        if (reason is not null) tags.Add("qian.events.reason", reason);
        return tags;
    }

    private static QianEventsException Misconfigured(string reason) => new(EventsErrors.Misconfigured, reason);

    private ILogger Logger => logger;

    private string Service => registrations.Service;

    /// <summary>
    /// 一条消息的收尾：转发副本（重试或死信）得到 broker 确认后才确认原消息，结果计数与日志都在确认之后，每次投递恰落入一个结果。
    /// 转发失败时原地退避重发，不放回原消息：quorum 队列的 nack 放回不计入 x-delivery-limit，放回会让 handler 无上限地反复执行。
    /// 连续 <see cref="ForwardAttempts"/> 次失败后计 forward_failed 并 nack 不放回，交 broker 按队列的死信参数处理；停机时仍放回原消息，不计失败。
    /// </summary>
    private sealed class Settlement(QianEventSubscriber owner, ConsumerQueue queue, IChannel channel, SettlePublisher publisher, BasicDeliverEventArgs delivery, int attempt,
        CancellationToken stoppingToken)
    {
        private string Service => owner.Service;

        /// <summary>handler 之前就判定不可处理：进订阅的死信队列；广播队列里无法归属订阅的消息没有死信队列，确认后丢弃。</summary>
        public async Task RejectAsync(QianEventEnvelope? envelope, Target? target, string reason, Exception? error)
        {
            var deadLetter = target?.DeadLetterQueue ?? queue.DeadLetter;
            var consumer = target?.Consumer ?? (queue.Mode == EventSubscriptionMode.Competing ? Service + "." + queue.RetryName : Service + "@" + queue.RetryName);
            var type = envelope?.Type ?? delivery.BasicProperties.Type;
            if (deadLetter is null) await owner.AckAsync(channel, delivery.DeliveryTag).ConfigureAwait(false);
            else if (!await ForwardAsync(QianEventTopology.DeadLetterExchange, deadLetter, null, DeadLetterHeaders(envelope, target, consumer, reason, error),
                target, envelope?.EventId, type, consumer, reason).ConfigureAwait(false)) return;
            EventsMetrics.ConsumerRejected.Add(1, Tags(target, reason: reason, consumer: consumer));
            Log.Rejected(owner.Logger, error, envelope?.EventId, type, consumer, attempt, reason);
        }

        /// <summary>重试用尽或 handler 确定性失败：进订阅的死信队列并告警（计数加一、Error 日志，不含载荷）。</summary>
        public async Task DeadLetterAsync(QianEventEnvelope envelope, Target target, string reason, Exception? error)
        {
            if (await ForwardAsync(QianEventTopology.DeadLetterExchange, target.DeadLetterQueue, null,
                DeadLetterHeaders(envelope, target, target.Consumer, reason, error), target, envelope.EventId, envelope.Type, target.Consumer, reason).ConfigureAwait(false))
            {
                EventsMetrics.ConsumerDeadLettered.Add(1, Tags(target, reason: reason));
                Log.DeadLettered(owner.Logger, error, envelope.EventId, envelope.Type, target.Consumer, attempt, reason);
            }
        }

        /// <summary>瞬时失败：副本带着计数进第 n 个重试队列，到期后经默认 exchange 回到原队列。</summary>
        public async Task RetryAsync(QianEventEnvelope envelope, Target target, Exception error)
        {
            var delay = owner._options.RetryDelay(attempt);
            var retry = QianEventTopology.RetryQueue(Service, queue.RetryName, attempt, delay);
            var headers = CopyHeaders();
            headers[AttemptsHeader] = attempt;
            if (await ForwardAsync("", retry, (ch, ct) => RabbitTopology.DeclareRetryQueueAsync(ch, retry, queue.Name, delay, queue.Mode == EventSubscriptionMode.Broadcast, ct),
                headers, target, envelope.EventId, envelope.Type, target.Consumer, "retry").ConfigureAwait(false))
            {
                EventsMetrics.ConsumerRetried.Add(1, Tags(target));
                Log.Retrying(owner.Logger, error, envelope.EventId, envelope.Type, target.Consumer, attempt, delay.TotalMilliseconds);
            }
        }

        private Dictionary<string, object?> DeadLetterHeaders(QianEventEnvelope? envelope, Target? target, string consumer, string reason, Exception? error)
        {
            var headers = CopyHeaders();
            headers[AttemptsHeader] = attempt;
            headers[ReasonHeader] = reason;
            headers["x-qian-consumer"] = consumer;
            headers["x-qian-instance"] = owner._options.Instance;
            if (target is not null) headers["x-qian-subscription"] = target.Subscription.Name;
            headers["x-qian-subject"] = envelope?.Subject ?? delivery.RoutingKey;
            if (error is not null) headers["x-qian-error"] = error.GetType().FullName;
            return headers;
        }

        // 原样保留发布端写出的头（traceparent、tracestate、tenant_id），去掉 broker 生成、会逐次增长的死信记录。
        private Dictionary<string, object?> CopyHeaders()
        {
            var headers = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (delivery.BasicProperties.Headers is { } original)
                foreach (var (key, value) in original)
                    if (key is not ("x-death" or "x-delivery-count") && !key.StartsWith("x-first-death-", StringComparison.Ordinal) && !key.StartsWith("x-last-death-", StringComparison.Ordinal))
                        headers[key] = value;
            return headers;
        }

        // 返回 true 表示副本已被 broker 确认、原消息已确认；false 表示原消息已放回（停机）或已 nack 不放回（连续转发失败）。
        // eventId、type、reason 只用于 forward_failed 的计数与日志：reason 在死信转发时为死信原因，重试转发时为 retry。
        private async Task<bool> ForwardAsync(string exchange, string routingKey, Func<IChannel, CancellationToken, Task>? declare, Dictionary<string, object?> headers,
            Target? target, Guid? eventId, string? type, string consumer, string reason)
        {
            var properties = new BasicProperties(delivery.BasicProperties) { Headers = headers, DeliveryMode = DeliveryModes.Persistent };
            for (var failures = 1; ; failures++)
            {
                try
                {
                    using var timeout = new CancellationTokenSource(SettleTimeout);
                    await publisher.PublishAsync(exchange, routingKey, properties, delivery.Body, declare, timeout.Token).ConfigureAwait(false);
                    break;
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Log.ForwardFailed(owner.Logger, error, routingKey, failures);
                }
                if (stoppingToken.IsCancellationRequested)
                {
                    await owner.RequeueAsync(channel, delivery.DeliveryTag).ConfigureAwait(false);
                    return false;
                }
                if (failures >= ForwardAttempts)
                {
                    // 与 rejected 一样，没有归属订阅时不带类型标签：类型取自未受信的消息属性，不能进指标维度。
                    EventsMetrics.ConsumerForwardFailed.Add(1, Tags(target, reason: reason, consumer: consumer));
                    Log.ForwardAbandoned(owner.Logger, eventId, type, consumer, attempt, routingKey, failures, reason);
                    await owner.DiscardAsync(channel, delivery.DeliveryTag).ConfigureAwait(false);
                    return false;
                }
                try { await Task.Delay(owner._options.RetryDelay(failures), stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    await owner.RequeueAsync(channel, delivery.DeliveryTag).ConfigureAwait(false);
                    return false;
                }
            }
            await owner.AckAsync(channel, delivery.DeliveryTag).ConfigureAwait(false);
            return true;
        }
    }

    /// <summary>重试与死信副本的确认通道：mandatory 发布，broker 确认且已路由到队列才算成功；出错即丢弃重建。</summary>
    private sealed class SettlePublisher(IConnection connection) : IAsyncDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private IChannel? _channel;

        public async Task PublishAsync(string exchange, string routingKey, BasicProperties properties, ReadOnlyMemory<byte> body,
            Func<IChannel, CancellationToken, Task>? declare, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_channel is not { IsOpen: true })
                {
                    if (_channel is not null) await _channel.DisposeAsync().ConfigureAwait(false);
                    _channel = null;
                    _channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                        cancellationToken).ConfigureAwait(false);
                }
                try
                {
                    if (declare is not null) await declare(_channel, cancellationToken).ConfigureAwait(false);
                    await _channel.BasicPublishAsync(exchange, routingKey, mandatory: true, properties, body, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    var broken = _channel;
                    _channel = null;
                    try { await broken.DisposeAsync().ConfigureAwait(false); }
                    catch (Exception error) when (error is not OutOfMemoryException) { _ = error; }
                    throw;
                }
            }
            finally { _gate.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_channel is not null) await _channel.DisposeAsync().ConfigureAwait(false);
                _channel = null;
            }
            finally { _gate.Release(); }
        }
    }

    /// <summary>一次连接会话：连接、通道或消费者被动中断时结束等待，由外层退避后整体重建。</summary>
    private sealed class Session
    {
        private readonly TaskCompletionSource<string> _broken = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile bool _closing;

        public bool Closing => _closing;

        public void Close() => _closing = true;

        public Task Break(string reason)
        {
            if (!_closing) _broken.TrySetResult(reason);
            return Task.CompletedTask;
        }

        public async Task WaitAsync(CancellationToken stoppingToken)
        {
            var reason = await _broken.Task.WaitAsync(stoppingToken).ConfigureAwait(false);
            throw new IOException("事件 broker 会话中断：" + reason);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 20, Level = LogLevel.Information, Message = "已订阅队列 {Queue}（订阅 {Subscriptions}，绑定 {Bindings}）。")]
        public static partial void Subscribed(ILogger logger, string queue, string subscriptions, string bindings);

        [LoggerMessage(EventId = 21, Level = LogLevel.Information, Message = "事件订阅器已停止：服务 {Service}，实例 {Instance}。")]
        public static partial void Stopped(ILogger logger, string service, string instance);

        [LoggerMessage(EventId = 22, Level = LogLevel.Warning, Message = "服务 {Service} 的事件订阅会话失败，{Backoff} 秒后重连。")]
        public static partial void SessionFailed(ILogger logger, Exception error, string service, double backoff);

        [LoggerMessage(EventId = 23, Level = LogLevel.Information, Message = "集成事件 {EventId}（{Type}）已由 {Consumer} 处理过，跳过重复投递。")]
        public static partial void Duplicate(ILogger logger, Guid eventId, string type, string consumer);

        [LoggerMessage(EventId = 24, Level = LogLevel.Warning, Message = "集成事件 {EventId}（{Type}）在 {Consumer} 第 {Attempt} 次处理失败，{Delay} 毫秒后重试。")]
        public static partial void Retrying(ILogger logger, Exception error, Guid eventId, string type, string consumer, int attempt, double delay);

        [LoggerMessage(EventId = 25, Level = LogLevel.Error, Message = "集成事件 {EventId}（{Type}）在 {Consumer} 第 {Attempt} 次处理后转入死信队列：{Reason}。")]
        public static partial void DeadLettered(ILogger logger, Exception? error, Guid eventId, string type, string consumer, int attempt, string reason);

        [LoggerMessage(EventId = 26, Level = LogLevel.Error, Message = "集成事件 {EventId}（{Type}）不满足消费契约，{Consumer} 第 {Attempt} 次投递时直接拒绝：{Reason}。")]
        public static partial void Rejected(ILogger logger, Exception? error, Guid? eventId, string? type, string consumer, int attempt, string reason);

        [LoggerMessage(EventId = 27, Level = LogLevel.Information, Message = "宿主停机，集成事件 {EventId}（{Type}）放回 {Consumer} 的队列，不计失败次数。")]
        public static partial void Requeued(ILogger logger, Guid eventId, string type, string consumer);

        [LoggerMessage(EventId = 28, Level = LogLevel.Warning, Message = "转发到 {RoutingKey} 失败（连续第 {Failures} 次）。")]
        public static partial void ForwardFailed(ILogger logger, Exception error, string routingKey, int failures);

        [LoggerMessage(EventId = 29, Level = LogLevel.Warning, Message = "确认或放回消息失败；通道已断开时 broker 会重投未确认的消息。")]
        public static partial void SettleFailed(ILogger logger, Exception error);

        [LoggerMessage(EventId = 30, Level = LogLevel.Error, Message = "队列 {Queue} 的一条消息处理异常，已放回队列。")]
        public static partial void DeliveryFailed(ILogger logger, Exception error, string queue);

        [LoggerMessage(EventId = 31, Level = LogLevel.Debug, Message = "关闭消费通道失败。")]
        public static partial void CloseFailed(ILogger logger, Exception error);

        [LoggerMessage(EventId = 32, Level = LogLevel.Warning, Message = "服务 {Service} 的幂等记录在进程内存里，不跨副本，只能用于测试。")]
        public static partial void InMemoryIdempotency(ILogger logger, string service);

        [LoggerMessage(EventId = 33, Level = LogLevel.Information, Message = "宿主未登记 ITenantContextPropagator，服务 {Service} 的消费端经 ITenantContext.BeginScope 回灌信封租户。")]
        public static partial void PropagatorMissing(ILogger logger, string service);

        [LoggerMessage(EventId = 34, Level = LogLevel.Information, Message = "宿主的租户上下文不是多租户，服务 {Service} 的消费端不回灌租户。")]
        public static partial void SingleTenantHost(ILogger logger, string service);

        [LoggerMessage(EventId = 35, Level = LogLevel.Warning, Message = "集成事件 {EventId}（{Type}）的幂等记录已由另一次投递写入：{Consumer} 的 handler 至少执行了两次（并发重复投递）。")]
        public static partial void DuplicateExecuted(ILogger logger, Guid eventId, string type, string consumer);

        [LoggerMessage(EventId = 36, Level = LogLevel.Error,
            Message = "集成事件 {EventId}（{Type}）在 {Consumer} 第 {Attempt} 次投递后转发到 {RoutingKey} 连续失败 {Failures} 次（{Reason}），原消息不再放回，交 broker 按队列的死信参数处理。")]
        public static partial void ForwardAbandoned(ILogger logger, Guid? eventId, string? type, string consumer, int attempt, string routingKey, int failures, string reason);
    }
}

/// <summary>一个消费队列：竞争订阅一个，广播订阅共用一个。</summary>
/// <param name="Name">队列名。</param>
/// <param name="RetryName">队列名与重试队列名里的名称段：竞争为订阅名，广播为实例名。</param>
/// <param name="Mode">订阅模式。</param>
/// <param name="Subscriptions">队列承载的订阅名，只用于日志。</param>
/// <param name="Bindings">绑定键，即各事件的 subject。</param>
/// <param name="Routes">路由表，键是稳定事件 ID。</param>
/// <param name="DeadLetter">无法归属订阅时用的死信队列；广播队列为 null。</param>
internal sealed record ConsumerQueue(string Name, string RetryName, EventSubscriptionMode Mode, string Subscriptions, IReadOnlyList<string> Bindings,
    IReadOnlyDictionary<string, Target> Routes, string? DeadLetter)
{
    public string BindingList { get; } = string.Join(',', Bindings);
}

/// <summary>一个事件在一个订阅里的处理目标。</summary>
internal sealed record Target(SubscriptionSpec Subscription, BoundEventRoute Route, string Consumer, string DeadLetterQueue);

/// <summary>handler 之前的契约判定失败（租户、载荷）；不重投、不记幂等。</summary>
internal sealed class DeliveryRejectedException(string reason, Exception inner) : Exception(reason, inner)
{
    public string Reason { get; } = reason;
}
