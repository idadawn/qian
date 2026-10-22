using System.Text.Json;
using Qian.Runtime;

namespace Qian.Events;

/// <summary>事件 seam 的稳定服务键；业务组件只依赖 Definition。</summary>
public static class EventsDefinition
{
    /// <summary>当前 scope 可见的事件 Provider。</summary>
    public static readonly ServiceKey Key = new("Events");

    /// <summary>解析已挂载的事件 Provider。</summary>
    public static IEventsService Events(this IPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Inject<IEventsService>(Key);
    }
}

/// <summary>
/// 事件 Provider 的只读视图。发布不经过这里：命令 handler 通过同一数据会话的 Outbox 写入端发布，
/// 信封由 Provider 在入队时按 <see cref="EventEnvelopeScope"/> 生成。
/// </summary>
public interface IEventsService
{
    /// <summary>宿主登记的全部集成事件，按事件 ID 排序。</summary>
    IReadOnlyList<IntegrationEventDescriptor> Catalog { get; }

    /// <summary>按事件 ID 查找；未登记时返回 null。</summary>
    IntegrationEventDescriptor? Find(string type);

    /// <summary>宿主订阅器登记的全部订阅，每个（订阅, 事件 ID）一项，按订阅名与事件 ID 排序；宿主没有订阅器时为空。</summary>
    IReadOnlyList<EventSubscriptionDescriptor> Subscriptions { get; }
}

/// <summary>事件目录中的一项：稳定 ID、subject、payload 版本与由 JsonTypeInfo 导出的 payload Schema。</summary>
/// <param name="Type">稳定事件 ID。</param>
/// <param name="Subject">信封 subject，也是路由键。</param>
/// <param name="Version">payload 版本，即事件 ID 末尾的 N。</param>
/// <param name="PayloadSchema">payload 的 JSON Schema（2020-12）。</param>
public sealed record IntegrationEventDescriptor(string Type, string Subject, int Version, JsonElement PayloadSchema);

/// <summary>订阅模式（PRD 7.10、ADR-008 D8.9）。</summary>
public enum EventSubscriptionMode
{
    /// <summary>竞争消费：同一服务的全部实例共享 <c>q.&lt;service&gt;.&lt;subscription&gt;</c>，每条消息只由其中一个实例处理。</summary>
    Competing,
    /// <summary>广播：每个实例一个 <c>q.&lt;service&gt;.&lt;instance&gt;</c>，每个在线实例各处理一次；实例下线后队列删除。</summary>
    Broadcast
}

/// <summary>订阅目录中的一项，与 <see cref="IntegrationEventDescriptor"/> 按事件 ID 对齐，是事件目录「订阅者」的来源。</summary>
/// <param name="Service">订阅方服务名，队列名的第二段。</param>
/// <param name="Subscription">订阅名；同一订阅可以覆盖多个事件。</param>
/// <param name="Mode">竞争消费或广播。</param>
/// <param name="Type">订阅的稳定事件 ID。</param>
/// <param name="Subject">信封 subject，也是队列的绑定键。</param>
/// <param name="Version">payload 版本。</param>
/// <param name="Handler">处理该事件的 handler 类型全名。</param>
/// <param name="AcceptPlatformEvents">是否派发 tenant_id 为 null 的平台级事件；为 false 时这类消息直接拒绝。</param>
public sealed record EventSubscriptionDescriptor(string Service, string Subscription, EventSubscriptionMode Mode, string Type, string Subject, int Version,
    string Handler, bool AcceptPlatformEvents);
