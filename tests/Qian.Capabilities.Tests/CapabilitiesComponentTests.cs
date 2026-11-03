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

    [Fact]
    public void 清单的optionalInject与入口代码一致_Authz超管只读判定是可选依赖_不与必需依赖和提供项相交()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.capabilities/.qian-plugin/plugin.json")));
        var optional = manifest.RootElement.GetProperty("optionalInject").EnumerateArray().Select(key => key.GetString()!).ToArray();
        var inject = manifest.RootElement.GetProperty("inject").EnumerateArray().Select(key => key.GetString()!).ToArray();
        var provides = manifest.RootElement.GetProperty("provides").EnumerateArray().Select(key => key.GetString()!).ToArray();
        // 平台控制台目录（S1-YUE-018）按调用解析 Authz 的只读判定；缺席时目录为空，不阻塞装载。
        Assert.Equal(["AuthzSuperadminCheck"], optional);
        Assert.Equal(optional, CapabilitiesComponent.OptionalInject.Select(key => key.Name));
        Assert.Equal(optional, CapabilitiesPlugin.OptionalInject.Select(key => key.Name));
        Assert.Equal(inject, new CapabilitiesComponent().Inject.Select(key => key.Name).Order(StringComparer.Ordinal));
        Assert.Empty(optional.Intersect(inject, StringComparer.Ordinal)); Assert.Empty(optional.Intersect(provides, StringComparer.Ordinal));
    }

    private static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("找不到仓库根目录。");
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
