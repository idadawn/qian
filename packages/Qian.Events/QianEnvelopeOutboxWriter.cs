using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Kun.Core.MultiTenancy;
using Kun.EventBus;
using Kun.EventBus.Outbox;
using Qian.Data;

namespace Qian.Events;

/// <summary>
/// 替换 Kun 默认累加器的 Outbox 写入端：入队时就生成完整 v1 信封并写进 Outbox 行的 payload，
/// 发布器原样发出。来源（producer、actor、因果链）取自 <see cref="EventEnvelopeScope"/>，缺失即拒绝，
/// 这是「集成事件只能在命令 handler 同 scope 发出」的运行时兜底。
/// </summary>
internal sealed class QianEnvelopeOutboxWriter(IntegrationEventBuffer buffer, IEventTypeRegistry registry, ITenantContext tenant, TimeProvider clock)
    : IIntegrationEventBus, IOutboxWriter, IOutboxBufferBinding
{
    public IntegrationEventBuffer Buffer => buffer;

    public void Publish<TEvent>(TEvent @event) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(@event);
        var origin = EventEnvelopeScope.Current
            ?? throw new QianEventsException(EventsErrors.PublishRejected, "集成事件只能在命令 handler 的同一作用域内发出；后台发布方须以服务主体打开 EventEnvelopeScope。");
        var type = registry.GetEventType(typeof(TEvent))
            ?? throw new QianEventsException(EventsErrors.PublishRejected, "事件类型未在 Kun 事件目录登记。");
        if (registry.GetJsonTypeInfo(typeof(TEvent)) is not JsonTypeInfo<TEvent> typeInfo)
            throw new QianEventsException(EventsErrors.PublishRejected, "事件必须以源生成 JsonTypeInfo 登记，payload Schema 才能与发出的 JSON 一致。");
        var subject = IntegrationEventTypes.SubjectOf(type);
        var payload = JsonSerializer.SerializeToElement(@event, typeInfo);
        var occurredAt = clock.GetUtcNow();
        var tenantId = TenantOf(tenant.TenantId);
        var activity = Activity.Current;
        var traceParent = origin.TraceParent ?? (activity is { IdFormat: ActivityIdFormat.W3C } && QianEventEnvelope.IsTraceParent(activity.Id) ? activity.Id : null)
            ?? "00-" + ActivityTraceId.CreateRandom().ToHexString() + "-" + ActivitySpanId.CreateRandom().ToHexString() + "-00";
        var envelope = new QianEventEnvelope
        {
            Version = QianEventEnvelope.CurrentVersion,
            EventId = Guid.CreateVersion7(occurredAt),
            Subject = subject,
            Type = type,
            OccurredAt = occurredAt,
            TenantId = tenantId,
            Producer = origin.Producer,
            CorrelationId = origin.CorrelationId,
            CausationId = origin.CausationId,
            Actor = origin.Actor,
            TraceParent = traceParent,
            Payload = payload,
        };
        buffer.Add(new OutboxMessage(envelope.EventId, type, envelope.ToJson(), tenantId?.ToString("D"), traceParent,
            activity?.TraceStateString, occurredAt));
    }

    public void Enqueue<TEvent>(TEvent @event) where TEvent : class => Publish(@event);

    // Qian 的租户是小写规范 UUID；信封与 Outbox 的路由租户必须是同一个值。
    private static Guid? TenantOf(string? tenantId)
    {
        if (tenantId is null) return null;
        if (!Guid.TryParseExact(tenantId, "D", out var parsed) || parsed == Guid.Empty || parsed.ToString("D") != tenantId)
            throw new QianEventsException(EventsErrors.PublishRejected, "租户标识必须是小写规范 UUID，平台级事件不在租户作用域内发布。");
        return parsed;
    }
}
