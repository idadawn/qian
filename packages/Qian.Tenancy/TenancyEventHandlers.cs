using Kun.Core.Domain;
using Kun.EventBus;
using Kun.Mediator;
using Qian.Data;

namespace Qian.Tenancy;

/// <summary>宿主的同事务租户事件接线。</summary>
public static class TenancyEventHandlers
{
    /// <summary>与 RegisterTenancyEvents、AddQianEvents 共用；漏装时写入回滚，不能静默丢事件。</summary>
    public static KunMediatorBuilder AddQianTenancyEvents(this KunMediatorBuilder builder) => builder.AddNotificationHandler<TenantChange, TenantChangeHandler>();
}
internal sealed class TenantChange(string kind, TenantRow row, TenantStatus? previous = null) : IDomainEvent, INotification
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    internal string Kind { get; } = kind;
    internal TenantRow Row { get; } = row;
    internal TenantStatus? Previous { get; } = previous;
    internal bool Published { get; set; }
    internal static async Task Save(IDataSession<TenancyDbContext> session, TenantChange change, CancellationToken ct)
    {
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        if (!change.Published) throw new InvalidOperationException("租户事件未进入 Outbox：宿主须登记 AddQianTenancyEvents。");
    }
}
internal sealed class TenantChangeHandler(IIntegrationEventBus events) : INotificationHandler<TenantChange>
{
    public ValueTask Handle(TenantChange notification, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var row = notification.Row;
        switch (notification.Kind)
        {
            case TenancyEvents.Created: events.Publish(new TenantCreated(row.Id, row.Code)); break;
            case TenancyEvents.Activated: events.Publish(new TenantActivated(row.Id, PostgresTenantDirectory.Describe(row).Status, row.ValidUntil)); break;
            case TenancyEvents.Suspended: events.Publish(new TenantSuspended(row.Id, notification.Previous!.Value)); break;
            case TenancyEvents.Resumed: events.Publish(new TenantResumed(row.Id, PostgresTenantDirectory.Describe(row).Status)); break;
            case TenancyEvents.Expired: events.Publish(new TenantExpired(row.Id, row.ValidUntil!.Value)); break;
            default: throw new InvalidOperationException("未知租户集成事件。");
        }
        notification.Published = true; return ValueTask.CompletedTask;
    }
}
