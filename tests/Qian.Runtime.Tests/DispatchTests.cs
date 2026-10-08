using Microsoft.Extensions.DependencyInjection;

namespace Qian.Runtime.Tests;

public sealed class DispatchTests
{
    [Fact]
    public async Task Parallel_is_concurrent_and_returns_registration_order()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        var evt = QianEvent.Define<string, IReadOnlyList<int>>("parallel", DispatchMode.Parallel);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await loader.MountAsync([LifecycleTests.Define("A", ctx =>
        {
            ctx.On(evt, evt.Mode, (Func<string, ValueTask<int>>)(async _ => { await secondStarted.Task.WaitAsync(TestContext.Current.CancellationToken); return 1; }));
            ctx.On(evt, evt.Mode, (Func<string, ValueTask<int>>)(_ => { secondStarted.SetResult(); return ValueTask.FromResult(2); }));
        })], TestContext.Current.CancellationToken);
        Assert.Equal([1, 2], await loader.GetContext("A", ScopeKey.Global).DispatchAsync(evt, "", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Duplicate_event_definition_fails_even_when_name_and_shape_match()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        var first = QianEvent.Define<string, string?>("same", DispatchMode.Emit);
        var second = QianEvent.Define<string, string?>("same", DispatchMode.Emit);
        Func<string, ValueTask> listener = _ => ValueTask.CompletedTask;
        await loader.MountAsync([
            LifecycleTests.Define("A", ctx => ctx.On(first, first.Mode, listener)),
            LifecycleTests.Define("B", ctx => ctx.On(second, second.Mode, listener))], TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Failed, loader.GetState("B", ScopeKey.Global));
        Assert.Equal("A", Assert.IsType<Errors.QianErrorException>(loader.GetFailure("B", ScopeKey.Global)).Error.Args["owner"]);
        await loader.UnmountAsync("A", ScopeKey.Global, TestContext.Current.CancellationToken);
        await loader.ReloadAsync(LifecycleTests.Define("B", ctx => ctx.On(second, second.Mode, listener)), TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Ready, loader.GetState("B", ScopeKey.Global));
    }

    [Fact]
    public async Task Empty_dispatch_and_nullable_value_bail_have_typed_results()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        var bail = QianEvent.Define<string, int?>("bail", DispatchMode.Bail);
        await loader.MountAsync([LifecycleTests.Define("A", ctx =>
        {
            ctx.On(bail, bail.Mode, (Func<string, ValueTask<int?>>)(_ => ValueTask.FromResult<int?>(null)));
            ctx.On(bail, bail.Mode, (Func<string, ValueTask<int?>>)(_ => ValueTask.FromResult<int?>(0)));
        })], TestContext.Current.CancellationToken);
        var context = loader.GetContext("A", ScopeKey.Global);
        Assert.Equal(0, await context.DispatchAsync(bail, "", TestContext.Current.CancellationToken));
        var serial = QianEvent.Define<string, IReadOnlyList<int>>("serial", DispatchMode.Serial);
        Assert.Empty(await context.DispatchAsync(serial, "", TestContext.Current.CancellationToken));
        var waterfall = QianEvent.Define<string, int>("waterfall", DispatchMode.Waterfall) with { Terminal = _ => ValueTask.FromResult(42) };
        Assert.Equal(42, await context.DispatchAsync(waterfall, "", TestContext.Current.CancellationToken));
    }
}
