using Kun.Core.MultiTenancy;
using Kun.EventBus;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qian.Events.Tests;

/// <summary>消费端装配：缺 broker、幂等存储或事件登记时宿主拒绝启动；订阅登记可枚举；队列命名与保留后缀。</summary>
public sealed class SubscriberConfigurationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 缺broker或幂等存储或订阅的事件未登记时_宿主以EVENTS_MISCONFIGURED拒绝启动()
    {
        using (var noBroker = Host(broker: false, register: true))
            Assert.Contains("AddQianEventsBroker", await RefusedAsync(noBroker), StringComparison.Ordinal);
        using (var noStore = Host(broker: true, register: true))
            Assert.Contains("IConnectionMultiplexer", await RefusedAsync(noStore), StringComparison.Ordinal);
        using (var unregistered = Host(broker: true, register: false, store: true))
            Assert.Contains("未在 Kun 事件目录登记", await RefusedAsync(unregistered), StringComparison.Ordinal);
    }

    [Fact]
    public void 订阅登记的冲突在登记时拒绝_同一宿主只能有一个服务名()
    {
        var services = new ServiceCollection();
        Assert.Equal(EventsErrors.Misconfigured, Assert.Throws<QianEventsException>(() => services.AddQianEventsSubscriber("qian.tests", subscriber => subscriber
            .Subscribe<PingSent, FirstHandler>("work").Subscribe<PingSent, SecondHandler>("work"))).Error.Code);
        Assert.Throws<QianEventsException>(() => new ServiceCollection().AddQianEventsSubscriber("qian.tests", subscriber => subscriber
            .Subscribe<PingSent, FirstHandler>("one", EventSubscriptionMode.Broadcast).Subscribe<PingSent, SecondHandler>("two", EventSubscriptionMode.Broadcast)));
        Assert.Throws<QianEventsException>(() => new ServiceCollection().AddQianEventsSubscriber("qian.tests", subscriber => subscriber
            .Subscribe<PingSent, FirstHandler>("work").Subscribe<OtherSent, OtherHandler>("work", EventSubscriptionMode.Broadcast)));
        Assert.Throws<QianEventsException>(() => new ServiceCollection().AddQianEventsSubscriber("qian.tests", subscriber => subscriber
            .Subscribe<PingSent, FirstHandler>("work-dlq")));
        Assert.Throws<QianEventsException>(() => new ServiceCollection().AddQianEventsSubscriber("Qian.Tests", subscriber => subscriber.Subscribe<PingSent, FirstHandler>("work")));
        var shared = new ServiceCollection();
        shared.AddQianEventsSubscriber("qian.tests", subscriber => subscriber.Subscribe<PingSent, FirstHandler>("work"));
        Assert.Throws<QianEventsException>(() => shared.AddQianEventsSubscriber("qian.other", subscriber => subscriber.Subscribe<OtherSent, OtherHandler>("other")));
    }

    [Fact]
    public void 订阅目录与事件目录按事件ID对齐_投递上下文只在订阅器作用域内可解析()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        services.AddKunEventBus(events => events.Register(PingSent.Type, TestEventJson.Default.PingSent).Register(OtherSent.Type, OtherJson.Default.OtherSent));
        services.AddQianEventsSubscriber("qian.tests", subscriber => subscriber.Subscribe<PingSent, FirstHandler>("work"));
        services.AddQianEventsSubscriber("qian.tests", subscriber => subscriber
            .Subscribe<OtherSent, OtherHandler>("work")
            .Subscribe<PingSent, SecondHandler>("notice", EventSubscriptionMode.Broadcast, acceptPlatformEvents: true));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var events = provider.GetRequiredService<IEventsService>();
        Assert.Equal(
        [
            new EventSubscriptionDescriptor("qian.tests", "notice", EventSubscriptionMode.Broadcast, PingSent.Type, "tests.ping.sent", 1, typeof(SecondHandler).FullName!, true),
            new EventSubscriptionDescriptor("qian.tests", "work", EventSubscriptionMode.Competing, OtherSent.Type, "tests.other.sent", 2, typeof(OtherHandler).FullName!, false),
            new EventSubscriptionDescriptor("qian.tests", "work", EventSubscriptionMode.Competing, PingSent.Type, "tests.ping.sent", 1, typeof(FirstHandler).FullName!, false),
        ], events.Subscriptions);
        Assert.All(events.Subscriptions, subscription => Assert.NotNull(events.Find(subscription.Type)));
        using var scope = provider.CreateScope();
        Assert.Equal(EventsErrors.Misconfigured, Assert.Throws<QianEventsException>(() => scope.ServiceProvider.GetRequiredService<QianEventDelivery>()).Error.Code);
        Assert.Single(provider.GetServices<IHostedService>().OfType<QianEventSubscriber>());
    }

    [Fact]
    public void 死信与重试队列命名_保留后缀不能用作订阅名或实例名_默认实例名规范化()
    {
        Assert.Equal("q.qian.sample.crm.customer-created-dlq", QianEventTopology.DeadLetterQueue("qian.sample.crm", "customer-created"));
        Assert.Equal("q.qian.sample.crm.customer-created-retry-3-4000", QianEventTopology.RetryQueue("qian.sample.crm", "customer-created", 3, TimeSpan.FromSeconds(4)));
        Assert.Equal("q.qian.gateway.gw-1-retry-1-250", QianEventTopology.RetryQueue("qian.gateway", "gw-1", 1, TimeSpan.FromMilliseconds(250)));
        foreach (var name in new[] { "orders-dlq", "orders-retry-1-1000", "Orders", "orders.sync" })
        {
            Assert.Throws<ArgumentException>(() => QianEventTopology.SubscriptionQueue("qian.tests", name));
            Assert.Throws<ArgumentException>(() => QianEventTopology.BroadcastQueue("qian.tests", name));
            Assert.Throws<ArgumentException>(() => QianEventTopology.DeadLetterQueue("qian.tests", name));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => QianEventTopology.RetryQueue("qian.tests", "orders", 0, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => QianEventTopology.RetryQueue("qian.tests", "orders", 1, TimeSpan.Zero));
        Assert.Equal("dawns-macbook-pro-local-4242", QianEventsSubscriberOptions.Normalize("Dawns-MacBook-Pro.local-4242"));
        Assert.Equal("i-7f3a-k8s-12", QianEventsSubscriberOptions.Normalize("7F3A_K8S-12"));
        Assert.Equal("node-retry-1-12-i", QianEventsSubscriberOptions.Normalize("node-retry-1-12"));
        Assert.Equal("q.qian.tests." + QianEventsSubscriberOptions.DefaultInstance(), QianEventTopology.BroadcastQueue("qian.tests", QianEventsSubscriberOptions.DefaultInstance()));
        var options = new QianEventsSubscriberOptions();
        Assert.Equal((5, TimeSpan.FromSeconds(1), TimeSpan.FromDays(7)), (options.MaxRetries, options.RetryBaseDelay, options.IdempotencyTtl));
        // 第 n 次重试前退避 RetryBaseDelay × 2^(n-1)，封顶一分钟。
        Assert.Equal([1d, 2d, 4d, 8d, 16d, 32d, 60d, 60d], Enumerable.Range(1, 8).Select(attempt => options.RetryDelay(attempt).TotalSeconds));
        options.MaxRetries = -1;
        Assert.Throws<QianEventsException>(() => options.Validate("qian.tests"));
    }

    private static IHost Host(bool broker, bool register, bool store = false)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddKunEventBus(events =>
        {
            if (register) events.Register(PingSent.Type, TestEventJson.Default.PingSent);
            if (store) events.UseInMemoryIdempotency();
        });
        if (broker) builder.Services.AddQianEventsBroker(_ => "amqp://qian:unused@127.0.0.1:1/");
        builder.Services.AddQianEventsSubscriber("qian.tests", subscriber => subscriber.Subscribe<PingSent, FirstHandler>("work"));
        return builder.Build();
    }

    private static async Task<string> RefusedAsync(IHost host)
    {
        var error = await Assert.ThrowsAsync<QianEventsException>(() => host.StartAsync(Ct));
        Assert.Equal(EventsErrors.Misconfigured, error.Error.Code);
        return error.Message;
    }

    internal sealed class FirstHandler : IIntegrationEventHandler<PingSent>
    {
        public ValueTask HandleAsync(PingSent @event, MessageContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    internal sealed class SecondHandler : IIntegrationEventHandler<PingSent>
    {
        public ValueTask HandleAsync(PingSent @event, MessageContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    internal sealed class OtherHandler : IIntegrationEventHandler<OtherSent>
    {
        public ValueTask HandleAsync(OtherSent @event, MessageContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}

[IntegrationEventId(Type)]
internal sealed record OtherSent(int Id)
{
    public const string Type = "tests.other.sent.v2";
}

[System.Text.Json.Serialization.JsonSerializable(typeof(OtherSent))]
internal sealed partial class OtherJson : System.Text.Json.Serialization.JsonSerializerContext;
