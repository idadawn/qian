using Microsoft.Extensions.DependencyInjection;

namespace Qian.Runtime.Tests;

public sealed class ScopeTests
{
    private static readonly ScopeKey s_tenant = new("tenant", "A");

    [Fact]
    public async Task Shadow_and_restriction_changes_rebind_declared_dependencies()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        List<string> resolutions = [];
        var stops = 0;
        await loader.MountAsync([
            LifecycleTests.Define("global", ctx => ctx.Provide(new("X"), "global"), provides: [new("X")]),
            LifecycleTests.Define("consumer", ctx => { resolutions.Add(ctx.Inject<string>(new("X"))); ctx.Effect(() => stops++); }, inject: [new("X")], scope: s_tenant)], TestContext.Current.CancellationToken);
        await loader.MountAsync([LifecycleTests.Define("local", ctx => ctx.Provide(new("X"), "local"), provides: [new("X")], scope: s_tenant)], TestContext.Current.CancellationToken);
        Assert.Equal(["global", "local"], resolutions);
        Assert.Equal(1, stops);
        await loader.UnmountAsync("local", s_tenant, TestContext.Current.CancellationToken);
        Assert.Equal(["global", "local", "global"], resolutions);
        var restriction = loader.GetContext("global", ScopeKey.Global).Restrict(s_tenant, _ => false);
        await loader.WhenIdleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Pending, loader.GetState("consumer", s_tenant));
        restriction.Dispose();
        await loader.WhenIdleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Ready, loader.GetState("consumer", s_tenant));
        Assert.Equal(4, resolutions.Count);
    }

    [Fact]
    public async Task Restrictions_only_filter_global_items_and_scoped_effects_share_the_owner_lifetime()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        await loader.MountAsync([
            LifecycleTests.Define("global", ctx => { ctx.Provide(new("Tools"), "a", "global"); ctx.Provide(new("Tools"), "b", "global"); }),
            LifecycleTests.Define("policy", ctx => { ctx.Scoped(s_tenant).Provide(new("Tools"), "a", "local"); ctx.Restrict(s_tenant, _ => false); })], TestContext.Current.CancellationToken);
        var view = loader.GetContext("global", ScopeKey.Global).Scoped(s_tenant);
        var ownedView = loader.GetContext("policy", ScopeKey.Global).Scoped(s_tenant);
        Assert.Single(view.List(new("Tools")));
        Assert.Equal("local", view.Inject<string>(new("Tools"), "a"));
        await loader.UnmountAsync("policy", ScopeKey.Global, TestContext.Current.CancellationToken);
        Assert.Equal(2, view.List(new("Tools")).Count);
        Assert.Equal("global", view.Inject<string>(new("Tools"), "a"));
        Assert.Throws<ObjectDisposedException>(() => ownedView.Provide(new("leak"), "bad"));
    }

    [Fact]
    public async Task Event_catalog_and_dispatch_apply_the_same_restrictions()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        var evt = QianEvent.Define<string, IReadOnlyList<string>>("tools/run", DispatchMode.Serial);
        await loader.MountAsync([
            LifecycleTests.Define("allowed", ctx => ctx.On(evt, evt.Mode, (Func<string, ValueTask<string>>)(_ => ValueTask.FromResult("allowed")))),
            LifecycleTests.Define("hidden", ctx => ctx.On(evt, evt.Mode, (Func<string, ValueTask<string>>)(_ => throw new InvalidOperationException("不可执行")))),
            LifecycleTests.Define("policy", ctx => ctx.Restrict(s_tenant, r => r.OwnerEntryId == "allowed"))], TestContext.Current.CancellationToken);
        var view = loader.GetContext("policy", ScopeKey.Global).Scoped(s_tenant);
        Assert.Equal("allowed", Assert.Single(view.List(new(evt.Name))).OwnerEntryId);
        Assert.Equal(["allowed"], await view.DispatchAsync(evt, "", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Identical_entry_ids_can_mount_in_different_scopes_and_shadow_listeners()
    {
        using var host = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(host);
        var evt = QianEvent.Define<string, IReadOnlyList<string>>("same", DispatchMode.Serial);
        await loader.MountAsync([
            LifecycleTests.Define("same", ctx => ctx.On(evt, evt.Mode, (Func<string, ValueTask<string>>)(_ => ValueTask.FromResult("global")))),
            LifecycleTests.Define("same", ctx => ctx.On(evt, evt.Mode, (Func<string, ValueTask<string>>)(_ => ValueTask.FromResult("local"))), scope: s_tenant)], TestContext.Current.CancellationToken);
        var context = loader.GetContext("same", ScopeKey.Global);
        Assert.Equal(["global"], await context.DispatchAsync(evt, "", TestContext.Current.CancellationToken));
        Assert.Equal(["local"], await context.Scoped(s_tenant).DispatchAsync(evt, "", TestContext.Current.CancellationToken));
    }
}
