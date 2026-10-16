using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;
using Qian.Testing;

namespace Qian.Plugin.Sdk.Tests;

public sealed class TenantInitializationHookTests
{
    [Theory]
    [InlineData("destructive")]
    [InlineData("external")]
    public async Task 已在许可清单中的高风险能力仍在实际hook适配器中被拒(string sideEffect)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(services);
        var fixture = new HookProvider(sideEffect);
        await loader.MountAsync([Define("producer", "qian.tenancy", new Producer()), Define("target", "qian.sdk-tests", fixture)], TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Ready, loader.GetState("target", ScopeKey.Global));
        var source = loader.GetContext("producer", ScopeKey.Global).Scoped(new ScopeKey("tenant", Guid.NewGuid().ToString()));
        var target = loader.GetContext("target", ScopeKey.Global).Inject<IHookHandler>(HookDefinition.Handlers, "qian.sdk-tests/init");
        using var grant = TenantInitializationAccess.Begin(source, "operator", new HashSet<string> { fixture.Binding.Contract.Id });
        var error = await Assert.ThrowsAsync<QianErrorException>(() => target.InvokeAsync(new HookNotification(source, source.Scope.Id!,
            JsonSerializer.SerializeToElement(new { }), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken).AsTask());
        Assert.Equal("AUTHZ_DENIED", error.Error.Code.Code);
        Assert.Equal(0, fixture.Binding.Calls);
    }
    private static PluginDefinition Define(string id, string plugin, IQianPlugin provider) => new(
        new PluginEntry(id, plugin, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), provider, []);
    private sealed class Producer : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext context, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
    }
    private sealed class HookProvider(string sideEffect) : IQianPlugin
    {
        internal Probe Binding { get; } = new(sideEffect);
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext context, JsonElement config, CancellationToken ct)
        {
            context.Provide<IExecutableCapability>(CapabilityBindingRegistration.Key, Binding.Contract.Id, Binding);
            context.ExposeCapabilityHook<CreateRequest, CustomerResponse>("init", _ => new CreateRequest(Guid.CreateVersion7(), "never execute"));
            return ValueTask.CompletedTask;
        }
    }
    // 只观察是否越过 SDK 的风险门，不模拟真实开户、身份或业务持久化。
    private sealed class Probe : IExecutableCapability
    {
        internal int Calls { get; private set; }
        public CapabilityContract Contract { get; }
        internal Probe(string sideEffect)
        {
            var original = CreateRequest.QianCapability;
            var metadata = JsonNode.Parse(original.Metadata.GetRawText())!; metadata["sideEffect"] = sideEffect;
            Contract = new CapabilityContract(original.ComponentId, metadata.ToJsonString(), original.RequestTypeInfo, original.ResponseTypeInfo);
        }
        public ValueTask<JsonElement> InvokeAsync(IPluginContext context, JsonElement input, CancellationToken cancellationToken = default)
        { Calls++; return ValueTask.FromResult(JsonSerializer.SerializeToElement(new { })); }
    }
}
