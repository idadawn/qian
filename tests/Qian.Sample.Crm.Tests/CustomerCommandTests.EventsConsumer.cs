using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kun.Core.MultiTenancy;
using Kun.EventBus;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Authz;
using Qian.Events;
using Qian.Tenancy;
using Qian.Testing.Containers;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Qian.Sample.Crm.Tests;

// 消费端联调（S1-EVT-002）：参考宿主（Kun 多租户生产模式、受限应用角色 + RLS）经真实 RabbitMQ 与 Redis 消费自己发布的客户建档事件。
public sealed partial class CustomerCommandTests
{
    private static readonly string CustomerCreatedQueue = QianEventTopology.SubscriptionQueue("qian.sample.crm", "customer-created");
    private static readonly string TenantProbeQueue = QianEventTopology.SubscriptionQueue("qian.sample.crm", "tenant-probe");
    private static readonly string TenantPeerQueue = QianEventTopology.SubscriptionQueue("qian.sample.crm", "tenant-peer");
    private static readonly string[] ProbedSubscriptions = ["tenant-probe", "tenant-peer", "customer-created"];

    [Fact]
    public async Task 消费端handler内租户为事件租户_EF查询自动带tenant_id过滤且只读到本租户_两个租户并发互不串()
    {
        await using var rabbit = new RabbitMqFixture();
        await rabbit.InitializeAsync();
        await using var redis = new RedisFixture();
        await redis.InitializeAsync();
        var probe = new TenantProbe();
        await using var host = await Start(events: rabbit.ConnectionString, configure: builder => ConsumeWithProbe(builder, redis, probe, peer: true));
        await ConsumingAsync(rabbit, CustomerCreatedQueue, TenantProbeQueue, TenantPeerQueue);
        var receipts = host.App.Services.GetRequiredService<CustomerCreatedReceipts>();
        // 测试宿主只给 B 的写客户端使用者角色；这里经同一个真实角色存储授予 Owner，B 才能建客户、发出自己的事件。
        var tenancy = host.App.Services.GetRequiredService<ITenantContext>();
        using (tenancy.BeginScope(B))
            await new PostgresTenantRoleStore(host.App.Services.GetRequiredService<IDataService>(), tenancy).AssignAsync("tenant.owner", "service", Writer, "test-bootstrap", Ct);

        // B 先有一位客户；随后 A、B 并发各建一位，三条事件经 Outbox → RabbitMQ → 订阅器各自回到自己的租户。
        var seed = await CreateAsync(host, B, "consumer-b-seed");
        await UntilAsync(() => probe.Seen.Count == 1 && probe.PeerSeen.Count == 1 && receipts.Items.Count == 1, "B 的第一条事件没有处理完\n" + host.FailureEvidence);
        // 同一订阅内的消息依次处理，两个租户的事件只能在不同订阅里同时处理：探针与对端两个订阅之间设屏障，
        // 让一边停在一个租户的事件里、另一边进入另一个租户的事件，两边在重叠期间各自读租户上下文并做 EF 查询。
        probe.Overlap.Arm();
        var concurrent = await Task.WhenAll(CreateAsync(host, A, "consumer-a"), CreateAsync(host, B, "consumer-b"));
        await UntilAsync(() => probe.Seen.Count == 3 && probe.PeerSeen.Count == 3 && receipts.Items.Count == 3, "并发的两条事件没有处理完\n" + host.FailureEvidence);
        var owners = new Dictionary<Guid, string> { [seed.Id] = B, [concurrent[0].Id] = A, [concurrent[1].Id] = B };

        foreach (var seen in probe.Seen.Concat(probe.PeerSeen))
        {
            var tenant = owners[seen.CustomerId];
            Assert.Equal((tenant, tenant, Guid.Parse(tenant)), (seen.Tenant, seen.ContextTenant, seen.EnvelopeTenant));
            // EF 全局过滤器按回灌的租户生成条件：WHERE 里有 tenant_id 与参数的比较，参数值就是该租户。去掉全局过滤器时 SQL 没有这条谓词，
            // 而行级安全照样只返回本租户的行，所以结果断言区分不了，必须看 SQL。
            Assert.True(FilterTenant(seen.Sql) == tenant, "WHERE 里没有以本租户为参数值的 tenant_id 谓词：\n" + seen.Sql);
            Assert.All(seen.Rows, row => Assert.Equal(tenant, row.TenantId));
            Assert.Contains(seen.Rows, row => row.Id == seen.CustomerId);
            Assert.DoesNotContain(seen.Rows, row => owners.TryGetValue(row.Id, out var owner) && owner != tenant);
            Assert.Equal(("qian.sample.crm", Writer), (seen.Producer, seen.Actor));
        }
        // 重叠确实发生：探针停在一个租户的事件里时，对端处理着另一个租户的事件，两边的观察都取自重叠期间，且各自只见本租户。
        var overlapping = probe.Seen.Concat(probe.PeerSeen).Where(seen => seen.DuringOverlap).ToArray();
        Assert.Equal(2, overlapping.Length);
        Assert.Equal([A, B], overlapping.Select(seen => owners[seen.CustomerId]).Order());
        Assert.Equal(1, probe.Seen.Count(seen => seen.DuringOverlap));
        Assert.Empty(probe.Overlap.Timeouts);
        // 参考宿主自己的订阅同样在事件租户里读回了客户；每条事件在三个订阅里都经 ITenantContextPropagator 进入，授权租户即信封租户。
        // 瞬时重试会让同一订阅再进入一次，所以按事件去重比较，不比总次数。
        Assert.Equal(owners.OrderBy(pair => pair.Key), receipts.Items.Select(receipt => KeyValuePair.Create(receipt.CustomerId, receipt.TenantId)).OrderBy(pair => pair.Key));
        Assert.All(probe.Scopes, scope => Assert.Null(scope.Rejected));
        Assert.Equal(probe.Seen.Select(seen => (seen.EventId, Guid.Parse(owners[seen.CustomerId]))).Order(),
            probe.Scopes.Select(scope => (scope.EventId!.Value, scope.Authorized)).Distinct().Order());
        Assert.All(probe.Scopes.GroupBy(scope => scope.EventId), entries => Assert.True(entries.Count() >= 3, entries.Key + " 只经传播器进入了 " + entries.Count() + " 次"));
        // 幂等记录在 handler 返回之后才写入，handler 里记下观察不代表键已经写好：等到键出现再断言。
        await using var keys = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var expected = probe.Seen.SelectMany(seen => ProbedSubscriptions.Select(subscription => "idemp:qian.sample.crm." + subscription + ":" + seen.EventId)).ToArray();
        await UntilAsync(async () => (await Task.WhenAll(expected.Select(key => keys.GetDatabase().KeyExistsAsync(key)))).All(exists => exists), "幂等记录没有全部写入");
    }

