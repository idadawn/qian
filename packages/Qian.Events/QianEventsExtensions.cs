using Kun.EventBus;
using Kun.EventBus.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Events;

/// <summary>事件 Provider 的宿主装配；组件入口是 <see cref="EventsPlugin"/>。</summary>
public static class QianEventsExtensions
{
    /// <summary>
    /// 把数据会话的 Outbox 写入端换成 v1 信封写入端，并登记事件目录。与 AddQianData 的调用顺序无关。
    /// 只写 Outbox 不投递；需要发往 RabbitMQ 时再调用 <see cref="AddQianEventsBroker"/>。
    /// </summary>
    public static IServiceCollection AddQianEvents(this IServiceCollection services, Action<QianEventsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLogging();
        var options = services.AddOptions<QianEventsOptions>();
        if (configure is not null) options.Configure(configure);
        if (services.Any(service => service.ServiceType == typeof(EventsHostMarker))) return services;
        services.AddSingleton(new EventsHostMarker());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(ErrorCatalog.CreateDefault());
        services.TryAddScoped<IntegrationEventBuffer>();
        services.TryAddScoped(sp => new QianEnvelopeOutboxWriter(sp.GetRequiredService<IntegrationEventBuffer>(), sp.GetRequiredService<IEventTypeRegistry>(),
            sp.GetRequiredService<Kun.Core.MultiTenancy.ITenantContext>(), sp.GetRequiredService<TimeProvider>()));
        // AddQianData 以 TryAdd 登记 Kun 累加器；这里替换，两个端口解析到同一个 scoped 信封写入端。
        services.Replace(ServiceDescriptor.Scoped<IIntegrationEventBus>(sp => sp.GetRequiredService<QianEnvelopeOutboxWriter>()));
        services.Replace(ServiceDescriptor.Scoped<IOutboxWriter>(sp => sp.GetRequiredService<QianEnvelopeOutboxWriter>()));
        services.TryAddSingleton<IEventsService>(sp => new EventsService(sp.GetRequiredService<IEventTypeRegistry>()));
        services.AddHostedService<EventsStartupCheck>();
        return services;
    }

    /// <summary>
    /// 登记 RabbitMQ 发布端与 Outbox 投递后台服务。AMQP URI 在首次连接时经 <paramref name="resolveAmqpUri"/> 读取，值不进入组件清单；
    /// <paramref name="brokerReference"/> 是组件配置 <c>broker</c> 应匹配的 secretRef。
    /// </summary>
    public static IServiceCollection AddQianEventsBroker(this IServiceCollection services, Func<IServiceProvider, string> resolveAmqpUri, string? brokerReference = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolveAmqpUri);
        if (brokerReference is not null) EventsConfiguration.ValidateReference(brokerReference);
        if (services.Any(service => service.ServiceType == typeof(EventsBrokerBinding)))
            throw new QianEventsException(EventsErrors.Misconfigured, "同一宿主只能登记一个事件 broker。");
        services.AddQianEvents();
        services.AddSingleton(new EventsBrokerBinding(brokerReference));
        services.AddSingleton(sp => new RabbitMqBroker(() => resolveAmqpUri(sp) is { Length: > 0 } uri ? uri
            : throw new QianEventsException(EventsErrors.Misconfigured, "broker 引用未解析出 AMQP 地址。"), sp.GetRequiredService<ILogger<RabbitMqBroker>>()));
        services.Replace(ServiceDescriptor.Singleton<IIntegrationEventPublisher>(sp => new QianEnvelopePublisher(sp.GetRequiredService<RabbitMqBroker>())));
        services.AddHostedService(sp => new QianOutboxRelay(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<IDataContextCatalog>(),
            sp.GetRequiredService<IIntegrationEventPublisher>(), sp.GetRequiredService<IOptions<QianEventsOptions>>(), sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<QianOutboxRelay>>(), sp));
        return services;
    }
}

internal sealed class EventsHostMarker;

internal sealed record EventsBrokerBinding(string? Reference);

/// <summary>启动时校验装配并登记错误码；事件目录在这里构建一次，不合规的登记让宿主拒绝启动。</summary>
internal sealed class EventsStartupCheck(IServiceProvider services, ErrorCatalog catalog, IOptions<QianEventsOptions> options) : IHostedService, IDisposable
{
    private readonly List<IDisposable> _registrations = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_registrations.Count == 0)
        {
            _registrations.Add(catalog.Register(EventsErrors.Misconfigured));
            _registrations.Add(catalog.Register(EventsErrors.PublishRejected));
        }
        options.Value.Validate();
        if (options.Value.OutboxLock == OutboxLockMode.Redis && services.GetService<StackExchange.Redis.IConnectionMultiplexer>() is null)
            throw new QianEventsException(EventsErrors.Misconfigured, "outbox.lock 为 redis 时宿主必须登记 IConnectionMultiplexer。");
        if (services.GetService<IEventTypeRegistry>() is null)
            throw new QianEventsException(EventsErrors.Misconfigured, "宿主必须以 AddKunEventBus 显式登记事件类型目录。");
        _ = services.GetRequiredService<IEventsService>().Catalog;
        using var scope = services.CreateScope();
        if (scope.ServiceProvider.GetRequiredService<IOutboxWriter>() is not QianEnvelopeOutboxWriter writer ||
            !ReferenceEquals(writer, scope.ServiceProvider.GetRequiredService<IIntegrationEventBus>()))
            throw new QianEventsException(EventsErrors.Misconfigured, "Outbox 写入端被其他装配覆盖，事件不会带上 v1 信封。");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var registration in _registrations) registration.Dispose();
        _registrations.Clear();
    }
}

internal sealed class EventsService(IEventTypeRegistry registry) : IEventsService
{
    private readonly Lazy<IReadOnlyList<IntegrationEventDescriptor>> _catalog = new(() => IntegrationEventSchemas.Describe(registry));

    public IReadOnlyList<IntegrationEventDescriptor> Catalog => _catalog.Value;

    public IntegrationEventDescriptor? Find(string type) => Catalog.FirstOrDefault(item => string.Equals(item.Type, type, StringComparison.Ordinal));
}
