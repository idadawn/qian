using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Core.MultiTenancy;
using Kun.EventBus;
using Kun.EventBus.DependencyInjection;
using Kun.EventBus.Outbox;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Qian.Testing.Containers;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Qian.Events.Tests;

/// <summary>
/// 真实 RabbitMQ 与 Redis：消费端至少一次（手动确认、退避重试、死信、Redis 幂等）、竞争与广播两种订阅、按信封租户回灌。
/// 发布走生产发布器（QianEnvelopePublisher），多副本用同一套容器起多个宿主（PRD 14.2.1）。
/// </summary>
public sealed partial class SubscriberTests(RabbitMqFixture rabbit, RedisFixture redis) : IClassFixture<RabbitMqFixture>, IClassFixture<RedisFixture>, IAsyncDisposable
{
    private ConnectionMultiplexer? _redis;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string TenantA = "01900000-0000-7000-8000-0000000000a1";
    private const string TenantB = "01900000-0000-7000-8000-0000000000b2";

    [Fact]
    public async Task 前两次瞬时失败第三次成功_handler恰执行3次_幂等键恰一条_队列为空且重投间隔满足退避()
    {
        var service = Service();
        var probe = new Probe(attempt => attempt <= 2 ? new FlakyDependencyException() : null);
        using var metrics = new ConsumerMetrics(service + ".work");
        using var traces = new TraceRecorder();
        using var host = Host(service, probe, subscriber => subscriber.Subscribe<PingSent, ProbeHandler>("work"), retryBaseDelay: TimeSpan.FromMilliseconds(200));
        await StartAsync(host);
        var envelope = await PublishAsync(TenantA, sequence: 1);

        // 等到结束态再断言投递次数：processed 在第三次投递确认之后才计数；每次失败恰产生一份重试副本，前两份此时都已回流并被消费，
        // 主队列与两个重试队列的 messages（含未确认）也都为 0，消息已没有任何副本，不会再有投递。
        var retries = new[] { QianEventTopology.RetryQueue(service, "work", 1, TimeSpan.FromMilliseconds(200)), QianEventTopology.RetryQueue(service, "work", 2, TimeSpan.FromMilliseconds(400)) };
        await Until(async () => metrics.Count("processed") == 1 && await SettledAsync([QianEventTopology.SubscriptionQueue(service, "work"), .. retries]), "第三次投递没有处理完");
        var calls = probe.Calls.ToArray();
        Assert.Equal([1, 2, 3], calls.Select(call => call.Attempt));
        Assert.All(calls, call => Assert.Equal(envelope.EventId, call.EventId));
        // 第 n 次重试前退避 RetryBaseDelay × 2^(n-1)：200 毫秒、400 毫秒，由 broker 上的重试队列计时。
        Assert.True(calls[1].At - calls[0].At >= TimeSpan.FromMilliseconds(190), (calls[1].At - calls[0].At).ToString());
        Assert.True(calls[2].At - calls[1].At >= TimeSpan.FromMilliseconds(390), (calls[2].At - calls[1].At).ToString());
        Assert.Equal(2, metrics.Count("retried"));
        Assert.Equal(0, metrics.Count("dead_lettered"));
        Assert.Equal(0, metrics.Count("rejected"));

        var keys = await IdempotencyKeysAsync(service + ".work");
        Assert.Equal(["idemp:" + service + ".work:" + envelope.EventId], keys);
        var ttl = await Redis().KeyTimeToLiveAsync(keys[0]);
        Assert.InRange(ttl!.Value, TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1), TimeSpan.FromDays(7));
        var queues = await QueuesAsync();
        Assert.Equal(0, queues[QianEventTopology.SubscriptionQueue(service, "work")]);
        Assert.Equal(0, queues[QianEventTopology.DeadLetterQueue(service, "work")]);
        Assert.All(retries, retry => Assert.Equal(0, queues[retry]));

