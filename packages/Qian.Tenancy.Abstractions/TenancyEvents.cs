using System.Text.Json.Serialization;
using Kun.EventBus;

namespace Qian.Tenancy;

/// <summary>主档创建及状态变更的同事务集成事件。</summary>
public static class TenancyEvents
{
    /// <summary>主档已创建。</summary>
    public const string Created = "tenant.created.v1";
    /// <summary>租户已激活。</summary>
    public const string Activated = "tenant.activated.v1";
    /// <summary>租户已冻结。</summary>
    public const string Suspended = "tenant.suspended.v1";
    /// <summary>租户已恢复。</summary>
    public const string Resumed = "tenant.resumed.v1";
    /// <summary>租户已到期进入宽限。</summary>
    public const string Expired = "tenant.expired.v1";
    /// <summary>宿主与发布契约共用的唯一事件类型登记。</summary>
    public static IEventTypeRegistrar RegisterTenancyEvents(this IEventTypeRegistrar registrar) => registrar
        .Register(Created, TenancyEventJson.Default.TenantCreated).Register(Activated, TenancyEventJson.Default.TenantActivated)
        .Register(Suspended, TenancyEventJson.Default.TenantSuspended).Register(Resumed, TenancyEventJson.Default.TenantResumed)
        .Register(Expired, TenancyEventJson.Default.TenantExpired);
}
/// <summary>新建的 pending 主档；不是 Membership 或可登录证明。</summary>
[IntegrationEventId(TenancyEvents.Created)] public sealed record TenantCreated(Guid Id, string Code);
/// <summary>激活后的状态与有效期。</summary>
[IntegrationEventId(TenancyEvents.Activated)] public sealed record TenantActivated(Guid Id, TenantStatus Status, DateTimeOffset? ValidUntil);
/// <summary>冻结前状态，供下游吊销会话。</summary>
[IntegrationEventId(TenancyEvents.Suspended)] public sealed record TenantSuspended(Guid Id, TenantStatus Previous);
/// <summary>恢复后的状态。</summary>
[IntegrationEventId(TenancyEvents.Resumed)] public sealed record TenantResumed(Guid Id, TenantStatus Status);
/// <summary>已到期进入宽限；下游登录与会话组件订阅此事件。</summary>
[IntegrationEventId(TenancyEvents.Expired)] public sealed record TenantExpired(Guid Id, DateTimeOffset ValidUntil);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TenantCreated))][JsonSerializable(typeof(TenantActivated))][JsonSerializable(typeof(TenantSuspended))]
[JsonSerializable(typeof(TenantResumed))][JsonSerializable(typeof(TenantExpired))]
internal sealed partial class TenancyEventJson : JsonSerializerContext;
