using System.Text.Json;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Audit.Tests;

// S1-AUD-001：审计强制执行点是 base 组合包登记在 command/dispatching 上的 waterfall 监听器（PRD 7.11.1、ADR-020 D20.2），
// 卸载组件时随效果栈按登记的逆序撤销。被测的是真实 AuditPlugin 与真实 PluginLoader；记录器只原样转发上下文调用，
// 并在每次登记后压入一个标记 effect——效果栈后进先出，标记总在它前一项登记被撤销之前执行，标记序列即撤销顺序。
public sealed partial class AuditStoreTests
{
    [Fact]
    public async Task 卸载审计组件时waterfall监听器按登记逆序撤销_先于审计服务释放与错误码撤销_无残留且重装后只有一个监听器()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var catalog = host.Services.GetRequiredService<ErrorCatalog>();
        var registered = new List<string>(); var revoked = new List<string>(); var codesAtListenerRevocation = new List<bool>();
        var recorder = new RecordingPlugin(new AuditPlugin(), registered, revoked,
            label => { if (label == "on:command/dispatching") codesAtListenerRevocation.Add(catalog.Contains(AuditErrors.SchemaInvalid)); });
        await using var runtime = new PluginLoader(host.Services); var observer = new DispatchObserver();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("observer", observer, []),
            Define("audit", recorder, [AuditDefinition.Key, CommandPolicies.AuditKey], new { chain = new { store = "pg" } })], Ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("audit", ScopeKey.Global));
        var service = observer.Context!.Audit();

        // 真实登记顺序：错误码 → 审计服务释放 → 审计服务 → 策略租约释放 → waterfall 监听器（优先级 100） → 策略租约。
        Assert.Equal(["effect#1", "effect#2", "provide:Audit", "effect#3", "on:command/dispatching", "provide:CommandAudit"], registered);
        Assert.Equal(["audit", "observer"], DispatchListeners(observer));
        Assert.True(catalog.Contains(AuditErrors.SchemaInvalid));

        await runtime.UnmountAsync("audit", ScopeKey.Global, Ct);

        // 撤销严格逆序：监听器在策略租约之后、审计服务注册与服务释放之前撤下，错误码最后撤销。
        Assert.Equal(Enumerable.Reverse(registered), revoked);
        Assert.Equal([true], codesAtListenerRevocation);
        Assert.False(catalog.Contains(AuditErrors.SchemaInvalid));
        // 卸载后无残留：链上只剩其它组件的监听器，审计服务与策略租约都不可解析，旧服务句柄已释放。
        Assert.Equal(["observer"], DispatchListeners(observer));
        Assert.Null(observer.Context!.TryInject<IAuditService>(AuditDefinition.Key));
        Assert.Empty(observer.Context!.List(CommandPolicies.AuditKey));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.VerifyAsync(Ct));

        // 重装得到新的单个监听器，而不是在旧登记上叠加。
        await runtime.MountAsync([Define("audit", new AuditPlugin(), [AuditDefinition.Key, CommandPolicies.AuditKey])], Ct);
        Assert.Equal(["audit", "observer"], DispatchListeners(observer));
        Assert.True(catalog.Contains(AuditErrors.SchemaInvalid));
    }

    private static string[] DispatchListeners(DispatchObserver observer) =>
        observer.Context!.List(new ServiceKey(CommandEvents.Dispatching.Name)).Select(registration => registration.OwnerEntryId).Order(StringComparer.Ordinal).ToArray();

    // 链上的另一个组件：只透传，用来证明卸载审计只撤下审计自己的监听器。
    private sealed class DispatchObserver : IQianPlugin
    {
        public IPluginContext? Context { get; private set; }
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            Context = ctx;
            ctx.On(CommandEvents.Dispatching, DispatchMode.Waterfall, (Func<CommandInvocation, Func<ValueTask<object?>>, ValueTask<object?>>)((_, next) => next()));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingPlugin(IQianPlugin inner, List<string> registered, List<string> revoked, Action<string> onRevoke) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => inner.Inject;
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => inner.ApplyAsync(new RecordingContext(ctx, registered, revoked, onRevoke), config, ct);
    }

    // 原样转发，不改变任何解析、登记或分发结果；Effect 包装后记录实际执行，Provide / On / Restrict 之后压入标记。
    private sealed class RecordingContext(IPluginContext inner, List<string> registered, List<string> revoked, Action<string> onRevoke) : IPluginContext
    {
        private int _effects;
        public PluginEntry Entry => inner.Entry;
        public ScopeKey Scope => inner.Scope;
        public IServiceProvider Services => inner.Services;
        public ILogger Logger => inner.Logger;
        public IRegistrationHandle<TService> Provide<TService>(ServiceKey key, TService instance) where TService : class => Mark("provide:" + key.Name, inner.Provide(key, instance));
        public IRegistrationHandle<TService> Provide<TService>(ServiceKey key, string name, TService instance) where TService : class => Mark("provide:" + key.Name + ":" + name, inner.Provide(key, name, instance));
        public TService Inject<TService>(ServiceKey key) where TService : class => inner.Inject<TService>(key);
        public TService Inject<TService>(ServiceKey key, string name) where TService : class => inner.Inject<TService>(key, name);
        public TService? TryInject<TService>(ServiceKey key) where TService : class => inner.TryInject<TService>(key);
        public ValueTask<TService> WhenReadyAsync<TService>(ServiceKey key, CancellationToken ct) where TService : class => inner.WhenReadyAsync<TService>(key, ct);
        public void Effect(IDisposable disposer) { var label = Next(); inner.Effect(() => { Revoked(label); disposer.Dispose(); }); }
        public void Effect(Action dispose) { var label = Next(); inner.Effect(() => { Revoked(label); dispose(); }); }
        public void Effect(Func<ValueTask> disposeAsync) { var label = Next(); inner.Effect(() => { Revoked(label); return disposeAsync(); }); }
        public IDisposable On<TPayload, TResult>(QianEvent<TPayload, TResult> evt, DispatchMode mode, Delegate handler, int priority) => Mark("on:" + evt.Name, inner.On(evt, mode, handler, priority));
        public IDisposable On<TPayload, TResult>(QianEvent<TPayload, IReadOnlyList<TResult>> evt, DispatchMode mode, Delegate handler, int priority) => Mark("on:" + evt.Name, inner.On(evt, mode, handler, priority));
        public ValueTask<TResult> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, TResult> evt, TPayload payload, CancellationToken ct) => inner.DispatchAsync(evt, payload, ct);
        public ValueTask<IReadOnlyList<TResult>> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, IReadOnlyList<TResult>> evt, TPayload payload, CancellationToken ct) => inner.DispatchAsync(evt, payload, ct);
        public ValueTask<TResult> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, TResult> evt, TPayload payload, Func<ValueTask<TResult>> terminal, CancellationToken ct) => inner.DispatchAsync(evt, payload, terminal, ct);
        public IPluginContext Scoped(ScopeKey scope) => inner.Scoped(scope);
        public IDisposable Restrict(ScopeKey scope, Func<Registration, bool> keep) => Mark("restrict:" + scope.Kind, inner.Restrict(scope, keep));
        public IReadOnlyList<Registration> List(ServiceKey key) => inner.List(key);

        private string Next() { var label = "effect#" + ++_effects; registered.Add(label); return label; }
        private T Mark<T>(string label, T registration)
        {
            registered.Add(label);
            inner.Effect(() => Revoked(label));
            return registration;
        }
        private void Revoked(string label) { revoked.Add(label); onRevoke(label); }
    }
}
