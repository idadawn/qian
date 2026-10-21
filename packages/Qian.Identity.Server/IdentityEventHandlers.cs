using Kun.Core.Domain;
using Kun.EventBus;
using Kun.Mediator;
using Qian.Data;
using Qian.Identity.Abstractions;

namespace Qian.Identity.Server;

/// <summary>宿主的同事务身份事件接线。</summary>
public static class IdentityEventHandlers
{
    /// <summary>
    /// 与 <c>RegisterIdentityEvents</c>、<c>AddQianEvents</c> 共用；漏装时保存回滚，不会静默丢事件。
    /// 登录、锁定、改密事件由聚合根的领域通知在同一数据会话内转成集成事件，随业务数据一起落入 Outbox。
    /// </summary>
    public static KunMediatorBuilder AddQianIdentityEvents(this KunMediatorBuilder builder) =>
        builder.AddNotificationHandler<IdentityChange, IdentityChangeHandler>();
}

internal sealed class IdentityChange(IdentityIntegrationEvent payload) : IDomainEvent, INotification
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    internal IdentityIntegrationEvent Payload { get; } = payload;
    internal bool Published { get; set; }

    internal static async Task Save(IDataSession<IdentityDbContext> session, IReadOnlyCollection<IdentityChange> changes, CancellationToken ct)
    {
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        if (changes.Any(change => !change.Published)) throw new InvalidOperationException("身份事件未进入 Outbox：宿主须登记 AddQianIdentityEvents。");
    }
}

internal sealed class IdentityChangeHandler(IIntegrationEventBus events) : INotificationHandler<IdentityChange>
{
    public ValueTask Handle(IdentityChange notification, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (notification.Payload)
        {
            case IdentityLoginEvent login: events.Publish(login); break;
            case IdentityLogoutEvent logout: events.Publish(logout); break;
            case IdentityPasswordChangedEvent changed: events.Publish(changed); break;
            case IdentityLockedEvent locked: events.Publish(locked); break;
            case IdentitySessionRevokedEvent revoked: events.Publish(revoked); break;
            default: throw new InvalidOperationException("未登记的身份集成事件。");
        }
        notification.Published = true;
        return ValueTask.CompletedTask;
    }
}
