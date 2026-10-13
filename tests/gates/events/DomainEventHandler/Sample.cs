using Kun.Core.Domain;
using Kun.EventBus.Outbox;
using Kun.Mediator;

internal sealed record ThingCreated(Guid Id);
internal sealed record ThingChanged(Guid Id) : IDomainEvent, INotification { public DateTime OccurredAt { get; } = DateTime.UtcNow; }
// 领域事件处理器随工作单元在同一作用域派发，允许经 Outbox 发出集成事件。
internal sealed class Handler(IOutboxWriter writer) : INotificationHandler<ThingChanged>
{
    public ValueTask Handle(ThingChanged notification, CancellationToken token)
    {
        writer.Enqueue(new ThingCreated(notification.Id));
        return ValueTask.CompletedTask;
    }
}
