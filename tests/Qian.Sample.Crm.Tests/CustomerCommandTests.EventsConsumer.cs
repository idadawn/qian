using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    [Fact]
    public async Task 消费端handler内租户为事件租户_EF查询自动带tenant_id过滤且只读到本租户_两个租户并发互不串()
    {
        await using var rabbit = new RabbitMqFixture();
        await rabbit.InitializeAsync();
        await using var redis = new RedisFixture();
        await redis.InitializeAsync();
        var probe = new TenantProbe();
        await using var host = await Start(events: rabbit.ConnectionString, configure: builder => ConsumeWithProbe(builder, redis, probe));
        await ConsumingAsync(rabbit, CustomerCreatedQueue, TenantProbeQueue);
        var receipts = host.App.Services.GetRequiredService<CustomerCreatedReceipts>();
        // 测试宿主只给 B 的写客户端使用者角色；这里经同一个真实角色存储授予 Owner，B 才能建客户、发出自己的事件。
        var tenancy = host.App.Services.GetRequiredService<ITenantContext>();
        using (tenancy.BeginScope(B))
            await new PostgresTenantRoleStore(host.App.Services.GetRequiredService<IDataService>(), tenancy).AssignAsync("tenant.owner", "service", Writer, "test-bootstrap", Ct);

        // B 先有一位客户；随后 A、B 并发各建一位，三条事件经 Outbox → RabbitMQ → 订阅器各自回到自己的租户。
        var seed = await CreateAsync(host, B, "consumer-b-seed");
        await UntilAsync(() => probe.Seen.Count == 1 && receipts.Items.Count == 1, "B 的第一条事件没有处理完\n" + host.FailureEvidence);
        var concurrent = await Task.WhenAll(CreateAsync(host, A, "consumer-a"), CreateAsync(host, B, "consumer-b"));
        await UntilAsync(() => probe.Seen.Count == 3 && receipts.Items.Count == 3, "并发的两条事件没有处理完\n" + host.FailureEvidence);
        var owners = new Dictionary<Guid, string> { [seed.Id] = B, [concurrent[0].Id] = A, [concurrent[1].Id] = B };

        foreach (var seen in probe.Seen)
        {
            var tenant = owners[seen.CustomerId];
            Assert.Equal((tenant, tenant, Guid.Parse(tenant)), (seen.Tenant, seen.ContextTenant, seen.EnvelopeTenant));
            // EF 全局过滤器按回灌的租户生成条件：SQL 里有 tenant_id 谓词，参数值就是该租户；结果只有本租户的行。
            Assert.Contains("tenant_id", seen.Sql, StringComparison.Ordinal);
            Assert.Contains(tenant, seen.Sql, StringComparison.Ordinal);
            Assert.All(seen.Rows, row => Assert.Equal(tenant, row.TenantId));
            Assert.Contains(seen.Rows, row => row.Id == seen.CustomerId);
            Assert.DoesNotContain(seen.Rows, row => owners.TryGetValue(row.Id, out var owner) && owner != tenant);
            Assert.Equal(("qian.sample.crm", Writer), (seen.Producer, seen.Actor));
        }
        // 参考宿主自己的订阅同样在事件租户里读回了客户；每条事件都经 ITenantContextPropagator 进入，授权租户即信封租户。
        Assert.Equal(owners.OrderBy(pair => pair.Key), receipts.Items.Select(receipt => KeyValuePair.Create(receipt.CustomerId, receipt.TenantId)).OrderBy(pair => pair.Key));
        Assert.Equal(6, probe.Scopes.Count);
        Assert.All(probe.Scopes, scope => Assert.Null(scope.Rejected));
        Assert.Equal(probe.Seen.Select(seen => (seen.EventId, Guid.Parse(owners[seen.CustomerId]))).Order(),
            probe.Scopes.Select(scope => (scope.EventId!.Value, scope.Authorized)).Distinct().Order());
        await using var keys = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        foreach (var seen in probe.Seen)
        {
            Assert.True(await keys.GetDatabase().KeyExistsAsync("idemp:qian.sample.crm.tenant-probe:" + seen.EventId));
            Assert.True(await keys.GetDatabase().KeyExistsAsync("idemp:qian.sample.crm.customer-created:" + seen.EventId));
        }
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
        await Task.Delay(TimeSpan.FromMilliseconds(500), Ct);

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

    // 用例的探针订阅与计数传播器先于参考宿主自己的装配登记：同一服务名追加订阅，传播器以 TryAdd 让位给这里的包装。
    private static void ConsumeWithProbe(WebApplicationBuilder builder, RedisFixture redis, TenantProbe probe)
    {
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Redis"] = redis.ConnectionString });
        builder.Services.AddSingleton(probe);
        builder.Services.AddSingleton<ITenantContextPropagator>(sp => new RecordingPropagator(new TenantContextPropagator(sp.GetRequiredService<ITenantContext>()), probe));
        builder.Services.AddQianEventsSubscriber("qian.sample.crm", subscriber => subscriber.Subscribe<CrmCustomerCreated, TenantProbeHandler>("tenant-probe"),
            options => options.RetryBaseDelay = TimeSpan.FromMilliseconds(100));
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
        IReadOnlyList<ProbeRow> Rows, string Producer, string Actor);

    private sealed record ScopeEntry(Guid? EventId, Guid Authorized, string? Rejected);

    private sealed class TenantProbe
    {
        public ConcurrentQueue<TenantObservation> Seen { get; } = new();
        public ConcurrentQueue<ScopeEntry> Scopes { get; } = new();
    }

    // 探针 handler：在订阅器回灌的租户里查客户表，记下租户上下文、EF 生成的 SQL 与结果。
    private sealed class TenantProbeHandler(IDataService data, ITenantContext tenant, QianEventDelivery delivery, TenantProbe probe) : IIntegrationEventHandler<CrmCustomerCreated>
    {
        public async ValueTask HandleAsync(CrmCustomerCreated @event, MessageContext context, CancellationToken cancellationToken = default)
        {
            await using var session = await data.OpenAsync<CrmDbContext>(cancellationToken);
            var query = session.Context.Customers.AsNoTracking().OrderBy(customer => customer.Id).Select(customer => new ProbeRow(customer.Id, customer.TenantId));
            var rows = await query.ToListAsync(cancellationToken);
            probe.Seen.Enqueue(new TenantObservation(context.MessageId, @event.CustomerId, tenant.TenantId, context.TenantId, delivery.Envelope.TenantId, query.ToQueryString(),
                rows, delivery.Envelope.Producer, delivery.Envelope.Actor.Id));
        }
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
