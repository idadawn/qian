using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime.Errors;
using static Qian.Runtime.Tests.LifecycleTests;

namespace Qian.Runtime.Tests;

public sealed class RegistrationHandleTests
{
    [Fact]
    public async Task Replace_is_atomic_for_concurrent_readers_and_keeps_registration_order()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        IRegistrationHandle<string> plain = null!, named = null!;
        await loader.MountAsync([
            Define("provider", ctx =>
            {
                plain = ctx.Provide(new("X"), "a");
                named = ctx.Provide(new("Tools"), "first", "one");
                ctx.Provide(new("Tools"), "second", "two");
            }, provides: [new("X")]),
            Define("observer", _ => { })], TestContext.Current.CancellationToken);
        var observer = loader.GetContext("observer", ScopeKey.Global);
        var seen = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        long misses = 0;
        using var stop = new CancellationTokenSource();
        using var started = new CountdownEvent(4);
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            started.Signal();
            while (!stop.IsCancellationRequested)
            {
                // 每种读取入口都只能得到旧值或新值；缺失、空值或顺序变化都计为失败。
                try { seen.TryAdd(observer.Inject<string>(new("X")), 0); }
                catch (ServiceNotReadyException) { Interlocked.Increment(ref misses); }
                try { seen.TryAdd(observer.Inject<string>(new("Tools"), "first"), 0); }
                catch (QianErrorException) { Interlocked.Increment(ref misses); }
                if (observer.TryInject<string>(new("X")) is null || observer.Services.GetKeyedService<string>(new ServiceKey("Tools:first")) is null) Interlocked.Increment(ref misses);
                if (!observer.List(new("Tools")).Select(item => item.Name).SequenceEqual(["first", "second"])) Interlocked.Increment(ref misses);
            }
        }, TestContext.Current.CancellationToken)).ToArray();
        Assert.True(started.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        for (var i = 1; i <= 2000; i++)
        {
            plain.Replace(i % 2 == 0 ? "a" : "b");
            named.Replace(i % 2 == 0 ? "one" : "uno");
        }
        await stop.CancelAsync();
        await Task.WhenAll(readers);
        Assert.Equal(0, Interlocked.Read(ref misses));
        Assert.Subset(new HashSet<string>(["a", "b", "one", "uno"]), seen.Keys.ToHashSet());
        Assert.Equal("a", observer.Inject<string>(new("X")));
        Assert.Equal("one", observer.Inject<string>(new("Tools"), "first"));
    }

    [Fact]
    public async Task Replace_keeps_scope_and_is_rejected_after_revoke_or_unmount()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        IRegistrationHandle<string> scoped = null!, global = null!;
        var tenant = new ScopeKey("tenant", "t-001");
        await loader.MountAsync([
            Define("provider", ctx =>
            {
                scoped = ctx.Scoped(tenant).Provide(new("Y"), "tenant-old");
                global = ctx.Provide(new("Z"), "global");
            }),
            Define("observer", _ => { })], TestContext.Current.CancellationToken);
        var observer = loader.GetContext("observer", ScopeKey.Global);
        scoped.Replace("tenant-new");
        Assert.Equal("tenant-new", observer.Scoped(tenant).Inject<string>(new("Y")));
        Assert.Equal(tenant, Assert.Single(observer.Scoped(tenant).List(new("Y"))).Scope);
        Assert.Null(observer.TryInject<string>(new("Y")));
        Assert.Null(observer.Scoped(new ScopeKey("tenant", "t-002")).TryInject<string>(new("Y")));
        Assert.Throws<ArgumentNullException>(() => scoped.Replace(null!));
        scoped.Dispose();
        scoped.Dispose();
        Assert.Throws<ObjectDisposedException>(() => scoped.Replace("late"));
        Assert.Null(observer.Scoped(tenant).TryInject<string>(new("Y")));
        await loader.UnmountAsync("provider", ScopeKey.Global, TestContext.Current.CancellationToken);
        Assert.Throws<ObjectDisposedException>(() => global.Replace("late"));
        Assert.Null(observer.TryInject<string>(new("Z")));
    }

    [Fact]
    public async Task Replace_restarts_dependents_like_revoke_then_provide()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        IRegistrationHandle<string> handle = null!;
        List<string> applied = [];
        var stops = 0;
        await loader.MountAsync([
            Define("provider", ctx => handle = ctx.Provide(new("X"), "first"), provides: [new("X")]),
            Define("consumer", ctx => { applied.Add(ctx.Inject<string>(new("X"))); ctx.Effect(() => stops++); }, inject: [new("X")])], TestContext.Current.CancellationToken);
        handle.Replace("second");
        await loader.WhenIdleAsync(TestContext.Current.CancellationToken);
        // 依赖者在 Apply 中捕获的是旧实例，所以与撤销后再登记一样释放并重新 Apply，拿到新实例。
        Assert.Equal(["first", "second"], applied);
        Assert.Equal(1, stops);
        Assert.Equal(PluginState.Ready, loader.GetState("consumer", ScopeKey.Global));
        Assert.Equal(PluginState.Ready, loader.GetState("provider", ScopeKey.Global));
    }

    [Fact]
    public async Task Replace_during_cascade_cleanup_disposes_the_stale_dependent_before_reapply()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        IRegistrationHandle<string> x = null!, y = null!;
        ConcurrentQueue<string> trace = [];
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await loader.MountAsync([
            Define("provider", ctx => { x = ctx.Provide(new("X"), "x1"); y = ctx.Provide(new("Y"), "y1"); }, provides: [new("X"), new("Y")]),
            Define("consumer", ctx =>
            {
                var value = ctx.Inject<string>(new("X"));
                trace.Enqueue($"apply:consumer:{value}");
                ctx.Provide(new("C"), value);
                ctx.Effect(() => trace.Enqueue("dispose:consumer"));
            }, inject: [new("X")], provides: [new("C")]),
            Define("slow", ctx =>
            {
                trace.Enqueue("apply:slow");
                ctx.Effect(async () =>
                {
                    blocked.TrySetResult();
                    await gate.Task;
                    trace.Enqueue("dispose:slow");
                });
            }, inject: [new("Y")]),
            Define("observer", _ => { })], TestContext.Current.CancellationToken);
        await loader.WhenIdleAsync(TestContext.Current.CancellationToken);
        var stale = loader.GetContext("consumer", ScopeKey.Global);
        try
        {
            // 替换 Y 让 slow 失效，级联清理停在 slow 的撤销上；此时本线程替换 X，consumer 在清理快照之后回到 Pending。
            y.Replace("y2");
            await blocked.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            x.Replace("x2");
            Assert.Equal(PluginState.Pending, loader.GetState("consumer", ScopeKey.Global));
        }
        finally { gate.TrySetResult(); }
        await loader.WhenIdleAsync(TestContext.Current.CancellationToken);
        // 旧上下文的效果与注册先撤销，再以新实例重新 Apply；否则旧注册残留，新 Apply 的 Provide 撞冲突。
        Assert.Equal(["apply:consumer:x1", "apply:slow", "dispose:slow", "dispose:consumer", "apply:consumer:x2", "apply:slow"], trace);
        foreach (var id in (string[])["provider", "consumer", "slow"])
        {
            Assert.Equal(PluginState.Ready, loader.GetState(id, ScopeKey.Global));
            Assert.Null(loader.GetFailure(id, ScopeKey.Global));
        }
        Assert.Throws<ObjectDisposedException>(() => stale.Provide(new("leak"), "late"));
        var observer = loader.GetContext("observer", ScopeKey.Global);
        Assert.Equal("x2", observer.Inject<string>(new("C")));
        await loader.UnmountAsync("consumer", ScopeKey.Global, TestContext.Current.CancellationToken);
        Assert.Null(observer.TryInject<string>(new("C")));
        await loader.MountAsync([Define("probe", ctx => ctx.Provide(new("C"), "probe"), provides: [new("C")])], TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Ready, loader.GetState("probe", ScopeKey.Global));
        Assert.Equal("probe", observer.Inject<string>(new("C")));
    }
}
