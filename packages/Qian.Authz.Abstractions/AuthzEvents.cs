using System.Text.Json.Serialization;
using Kun.EventBus;

namespace Qian.Authz.Abstractions;

/// <summary>授权变更的稳定跨进程事件 ID；首个发布版本之后，破坏性载荷改动必须升版本。</summary>
public static class AuthzEventIds
{
    /// <summary>主体的角色绑定被授予、撤销或改了有效期。</summary>
    public const string GrantChanged = "authz.grant.changed.v1";
    /// <summary>租户内角色被建出或其默认授权随组件策略安装 / 升级而变化。</summary>
    public const string RoleChanged = "authz.role.changed.v1";
    /// <summary>全部 S1 授权事件。</summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { GrantChanged, RoleChanged });
}

/// <summary>授权变更共用字段：租户在信封上，载荷不重复（S1-DEC-020）；AuthzRevision 为变更后的租户授权修订。</summary>
public abstract record AuthzIntegrationEvent(long AuthzRevision)
{
    /// <inheritdoc />
    public sealed override string ToString() => "AuthzIntegrationEvent(redacted)";
}

/// <summary>
/// 主体的角色绑定变化：Change 为 assigned / revoked / updated。身份组件据此递增受影响主体的 identity_revision（PRD 7.4），
/// 实时推送据此刷新客户端；载荷不含权限集合。
/// </summary>
[IntegrationEventId(AuthzEventIds.GrantChanged)]
public sealed record AuthzGrantChangedEvent(long AuthzRevision, string SubjectType, string SubjectId, string RoleCode, string Change)
    : AuthzIntegrationEvent(AuthzRevision);

/// <summary>租户内角色变化：Change 为 created / grants_installed；PluginId / PluginVersion 为触发变化的组件策略，建内置角色时为 null。</summary>
[IntegrationEventId(AuthzEventIds.RoleChanged)]
public sealed record AuthzRoleChangedEvent(long AuthzRevision, string RoleCode, string Change, string? PluginId, string? PluginVersion)
    : AuthzIntegrationEvent(AuthzRevision);

/// <summary>框架事件目录注册；宿主与发布契约共用这一处。</summary>
public static class AuthzEventRegistration
{
    /// <summary>显式传入源生成 JsonTypeInfo，避免反射序列化回退。</summary>
    public static IEventTypeRegistrar RegisterAuthzEvents(this IEventTypeRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        registrar.Register(AuthzEventIds.GrantChanged, AuthzEventJson.Default.AuthzGrantChangedEvent);
        return registrar.Register(AuthzEventIds.RoleChanged, AuthzEventJson.Default.AuthzRoleChangedEvent);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(AuthzGrantChangedEvent))]
[JsonSerializable(typeof(AuthzRoleChangedEvent))]
internal sealed partial class AuthzEventJson : JsonSerializerContext;
