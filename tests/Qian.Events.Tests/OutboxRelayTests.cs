using System.Collections.Concurrent;
using System.Text;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus;
using Kun.EventBus.DependencyInjection;
using Kun.EventBus.Outbox;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Qian.Data;
using Qian.Testing.Containers;
using RabbitMQ.Client;

namespace Qian.Events.Tests;

/// <summary>真实 PostgreSQL 与 RabbitMQ：多副本按租约领取、停掉一个副本由另一副本接管、崩溃持有的租约到期后被接管，确认数始终等于 Outbox 行数。</summary>
public sealed class OutboxRelayTests(PostgresFixture postgres, RabbitMqFixture rabbit) : IClassFixture<PostgresFixture>, IClassFixture<RabbitMqFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Tenant = "01900000-0000-7000-8000-0000000000e1";

    [Fact]
    public async Task 停掉一个发布副本后另一副本接管_确认数等于Outbox行数且无重复()
    {
        const int total = 120;
        var schema = await PrepareAsync();
        var queue = await BindQueueAsync();
        await SeedAsync(schema, total);
        var a = new Counter(TimeSpan.FromMilliseconds(15));
        var b = new Counter(TimeSpan.FromMilliseconds(15));
        using var replicaA = Replica(schema, "a", a, batch: 10);
        using var replicaB = Replica(schema, "b", b, batch: 10);
        await Task.WhenAll(replicaA.StartAsync(Ct), replicaB.StartAsync(Ct));
        await Until(() => a.Confirmed >= 20, "副本 a 尚未开始发布");
        // 等价于 K8s 删除 Pod：SIGTERM 触发宿主停机，手上这一条确认并标记后交还本批其余行。
        await replicaA.StopAsync(Ct);
        var stoppedAt = a.Confirmed;
        await Until(async () => await PendingAsync(schema) == 0, "副本 b 没有接管剩余消息");
        await replicaB.StopAsync(Ct);
        Assert.Equal(stoppedAt, a.Confirmed);
        Assert.InRange(a.Confirmed, 20, total - 1);
        Assert.True(b.Confirmed > 0);
        Assert.Equal(total, a.Confirmed + b.Confirmed);
        var received = await DrainAsync(queue);
        Assert.Equal(total, received.Count);
        Assert.Equal(total, received.Select(item => item.EventId).Distinct().Count());
        Assert.Equal(await PublishedIdsAsync(schema), received.Select(item => item.EventId).Order().ToArray());
        Assert.All(received, item => Assert.Equal(Tenant, item.TenantId?.ToString("D")));
    }

    [Fact]
    public async Task 副本领取后崩溃_租约到期由另一副本接管且每条只发布一次()
    {
        const int total = 30;
        var schema = await PrepareAsync();
        var queue = await BindQueueAsync();
        await SeedAsync(schema, total);
        // 崩溃的副本：领取并提交了租约，随即退出，没有发布任何一条。
        using (var crashed = Replica(schema, "crashed", new Counter(TimeSpan.Zero), batch: total))
        {
            await using var scope = crashed.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<RelayContext>();
            await context.Database.OpenConnectionAsync(Ct);
            var claimed = await OutboxTable.For(context).ClaimAsync(context.Database.GetDbConnection(), TimeSpan.FromSeconds(3), total, Ct);
            Assert.Equal(total, claimed.Count);
        }
        var counter = new Counter(TimeSpan.Zero);
        using var survivor = Replica(schema, "survivor", counter, batch: 100);
        var started = DateTimeOffset.UtcNow;
        await survivor.StartAsync(Ct);
        await Task.Delay(TimeSpan.FromSeconds(1), Ct);
        Assert.Equal(0, counter.Confirmed);
        await Until(async () => await PendingAsync(schema) == 0, "租约到期后未被接管");
        await survivor.StopAsync(Ct);
        Assert.True(DateTimeOffset.UtcNow - started >= TimeSpan.FromSeconds(2));
        Assert.Equal(total, counter.Confirmed);
        var received = await DrainAsync(queue);
        Assert.Equal(total, received.Select(item => item.EventId).Distinct().Count());
        Assert.Equal(total, received.Count);
    }

    [Fact]
    public async Task broker变慢时在租约到期前交还未发的行_接管副本不会重复发布()
    {
        const int total = 12;
        var schema = await PrepareAsync();
        var queue = await BindQueueAsync();
        await SeedAsync(schema, total);
        // 租约 2 秒、单条确认上限 1 秒，慢副本一次领满 12 条，按每条 300 毫秒要 3.6 秒才发得完。
        var slow = new Counter(TimeSpan.FromMilliseconds(300));
        var fast = new Counter(TimeSpan.Zero);
        using var slowReplica = Replica(schema, "slow", slow, batch: total, lease: TimeSpan.FromSeconds(2), publishTimeout: TimeSpan.FromSeconds(1));
        await slowReplica.StartAsync(Ct);
        await Until(() => slow.Confirmed >= 1, "慢副本尚未开始发布");
        using var fastReplica = Replica(schema, "fast", fast, batch: total);
        await fastReplica.StartAsync(Ct);
        await Until(async () => await PendingAsync(schema) == 0, "Outbox 没有发完");
        await Task.WhenAll(slowReplica.StopAsync(Ct), fastReplica.StopAsync(Ct));
        // 没有余量检查时，快副本会在 2 秒租约到期后接管慢副本手上未发的行，两边各发一次。
        Assert.Equal(total, slow.Confirmed + fast.Confirmed);
        Assert.Empty(slow.Ids.Intersect(fast.Ids));
        var received = await DrainAsync(queue);
        Assert.Equal(total, received.Count);
        Assert.Equal(total, received.Select(item => item.EventId).Distinct().Count());
    }

    [Fact]
    public async Task 信封与行不一致时直接标记失败_broker不可达时按退避重试且不丢消息()
    {
        var schema = await PrepareAsync();
        var queue = await BindQueueAsync();
        await SeedAsync(schema, 1);
        await AdminAsync($"""UPDATE {schema}.outbox_messages SET payload = replace(payload, '"tests.ping.sent.v1"', '"tests.ping.other.v1"');""");
        var counter = new Counter(TimeSpan.Zero);
        using (var replica = Replica(schema, "reject", counter, batch: 10))
        {
            await replica.StartAsync(Ct);
            await Until(async () => await ScalarAsync($"SELECT count(*) FROM {schema}.outbox_messages WHERE status = 2") == 1, "不一致的信封没有标记失败");
            await replica.StopAsync(Ct);
        }
        Assert.Equal(0, counter.Confirmed);
        Assert.Equal(1, await ScalarAsync($"SELECT attempt_count FROM {schema}.outbox_messages"));
        Assert.Empty(await DrainAsync(queue));

        await AdminAsync($"DELETE FROM {schema}.outbox_messages;");
        await SeedAsync(schema, 1);
        var unreachable = new Counter(TimeSpan.Zero);
        using (var replica = Replica(schema, "unreachable", unreachable, batch: 10, amqp: "amqp://qian:wrong@127.0.0.1:1/"))
        {
            await replica.StartAsync(Ct);
            await Until(async () => await ScalarAsync($"SELECT attempt_count FROM {schema}.outbox_messages") >= 1, "broker 不可达时没有记录重试");
            await replica.StopAsync(Ct);
        }
        Assert.Equal(0, await ScalarAsync($"SELECT status FROM {schema}.outbox_messages"));
        Assert.Equal(1, await ScalarAsync($"SELECT count(*) FROM {schema}.outbox_messages WHERE next_attempt_at > now()"));
        await AdminAsync($"UPDATE {schema}.outbox_messages SET next_attempt_at = NULL;");
        var recovered = new Counter(TimeSpan.Zero);
        using (var replica = Replica(schema, "recovered", recovered, batch: 10))
        {
            await replica.StartAsync(Ct);
            await Until(async () => await PendingAsync(schema) == 0, "broker 恢复后没有发布");
            await replica.StopAsync(Ct);
        }
        Assert.Equal(1, recovered.Confirmed);
        Assert.Single(await DrainAsync(queue));
    }

    [Fact]
    public async Task 拓扑_竞争消费为quorum队列_广播为自动删除的经典队列()
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        var shared = await RabbitTopology.DeclareSubscriptionAsync(channel, "qian.sample.crm", "customer-sync", ["crm.customer.created", "crm.customer.*"], Ct);
        var broadcast = await RabbitTopology.DeclareBroadcastAsync(channel, "qian.gateway", "gw-1", ["identity.session_revoked"], Ct);
        Assert.Equal("q.qian.sample.crm.customer-sync", shared);
        Assert.Equal("q.qian.gateway.gw-1", broadcast);
        var listing = await rabbit.Container.ExecAsync(["rabbitmqctl", "list_queues", "-q", "name", "type", "durable", "auto_delete"], Ct);
        Assert.Equal(0, listing.ExitCode);
        var rows = listing.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('\t', StringSplitOptions.TrimEntries)).Where(cells => cells.Length == 4).ToDictionary(cells => cells[0], cells => cells[1..]);
        Assert.Equal(["quorum", "true", "false"], rows[shared]);
        Assert.Equal(["classic", "true", "true"], rows[broadcast]);
        var exchanges = await rabbit.Container.ExecAsync(["rabbitmqctl", "list_exchanges", "-q", "name", "type", "durable"], Ct);
        Assert.Contains("qian.events\ttopic\ttrue", exchanges.Stdout, StringComparison.Ordinal);
        Assert.Contains("qian.dlx\ttopic\ttrue", exchanges.Stdout, StringComparison.Ordinal);
    }

    private IHost Replica(string schema, string name, Counter counter, int batch, string? amqp = null, TimeSpan? lease = null, TimeSpan? publishTimeout = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddKunEventBus(events => events.Register(PingSent.Type, TestEventJson.Default.PingSent));
        builder.Services.AddQianData<RelayContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(schema));
        builder.Services.AddQianEvents(options =>
        {
            options.RelayInstance = name;
            options.BatchSize = batch;
            options.PollInterval = TimeSpan.FromMilliseconds(50);
            options.LeaseDuration = lease ?? TimeSpan.FromSeconds(15);
            options.PublishTimeout = publishTimeout ?? TimeSpan.FromSeconds(5);
            options.RetryBaseDelay = TimeSpan.FromMinutes(1);
        });
        builder.Services.AddQianEventsBroker(_ => amqp ?? rabbit.ConnectionString);
        builder.Services.Replace(ServiceDescriptor.Singleton<IIntegrationEventPublisher>(sp =>
            new CountingPublisher(new QianEnvelopePublisher(sp.GetRequiredService<RabbitMqBroker>()), counter)));
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(20));
        return builder.Build();
    }

    private async Task SeedAsync(string schema, int total)
    {
        var builder = Host.CreateApplicationBuilder();
        var tenant = new AsyncLocalTenantContext();
        builder.Services.AddSingleton<ITenantContext>(tenant);
        builder.Services.AddKunEventBus(events => events.Register(PingSent.Type, TestEventJson.Default.PingSent));
        builder.Services.AddQianData<RelayContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(schema));
        builder.Services.AddQianEvents();
        using var host = builder.Build();
        var data = host.Services.GetRequiredService<IDataService>();
        using (tenant.BeginScope(Tenant))
        using (EventEnvelopeScope.Begin(new EventOrigin("qian.events-tests", EventActor.Service("relay-seeder"))))
        {
            for (var offset = 0; offset < total; offset += 50)
            {
                await using var session = await data.OpenAsync<RelayContext>(Ct);
                for (var index = offset; index < Math.Min(total, offset + 50); index++) session.Outbox.Enqueue(new PingSent(index, "relay"));
                await session.SaveChangesAsync(Ct);
            }
        }
        Assert.Equal(total, await PendingAsync(schema));
    }

    private async Task<string> PrepareAsync()
    {
        // EF 按 DbContext 类型缓存模型（含 schema），同一个上下文只能用一个固定 schema；每个测试重建它。
        const string schema = "plugin_relay";
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddKunEventBus();
        builder.Services.AddQianData<RelayContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(schema));
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        var create = scope.ServiceProvider.GetRequiredService<RelayContext>().Database.GenerateCreateScript();
        await AdminAsync($"DROP SCHEMA IF EXISTS {schema} CASCADE;" + create
            + $"GRANT USAGE ON SCHEMA {schema} TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {schema} TO qian_app;");
        return schema;
    }

    private async Task<string> BindQueueAsync()
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        return await RabbitTopology.DeclareSubscriptionAsync(channel, "qian.events-tests", "relay-" + Guid.NewGuid().ToString("N")[..8], ["tests.ping.sent"], Ct);
    }

    private async Task<List<QianEventEnvelope>> DrainAsync(string queue)
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        var received = new List<QianEventEnvelope>();
        while (await channel.BasicGetAsync(queue, autoAck: true, Ct) is { } message)
        {
            Assert.Equal("tests.ping.sent", message.RoutingKey);
            Assert.Equal(QianEventTopology.Exchange, message.Exchange);
            var envelope = QianEventEnvelope.Parse(Encoding.UTF8.GetString(message.Body.Span));
            Assert.Equal(envelope.EventId.ToString("D"), message.BasicProperties.MessageId);
            Assert.Equal(envelope.Type, message.BasicProperties.Type);
            Assert.Equal(DeliveryModes.Persistent, message.BasicProperties.DeliveryMode);
            received.Add(envelope);
        }
        return received;
    }

    private async Task<long> PendingAsync(string schema) => await ScalarAsync($"SELECT count(*) FROM {schema}.outbox_messages WHERE status = 0");

    private async Task<Guid[]> PublishedIdsAsync(string schema)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand($"SELECT id FROM {schema}.outbox_messages WHERE status = 1 AND published_at IS NOT NULL ORDER BY id", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var ids = new List<Guid>();
        while (await reader.ReadAsync(Ct)) ids.Add(reader.GetGuid(0));
        return [.. ids.Order()];
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task AdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

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

    // 只在内层发布器返回（broker 已确认）后计数；可选延迟让两个副本的发布交错。
    private sealed class CountingPublisher(IIntegrationEventPublisher inner, Counter counter) : IIntegrationEventPublisher
    {
        public async ValueTask PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            if (counter.Delay > TimeSpan.Zero) await Task.Delay(counter.Delay, cancellationToken);
            await inner.PublishAsync(message, cancellationToken);
            counter.Add(message.Id);
        }
    }

    private sealed class Counter(TimeSpan delay)
    {
        private readonly ConcurrentDictionary<Guid, int> _ids = new();
        private int _confirmed;
        public TimeSpan Delay { get; } = delay;
        public int Confirmed => Volatile.Read(ref _confirmed);
        public ICollection<Guid> Ids => _ids.Keys;
        public void Add(Guid id)
        {
            _ids.AddOrUpdate(id, 1, (_, count) => count + 1);
            Interlocked.Increment(ref _confirmed);
        }
    }

    public sealed class RelayContext(DbContextOptions<RelayContext> options, ITenantContext tenant) : QianDbContext(options, tenant);
}