        // handler 拿到的是由信封填充的 Kun MessageContext 与完整信封；Activity 从信封 trace_parent 恢复。
        var last = calls[^1];
        Assert.Equal((envelope.Type, TenantA, envelope.TraceParent, envelope.OccurredAt), (last.Context.EventType, last.Context.TenantId, last.Context.TraceParent, last.Context.OccurredOn));
        Assert.Equal(("qian.events-tests", EventActorKind.Service, "subscriber-tests"), (last.Delivery.Envelope.Producer, last.Delivery.Envelope.Actor.Kind, last.Delivery.Envelope.Actor.Id));
        Assert.Equal((service, "work", EventSubscriptionMode.Competing, service + ".work"), (last.Delivery.Service, last.Delivery.Subscription, last.Delivery.Mode, last.Delivery.Consumer));
        Assert.Equal(TenantA, last.Tenant);
        Assert.Equal((envelope.TraceParent.Substring(3, 32), ActivityKind.Consumer), (last.TraceId, last.ActivityKind));
        Assert.Contains(traces.Names, name => name == "consume tests.ping.sent");
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task 持续瞬时失败_首投加5次重试共6次投递后进死信队列_保留原属性并计数告警_不留幂等键()
    {
        var service = Service();
        var probe = new Probe(_ => new FlakyDependencyException());
        using var metrics = new ConsumerMetrics(service + ".work");
        using var host = Host(service, probe, subscriber => subscriber.Subscribe<PingSent, ProbeHandler>("work"), retryBaseDelay: TimeSpan.FromMilliseconds(100));
        await StartAsync(host);
        var envelope = await PublishAsync(TenantA, sequence: 2, note: "payload-marker");
        var deadLetter = QianEventTopology.DeadLetterQueue(service, "work");

        // 结束态：dead_lettered 与告警日志都已写出（计数先于日志，须分别等待），死信队列恰有一条，主队列与 5 个重试队列都已清空，之后不会再有投递。
        var retries = Enumerable.Range(1, 5).Select(attempt => QianEventTopology.RetryQueue(service, "work", attempt, TimeSpan.FromMilliseconds(100 << (attempt - 1)))).ToArray();
        await Until(async () => metrics.Count("dead_lettered") == 1 && probe.Logs.Entries.Any(entry => entry.EventId == 25)
            && (await QueuesAsync()).GetValueOrDefault(deadLetter) == 1 && await SettledAsync([QianEventTopology.SubscriptionQueue(service, "work"), .. retries]), "第 6 次失败后没有进死信队列");
        Assert.Equal([1, 2, 3, 4, 5, 6], probe.Calls.Select(call => call.Attempt));
        Assert.Equal(0, probe.Succeeded);
        Assert.Equal(5, metrics.Count("retried"));
        Assert.Equal(1, metrics.Count("dead_lettered"));
        Assert.Equal(0, metrics.Count("processed"));
        Assert.Empty(await IdempotencyKeysAsync(service + ".work"));
        Assert.Equal(0, (await QueuesAsync())[QianEventTopology.SubscriptionQueue(service, "work")]);

        var message = await GetAsync(deadLetter);
        Assert.Equal(envelope.ToJson(), Encoding.UTF8.GetString(message.Body.Span));
        Assert.Equal((envelope.EventId.ToString("D"), envelope.Type), (message.BasicProperties.MessageId, message.BasicProperties.Type));
        Assert.Equal(TenantA, Header(message, "tenant_id"));
        Assert.Equal(envelope.TraceParent, Header(message, "traceparent"));
        Assert.Equal(6, message.BasicProperties.Headers!["x-qian-attempts"]);
        Assert.Equal(("exhausted", service + ".work", "work", "tests.ping.sent"),
            (Header(message, "x-qian-reason"), Header(message, "x-qian-consumer"), Header(message, "x-qian-subscription"), Header(message, "x-qian-subject")));
        Assert.Equal(typeof(FlakyDependencyException).FullName, Header(message, "x-qian-error"));
        Assert.Equal((QianEventTopology.DeadLetterExchange, deadLetter), (message.Exchange, message.RoutingKey));
        // 告警：死信计数加一，并记一条 Error 日志（事件 id、类型、消费者、第几次），日志里没有载荷。
        var alert = Assert.Single(probe.Logs.Entries, entry => entry.Level == LogLevel.Error && entry.EventId == 25);
        Assert.Contains(envelope.EventId.ToString(), alert.Message, StringComparison.Ordinal);
        Assert.Contains(envelope.Type, alert.Message, StringComparison.Ordinal);
        Assert.Contains(service + ".work", alert.Message, StringComparison.Ordinal);
        Assert.Contains("第 6 次", alert.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(probe.Logs.Entries, entry => entry.Message.Contains("payload-marker", StringComparison.Ordinal));
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task handler确定性失败_不重投直接进死信队列_retried为0()
    {
        var service = Service();
        var probe = new Probe(_ => new InvalidOperationException("派生失败，重试也不会成功"));
        using var metrics = new ConsumerMetrics(service + ".work");
        using var host = Host(service, probe, subscriber => subscriber.Subscribe<PingSent, ProbeHandler>("work"));
        await StartAsync(host);
        var envelope = await PublishAsync(TenantA, sequence: 3);
        var deadLetter = QianEventTopology.DeadLetterQueue(service, "work");

        // 结束态：dead_lettered 已计数（确认原消息之后），死信队列恰有一条、主队列已清空，之后不会再有投递。
        await Until(async () => metrics.Count("dead_lettered") == 1 && (await QueuesAsync()).GetValueOrDefault(deadLetter) == 1
            && await SettledAsync([QianEventTopology.SubscriptionQueue(service, "work")]), "确定性失败没有直接进死信队列");
        Assert.Equal([1], probe.Calls.Select(call => call.Attempt));
        Assert.Equal(0, metrics.Count("retried"));
        Assert.Equal(1, metrics.Count("dead_lettered"));
        Assert.Empty(await IdempotencyKeysAsync(service + ".work"));
        var message = await GetAsync(deadLetter);
        Assert.Equal(envelope.EventId.ToString("D"), message.BasicProperties.MessageId);
        Assert.Equal(("handler_rejected", typeof(InvalidOperationException).FullName), (Header(message, "x-qian-reason"), Header(message, "x-qian-error")));
        Assert.Equal(1, message.BasicProperties.Headers!["x-qian-attempts"]);
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task 同一事件经broker投递两次_幂等记录拦截第二次_handler只执行一次()
    {
        var service = Service();
        var probe = new Probe(_ => null);
        using var metrics = new ConsumerMetrics(service + ".work");
        using var host = Host(service, probe, subscriber => subscriber.Subscribe<PingSent, ProbeHandler>("work"));
        await StartAsync(host);
        // Outbox 发布器在确认丢失时会重发同一行：两次发布同一条信封，消息体与属性完全相同。
        var envelope = await PublishAsync(TenantA, sequence: 4, times: 2);

        await Until(() => metrics.Count("processed") + metrics.Count("duplicate") == 2, "两次投递没有都处理完");
        // 消费计数先于重复日志写入；计数到齐不表示日志已经可见，须等待本次信封的日志。
        await Until(() => probe.Logs.Entries.Any(entry => entry.EventId == 23 && entry.Message.Contains(envelope.EventId.ToString(), StringComparison.Ordinal)), "重复投递日志尚未写入");
        Assert.Single(probe.Calls);
        Assert.Equal(1, metrics.Count("processed"));
        Assert.Equal(1, metrics.Count("duplicate"));
        Assert.Equal(["idemp:" + service + ".work:" + envelope.EventId], await IdempotencyKeysAsync(service + ".work"));
        Assert.Contains(probe.Logs.Entries, entry => entry.EventId == 23 && entry.Message.Contains(envelope.EventId.ToString(), StringComparison.Ordinal));
        Assert.Equal(0, (await QueuesAsync())[QianEventTopology.SubscriptionQueue(service, "work")]);
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task 两个实例共用一套容器_竞争订阅每条只由一个实例处理_广播订阅每个实例各处理一次_实例下线后广播队列被删除()
    {
        const int total = 20;
        var service = Service();
        var a = new Probe(_ => null, delay: TimeSpan.FromMilliseconds(20));
        var b = new Probe(_ => null, delay: TimeSpan.FromMilliseconds(20));
        using var workCounts = new ConsumerMetrics(service + ".work");
        using var noticeCountsA = new ConsumerMetrics(service + ".notice@node-a");
        using var noticeCountsB = new ConsumerMetrics(service + ".notice@node-b");
        void Subscribe(QianEventsSubscriberBuilder subscriber) => subscriber
            .Subscribe<PingSent, ProbeHandler>("work")
            .Subscribe<PingSent, NoticeHandler>("notice", EventSubscriptionMode.Broadcast);
        using var hostA = Host(service, a, Subscribe, instance: "node-a", prefetch: 1);
        using var hostB = Host(service, b, Subscribe, instance: "node-b", prefetch: 1);
        await Task.WhenAll(StartAsync(hostA), StartAsync(hostB));
        var published = new List<Guid>();
        for (var index = 0; index < total; index++) published.Add((await PublishAsync(TenantA, sequence: 100 + index)).EventId);

        // 结束态：三个消费者名的 processed 都到齐（确认之后计数），竞争队列与两个广播队列的 messages（含未确认）都为 0，之后不会再有投递；
        // 「没有一条被两边都处理」因此可以在这一刻断言。
        await Until(async () => workCounts.Count("processed") == total && noticeCountsA.Count("processed") == total && noticeCountsB.Count("processed") == total
            && await SettledAsync([QianEventTopology.SubscriptionQueue(service, "work"), QianEventTopology.BroadcastQueue(service, "node-a"), QianEventTopology.BroadcastQueue(service, "node-b")]),
            "两个实例没有处理完");
        // 竞争：两个实例合起来恰好每条一次，没有一条被两边都处理；两边都分到了消息。
        Assert.Equal(published.Order(), a.Work.Concat(b.Work).Order());
        Assert.Empty(a.Work.Intersect(b.Work));
        Assert.NotEmpty(a.Work);
        Assert.NotEmpty(b.Work);
        // 广播：每个实例各收到全部消息一次；广播幂等记录按实例区分。
        Assert.Equal(published.Order(), a.Notices.Order());
        Assert.Equal(published.Order(), b.Notices.Order());
        Assert.Equal(total, (await IdempotencyKeysAsync(service + ".notice@node-a")).Count);
        Assert.Equal(total, (await IdempotencyKeysAsync(service + ".notice@node-b")).Count);
        Assert.Equal(total, (await IdempotencyKeysAsync(service + ".work")).Count);

        var listing = await QueueTypesAsync();
        Assert.Equal(("quorum", "false"), listing[QianEventTopology.SubscriptionQueue(service, "work")]);
        Assert.Equal(("classic", "true"), listing[QianEventTopology.BroadcastQueue(service, "node-a")]);
        Assert.Equal(("classic", "true"), listing[QianEventTopology.BroadcastQueue(service, "node-b")]);
        Assert.Equal(("quorum", "false"), listing[QianEventTopology.DeadLetterQueue(service, "notice")]);

        // 实例 a 下线：它的广播队列随最后一个消费者断开而删除，竞争队列与死信队列保留；之后的消息只到 b。
        await hostA.StopAsync(Ct);
        await Until(async () => !(await QueueTypesAsync()).ContainsKey(QianEventTopology.BroadcastQueue(service, "node-a")), "下线实例的广播队列没有被删除");
        listing = await QueueTypesAsync();
        Assert.True(listing.ContainsKey(QianEventTopology.SubscriptionQueue(service, "work")));
        Assert.True(listing.ContainsKey(QianEventTopology.BroadcastQueue(service, "node-b")));
        var workA = a.Work.Count; var noticeA = a.Notices.Count; var workB = b.Work.Count;
        var later = new List<Guid>();
        for (var index = 0; index < 5; index++) later.Add((await PublishAsync(TenantA, sequence: 200 + index)).EventId);
        await Until(() => b.Work.Count == workB + 5 && b.Notices.Count == total + 5, "下线后的消息没有全部到 b");
        Assert.Equal((workA, noticeA), (a.Work.Count, a.Notices.Count));
        Assert.Subset(b.Work.ToHashSet(), later.ToHashSet());
        await hostB.StopAsync(Ct);
    }

    [Fact]
    public async Task 未登记传播器时按信封租户回灌_原文重复tenant_id或与头不一致或平台级即拒绝_不进handler不记幂等不重投()
    {
        var service = Service();
        var probe = new Probe(_ => null);
        using var tenantMetrics = new ConsumerMetrics(service + ".work");
        using var platformMetrics = new ConsumerMetrics(service + ".platform");
        using var host = Host(service, probe, subscriber => subscriber
            .Subscribe<PingSent, ProbeHandler>("work")
            .Subscribe<PingSent, NoticeHandler>("platform", acceptPlatformEvents: true));
        await StartAsync(host);
        var a = await PublishAsync(TenantA, sequence: 10);
        var b = await PublishAsync(TenantB, sequence: 11);
        await Until(() => probe.Calls.Count == 2 && probe.Notices.Count == 2, "租户事件没有处理完");
        Assert.Equal([(a.EventId, TenantA), (b.EventId, TenantB)], probe.Calls.OrderBy(call => call.Sequence).Select(call => (call.EventId, call.Tenant)));

        var platform = Envelope(null, sequence: 12);
        var duplicated = Envelope(TenantA, sequence: 13);
        var mismatched = Envelope(TenantA, sequence: 14);
        await RawPublishAsync(platform, platform.ToJson(), tenantHeader: null);
        await RawPublishAsync(duplicated, DuplicateTenant(duplicated.ToJson(), TenantA), tenantHeader: TenantA);
        await RawPublishAsync(mismatched, mismatched.ToJson(), tenantHeader: TenantB);
        var deadLetter = QianEventTopology.DeadLetterQueue(service, "work");
        // 结束态：两个订阅的 rejected 与 processed 都到齐（确认之后计数），死信队列恰有三条，两个主队列都已清空，之后不会再有投递。
        await Until(async () => tenantMetrics.Count("rejected") == 3 && platformMetrics.Count("rejected") == 2 && platformMetrics.Count("processed") == 3
            && (await QueuesAsync()).GetValueOrDefault(deadLetter) == 3
            && await SettledAsync([QianEventTopology.SubscriptionQueue(service, "work"), QianEventTopology.SubscriptionQueue(service, "platform")]), "三条反例没有全部进死信队列");

        Assert.Equal(2, probe.Calls.Count);
        Assert.Equal(3, tenantMetrics.Count("rejected"));
        Assert.Equal(["platform_event", "tenant_mismatch", "tenant_rejected"], (await DrainAsync(deadLetter)).Select(message => Header(message, "x-qian-reason")!).Order());
        foreach (var id in new[] { platform.EventId, duplicated.EventId, mismatched.EventId })
            Assert.False(await Redis().KeyExistsAsync("idemp:" + service + ".work:" + id));
        // 显式接受平台级事件的订阅照常派发，且不开租户作用域；同一订阅对租户不一致的消息同样拒绝。
        Assert.Contains(probe.NoticeTenants, entry => entry.Key == platform.EventId && entry.Value is null);
        Assert.Equal(2, platformMetrics.Count("rejected"));
        Assert.Equal(0, (await QueuesAsync())[QianEventTopology.SubscriptionQueue(service, "work")]);
        await host.StopAsync(Ct);
    }

    private IHost Host(string service, Probe probe, Action<QianEventsSubscriberBuilder> subscribe, TimeSpan? retryBaseDelay = null, string? instance = null, ushort prefetch = 16,
        Action<IServiceCollection>? services = null)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddProvider(probe.Logs);
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddKunEventBus(events => events.Register(PingSent.Type, TestEventJson.Default.PingSent));
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis.ConnectionString));
        builder.Services.AddSingleton(probe);
        builder.Services.AddQianEventsBroker(_ => rabbit.ConnectionString);
        builder.Services.AddQianEventsSubscriber(service, subscribe, options =>
        {
            options.RetryBaseDelay = retryBaseDelay ?? TimeSpan.FromMilliseconds(100);
            options.PrefetchCount = prefetch;
            if (instance is not null) options.Instance = instance;
        });
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(20));
        services?.Invoke(builder.Services);
        return builder.Build();
    }

