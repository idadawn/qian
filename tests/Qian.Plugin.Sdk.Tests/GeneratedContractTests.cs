using System.Text.Json;
using System.Text.Json.Serialization;
using Qian.Runtime;

namespace Qian.Plugin.Sdk.Tests;

public sealed class GeneratedContractTests
{
    [Fact]
    public void 实际编译生成契约保留描述权限和Agent字段_输入响应来自JsonTypeInfo()
    {
        var contract = CreateRequest.QianCapability; var metadata = contract.Metadata;
        Assert.Equal("qian.sdk-tests/customer.create", contract.Id);
        Assert.Equal("为当前租户创建客户，已有客户请使用查询。", metadata.GetProperty("description").GetString());
        Assert.Equal("qian.sdk-tests:customer:create", metadata.GetProperty("requiredPermission").GetString());
        Assert.Equal("qian.sdk-tests:write", Assert.Single(metadata.GetProperty("requiredScopes").EnumerateArray()).GetString());
        Assert.True(metadata.GetProperty("audit").GetBoolean()); Assert.False(metadata.GetProperty("agent").GetProperty("expose").GetBoolean());
        Assert.Equal(typeof(CreateRequest), contract.RequestTypeInfo.Type); Assert.Equal(typeof(CustomerResponse), contract.ResponseTypeInfo.Type);
        Assert.Equal("object", contract.InputSchema["type"]!.GetValue<string>());
        Assert.False(contract.InputSchema["additionalProperties"]!.GetValue<bool>());
        Assert.NotNull(contract.InputSchema["properties"]!["idempotencyKey"]); Assert.NotNull(contract.OutputSchema["properties"]!["id"]);
        var input = JsonSerializer.SerializeToElement(new CreateRequest(Guid.NewGuid(), "测试客户"), ContractJson.Default.CreateRequest);
        Assert.Equal(2, input.EnumerateObject().Count()); Assert.False(input.TryGetProperty("qianCapability", out _));
    }

    [Fact]
    public void 组件入口和服务键从实际代码派生_不会实例化业务插件()
    {
        var metadata = new ContractPluginQianCodeMetadata().Create();
        Assert.Equal("qian.sdk-tests", metadata.ComponentId);
        Assert.Equal("Qian.Plugin.Sdk.Tests.ContractPlugin, Qian.Plugin.Sdk.Tests", metadata.RuntimeEntry);
        Assert.Equal("TestInput", Assert.Single(metadata.Inject)); Assert.Equal("TestOutput", Assert.Single(metadata.Provides));
        Assert.Equal(3, metadata.Capabilities.Count);
    }

    [Fact]
    public void 查询默认不审计_显式开启后记录_派生副本修改不影响原契约()
    {
        Assert.False(ReadRequest.QianCapability.Metadata.GetProperty("audit").GetBoolean());
        Assert.True(AuditedReadRequest.QianCapability.Metadata.GetProperty("audit").GetBoolean());
        var schema = CreateRequest.QianCapability.InputSchema; schema["properties"]!["name"] = null;
        Assert.NotNull(CreateRequest.QianCapability.InputSchema["properties"]!["name"]);
    }
}

[QianPlugin("qian.sdk-tests")]
internal sealed class ContractPlugin : IQianPlugin
{
    public IReadOnlyList<ServiceKey> Inject => [ContractKeys.Input];
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ctx.Provide(ContractKeys.Output, new object()); return ValueTask.CompletedTask;
    }
}

[Capability("customer.create", typeof(CustomerResponse), typeof(ContractJson),
    Description = "为当前租户创建客户，已有客户请使用查询。", SideEffect = "write", Idempotent = true,
    RequiredPermission = "qian.sdk-tests:customer:create", RequiredScopes = ["qian.sdk-tests:write"], ContractVersion = "customers-v1")]
internal sealed partial record CreateRequest(Guid IdempotencyKey, string Name);
[Capability("customer.read", typeof(CustomerResponse), typeof(ContractJson), Description = "查询当前租户已有客户信息，不会修改客户数据。")]
internal sealed partial record ReadRequest(Guid Id);
[Capability("customer.inspect", typeof(CustomerResponse), typeof(ContractJson), Description = "核查当前租户客户信息，并记录这次审计查询。", Audit = true)]
internal sealed partial record AuditedReadRequest(Guid Id);
internal sealed record CustomerResponse(Guid Id, string Name);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(CreateRequest))]
[JsonSerializable(typeof(ReadRequest))]
[JsonSerializable(typeof(AuditedReadRequest))]
[JsonSerializable(typeof(CustomerResponse))]
internal sealed partial class ContractJson : JsonSerializerContext;

internal static class ContractKeys
{
    public static readonly ServiceKey Input = new("TestInput");
    public static readonly ServiceKey Output = new("TestOutput");
}
