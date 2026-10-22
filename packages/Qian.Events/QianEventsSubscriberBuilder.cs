using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Kun.EventBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Qian.Events;

/// <summary>
/// 订阅登记（PRD 7.10 的 <c>Subscribe&lt;TEvent, THandler&gt;()</c>）：每次登记把一个 Kun handler 挂到一个订阅上。
/// 同一订阅可以覆盖多个事件，但一个事件在同一订阅里只有一个 handler；广播订阅共用实例队列，同一事件在一个服务里只能有一个广播订阅。
/// </summary>
public sealed class QianEventsSubscriberBuilder
{
    private readonly IServiceCollection _services;
    private readonly EventSubscriptionRegistry _registry;

    internal QianEventsSubscriberBuilder(IServiceCollection services, EventSubscriptionRegistry registry)
    {
        _services = services;
        _registry = registry;
    }

    /// <summary>订阅方服务名。</summary>
    public string Service => _registry.Service;

    /// <summary>
    /// 把 <typeparamref name="THandler"/> 登记为订阅 <paramref name="subscription"/> 里 <typeparamref name="TEvent"/> 的处理器，并以 scoped 登记该 handler 类型。
    /// 事件类型须已在 <c>AddKunEventBus</c> 以源生成 JsonTypeInfo 登记；宿主启动时核对，不满足即拒绝启动。
    /// </summary>
    /// <param name="subscription">订阅名，小写短横线标识，决定竞争队列 <c>q.&lt;service&gt;.&lt;subscription&gt;</c>、死信队列与幂等记录的消费者名。</param>
    /// <param name="mode">竞争消费或广播。</param>
    /// <param name="acceptPlatformEvents">是否派发 tenant_id 为 null 的平台级事件；默认拒绝，派发时不开租户作用域。</param>
    public QianEventsSubscriberBuilder Subscribe<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(string subscription,
        EventSubscriptionMode mode = EventSubscriptionMode.Competing, bool acceptPlatformEvents = false)
        where TEvent : class
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        _registry.Add(subscription, mode, acceptPlatformEvents, new EventRoute<TEvent, THandler>());
        _services.TryAddScoped<THandler>();
        return this;
    }
}

/// <summary>宿主登记的全部订阅；多次调用 AddQianEventsSubscriber 共用同一个实例。</summary>
internal sealed class EventSubscriptionRegistry(string service)
{
    private readonly List<Entry> _entries = [];

    public string Service { get; } = service;

    public void Add(string subscription, EventSubscriptionMode mode, bool acceptPlatformEvents, IEventRoute route)
    {
        try { _ = QianEventTopology.DeadLetterQueue(Service, subscription); }
        catch (ArgumentException error) { throw Misconfigured("订阅名不合法：" + error.Message); }
        if (!Enum.IsDefined(mode)) throw Misconfigured("订阅模式只能是 Competing 或 Broadcast。");
        lock (_entries)
        {
            foreach (var entry in _entries)
            {
                if (entry.Subscription == subscription && (entry.Mode != mode || entry.AcceptPlatformEvents != acceptPlatformEvents))
                    throw Misconfigured("订阅 " + subscription + " 的各次登记必须使用相同的模式与平台级开关。");
                if (entry.Subscription == subscription && entry.Route.EventClrType == route.EventClrType)
                    throw Misconfigured("订阅 " + subscription + " 已为事件 " + route.EventClrType.Name + " 登记过 handler。");
                if (mode == EventSubscriptionMode.Broadcast && entry.Mode == EventSubscriptionMode.Broadcast && entry.Route.EventClrType == route.EventClrType)
                    throw Misconfigured("广播订阅共用实例队列，事件 " + route.EventClrType.Name + " 在一个服务里只能有一个广播订阅。");
            }
            _entries.Add(new Entry(subscription, mode, acceptPlatformEvents, route));
        }
    }

