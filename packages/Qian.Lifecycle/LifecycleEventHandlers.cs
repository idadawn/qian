using Kun.Core.Domain;
using Kun.EventBus;
using Kun.Mediator;

namespace Qian.Lifecycle;

/// <summary>平台宿主接入生命周期集成事件的装配入口。</summary>
public static class LifecycleEventHandlers
{
    /// <summary>
    /// 登记修订激活与租户升级的领域事件处理器：状态写入在保存时经它把 lifecycle.*.v1 放进同一事务的 Outbox。宿主还须以
    /// <see cref="LifecycleEvents.RegisterLifecycleEvents"/> 登记事件类型并装配写入信封的 Outbox 端口（AddQianEvents）；漏装处理器时状态写入在提交前失败。
    /// </summary>
    public static KunMediatorBuilder AddQianLifecycleEvents(this KunMediatorBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddNotificationHandler<RevisionActivated, LifecycleChangeHandler>()
            .AddNotificationHandler<TenantPluginUpgraded, LifecycleChangeHandler>();
    }
}

// 领域事件由状态行登记，Published 由处理器置位，写入端在提交前核对。
internal abstract class LifecycleChange : IDomainEvent, INotification
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    internal bool Published { get; set; }
}

internal sealed class RevisionActivated(string pluginId, int revision, string version, int? previous) : LifecycleChange
{
    internal LifecyclePluginActivated Event { get; } = new(pluginId, revision, version, previous);
}

internal sealed class TenantPluginUpgraded(string pluginId, string fromVersion, string toVersion) : LifecycleChange
{
    internal LifecycleTenantPluginUpgraded Event { get; } = new(pluginId, fromVersion, toVersion);
}

// 在保存的同一 DI scope 内执行，事件总线写入数据会话的缓冲器，随本次保存落库。
internal sealed class LifecycleChangeHandler(IIntegrationEventBus events) : INotificationHandler<RevisionActivated>, INotificationHandler<TenantPluginUpgraded>
{
    public ValueTask Handle(RevisionActivated notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification); cancellationToken.ThrowIfCancellationRequested();
        events.Publish(notification.Event); notification.Published = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask Handle(TenantPluginUpgraded notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification); cancellationToken.ThrowIfCancellationRequested();
        events.Publish(notification.Event); notification.Published = true;
        return ValueTask.CompletedTask;
    }
}
