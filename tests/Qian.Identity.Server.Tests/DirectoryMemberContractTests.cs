using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;

namespace Qian.Identity.Server.Tests;

// 成员管理的 HTTP 契约快照（S1-IDP-017 验收 5）：contracts/identity/v1/directory-members.openapi.json 登记的操作与 IdentityDirectoryEndpoints 的
// 实际端点一致，请求 Schema 与服务端绑定的请求体逐字段一致，响应 Schema 与新增结果的序列化逐字段一致，Membership 与目录查询契约同一份定义，
// 示例都符合 Schema。真实响应的符合性在 DirectoryMemberAddTests 里验。
public sealed partial class DirectoryMemberContractTests
{
    [GeneratedRegex(@"\{(\w+)(:[^}]+)?\}")]
    private static partial Regex RouteParameter();

    private static async Task<(string Method, string Path)[]> Routes()
    {
        var builder = WebApplication.CreateSlimBuilder();
        await using var app = builder.Build();
        IdentityDirectoryEndpoints.Map(app);
        return [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .Select(method => (method, RouteParameter().Replace(endpoint.RoutePattern.RawText!, "{$1}"))))];
    }

    private static string[] Names(JsonNode schema, string key) => [.. (schema[key] switch
    {
        JsonObject properties => properties.Select(property => property.Key),
        JsonArray items => items.Select(item => item!.GetValue<string>()),
        _ => []
    }).Order(StringComparer.Ordinal)];

    // 服务端绑定的 JSON 字段名（目录接口的 snake_case 选项）。
    private static readonly JsonSerializerOptions Binding = new(IdentityDirectoryEndpoints.Json) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
    private static string[] Bound(Type type) => [.. Binding.GetTypeInfo(type).Properties.Select(property => property.Name).Order(StringComparer.Ordinal)];

    [Fact]
    public async Task 成员管理契约与实际端点一致_请求体与响应逐字段对应_Membership与查询契约同一定义_示例符合Schema()
    {
        var contract = DirectoryAgentContract.Load(DirectoryMemberAddTests.ContractPath);
        Assert.Equal("3.1.0", contract.Document["openapi"]!.GetValue<string>());
        var routes = await Routes();
        var operations = contract.Operations().ToArray();
        // 契约登记的每个操作都是实际端点；目前只有新增成员这一项写入。
        Assert.Equal([("/api/directory/members", "post")], operations.Select(operation => (operation.Path, operation.Method)));
        Assert.All(operations, operation => Assert.Contains((operation.Method.ToUpperInvariant(), operation.Path), routes));
        var (_, _, add) = Assert.Single(operations);
        Assert.Equal("directory.members.add", add["operationId"]!.GetValue<string>());
        Assert.Equal("write", add["x-qian-side-effect"]!.GetValue<string>());
        Assert.Equal(DirectoryAddCapability.Definition.Id, add["x-qian-capability"]!.GetValue<string>());
        // 请求：与服务端绑定的请求体逐字段一致，必填只有标识与显示名，不允许多余字段（因此也没有租户字段）。
        var request = contract.Resolve(add["requestBody"]!["content"]!["application/json"]!["schema"]!);
        Assert.Equal(Bound(typeof(IdentityDirectoryEndpoints.AddMemberBody)), Names(request, "properties"));
        Assert.Equal(["display_name", "identifier"], Names(request, "required"));
        Assert.False(request["additionalProperties"]!.GetValue<bool>());
        Assert.DoesNotContain(Names(request, "properties"), name => name.Contains("tenant", StringComparison.OrdinalIgnoreCase));
        // 响应：与新增结果及成员投影的序列化逐字段一致，全部必填；Membership 与目录查询契约是同一份定义。
        var response = contract.Resolve(contract.ResponseSchema(add, "200")!);
        Assert.Equal(Bound(typeof(DirectoryMemberAdded)), Names(response, "properties"));
        Assert.Equal(Names(response, "properties"), Names(response, "required"));
        var membership = contract.Resolve(contract.Document["components"]!["schemas"]!["Membership"]!);
        Assert.Equal(Bound(typeof(Membership)), Names(membership, "properties"));
        Assert.True(JsonNode.DeepEquals(membership, DirectoryAgentContract.Load().Document["components"]!["schemas"]!["Membership"]));
        // 错误一律是 ErrorDetail：会话、管理员、冲突、配额与请求体限制都有登记。
        Assert.Equal(["200", "400", "401", "403", "409", "413", "415", "422"], add["responses"]!.AsObject().Select(item => item.Key).Order(StringComparer.Ordinal));
        foreach (var (status, problem) in add["responses"]!.AsObject().Where(item => item.Key != "200"))
            Assert.Equal(DirectoryAgentContract.ErrorDetailId, problem!["content"]!["application/problem+json"]!["schema"]!["$ref"]!.GetValue<string>());
        // 示例：请求与响应示例都符合 Schema，响应示例不含任何密码字段。
        var requests = add["requestBody"]!["content"]!["application/json"]!["examples"]!.AsObject();
        Assert.Equal(2, requests.Count);
        foreach (var (name, sample) in requests)
            Assert.True(contract.Validate(request, JsonSerializer.SerializeToElement(sample!["value"])).Length == 0, "请求示例 " + name + " 不符合 Schema");
        var responses = contract.Resolve(add["responses"]!["200"]!)["content"]!["application/json"]!["examples"]!.AsObject();
        Assert.Equal(2, responses.Count);
        foreach (var (name, sample) in responses)
        {
            var value = JsonSerializer.SerializeToElement(sample!["value"]);
            Assert.True(contract.ValidateComponent("AddMemberResult", value).Length == 0, "响应示例 " + name + " 不符合 Schema");
            Assert.DoesNotContain("password", value.GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
    }

    // 能力与权限目录（S1-IDP-017 验收 5，不起宿主）：代码派生的新增能力标 write + confirm，不对 Agent 暴露但仍须人确认；权限点 member:create
    // 以中风险进组件权限目录，默认角色只给 tenant.owner / tenant.admin；发布的 tools/ 与 metadata/ 与代码派生结果一致。
    [Fact]
    public void 新增成员能力标write与confirm且不对Agent暴露_权限点与默认角色登记进组件目录_发布表面与代码派生一致()
    {
        var metadata = ((IComponentCodeMetadataFactory)Activator.CreateInstance(
            typeof(IdentityServerComponent).Assembly.GetCustomAttribute<QianCodeMetadataAttribute>()!.FactoryType)!).Create();
        var contract = Assert.Single(metadata.Capabilities, item => item.Id == DirectoryAddCapability.Definition.Id).Metadata;
        Assert.Equal(("write", "confirm", true, "pii"), (contract.GetProperty("sideEffect").GetString(), contract.GetProperty("policyHint").GetString(),
            contract.GetProperty("idempotent").GetBoolean(), contract.GetProperty("dataClassification").GetString()));
        Assert.Equal((false, true), (contract.GetProperty("agent").GetProperty("expose").GetBoolean(), contract.GetProperty("agent").GetProperty("requiresConfirmation").GetBoolean()));
        Assert.Equal(DirectoryAddCapability.Permission, contract.GetProperty("requiredPermission").GetString());
        var artifact = Path.Combine(DirectoryAgentContract.Root, "plugins/qian.identity.server");
        // 发布的工具契约：代码派生的每个元数据字段原样出现；幂等键必填，初始密码标敏感。
        var tool = JsonElement.Parse(File.ReadAllText(Path.Combine(artifact, "tools/directory.member.add.json")));
        foreach (var property in contract.EnumerateObject())
            Assert.True(JsonElement.DeepEquals(property.Value, tool.GetProperty(property.Name)), "工具契约字段 " + property.Name + " 与代码派生不一致");
        var input = tool.GetProperty("inputSchema");
        Assert.Contains(input.GetProperty("required").EnumerateArray(), field => field.GetString() == "idempotencyKey");
        Assert.True(input.GetProperty("properties").GetProperty("initialPassword").GetProperty("sensitive").GetBoolean());
        // 权限目录：与代码派生一致，member:create 为中风险。
        using var permissions = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifact, "metadata/permissions.json")));
        Assert.True(JsonElement.DeepEquals(metadata.Permissions, permissions.RootElement));
        var create = Assert.Single(permissions.RootElement.EnumerateArray(), item => item.GetProperty("resource").GetString() == "member" &&
            item.GetProperty("actions").EnumerateArray().Any(action => action.GetString() == "create"));
        Assert.Equal("medium", create.GetProperty("risk").GetProperty("create").GetString());
        // 默认角色：只有两个管理角色带 member:create。
        using var roles = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifact, "metadata/roles.json")));
        Assert.Equal(["tenant.admin", "tenant.owner"], roles.RootElement.EnumerateArray()
            .Where(role => role.GetProperty("grant").EnumerateArray().Any(grant => grant.GetString() == DirectoryAddCapability.Permission))
            .Select(role => role.GetProperty("role").GetString()).Order(StringComparer.Ordinal));
    }
}
