using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    private static readonly string[] ExecutableNames = ["qian.sample.crm/customer.create", "qian.sample.crm/customer.get", "qian.sample.crm/customer.list"];
    [Fact]
    public async Task 真实客户插件登记生成的执行绑定_隐藏与卸载后旧引用不能调用()
    {
        await using var host = await Start();
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>();
        var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        using var scope = tenant.BeginScope(A); var caller = runtime.Source();
        var registrations = caller.List(CapabilityBindingRegistration.Key); Assert.Equal(3, registrations.Count);
        Assert.Equal(ExecutableNames, registrations.Select(item => item.Name).Order(StringComparer.Ordinal));
        var binding = caller.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, "qian.sample.crm/customer.create");
        Assert.Same(CreateCustomerCommand.QianCapability, binding.Contract);
        Assert.True(binding.Contract.InputSchema["properties"]!["name"]!["sensitive"]!.GetValue<bool>());
        var input = JsonSerializer.SerializeToElement(new CreateCustomerCommand(Guid.CreateVersion7(), "must stay absent"), CrmCommandJson.Default.CreateCustomerCommand);
        using (runtime.Loader.GetContext("data", ScopeKey.Global).Restrict(new ScopeKey("tenant", A), registration => registration.Key != CapabilityBindingRegistration.Key))
        {
            Assert.Empty(caller.List(CapabilityBindingRegistration.Key));
            Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<QianErrorException>(() => binding.InvokeAsync(caller, input, Ct).AsTask())).Error.Code.Code);
        }
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<QianErrorException>(() => binding.InvokeAsync(caller.Scoped(ScopeKey.Global), input, Ct).AsTask())).Error.Code.Code);
        await runtime.Loader.UnmountAsync("crm", ScopeKey.Global, Ct);
        Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<QianErrorException>(() => binding.InvokeAsync(caller, input, Ct).AsTask())).Error.Code.Code);
        await using var data = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
        Assert.Empty(await data.Context.Customers.ToListAsync(Ct));
    }
}
