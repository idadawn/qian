using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Qian.Identity.Server;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Surfaces;
using Qian.Tenancy;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    [Fact]
    public async Task 唯一清单入口运行真实CRM_工具与实际绑定同源_客户卸载撤销服务()
    {
        await using var host = await Start();
        var loader = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var crm = Metadata(typeof(CrmPlugin), "qian.sample.crm");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "samples/Qian.Sample.Crm/.qian-plugin/plugin.json")));
        Assert.Equal(crm.RuntimeEntry, manifest.RootElement.GetProperty("runtime").GetProperty("entry").GetString());
        Assert.Equal(crm.Provides, manifest.RootElement.GetProperty("provides").EnumerateArray().Select(item => item.GetString()!));
        await loader.UnmountAsync("crm", ScopeKey.Global, Ct);
        var entry = (IQianPlugin)Activator.CreateInstance(Type.GetType(crm.RuntimeEntry, throwOnError: true)!)!;
        await loader.MountAsync([new(new PluginEntry("crm", crm.ComponentId, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
            entry, crm.Provides.Select(key => new ServiceKey(key)).ToArray())], Ct);
        Assert.Equal(PluginState.Ready, loader.GetState("crm", ScopeKey.Global));
        AssertTools(crm, loader.GetContext("crm", ScopeKey.Global), "samples/Qian.Sample.Crm");
        AssertTools(Metadata(typeof(TenancyComponent), "qian.tenancy"), loader.GetContext("tenancy", ScopeKey.Global), "plugins/qian.tenancy");

        using (var created = await host.Post(A, new CreateCustomerCommand(Guid.CreateVersion7(), "manifest customer")))
            Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        using (var listed = await host.Get(A, "/api/customers")) Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        await loader.UnmountAsync("crm", ScopeKey.Global, Ct);
        using var removed = await host.Get(A, "/api/customers");
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
    }

    [Fact]
    public async Task 身份清单入口实际提供全部六个服务_卸载统一撤销()
    {
        await using var host = await Start(identity: true);
        var loader = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var identity = Metadata(typeof(IdentityServerComponent), "qian.identity.server");
        Assert.Equal(6, identity.Provides.Count);
        foreach (var key in identity.Provides) Assert.NotNull(loader.GetContext("data", ScopeKey.Global).TryInject<object>(new ServiceKey(key)));
        await loader.UnmountAsync("identity", ScopeKey.Global, Ct);
        foreach (var key in identity.Provides) Assert.Null(loader.GetContext("data", ScopeKey.Global).TryInject<object>(new ServiceKey(key)));
    }

    private static ComponentCodeMetadata Metadata(Type component, string id) => component.Assembly.GetCustomAttributes<QianCodeMetadataAttribute>()
        .Select(attribute => ((IComponentCodeMetadataFactory)Activator.CreateInstance(attribute.FactoryType)!).Create()).Single(code => code.ComponentId == id);

    private static void AssertTools(ComponentCodeMetadata code, IPluginContext context, string componentDirectory)
    {
        Assert.NotEmpty(code.Capabilities);
        foreach (var contract in code.Capabilities)
        {
            var bound = context.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, contract.Id);
            Assert.Same(contract, bound.Contract);
            using var tool = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), componentDirectory, "tools", contract.Name + ".json")));
            var expected = JsonNode.Parse(bound.Contract.Metadata.GetRawText())!.AsObject();
            expected["inputSchema"] = bound.Contract.InputSchema;
            expected["outputSchema"] = bound.Contract.OutputSchema;
            using var actualContract = JsonDocument.Parse(expected.ToJsonString());
            Assert.Equal(ToolContractHash.Compute(actualContract.RootElement), tool.RootElement.GetProperty("schemaSha256").GetString());
            Assert.Equal(contract.Id, tool.RootElement.GetProperty("binding").GetProperty("id").GetString());
        }
    }
}
