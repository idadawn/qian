using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Qian.Runtime.Tests;

public sealed class LifecycleTests
{
    [Fact]
    public async Task Async_Apply_does_not_publish_partial_services_and_cancellation_rolls_back_the_batch()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        await loader.MountAsync([Define("observer", _ => { })], TestContext.Current.CancellationToken);
        var observer = loader.GetContext("observer", ScopeKey.Global);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var disposed = 0;
        var plugin = new AsyncPlugin(async (ctx, ct) =>
        {
            ctx.Provide(new("X"), "partial");
            ctx.Effect(() => disposed++);
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        });
        var definition = new PluginDefinition(new PluginEntry("async", "async", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), plugin, [new("X")]);
        var mounting = loader.MountAsync([definition, Define("dependent", _ => { }, inject: [new("X")])], cancelled.Token).AsTask();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Null(observer.TryInject<string>(new("X")));
        Assert.Equal(PluginState.Pending, loader.GetState("dependent", ScopeKey.Global));
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mounting);
        await loader.WhenIdleAsync(TestContext.Current.CancellationToken);
        Assert.Null(observer.TryInject<string>(new("X")));
        Assert.Equal(1, disposed);
        await loader.MountAsync([Define("async", ctx => ctx.Provide(new("X"), "recovered"), provides: [new("X")])], TestContext.Current.CancellationToken);
        Assert.Equal("recovered", observer.Inject<string>(new("X")));
    }

    [Fact]
    public async Task Typed_config_uses_camel_case_and_invalid_config_never_enters_Apply()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        var plugin = new ConfigPlugin();
        var entry = new PluginEntry("config", "config", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { message = "hello" }));
        await loader.MountAsync([new PluginDefinition(entry, plugin, [new("Message")])], TestContext.Current.CancellationToken);
        Assert.Equal("hello", loader.GetContext("config", ScopeKey.Global).Inject<string>(new("Message")));
        await loader.ReloadAsync(new PluginDefinition(entry with { Config = JsonSerializer.SerializeToElement(new { message = 42 }) }, plugin, [new("Message")]), TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Failed, loader.GetState("config", ScopeKey.Global));
        Assert.Equal(1, plugin.Applies);
    }
    [Fact]
    public async Task Disposer_is_idempotent_and_lost_key_restarts_the_consumer()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        IDisposable registration = null!;
        var starts = 0;
        var stops = 0;
        await loader.MountAsync([
            Define("provider", ctx => registration = ctx.Provide(new("X"), "first"), provides: [new("X")]),
            Define("consumer", ctx => { starts++; ctx.Effect(() => stops++); }, inject: [new("X")])], TestContext.Current.CancellationToken);
        var oldContext = loader.GetContext("consumer", ScopeKey.Global);
        registration.Dispose();
        registration.Dispose();
        Assert.Equal(PluginState.Pending, loader.GetState("consumer", ScopeKey.Global));
        await loader.WhenIdleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, stops);
        Assert.Throws<ObjectDisposedException>(() => oldContext.Provide(new("leak"), "bad"));
        loader.GetContext("provider", ScopeKey.Global).Provide(new("X"), "second");
        await loader.WhenIdleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, starts);
        Assert.Equal(PluginState.Ready, loader.GetState("consumer", ScopeKey.Global));
    }

    [Fact]
    public async Task WhenReady_waits_for_successful_Apply_and_supports_cancellation()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        await loader.MountAsync([Define("observer", _ => { })], TestContext.Current.CancellationToken);
        var context = loader.GetContext("observer", ScopeKey.Global);
        var ready = context.WhenReadyAsync<string>(new("X"), TestContext.Current.CancellationToken).AsTask();
        Assert.False(ready.IsCompleted);
        await loader.MountAsync([Define("provider", ctx => ctx.Provide(new("X"), "value"), provides: [new("X")])], TestContext.Current.CancellationToken);
        Assert.Equal("value", await ready);
        using var cancellation = new CancellationTokenSource();
        var cancelled = context.WhenReadyAsync<string>(new("missing"), cancellation.Token).AsTask();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
    }

    [Fact]
    public async Task Duplicate_registration_rolls_back_only_the_failing_entry()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        await loader.MountAsync([
            Define("first", ctx => ctx.Provide(new("X"), "original"), provides: [new("X")]),
            Define("second", ctx => { ctx.Provide(new("Y"), "rollback"); ctx.Provide(new("X"), "duplicate"); }, provides: [new("X")])], TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Failed, loader.GetState("second", ScopeKey.Global));
        var error = Assert.IsType<Errors.QianErrorException>(loader.GetFailure("second", ScopeKey.Global));
        Assert.Equal("first", error.Error.Args["owner"]);
        var context = loader.GetContext("first", ScopeKey.Global);
        Assert.Equal("original", context.Inject<string>(new("X")));
        Assert.Null(context.TryInject<string>(new("Y")));
    }

    [Fact]
    public async Task Replace_during_Apply_discards_the_attempt_and_reapplies_with_the_new_instance()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        IRegistrationHandle<string> x = null!;
        await loader.MountAsync([
            Define("provider", ctx => x = ctx.Provide(new("X"), "x1"), provides: [new("X")]),
            Define("observer", _ => { })], TestContext.Current.CancellationToken);
        var consumer = new GatedConsumer();
        var mounting = loader.MountAsync([consumer.Definition], TestContext.Current.CancellationToken).AsTask();
        IPluginContext attempt;
        try
        {
            await consumer.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            attempt = loader.GetContext("consumer", ScopeKey.Global);
            Assert.Equal(PluginState.Applying, loader.GetState("consumer", ScopeKey.Global));
            // Apply 尚未结束时另一线程替换 X：本次 Apply 捕获的是旧实例，应与 Ready 后被替换一样作废重来。
            x.Replace("x2");
            Assert.Equal(PluginState.Pending, loader.GetState("consumer", ScopeKey.Global));
        }
        finally { consumer.Gate.TrySetResult(); }
        await mounting;
        await loader.WhenIdleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["apply:x1", "dispose:x1", "apply:x2"], consumer.Trace);
        Assert.Equal(PluginState.Ready, loader.GetState("consumer", ScopeKey.Global));
        Assert.Null(loader.GetFailure("consumer", ScopeKey.Global));
        Assert.Equal("x2", loader.GetContext("observer", ScopeKey.Global).Inject<string>(new("C")));
        Assert.Throws<ObjectDisposedException>(() => attempt.Provide(new("leak"), "late"));
    }

    [Fact]
    public async Task Revoke_during_Apply_returns_the_entry_to_Pending_until_the_key_is_back()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        IRegistrationHandle<string> x = null!;
        await loader.MountAsync([
            Define("provider", ctx => x = ctx.Provide(new("X"), "x1"), provides: [new("X")]),
            Define("observer", _ => { })], TestContext.Current.CancellationToken);
        var consumer = new GatedConsumer();
        var mounting = loader.MountAsync([consumer.Definition], TestContext.Current.CancellationToken).AsTask();
        try
        {
            await consumer.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            x.Dispose();
            Assert.Equal(PluginState.Pending, loader.GetState("consumer", ScopeKey.Global));
        }
        finally { consumer.Gate.TrySetResult(); }
        await mounting;
        await loader.WhenIdleAsync(TestContext.Current.CancellationToken);
        // 依赖已不在：作废的这次 Apply 撤销后条目留在 Pending，而不是 Failed。
        Assert.Equal(["apply:x1", "dispose:x1"], consumer.Trace);
        Assert.Equal(PluginState.Pending, loader.GetState("consumer", ScopeKey.Global));
        Assert.Null(loader.GetFailure("consumer", ScopeKey.Global));
        Assert.Equal([new ServiceKey("X")], loader.GetPendingDependencies("consumer", ScopeKey.Global));
        var observer = loader.GetContext("observer", ScopeKey.Global);
        Assert.Null(observer.TryInject<string>(new("C")));
        loader.GetContext("provider", ScopeKey.Global).Provide(new("X"), "x3");
        await loader.WhenIdleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["apply:x1", "dispose:x1", "apply:x3"], consumer.Trace);
        Assert.Equal(PluginState.Ready, loader.GetState("consumer", ScopeKey.Global));
        Assert.Equal("x3", observer.Inject<string>(new("C")));
    }

    [Fact]
    public async Task Hiding_its_own_dependency_during_Apply_still_fails()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        var tenant = new ScopeKey("tenant", "t-001");
        await loader.MountAsync([
            Define("provider", ctx => ctx.Provide(new("X"), "x1"), provides: [new("X")]),
            // 自己的 restriction 只对自己生效：别的条目看不到变化，不能当作外部失效重来，否则 Ready 后立刻自我失效。
            Define("selfish", ctx => ctx.Restrict(tenant, _ => false), inject: [new("X")], scope: tenant),
            Define("observer", _ => { }, scope: tenant)], TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Failed, loader.GetState("selfish", tenant));
        Assert.Contains("改变了自身依赖的可见性", Assert.IsType<InvalidOperationException>(loader.GetFailure("selfish", tenant)).Message, StringComparison.Ordinal);
        Assert.Equal("x1", loader.GetContext("observer", tenant).Inject<string>(new("X")));
    }

    internal static PluginDefinition Define(string id, Action<IPluginContext> apply, ServiceKey[]? inject = null, ServiceKey[]? provides = null, ScopeKey? scope = null)
        => new(new PluginEntry(id, id, "1.0.0", scope ?? ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new TestPlugin(apply, inject ?? []), provides ?? []);

    private sealed class TestPlugin(Action<IPluginContext> apply, ServiceKey[] inject) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => inject;
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            apply(ctx);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AsyncPlugin(Func<IPluginContext, CancellationToken, ValueTask> apply, ServiceKey[]? inject = null) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => inject ?? [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => apply(ctx, ct);
    }

    // 注入 X、提供 C 并登记效果，然后停在 Gate 上，放行后再解析一次 X；Gate 打开后的重新 Apply 不再等待。
    private sealed class GatedConsumer
    {
        internal ConcurrentQueue<string> Trace { get; } = [];
        internal TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal PluginDefinition Definition => new(new PluginEntry("consumer", "consumer", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
            new AsyncPlugin(async (ctx, _) =>
            {
                var value = ctx.Inject<string>(new("X"));
                Trace.Enqueue($"apply:{value}");
                ctx.Provide(new("C"), value);
                ctx.Effect(() => Trace.Enqueue($"dispose:{value}"));
                Blocked.TrySetResult();
                await Gate.Task;
                // X 若已被撤销，这里抛 ServiceNotReadyException：依赖失效之后的 Apply 失败同样作废重来，不记为 Failed。
                ctx.Inject<string>(new("X"));
            }, [new("X")]), [new("C")]);
    }

    private sealed record SampleConfig(string Message);
    private sealed class ConfigPlugin : IQianPlugin<SampleConfig>
    {
        internal int Applies;
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, SampleConfig config, CancellationToken ct)
        {
            Applies++;
            ctx.Provide(new("Message"), config.Message);
            return ValueTask.CompletedTask;
        }
    }
}
