using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Json.Schema;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Qian.Data;
using Qian.Events;
using Qian.Testing.Containers;
using RabbitMQ.Client;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    [Fact]
    public async Task 参考应用创建客户后Outbox加一条v1信封_RabbitMQ恰好收到一条()
    {
        await using var rabbit = new RabbitMqFixture();
        await rabbit.InitializeAsync();
        var factory = new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        // 订阅方先声明队列再发布；绑定 subject（路由键），与 qian.events 的拓扑命名一致。
        var queue = QianEventTopology.SubscriptionQueue("qian.sample.crm-tests", "customer-created");
        await channel.ExchangeDeclareAsync(QianEventTopology.Exchange, ExchangeType.Topic, durable: true, cancellationToken: Ct);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: Ct);
        await channel.QueueBindAsync(queue, QianEventTopology.Exchange, QianEventTopology.RoutingKey(CrmEvents.CustomerCreated), cancellationToken: Ct);

        await using var host = await Start(events: rabbit.ConnectionString);
        var data = host.App.Services.GetRequiredService<IDataService>(); var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        Assert.Equal(0, await OutboxCount());
        using var response = await host.Post(A, new CreateCustomerCommand(Guid.CreateVersion7(), "event-customer"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var customer = (await response.Content.ReadFromJsonAsync<CustomerItem>(Ct))!;

        // 业务事务提交时 Outbox 恰好多一条，行内容就是 v1 信封。
        Assert.Equal(1, await OutboxCount());
        string payload;
        using (tenant.BeginScope(A))
        {
            await using var session = await data.OpenAsync<CrmDbContext>(Ct);
            var row = Assert.Single(await session.Context.OutboxMessages.AsNoTracking().ToListAsync(Ct));
            Assert.Equal(CrmEvents.CustomerCreated, row.EventTypeId);
            Assert.Equal(A, row.TenantId);
            payload = row.Payload;
        }
        var envelope = QianEventEnvelope.Parse(payload);
        Assert.Equal("crm.customer.created", envelope.Subject);
        Assert.Equal(Guid.Parse(A), envelope.TenantId);
        Assert.Equal("qian.sample.crm", envelope.Producer);
        Assert.Equal((EventActorKind.Service, Writer), (envelope.Actor.Kind, envelope.Actor.Id));
        Assert.NotNull(envelope.CausationId);
        Assert.Equal(customer.Id, envelope.Payload.GetProperty("customerId").GetGuid());
        using (var document = JsonDocument.Parse(payload))
        {
            // 信封 Schema 经 $ref 引用 s2s-claims 的 Uuid，两份放进同一个注册表。
            var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
            _ = JsonSchema.FromText(File.ReadAllText(Path.Combine(RepositoryRoot(), "contracts/v0/s2s-claims.schema.json")), options);
            var envelopeSchema = JsonSchema.FromText(File.ReadAllText(Path.Combine(RepositoryRoot(), "contracts/v0/event-envelope.schema.json")), options);
            Assert.True(envelopeSchema.Evaluate(document.RootElement, new EvaluationOptions { RequireFormatValidation = true }).IsValid);
        }

        // 后台发布器确认后标记，RabbitMQ 上只有这一条，消息体与 Outbox 行逐字节相同。
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (await Scalar("SELECT count(*) FROM plugin_qian_sample_crm.outbox_messages WHERE status = 1 AND published_at IS NOT NULL") != 1)
        {
            if (DateTimeOffset.UtcNow > deadline) Assert.Fail("发布器没有确认这条事件。\n" + host.FailureEvidence);
            await Task.Delay(50, Ct);
        }
        var delivered = await channel.BasicGetAsync(queue, autoAck: true, Ct);
        Assert.NotNull(delivered);
        Assert.Equal(payload, Encoding.UTF8.GetString(delivered.Body.Span));
        Assert.Equal("crm.customer.created", delivered.RoutingKey);
        Assert.Equal(envelope.EventId.ToString("D"), delivered.BasicProperties.MessageId);
        Assert.Equal("qian.sample.crm", delivered.BasicProperties.AppId);
        Assert.Null(await channel.BasicGetAsync(queue, autoAck: true, Ct));
        Assert.Equal(1, await OutboxCount());
    }

    private async Task<long> OutboxCount() => await Scalar("SELECT count(*) FROM plugin_qian_sample_crm.outbox_messages");

    private async Task<long> Scalar(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);
    }
}
