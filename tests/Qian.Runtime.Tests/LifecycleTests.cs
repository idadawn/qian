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

    private sealed class AsyncPlugin(Func<IPluginContext, CancellationToken, ValueTask> apply) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => apply(ctx, ct);
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