    private static async Task StartAsync(IHost host)
    {
        await host.StartAsync(Ct);
        await host.Services.GetServices<IHostedService>().OfType<QianEventSubscriber>().Single().Ready.WaitAsync(TimeSpan.FromSeconds(30), Ct);
    }

    private static string Service() => "qian.events-tests.s" + Guid.NewGuid().ToString("N")[..8];

    private static QianEventEnvelope Envelope(string? tenant, int sequence, string note = "subscriber")
    {
        var occurredAt = DateTimeOffset.UtcNow;
        return new QianEventEnvelope
        {
            Version = QianEventEnvelope.CurrentVersion,
            EventId = Guid.CreateVersion7(occurredAt),
            Subject = "tests.ping.sent",
            Type = PingSent.Type,
            OccurredAt = occurredAt,
            TenantId = tenant is null ? null : Guid.Parse(tenant),
            Producer = "qian.events-tests",
            CausationId = Guid.CreateVersion7(),
            Actor = EventActor.Service("subscriber-tests"),
            TraceParent = "00-" + ActivityTraceId.CreateRandom().ToHexString() + "-" + ActivitySpanId.CreateRandom().ToHexString() + "-01",
            Payload = JsonSerializer.SerializeToElement(new PingSent(sequence, note), TestEventJson.Default.PingSent),
        };
    }

