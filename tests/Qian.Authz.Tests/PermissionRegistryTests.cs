using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Authz.Tests;

public sealed class PermissionRegistryTests
{
    private const string Plugin = "qian.sample.crm";
    private static PermissionDefinition Permission(string action, PermissionRisk risk = PermissionRisk.Low) => new(Plugin, "customer", action, risk, false, []);

    [Fact]
    public void 相同声明共享租约_释放一个调用方不会删掉其他调用方的权限()
    {
        var registry = new PermissionRegistry(); var permission = Permission("read");
        using var first = registry.Register("1.0.0", [permission]); using var second = registry.Register("1.0.0", [Permission("read")]);
        Assert.Single(registry.List(Plugin, "1.0.0")); Assert.Equal(permission.Code, registry.Find(permission.Code, "1.0.0")!.Code);
        first.Dispose(); first.Dispose(); Assert.NotNull(registry.Find(permission.Code, "1.0.0"));
        second.Dispose(); Assert.Null(registry.Find(permission.Code, "1.0.0")); Assert.Empty(registry.ListResources(Plugin, "1.0.0"));
        using var replacement = registry.Register("1.0.0", [Permission("read", PermissionRisk.High)]);
        first.Dispose(); second.Dispose(); Assert.Equal(PermissionRisk.High, registry.Find(permission.Code, "1.0.0")!.Risk);
    }

    [Fact]
    public void 一批声明冲突时不残留前半批_不同版本保持独立()
    {
        var registry = new PermissionRegistry(); using var initial = registry.Register("1.0.0", [Permission("read")]);
        var error = Assert.Throws<QianErrorException>(() => registry.Register("1.0.0", [Permission("write"), Permission("read", PermissionRisk.High)]));
        Assert.Equal("CONFLICT", error.Error.Code.Code); Assert.Null(registry.Find(Plugin + ":customer:write", "1.0.0"));
        using var next = registry.Register("2.0.0", [Permission("read", PermissionRisk.High)]);
        Assert.Equal(PermissionRisk.Low, registry.Find(Plugin + ":customer:read", "1.0.0")!.Risk);
        Assert.Equal(PermissionRisk.High, registry.Find(Plugin + ":customer:read", "2.0.0")!.Risk);
    }

    [Fact]
    public void 显式资源描述可撤销_不制造权限且释放后恢复派生视图()
    {
        var registry = new PermissionRegistry(); using var permission = registry.Register("1.0.0", [Permission("read")]);
        using var resource = registry.RegisterResources("1.0.0", [new ResourceTypeDefinition(Plugin, "customer", "客户记录")]);
        Assert.Equal("客户记录", Assert.Single(registry.ListResources(Plugin, "1.0.0")).Description);
        resource.Dispose(); Assert.Null(Assert.Single(registry.ListResources(Plugin, "1.0.0")).Description);
        permission.Dispose(); Assert.Empty(registry.ListResources(Plugin, "1.0.0"));
        using var descriptionOnly = registry.RegisterResources("1.0.0", [new ResourceTypeDefinition(Plugin, "order", "订单")]);
        Assert.Single(registry.ListResources(Plugin, "1.0.0")); Assert.Empty(registry.List(Plugin, "1.0.0"));
    }

    [Fact]
    public async Task 真实运行时通过Effect撤销权限与资源_卸载后没有活动声明残留()
    {
        var registry = new PermissionRegistry(); using var services = new ServiceCollection().BuildServiceProvider();
        await using var runtime = new PluginLoader(services);
        await runtime.MountAsync([Definition("provider", new RegistryProvider(registry), [AuthzDefinition.PermissionsKey, AuthzDefinition.ResourceTypesKey]),
            Definition("declarations", new DeclarationConsumer(), [])], TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Ready, runtime.GetState("declarations", ScopeKey.Global));
        Assert.Single(registry.List(Plugin, "1.0.0")); Assert.Equal("客户记录", Assert.Single(registry.ListResources(Plugin, "1.0.0")).Description);
        await runtime.UnmountAsync("declarations", ScopeKey.Global, TestContext.Current.CancellationToken);
        Assert.Empty(registry.List(Plugin, "1.0.0")); Assert.Empty(registry.ListResources(Plugin, "1.0.0"));
        Assert.DoesNotContain(runtime.DumpConfig(), entry => entry.Id == "declarations");
    }

    private static PluginDefinition Definition(string id, IQianPlugin plugin, IReadOnlyList<ServiceKey> provides) => new(
        new PluginEntry(id, "test." + id, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), plugin, provides);
    private sealed class RegistryProvider(IPermissionRegistry registry) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.Provide(AuthzDefinition.PermissionsKey, registry); ctx.Provide<IResourceTypeRegistry>(AuthzDefinition.ResourceTypesKey, registry);
            return ValueTask.CompletedTask;
        }
    }
    private sealed class DeclarationConsumer : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [AuthzDefinition.PermissionsKey, AuthzDefinition.ResourceTypesKey];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.Effect(ctx.Inject<IPermissionRegistry>(AuthzDefinition.PermissionsKey).Register("1.0.0", [Permission("read")]));
            ctx.Effect(ctx.Inject<IResourceTypeRegistry>(AuthzDefinition.ResourceTypesKey).RegisterResources("1.0.0", [new ResourceTypeDefinition(Plugin, "customer", "客户记录")]));
            return ValueTask.CompletedTask;
        }
    }
}
