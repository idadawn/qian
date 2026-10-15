using Kun.Core.Domain;
using Kun.EventBus;
using Kun.Mediator;

namespace Qian.Registry;

/// <summary>平台宿主接入注册表集成事件的装配入口。</summary>
public static class RegistryEventHandlers
{
    /// <summary>
    /// 登记 scope patch 变更的领域事件处理器：公开挂载 / 卸载写入在保存时经它把 registry.scope.*.v1 放进同一事务的 Outbox。
    /// 宿主还须以 <see cref="RegistryEvents.RegisterRegistryEvents"/> 登记事件类型、装配写入信封的 Outbox 端口（AddQianEvents）；
    /// 漏装处理器时公开写入在提交前失败，不会静默丢掉事件。
    /// </summary>
    public static KunMediatorBuilder AddQianRegistryEvents(this KunMediatorBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddNotificationHandler<ScopePatchChanged, ScopePatchChangedHandler>();
    }
}

// 公开写入随 scope patch 行登记的领域事件；Published 由处理器置位，写入端在提交前核对。
internal sealed class ScopePatchChanged(string scope, string entryId, string? plugin, string? version, int revision, bool mounted) : IDomainEvent, INotification
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    internal string Scope { get; } = scope;
    internal string EntryId { get; } = entryId;
    internal string? Plugin { get; } = plugin;
    internal string? Version { get; } = version;
    internal int Revision { get; } = revision;
    internal bool Mounted { get; } = mounted;
    internal bool Published { get; set; }
}

// 领域事件处理器在保存同一 DI scope 内执行，事件总线写入数据会话的缓冲器，随本次保存落库。
internal sealed class ScopePatchChangedHandler(IIntegrationEventBus events) : INotificationHandler<ScopePatchChanged>
{
    public ValueTask Handle(ScopePatchChanged notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        if (notification.Mounted) events.Publish(new RegistryScopeMounted(notification.Scope, notification.EntryId, notification.Plugin, notification.Version, notification.Revision));
        else events.Publish(new RegistryScopeUnmounted(notification.Scope, notification.EntryId, notification.Revision));
        notification.Published = true;
        return ValueTask.CompletedTask;
    }
}
