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
}

/// <summary>事件目录中的一项：稳定 ID、subject、payload 版本与由 JsonTypeInfo 导出的 payload Schema。</summary>
/// <param name="Type">稳定事件 ID。</param>
/// <param name="Subject">信封 subject，也是路由键。</param>
/// <param name="Version">payload 版本，即事件 ID 末尾的 N。</param>
/// <param name="PayloadSchema">payload 的 JSON Schema（2020-12）。</param>
public sealed record IntegrationEventDescriptor(string Type, string Subject, int Version, JsonElement PayloadSchema);