    /// <summary>按事件目录解析每条登记；事件未登记或缺少 JsonTypeInfo 即以 EVENTS_MISCONFIGURED 拒绝。</summary>
    public IReadOnlyList<SubscriptionSpec> Bind(IEventTypeRegistry registry)
    {
        Entry[] entries;
        lock (_entries) entries = [.. _entries];
        return entries.GroupBy(entry => entry.Subscription, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal).Select(group =>
        {
            var first = group.First();
            var routes = group.Select(entry => entry.Route.Bind(registry)).ToDictionary(route => route.Type, StringComparer.Ordinal);
            return new SubscriptionSpec(group.Key, first.Mode, first.AcceptPlatformEvents, routes);
        }).ToArray();
    }

    public IReadOnlyList<EventSubscriptionDescriptor> Describe(IEventTypeRegistry registry) => Bind(registry)
        .SelectMany(subscription => subscription.Routes.Values.OrderBy(route => route.Type, StringComparer.Ordinal).Select(route => new EventSubscriptionDescriptor(
            Service, subscription.Name, subscription.Mode, route.Type, route.Subject, route.Version, route.HandlerType.FullName ?? route.HandlerType.Name,
            subscription.AcceptPlatformEvents)))
        .ToArray();

    private static QianEventsException Misconfigured(string reason) => new(EventsErrors.Misconfigured, reason);

    private sealed record Entry(string Subscription, EventSubscriptionMode Mode, bool AcceptPlatformEvents, IEventRoute Route);
}

/// <summary>已按事件目录解析的一个订阅：路由表的键是稳定事件 ID。</summary>
internal sealed record SubscriptionSpec(string Name, EventSubscriptionMode Mode, bool AcceptPlatformEvents, IReadOnlyDictionary<string, BoundEventRoute> Routes);

internal interface IEventRoute
{
    Type EventClrType { get; }

    BoundEventRoute Bind(IEventTypeRegistry registry);
}

/// <summary>一个事件到一个 handler 的派发：载荷按登记的源生成 JsonTypeInfo 从信封 payload 反序列化，从不把整封信封当载荷。</summary>
internal abstract class BoundEventRoute(string type, string subject, int version, Type handlerType)
{
    public string Type { get; } = type;
    public string Subject { get; } = subject;
    public int Version { get; } = version;
    public Type HandlerType { get; } = handlerType;

    /// <summary>载荷不符合登记的 JSON 契约时抛 <see cref="JsonException"/>。</summary>
    public abstract object Deserialize(JsonElement payload);

    public abstract ValueTask InvokeAsync(IServiceProvider services, object payload, MessageContext context, CancellationToken cancellationToken);
}

internal sealed class EventRoute<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler> : IEventRoute
    where TEvent : class
    where THandler : class, IIntegrationEventHandler<TEvent>
{
    public Type EventClrType => typeof(TEvent);

    public BoundEventRoute Bind(IEventTypeRegistry registry)
    {
        var type = registry.GetEventType(typeof(TEvent))
            ?? throw new QianEventsException(EventsErrors.Misconfigured, "订阅的事件 " + typeof(TEvent).Name + " 未在 Kun 事件目录登记。");
        if (registry.GetJsonTypeInfo(typeof(TEvent)) is not JsonTypeInfo<TEvent> typeInfo)
            throw new QianEventsException(EventsErrors.Misconfigured, "订阅的事件 " + type + " 必须以源生成 JsonTypeInfo 登记。");
        if (!IntegrationEventTypes.TryParse(type, out var subject, out var version))
            throw new QianEventsException(EventsErrors.Misconfigured, "事件 ID " + type + " 必须为小写点分的 <subject>.v<N>。");
        return new Bound(type, subject, version, typeInfo);
    }

    private sealed class Bound(string type, string subject, int version, JsonTypeInfo<TEvent> typeInfo) : BoundEventRoute(type, subject, version, typeof(THandler))
    {
        public override object Deserialize(JsonElement payload) => payload.Deserialize(typeInfo) ?? throw new JsonException("payload 不能为 null。");

        public override ValueTask InvokeAsync(IServiceProvider services, object payload, MessageContext context, CancellationToken cancellationToken) =>
            services.GetRequiredService<THandler>().HandleAsync((TEvent)payload, context, cancellationToken);
    }
}

/// <summary>每条消息作用域里的当前投递；<see cref="QianEventDelivery"/> 经它注入。</summary>
internal sealed class EventDeliverySlot
{
    public QianEventDelivery? Current { get; set; }
}