    [Fact]
    public async Task 消费端经ITenantContextPropagator回灌信封租户_缺失重复与头不一致及平台级都fail_closed_不进handler不记幂等不重投()
    {
        await using var rabbit = new RabbitMqFixture();
        await rabbit.InitializeAsync();
        await using var redis = new RedisFixture();
        await redis.InitializeAsync();
        var probe = new TenantProbe();
        await using var host = await Start(events: rabbit.ConnectionString, configure: builder => ConsumeWithProbe(builder, redis, probe));
        await ConsumingAsync(rabbit, CustomerCreatedQueue, TenantProbeQueue);

        // 正例：handler 内读到的租户就是发送方的租户上下文（A 的令牌），经传播器进入。
        var customer = await CreateAsync(host, A, "propagated");
        await UntilAsync(() => probe.Seen.Count == 1, "A 的事件没有处理完\n" + host.FailureEvidence);
        var positive = Assert.Single(probe.Seen);
        Assert.Equal(A, positive.Tenant);
        Assert.Contains(probe.Scopes, scope => scope.EventId == positive.EventId && scope.Authorized == Guid.Parse(A) && scope.Rejected is null);

        // 反例：绕过 Outbox 发布器直接发到 qian.events，消息体或 AMQP 头的 tenant_id 不满足契约。
        var crafted = new (string Name, QianEventEnvelope Envelope, Func<string, string> Body, string? Header)[]
        {
            ("missing", Envelope(customer.Id, A), json => Without(json, "tenant_id"), A),
            ("missing-header", Envelope(customer.Id, A), json => json, null),
            ("duplicate", Envelope(customer.Id, A), json => WithDuplicateTenant(json, A), A),
            ("mismatch", Envelope(customer.Id, A), json => json, B),
            ("platform", Envelope(customer.Id, null), json => json, null),
        };
        foreach (var (_, envelope, body, header) in crafted) await RawPublishAsync(rabbit, envelope, body(envelope.ToJson()), header);
        var probeDeadLetter = QianEventTopology.DeadLetterQueue("qian.sample.crm", "tenant-probe");
        await UntilAsync(async () => await MessagesAsync(rabbit, probeDeadLetter) == crafted.Length, "反例没有全部进死信队列");
        await UntilAsync(async () => await MessagesAsync(rabbit, QianEventTopology.DeadLetterQueue("qian.sample.crm", "customer-created")) == crafted.Length,
            "参考宿主自己的订阅没有拒绝反例");
        // 结束态：两个主队列的 messages（就绪加未确认）都为 0，反例在死信队列里，之后不会再有投递。
        await UntilAsync(async () => await MessagesAsync(rabbit, TenantProbeQueue) == 0 && await MessagesAsync(rabbit, CustomerCreatedQueue) == 0, "主队列没有清空");

        Assert.Single(probe.Seen);
        Assert.Equal(0, await MessagesAsync(rabbit, TenantProbeQueue));
        var dead = await DrainAsync(rabbit, probeDeadLetter);
        Assert.Equal(crafted.Select(item => item.Envelope.EventId.ToString("D")).Order(), dead.Select(message => message.BasicProperties.MessageId!).Order());
        var reasons = dead.ToDictionary(message => Guid.Parse(message.BasicProperties.MessageId!), message => Encoding.UTF8.GetString((byte[])message.BasicProperties.Headers!["x-qian-reason"]!));
        Assert.Equal(["invalid_envelope", "tenant_mismatch", "tenant_rejected", "tenant_mismatch", "platform_event"], crafted.Select(item => reasons[item.Envelope.EventId]));
        // 原文里两个 tenant_id 的那条由传播器判定拒绝（两个订阅各进入一次）；其余在进入租户之前就被信封或 AMQP 头的核对拦下，传播器没有被调用。
        var duplicate = crafted[2].Envelope.EventId;
        var denied = probe.Scopes.Where(scope => scope.EventId == duplicate).ToArray();
        Assert.Equal(2, denied.Length);
        Assert.All(denied, scope => Assert.Equal((Guid.Parse(A), nameof(Qian.Runtime.Errors.QianErrorException)), (scope.Authorized, scope.Rejected)));
        Assert.DoesNotContain(probe.Scopes, scope => crafted.Where(item => item.Name != "duplicate").Any(item => item.Envelope.EventId == scope.EventId));
        await using var keys = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        foreach (var (_, envelope, _, _) in crafted)
        {
            Assert.False(await keys.GetDatabase().KeyExistsAsync("idemp:qian.sample.crm.tenant-probe:" + envelope.EventId));
            Assert.False(await keys.GetDatabase().KeyExistsAsync("idemp:qian.sample.crm.customer-created:" + envelope.EventId));
        }
    }

