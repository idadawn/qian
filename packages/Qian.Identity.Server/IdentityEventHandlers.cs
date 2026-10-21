using Kun.Core.Domain;
using Kun.EventBus;
using Kun.EventBus.DependencyInjection;
using Kun.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Tenancy;

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

    /// <summary>
    /// 会话中心订阅租户事件的处理器：<c>tenant.suspended.v1</c> 与 <c>tenant.expired.v1</c> 到达后，该租户的全部登录会话立即吊销，
    /// refresh 随之被拒。把返回的构建器交给宿主的事件总线配置（<c>AddKunEventBus(events =&gt; events.AddQianIdentitySessionHandlers())</c>），
    /// 订阅投递由消费端（S1-EVT-002）负责。即使事件丢失或迟到，会话中心的后台对账也会在 30 秒内按目录状态吊销。
    /// </summary>
    public static KunEventBusBuilder AddQianIdentitySessionHandlers(this KunEventBusBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddHandler<TenantSuspended, TenantSuspendedSessionHandler>();
        builder.AddHandler<TenantExpired, TenantExpiredSessionHandler>();
        return builder;
    }
}

/// <summary>租户被冻结：吊销该租户的全部登录会话。</summary>
public sealed class TenantSuspendedSessionHandler(IServiceProvider services) : IIntegrationEventHandler<TenantSuspended>
{
    /// <inheritdoc />
    public async ValueTask HandleAsync(TenantSuspended @event, MessageContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (services.GetRequiredService<IdentityAccountGate>().Service is { } accounts)
            await accounts.RevokeTenantSessionsAsync(@event.Id, "tenant_suspended", cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>租户已到期（进入宽限）：吊销该租户的全部登录会话。</summary>
public sealed class TenantExpiredSessionHandler(IServiceProvider services) : IIntegrationEventHandler<TenantExpired>
{
    /// <inheritdoc />
    public async ValueTask HandleAsync(TenantExpired @event, MessageContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (services.GetRequiredService<IdentityAccountGate>().Service is { } accounts)
            await accounts.RevokeTenantSessionsAsync(@event.Id, "tenant_expired", cancellationToken).ConfigureAwait(false);
    }
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
