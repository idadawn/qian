using Kun.Core.Domain;
using Kun.EventBus;
using Kun.Mediator;
using Qian.Authz.Abstractions;

namespace Qian.Authz;

/// <summary>宿主的同事务授权事件接线：角色、绑定与默认授权的每次写入在同一工作单元内经领域事件进入 Outbox（ADR-008 D8.2）。</summary>
public static class AuthzEventHandlers
{
    /// <summary>与 <see cref="AuthzEventRegistration.RegisterAuthzEvents"/>、AddQianEvents 共用；漏装时写入回滚，不能静默丢事件。</summary>
    public static KunMediatorBuilder AddQianAuthzEvents(this KunMediatorBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddNotificationHandler<AuthzChange, AuthzChangeHandler>();
    }
}

// 一次授权写入里的一项变更；修订号在写入递增后填入，由租户授权修订行作为聚合根发出。
internal sealed class AuthzChange : IDomainEvent, INotification
{
    private AuthzChange(string subject, string? subjectType, string? subjectId, string roleCode, string change, string? pluginId, string? pluginVersion)
    { Subject = subject; SubjectType = subjectType; SubjectId = subjectId; RoleCode = roleCode; Change = change; PluginId = pluginId; PluginVersion = pluginVersion; }
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    internal string Subject { get; }
    internal string? SubjectType { get; }
    internal string? SubjectId { get; }
    internal string RoleCode { get; }
    internal string Change { get; }
    internal string? PluginId { get; }
    internal string? PluginVersion { get; }
    internal long AuthzRevision { get; set; }
    internal bool Published { get; set; }
    internal static AuthzChange Grant(string subjectType, string subjectId, string roleCode, string change) => new("grant", subjectType, subjectId, roleCode, change, null, null);
    internal static AuthzChange Role(string roleCode, string change, string? pluginId, string? pluginVersion) => new("role", null, null, roleCode, change, pluginId, pluginVersion);
}

internal sealed class AuthzChangeHandler(IIntegrationEventBus events) : INotificationHandler<AuthzChange>
{
    public ValueTask Handle(AuthzChange notification, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (notification.Subject)
        {
            case "grant": events.Publish(new AuthzGrantChangedEvent(notification.AuthzRevision, notification.SubjectType!, notification.SubjectId!, notification.RoleCode, notification.Change)); break;
            case "role": events.Publish(new AuthzRoleChangedEvent(notification.AuthzRevision, notification.RoleCode, notification.Change, notification.PluginId, notification.PluginVersion)); break;
            default: throw new InvalidOperationException("未知授权集成事件。");
        }
        notification.Published = true; return ValueTask.CompletedTask;
    }
}