    // 生产发布器：属性与头（MessageId、Type、AppId、traceparent、tenant_id）都由它从信封写出。
    private async Task<QianEventEnvelope> PublishAsync(string? tenant, int sequence, string note = "subscriber", int times = 1)
    {
        var envelope = Envelope(tenant, sequence, note);
        await using var broker = new RabbitMqBroker(() => rabbit.ConnectionString, NullLogger<RabbitMqBroker>.Instance);
        var publisher = new QianEnvelopePublisher(broker);
        for (var index = 0; index < times; index++)
            await publisher.PublishAsync(new OutboxMessage(envelope.EventId, envelope.Type, envelope.ToJson(), tenant, envelope.TraceParent, null, envelope.OccurredAt), Ct);
        return envelope;
    }

    // 伪造的消息：消息体或头不满足契约，模拟绕过 Outbox 发布器的发布方。
    private async Task RawPublishAsync(QianEventEnvelope envelope, string body, string? tenantHeader)
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), Ct);
        var headers = new Dictionary<string, object?> { ["traceparent"] = envelope.TraceParent };
        if (tenantHeader is not null) headers["tenant_id"] = tenantHeader;
        var properties = new BasicProperties { MessageId = envelope.EventId.ToString("D"), Type = envelope.Type, ContentType = "application/json", Headers = headers };
        await channel.BasicPublishAsync(QianEventTopology.Exchange, envelope.Subject, mandatory: true, properties, Encoding.UTF8.GetBytes(body), Ct);
    }

    private static string DuplicateTenant(string json, string tenant)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove("tenant_id");
        var text = node.ToJsonString();
        return text[..^1] + ",\"tenant_id\":\"" + tenant + "\",\"tenant_id\":\"" + tenant + "\"}";
    }

    private IDatabase Redis() => (_redis ??= ConnectionMultiplexer.Connect(redis.ConnectionString)).GetDatabase();

    public async ValueTask DisposeAsync()
    {
        if (_redis is not null) await _redis.DisposeAsync();
    }

    private async Task<List<string>> IdempotencyKeysAsync(string consumer)
    {
        var connection = _redis ??= await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var server = connection.GetServer(connection.GetEndPoints()[0]);
        var keys = new List<string>();
        await foreach (var key in server.KeysAsync(pattern: "idemp:" + consumer + ":*")) keys.Add(key.ToString());
        return keys;
    }

    private async Task<Dictionary<string, long>> QueuesAsync()
    {
        var listing = await rabbit.Container.ExecAsync(["rabbitmqctl", "list_queues", "-q", "name", "messages"], Ct);
        Assert.Equal(0, listing.ExitCode);
        // rabbitmqctl 4 即使带 -q 也输出表头行，按第二列能否解析为数字跳过它。
        return Rows(listing.Stdout).Where(cells => cells.Length == 2 && long.TryParse(cells[1], System.Globalization.CultureInfo.InvariantCulture, out _))
            .ToDictionary(cells => cells[0], cells => long.Parse(cells[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    // 队列都已声明且 messages（就绪加未确认）为 0：消息在这些队列里没有任何副本，不会再有投递。
    private async Task<bool> SettledAsync(IEnumerable<string> queues)
    {
        var listing = await QueuesAsync();
        return queues.All(queue => listing.TryGetValue(queue, out var count) && count == 0);
    }

    private async Task<Dictionary<string, (string Type, string AutoDelete)>> QueueTypesAsync()
    {
        var listing = await rabbit.Container.ExecAsync(["rabbitmqctl", "list_queues", "-q", "name", "type", "auto_delete"], Ct);
        Assert.Equal(0, listing.ExitCode);
        return Rows(listing.Stdout).Where(cells => cells.Length == 3).ToDictionary(cells => cells[0], cells => (cells[1], cells[2]));
    }

    private static IEnumerable<string[]> Rows(string stdout) =>
        stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(line => line.Split('\t', StringSplitOptions.TrimEntries));

    private async Task<BasicGetResult> GetAsync(string queue) => Assert.Single(await DrainAsync(queue));

    private async Task<List<BasicGetResult>> DrainAsync(string queue)
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        var messages = new List<BasicGetResult>();
        while (await channel.BasicGetAsync(queue, autoAck: true, Ct) is { } message) messages.Add(message);
        return messages;
    }

    private static string? Header(BasicGetResult message, string key) =>
        message.BasicProperties.Headers?.TryGetValue(key, out var value) == true ? value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value?.ToString() : null;

    private static Task Until(Func<bool> condition, string because) => Until(() => Task.FromResult(condition()), because);

    private static async Task Until(Func<Task<bool>> condition, string because)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (!await condition())
        {
            if (DateTimeOffset.UtcNow > deadline) Assert.Fail(because);
            await Task.Delay(50, Ct);
        }
    }

    internal sealed class FlakyDependencyException() : Exception("下游暂时不可用");

    internal sealed record Call(Guid EventId, int Sequence, int Attempt, DateTimeOffset At, string? Tenant, MessageContext Context, QianEventDelivery Delivery,
        string? TraceId, ActivityKind? ActivityKind);

    /// <summary>每个宿主一份：记录 handler 看到的投递与租户，按第几次投递决定是否抛出。</summary>
    internal sealed class Probe(Func<int, Exception?> failure, TimeSpan delay = default, Func<Task>? gate = null)
    {
        private int _succeeded;
        public ConcurrentQueue<Call> Calls { get; } = new();
        public ConcurrentBag<Guid> Work { get; } = [];
        public ConcurrentBag<Guid> Notices { get; } = [];
        public ConcurrentDictionary<Guid, string?> NoticeTenants { get; } = new();
        public LogRecorder Logs { get; } = new();
        public int Succeeded => Volatile.Read(ref _succeeded);
        public Func<int, Exception?> Failure => failure;
        public TimeSpan Delay => delay;
        public Func<Task>? Gate => gate;
        public void Success() => Interlocked.Increment(ref _succeeded);
    }

    internal sealed class ProbeHandler(Probe probe, QianEventDelivery delivery, ITenantContext tenant) : IIntegrationEventHandler<PingSent>
    {
        public async ValueTask HandleAsync(PingSent @event, MessageContext context, CancellationToken cancellationToken = default)
        {
            var activity = Activity.Current;
            probe.Calls.Enqueue(new Call(context.MessageId, @event.Sequence, delivery.Attempt, DateTimeOffset.UtcNow, tenant.TenantId, context, delivery,
                activity?.TraceId.ToHexString(), activity?.Kind));
            if (probe.Delay > TimeSpan.Zero) await Task.Delay(probe.Delay, cancellationToken);
            if (probe.Gate is { } gate) await gate();
            if (probe.Failure(delivery.Attempt) is { } error) throw error;
            probe.Work.Add(context.MessageId);
            probe.Success();
        }
    }

    internal sealed class NoticeHandler(Probe probe, ITenantContext tenant) : IIntegrationEventHandler<PingSent>
    {
        public async ValueTask HandleAsync(PingSent @event, MessageContext context, CancellationToken cancellationToken = default)
        {
            if (probe.Delay > TimeSpan.Zero) await Task.Delay(probe.Delay, cancellationToken);
            probe.NoticeTenants[context.MessageId] = tenant.TenantId;
            probe.Notices.Add(context.MessageId);
        }
    }

    internal sealed record LogEntry(LogLevel Level, int EventId, string Message);

    internal sealed class LogRecorder : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);
        public void Dispose() { }

        private sealed class Logger(LogRecorder owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => category.StartsWith("Qian.Events", StringComparison.Ordinal);
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel)) owner.Entries.Enqueue(new LogEntry(logLevel, eventId.Id, formatter(state, exception)));
            }
        }
    }

    /// <summary>按消费者名过滤的消费端计数；meter 是进程级的，并行的其他测试类不会混进来。</summary>
    private sealed class ConsumerMetrics : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly ConcurrentDictionary<string, long> _counts = new();

        public ConsumerMetrics(string consumer)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Qian.Events" && instrument.Name.StartsWith("qian.events.consumer.", StringComparison.Ordinal)) listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                foreach (var tag in tags)
                    if (tag.Key == "qian.events.consumer" && Equals(tag.Value, consumer))
                        _counts.AddOrUpdate(instrument.Name["qian.events.consumer.".Length..], value, (_, current) => current + value);
            });
            _listener.Start();
        }

        public long Count(string outcome) => _counts.GetValueOrDefault(outcome);

        public void Dispose() => _listener.Dispose();
    }

    private sealed class TraceRecorder : IDisposable
    {
        private readonly ActivityListener _listener;
        public ConcurrentBag<string> Names { get; } = [];

        public TraceRecorder()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "Qian.Events",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => Names.Add(activity.DisplayName),
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose() => _listener.Dispose();
    }
}