    // 用例的探针订阅与计数传播器先于参考宿主自己的装配登记：同一服务名追加订阅，传播器以 TryAdd 让位给这里的包装。peer 再加一个对端订阅，与探针之间设屏障。
    private static void ConsumeWithProbe(WebApplicationBuilder builder, RedisFixture redis, TenantProbe probe, bool peer = false)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Redis"] = redis.ConnectionString });
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<ITenantContextPropagator>(sp => new RecordingPropagator(new TenantContextPropagator(sp.GetRequiredService<ITenantContext>()), probe));
        builder.Services.AddQianEventsSubscriber("qian.sample.crm", subscriber =>
        {
            subscriber.Subscribe<CrmCustomerCreated, TenantProbeHandler>("tenant-probe");
            if (peer) subscriber.Subscribe<CrmCustomerCreated, TenantPeerHandler>("tenant-peer");
        }, options => options.RetryBaseDelay = TimeSpan.FromMilliseconds(100));
    }

    // ToQueryString 把参数写成「-- @名字='值'」注释行；取 WHERE 里与 tenant_id 比较的参数，返回它的值，没有这条谓词时为 null。
    private static string? FilterTenant(string sql)
    {
        var where = Regex.Match(sql, @"\bWHERE\b(?<predicate>[\s\S]*?)(?:\bORDER BY\b|$)");
        var comparison = Regex.Match(where.Success ? where.Groups["predicate"].Value : "", @"\btenant_id\b\s*=\s*(?<parameter>@\w+)");
        if (!comparison.Success) return null;
        var value = Regex.Match(sql, "^-- " + Regex.Escape(comparison.Groups["parameter"].Value) + "='(?<value>[^']*)'", RegexOptions.Multiline);
        return value.Success ? value.Groups["value"].Value : null;
    }

    private static async Task<CustomerItem> CreateAsync(RunningApp host, string tenant, string name)
    {
        using var response = await host.Post(tenant, new CreateCustomerCommand(Guid.CreateVersion7(), name));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct) + "\n" + host.FailureEvidence);
        return (await response.Content.ReadFromJsonAsync<CustomerItem>(Ct))!;
    }

    private static QianEventEnvelope Envelope(Guid customerId, string? tenant)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        return new QianEventEnvelope
        {
            Version = QianEventEnvelope.CurrentVersion, EventId = Guid.CreateVersion7(occurredAt), Subject = "crm.customer.created", Type = CrmEvents.CustomerCreated,
            OccurredAt = occurredAt, TenantId = tenant is null ? null : Guid.Parse(tenant), Producer = "qian.sample.crm", Actor = EventActor.Service(Writer),
            TraceParent = "00-" + ActivityTraceId.CreateRandom().ToHexString() + "-" + ActivitySpanId.CreateRandom().ToHexString() + "-01",
            Payload = JsonSerializer.SerializeToElement(new CrmCustomerCreated(customerId), CrmCommandJson.Default.CrmCustomerCreated),
        };
    }

    private static string Without(string json, string field)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node.Remove(field);
        return node.ToJsonString();
    }

    private static string WithDuplicateTenant(string json, string tenant)
    {
        var text = Without(json, "tenant_id");
        return text[..^1] + ",\"tenant_id\":\"" + tenant + "\",\"tenant_id\":\"" + tenant + "\"}";
    }

    private static async Task RawPublishAsync(RabbitMqFixture rabbit, QianEventEnvelope envelope, string body, string? tenantHeader)
    {
        await using var connection = await new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) }.CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), Ct);
        var headers = new Dictionary<string, object?> { ["traceparent"] = envelope.TraceParent };
        if (tenantHeader is not null) headers["tenant_id"] = tenantHeader;
        var properties = new BasicProperties
        {
            MessageId = envelope.EventId.ToString("D"), Type = envelope.Type, AppId = envelope.Producer, ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent, Headers = headers,
        };
        await channel.BasicPublishAsync(QianEventTopology.Exchange, envelope.Subject, mandatory: true, properties, Encoding.UTF8.GetBytes(body), Ct);
    }

    private static async Task ConsumingAsync(RabbitMqFixture rabbit, params string[] queues)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (true)
        {
            var listing = await rabbit.Container.ExecAsync(["rabbitmqctl", "list_queues", "-q", "name", "consumers"], Ct);
            var consumers = Rows(listing.Stdout).ToDictionary(row => row.Name, row => row.Count);
            if (queues.All(queue => consumers.GetValueOrDefault(queue) > 0)) return;
            if (DateTimeOffset.UtcNow > deadline) Assert.Fail("订阅器没有开始消费：\n" + listing.Stdout);
            await Task.Delay(100, Ct);
        }
    }

    private static async Task<long> MessagesAsync(RabbitMqFixture rabbit, string queue)
    {
        var listing = await rabbit.Container.ExecAsync(["rabbitmqctl", "list_queues", "-q", "name", "messages"], Ct);
        return Rows(listing.Stdout).ToDictionary(row => row.Name, row => row.Count).GetValueOrDefault(queue);
    }

    // rabbitmqctl 4 即使带 -q 也输出表头行，按第二列能否解析为数字跳过它。
    private static IEnumerable<(string Name, long Count)> Rows(string stdout) => stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => line.Split('\t', StringSplitOptions.TrimEntries))
        .Where(cells => cells.Length == 2 && long.TryParse(cells[1], System.Globalization.CultureInfo.InvariantCulture, out _))
        .Select(cells => (cells[0], long.Parse(cells[1], System.Globalization.CultureInfo.InvariantCulture)));

    private static async Task<List<BasicGetResult>> DrainAsync(RabbitMqFixture rabbit, string queue)
    {
        await using var connection = await new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) }.CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        var messages = new List<BasicGetResult>();
        while (await channel.BasicGetAsync(queue, autoAck: true, Ct) is { } message) messages.Add(message);
        return messages;
    }

    private static Task UntilAsync(Func<bool> condition, string because) => UntilAsync(() => Task.FromResult(condition()), because);

    private static async Task UntilAsync(Func<Task<bool>> condition, string because)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (!await condition())
        {
            if (DateTimeOffset.UtcNow > deadline) Assert.Fail(because);
            await Task.Delay(50, Ct);
        }
    }

    private sealed record ProbeRow(Guid Id, string? TenantId);

    private sealed record TenantObservation(Guid EventId, Guid CustomerId, string? Tenant, string? ContextTenant, Guid? EnvelopeTenant, string Sql,
        IReadOnlyList<ProbeRow> Rows, string Producer, string Actor, bool DuringOverlap);

    private sealed record ScopeEntry(Guid? EventId, Guid Authorized, string? Rejected);

    private sealed class TenantProbe
    {
        public ConcurrentQueue<TenantObservation> Seen { get; } = new();
        public ConcurrentQueue<TenantObservation> PeerSeen { get; } = new();
        public ConcurrentQueue<ScopeEntry> Scopes { get; } = new();
        public OverlapBarrier Overlap { get; } = new();
    }

    /// <summary>
    /// 探针与对端两个订阅之间的屏障，Arm 之后生效一次：探针进入一个事件后等待，直到对端进入另一个租户的事件（重叠）；对端第一次进入时先等探针进入，
    /// 免得对端把两条事件都处理完探针还没开始；重叠时两边各自观察，都观察完才一起离开，于是两次观察都发生在对方仍在另一个租户的 handler 里时。
    /// 等待超过 30 秒只记一条超时并放行，由用例断言失败，不让 handler 抛出触发重试。
    /// </summary>
    private sealed class OverlapBarrier
    {
        private readonly Lock _gate = new();
        private readonly TaskCompletionSource _probeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _met = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _probeObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _peerObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile bool _armed;
        private string? _probeInside;
        private string? _peerInside;

        public ConcurrentQueue<string> Timeouts { get; } = new();

        public void Arm() => _armed = true;

        // 探针：返回是否处在重叠里；观察完调用 ProbeObservedAsync 再离开。
        public async Task<bool> ProbeEnterAsync(string tenant)
        {
            if (!_armed || _met.Task.IsCompleted) return false;
            lock (_gate) { _probeInside = tenant; Check(); }
            _probeEntered.TrySetResult();
            await WaitAsync(_met.Task, "探针等对端进入另一个租户的事件");
            return true;
        }

        public async Task ProbeObservedAsync()
        {
            _probeObserved.TrySetResult();
            await WaitAsync(_peerObserved.Task, "探针等对端观察完");
            lock (_gate) _probeInside = null;
        }

        // 对端：返回是否处在重叠里；不重叠时照常处理并离开，让它能走到下一个租户的事件。
        public async Task<bool> PeerEnterAsync(string tenant)
        {
            if (!_armed || _met.Task.IsCompleted) return false;
            await WaitAsync(_probeEntered.Task, "对端等探针进入事件");
            lock (_gate) { _peerInside = tenant; Check(); }
            if (!_met.Task.IsCompleted)
            {
                lock (_gate) _peerInside = null;
                return false;
            }
            return true;
        }

        public async Task PeerObservedAsync()
        {
            _peerObserved.TrySetResult();
            await WaitAsync(_probeObserved.Task, "对端等探针观察完");
            lock (_gate) _peerInside = null;
        }

        private void Check()
        {
            if (_probeInside is not null && _peerInside is not null && _probeInside != _peerInside) _met.TrySetResult();
        }

        private async Task WaitAsync(Task task, string what)
        {
            try { await task.WaitAsync(TimeSpan.FromSeconds(30)); }
            catch (TimeoutException) { Timeouts.Enqueue(what); }
        }
    }

    // 探针 handler：在订阅器回灌的租户里查客户表，记下租户上下文、EF 生成的 SQL 与结果；与对端之间经屏障制造跨租户重叠。
    private sealed class TenantProbeHandler(IDataService data, ITenantContext tenant, QianEventDelivery delivery, TenantProbe probe) : IIntegrationEventHandler<CrmCustomerCreated>
    {
        public async ValueTask HandleAsync(CrmCustomerCreated @event, MessageContext context, CancellationToken cancellationToken = default)
        {
            var overlap = await probe.Overlap.ProbeEnterAsync(tenant.TenantId!);
            probe.Seen.Enqueue(await ObserveAsync(data, tenant, delivery, @event, context, overlap, cancellationToken));
            if (overlap) await probe.Overlap.ProbeObservedAsync();
        }
    }

    // 对端 handler：与探针相同的观察，记在 PeerSeen。
    private sealed class TenantPeerHandler(IDataService data, ITenantContext tenant, QianEventDelivery delivery, TenantProbe probe) : IIntegrationEventHandler<CrmCustomerCreated>
    {
        public async ValueTask HandleAsync(CrmCustomerCreated @event, MessageContext context, CancellationToken cancellationToken = default)
        {
            var overlap = await probe.Overlap.PeerEnterAsync(tenant.TenantId!);
            probe.PeerSeen.Enqueue(await ObserveAsync(data, tenant, delivery, @event, context, overlap, cancellationToken));
            if (overlap) await probe.Overlap.PeerObservedAsync();
        }
    }

    private static async Task<TenantObservation> ObserveAsync(IDataService data, ITenantContext tenant, QianEventDelivery delivery, CrmCustomerCreated @event, MessageContext context,
        bool overlap, CancellationToken cancellationToken)
    {
        await using var session = await data.OpenAsync<CrmDbContext>(cancellationToken);
        var query = session.Context.Customers.AsNoTracking().OrderBy(customer => customer.Id).Select(customer => new ProbeRow(customer.Id, customer.TenantId));
        var rows = await query.ToListAsync(cancellationToken);
        return new TenantObservation(context.MessageId, @event.CustomerId, tenant.TenantId, context.TenantId, delivery.Envelope.TenantId, query.ToQueryString(),
            rows, delivery.Envelope.Producer, delivery.Envelope.Actor.Id, overlap);
    }

    // 包装真实的 TenantContextPropagator，只记录消费端每次进入的授权租户与结果，判定仍由真实实现做。
    private sealed class RecordingPropagator(ITenantContextPropagator inner, TenantProbe probe) : ITenantContextPropagator
    {
        public JsonElement Write(JsonElement carrier) => inner.Write(carrier);

        public IReadOnlyDictionary<string, string> WriteHeaders() => inner.WriteHeaders();

        public IDisposable BeginScope(JsonElement carrier, Guid authorizedTenantId)
        {
            Guid? eventId = carrier.ValueKind == JsonValueKind.Object && carrier.TryGetProperty("event_id", out var id) && id.TryGetGuid(out var parsed) ? parsed : null;
            try
            {
                var scope = inner.BeginScope(carrier, authorizedTenantId);
                probe.Scopes.Enqueue(new ScopeEntry(eventId, authorizedTenantId, null));
                return scope;
            }
            catch (Exception error)
            {
                probe.Scopes.Enqueue(new ScopeEntry(eventId, authorizedTenantId, error.GetType().Name));
                throw;
            }
        }

        public IDisposable BeginScope(IEnumerable<KeyValuePair<string, string>> headers, Guid authorizedTenantId) => inner.BeginScope(headers, authorizedTenantId);
    }
}
