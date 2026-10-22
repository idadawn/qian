using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Plugin.Sdk.Tests;

public sealed class NativeAuditOwnerTests
{
    [Theory]
    [InlineData("qian.audit.native", CommandPolicyKind.Audit, true)]
    [InlineData("qian.audit.native", CommandPolicyKind.Tenancy, false)]
    [InlineData("qian.audit.native", CommandPolicyKind.Authorization, false)]
    [InlineData("qian.audit.native.forged", CommandPolicyKind.Audit, false)]
    [InlineData("qian.other", CommandPolicyKind.Audit, false)]
    public async Task 只有真实原生审计组件ID可登记Audit策略_不能冒充其他强制策略(string component, CommandPolicyKind kind, bool allowed)
    {
        using var services = new ServiceCollection().BuildServiceProvider(); await using var loader = new PluginLoader(services);
        var entry = new PluginEntry("policy", component, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { }));
        await loader.MountAsync([new PluginDefinition(entry, new Policy(kind), [])], TestContext.Current.CancellationToken);
        Assert.Equal(allowed ? PluginState.Ready : PluginState.Failed, loader.GetState("policy", ScopeKey.Global));
        if (!allowed) Assert.Equal("AUTHZ_DENIED", Assert.IsType<QianErrorException>(loader.GetFailure("policy", ScopeKey.Global)).Error.Code.Code);
    }

    [Fact]
    public async Task Native策略卸载必须真实撤销监听器_同名新Definition才可再次登记()
    {
        using var services = new ServiceCollection().BuildServiceProvider(); await using var loader = new PluginLoader(services);
        var source = new Source();
        await loader.MountAsync([
            new PluginDefinition(new PluginEntry("source", "qian.test", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), source, []),
            new PluginDefinition(new PluginEntry("audit", "qian.audit.native", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new Policy(CommandPolicyKind.Audit), [])], TestContext.Current.CancellationToken);
        var sameNameNewDefinition = QianEvent.Define<string, object?>(CommandEvents.Dispatching.Name, DispatchMode.Waterfall);
        Assert.ThrowsAny<Exception>(() => source.Context!.On(sameNameNewDefinition, DispatchMode.Waterfall,
            (Func<string, Func<ValueTask<object?>>, ValueTask<object?>>)((_, next) => next())));
        await loader.UnmountAsync("audit", ScopeKey.Global, TestContext.Current.CancellationToken);
        // Runtime.CheckDefinition 遍历实际监听器集合，不会因旧上下文失效而跳过检查；只隐藏旧owner不足以让此断言通过。
        using var registration = source.Context!.On(sameNameNewDefinition, DispatchMode.Waterfall,
            (Func<string, Func<ValueTask<object?>>, ValueTask<object?>>)((_, next) => next()));
        Assert.Empty(source.Context.List(CommandPolicies.AuditKey));
    }
    private sealed class Policy(CommandPolicyKind kind) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        { CommandPolicies.Register(ctx, kind, (_, next) => next()); return ValueTask.CompletedTask; }
    }
    private sealed class Source : IQianPlugin
    {
        internal IPluginContext? Context { get; private set; }
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) { Context = ctx; return ValueTask.CompletedTask; }
    }
}
