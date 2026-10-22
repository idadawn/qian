using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Capabilities.Tests;

public sealed class CapabilitiesComponentTests
{
    [Fact]
    public async Task 平台组件漏装登记期契约检查时拒绝挂载()
    {
        await using var services = new ServiceCollection().AddSingleton(ErrorCatalog.CreateDefault()).BuildServiceProvider();
        await using var loader = new PluginLoader(services);
        var component = new CapabilitiesComponent();
        await loader.MountAsync([
            new(new PluginEntry("prerequisites", "test.prerequisites", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
                new Prerequisites(component.Inject), component.Inject),
            new(new PluginEntry("capabilities", "qian.capabilities", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
                component, [CapabilitiesDefinition.Key])], TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Failed, loader.GetState("capabilities", ScopeKey.Global));
        Assert.Contains("AddQianCapabilities()", Assert.IsType<InvalidOperationException>(loader.GetFailure("capabilities", ScopeKey.Global)).Message, StringComparison.Ordinal);
        Assert.Null(loader.GetContext("prerequisites", ScopeKey.Global).TryInject<object>(CapabilitiesDefinition.Key));
    }

    // 只验证宿主门：这些占位项让装载器越过依赖检查，错误必须发生在使用任何 Provider 之前。
    private sealed class Prerequisites(IReadOnlyList<ServiceKey> keys) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            foreach (var key in keys) ctx.Provide(key, new object());
            return ValueTask.CompletedTask;
        }
    }
}
