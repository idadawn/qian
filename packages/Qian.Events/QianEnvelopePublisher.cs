using System.Text;
using Kun.EventBus;
using Kun.EventBus.Outbox;
using RabbitMQ.Client;

namespace Qian.Events;

/// <summary>
/// Kun 发布端口的 Qian 实现：消息体就是 Outbox 行里的 v1 信封，路由键为信封 subject，
/// 等到 broker 确认才返回。行与信封不一致时以 <see cref="EventsErrors.PublishRejected"/> 拒绝，重试也不会变好。
/// </summary>
internal sealed class QianEnvelopePublisher(RabbitMqBroker broker) : IIntegrationEventPublisher
{
    public async ValueTask PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var envelope = QianEventEnvelope.Parse(message.Payload);
        if (envelope.EventId != message.Id || envelope.Type != message.EventTypeId || envelope.TenantId?.ToString("D") != message.TenantId)
            throw new QianEventsException(EventsErrors.PublishRejected, "Outbox 行与信封的 event_id、type 或 tenant_id 不一致。");
        var headers = new Dictionary<string, object?> { ["traceparent"] = envelope.TraceParent };
        if (message.TraceState is { Length: > 0 }) headers["tracestate"] = message.TraceState;
        if (envelope.TenantId is { } tenant) headers["tenant_id"] = tenant.ToString("D");
        var properties = new BasicProperties
        {
            MessageId = envelope.EventId.ToString("D"),
            Type = envelope.Type,
            AppId = envelope.Producer,
            CorrelationId = envelope.CorrelationId?.ToString("D"),
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(envelope.OccurredAt.ToUnixTimeSeconds()),
            Headers = headers,
        };
        await broker.PublishAsync(envelope.Subject, properties, Encoding.UTF8.GetBytes(message.Payload), cancellationToken).ConfigureAwait(false);
    }
}
