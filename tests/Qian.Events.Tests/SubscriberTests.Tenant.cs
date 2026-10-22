using System.Collections.Concurrent;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Tenancy;

namespace Qian.Events.Tests;

// 进入租户作用域（S1-EVT-004）：登记了 ITenantContextPropagator 时经它进入；两条路径进入后都核对当前租户等于信封租户。
public sealed partial class SubscriberTests
{
    [Fact]
    public async Task 登记传播器时经它按信封租户进入_carrier为信封原文_授权租户为信封租户_handler内租户为信封租户()
    {
        var service = Service();
        var probe = new Probe(_ => null);
        using var metrics = new ConsumerMetrics(service + ".work");
        using var host = Host(service, probe, subscriber => subscriber.Subscribe<PingSent, ProbeHandler>("work"),
            services: services => services.AddSingleton<ITenantContextPropagator>(sp => new ScriptedPropagator(sp.GetRequiredService<ITenantContext>())));
        var propagator = (ScriptedPropagator)host.Services.GetRequiredService<ITenantContextPropagator>();
        await StartAsync(host);
        var a = await PublishAsync(TenantA, sequence: 30);
        var b = await PublishAsync(TenantB, sequence: 31);

        await Until(async () => metrics.Count("processed") == 2 && await SettledAsync([QianEventTopology.SubscriptionQueue(service, "work")]), "两条租户事件没有处理完");
        Assert.Equal([(a.EventId, TenantA), (b.EventId, TenantB)], probe.Calls.OrderBy(call => call.Sequence).Select(call => (call.EventId, call.Tenant)));
        // 每条事件都经传播器进入：carrier 是消息体原文（带 event_id），authorizedTenantId 是信封租户；handler 结束后作用域已释放。
        Assert.Equal([(a.EventId, Guid.Parse(TenantA)), (b.EventId, Guid.Parse(TenantB))], propagator.Calls.Distinct().OrderBy(call => call.Authorized));
        Assert.All(propagator.Scopes.Values, scope => Assert.True(scope.Disposed));
        Assert.DoesNotContain(probe.Logs.Entries, entry => entry.EventId == 33);
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task 传播器没有进入作用域或进入了别的租户_进入后核对不一致_按tenant_rejected进死信_不进handler不记幂等()
    {
        var service = Service();
        var probe = new Probe(_ => null);
        using var metrics = new ConsumerMetrics(service + ".work");
        using var host = Host(service, probe, subscriber => subscriber.Subscribe<PingSent, ProbeHandler>("work"),
            services: services => services.AddSingleton<ITenantContextPropagator>(sp => new ScriptedPropagator(sp.GetRequiredService<ITenantContext>())));
        var propagator = (ScriptedPropagator)host.Services.GetRequiredService<ITenantContextPropagator>();
        await StartAsync(host);
        // 两条反例都是生产发布器发出的合法事件：信封、AMQP 头与原文都一致，只有传播器的作用域不对。
        var unscoped = await PublishAsync(TenantA, sequence: 32, note: ScriptedPropagator.NoScope);
        var misplaced = await PublishAsync(TenantA, sequence: 33, note: ScriptedPropagator.WrongTenant);
        var entered = await PublishAsync(TenantB, sequence: 34);
        var deadLetter = QianEventTopology.DeadLetterQueue(service, "work");

        // 结束态：rejected 与 processed 都已计数（确认之后），死信队列恰有两条、主队列已清空，之后不会再有投递。
        await Until(async () => metrics.Count("rejected") == 2 && metrics.Count("processed") == 1 && (await QueuesAsync()).GetValueOrDefault(deadLetter) == 2
            && await SettledAsync([QianEventTopology.SubscriptionQueue(service, "work")]), "两条反例没有按 tenant_rejected 进死信队列");
        var call = Assert.Single(probe.Calls);
        Assert.Equal((entered.EventId, TenantB), (call.EventId, call.Tenant));
        var dead = await DrainAsync(deadLetter);
        Assert.Equal([unscoped.EventId, misplaced.EventId], dead.Select(message => Guid.Parse(message.BasicProperties.MessageId!)).Order());
        Assert.All(dead, message => Assert.Equal(("tenant_rejected", typeof(QianEventsException).FullName), (Header(message, "x-qian-reason"), Header(message, "x-qian-error"))));
        foreach (var id in new[] { unscoped.EventId, misplaced.EventId })
            Assert.False(await Redis().KeyExistsAsync("idemp:" + service + ".work:" + id));
        // 传播器确实被调用了（判定发生在它返回之后）；进入了别的租户的作用域在拒绝前释放。
        Assert.Contains((misplaced.EventId, Guid.Parse(TenantA)), propagator.Calls);
        Assert.True(propagator.Scopes[misplaced.EventId].Disposed);
        Assert.Equal(2, probe.Logs.Entries.Count(entry => entry.EventId == 26 && entry.Level == LogLevel.Error && entry.Message.Contains("tenant_rejected", StringComparison.Ordinal)));
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task 未登记传播器而宿主上下文的BeginScope没有进入作用域_回退分支同样核对_按tenant_rejected进死信()
    {
        var service = Service();
        var probe = new Probe(_ => null);
        using var metrics = new ConsumerMetrics(service + ".work");
        using var host = Host(service, probe, subscriber => subscriber.Subscribe<PingSent, ProbeHandler>("work"),
            services: services => services.AddSingleton<ITenantContext>(new InertTenantContext()));
        await StartAsync(host);
        var envelope = await PublishAsync(TenantA, sequence: 35);
        var deadLetter = QianEventTopology.DeadLetterQueue(service, "work");

        // 结束态：rejected 已计数，死信队列恰有一条、主队列已清空，之后不会再有投递。
        await Until(async () => metrics.Count("rejected") == 1 && (await QueuesAsync()).GetValueOrDefault(deadLetter) == 1
            && await SettledAsync([QianEventTopology.SubscriptionQueue(service, "work")]), "回退分支没有拒绝未进入作用域的消息");
        Assert.Empty(probe.Calls);
        var message = await GetAsync(deadLetter);
        Assert.Equal((envelope.EventId.ToString("D"), "tenant_rejected"), (message.BasicProperties.MessageId, Header(message, "x-qian-reason")));
        Assert.Contains(probe.Logs.Entries, entry => entry.EventId == 33);
        Assert.Empty(await IdempotencyKeysAsync(service + ".work"));
        await host.StopAsync(Ct);
    }

    /// <summary>按载荷 note 决定怎样进入作用域的传播器替身：照常进入、不进入，或进入另一个租户；记录每次调用与返回的作用域是否已释放。</summary>
    private sealed class ScriptedPropagator(ITenantContext tenant) : ITenantContextPropagator
    {
        public const string NoScope = "no-scope";
        public const string WrongTenant = "wrong-tenant";

        public ConcurrentQueue<(Guid EventId, Guid Authorized)> Calls { get; } = new();
        public ConcurrentDictionary<Guid, TrackedScope> Scopes { get; } = new();

        public IDisposable BeginScope(JsonElement carrier, Guid authorizedTenantId)
        {
            var eventId = carrier.GetProperty("event_id").GetGuid();
            Calls.Enqueue((eventId, authorizedTenantId));
            var other = authorizedTenantId == Guid.Parse(TenantA) ? TenantB : TenantA;
            var scope = new TrackedScope(carrier.GetProperty("payload").GetProperty("note").GetString() switch
            {
                NoScope => null,
                WrongTenant => tenant.BeginScope(other),
                _ => tenant.BeginScope(authorizedTenantId.ToString("D")),
            });
            Scopes[eventId] = scope;
            return scope;
        }

        public JsonElement Write(JsonElement carrier) => throw new NotSupportedException();

        public IReadOnlyDictionary<string, string> WriteHeaders() => throw new NotSupportedException();

        public IDisposable BeginScope(IEnumerable<KeyValuePair<string, string>> headers, Guid authorizedTenantId) => throw new NotSupportedException();
    }

    private sealed class TrackedScope(IDisposable? inner) : IDisposable
    {
        private int _disposed;

        public bool Disposed => Volatile.Read(ref _disposed) == 1;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) inner?.Dispose();
        }
    }

    /// <summary>声称多租户、BeginScope 却不进入作用域的宿主上下文（违反 Qian.Tenancy README「必须支持可释放的 BeginScope」）。</summary>
    private sealed class InertTenantContext : ITenantContext
    {
        public string? TenantId => null;

        public bool IsMultiTenant => true;

        public IDisposable BeginScope(string? tenantId) => new TrackedScope(null);
    }
}
