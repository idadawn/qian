using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Qian.Identity.Abstractions;

namespace Qian.Identity.Server.Tests;

// 目录查询的 Agent 契约（S1-IDP-010 验收 4）：contracts/identity/v1/directory.openapi.json 与 IdentityDirectoryEndpoints 的 GET 端点逐一对应，
// 每个查询带意图式 description 与请求 / 响应示例，示例符合契约 Schema，契约 Schema 与目录记录的序列化逐字段一致；
// yue 业务组件契约（packages/yue/biz/contracts/biz.openapi.json）的目录部分与它逐字段一致。真实响应的符合性在 DirectoryQueryTests 里验。
public sealed partial class DirectoryAgentContractTests
{
    private static readonly Guid A = Guid.Parse("0193a000-0000-7000-8000-000000000001"), B = Guid.Parse("0193a000-0000-7000-8000-000000000002"),
        C = Guid.Parse("0193a000-0000-7000-8000-000000000003"), D = Guid.Parse("0193a000-0000-7000-8000-000000000004");

    private static readonly string[] Locations = ["path", "query"];

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

    [Fact]
    public async Task 目录查询契约与GET端点逐一对应_每个查询带意图描述与参数示例_响应示例符合Schema()
    {
        var contract = DirectoryAgentContract.Load();
        Assert.Equal("3.1.0", contract.Document["openapi"]!.GetValue<string>());
        var routes = await Routes();
        var gets = routes.Where(route => route.Method == "GET").Select(route => route.Path).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(13, gets.Length);
        // 契约只登记查询：每个 GET 端点都在册，写入端点（PUT / POST / DELETE）不在册。
        Assert.All(contract.Operations(), operation => Assert.Equal("get", operation.Method));
        Assert.Equal(gets, contract.Operations().Select(operation => operation.Path).Order(StringComparer.Ordinal));
        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        var examples = 0;
        foreach (var (path, _, operation) in contract.Operations())
        {
            var id = operation["operationId"]?.GetValue<string>();
            Assert.True(id is not null && Regex.IsMatch(id, "^directory\\.[a-zA-Z]+\\.[a-zA-Z]+$") && operationIds.Add(id), path + " 的 operationId 缺失、格式不对或重复");
            Assert.False(string.IsNullOrWhiteSpace(operation["summary"]?.GetValue<string>()), path + " 缺 summary");
            // 意图式描述：说清这个查询回答什么问题、什么时候用。
            var description = operation["description"]?.GetValue<string>() ?? "";
            Assert.True(description.Length >= 40 && description.Contains("时用", StringComparison.Ordinal), path + " 的 description 没有说明何时使用：" + description);
            Assert.Equal("read", operation["x-qian-side-effect"]?.GetValue<string>());
            // 参数：路径参数与路由一致且必填；每个参数都有描述与示例，示例符合参数 Schema；租户只取自会话，不得出现租户参数。
            var parameters = contract.Parameters(operation).ToArray();
            Assert.Equal(RouteParameter().Matches(path).Select(match => match.Groups[1].Value).Order(StringComparer.Ordinal),
                parameters.Where(parameter => parameter["in"]!.GetValue<string>() == "path").Select(parameter => parameter["name"]!.GetValue<string>()).Order(StringComparer.Ordinal));
            foreach (var parameter in parameters)
            {
                var name = parameter["name"]!.GetValue<string>();
                Assert.DoesNotContain("tenant", name, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(parameter["in"]!.GetValue<string>(), Locations);
                if (parameter["in"]!.GetValue<string>() == "path") Assert.True(parameter["required"]?.GetValue<bool>(), path + " 的路径参数 " + name + " 必须 required");
                Assert.False(string.IsNullOrWhiteSpace(parameter["description"]?.GetValue<string>()), path + " 的参数 " + name + " 缺描述");
                Assert.True(parameter["example"] is not null, path + " 的参数 " + name + " 缺示例");
                Assert.Empty(contract.Validate(parameter["schema"]!, JsonSerializer.SerializeToElement(parameter["example"])));
            }
            // 响应：200 带至少一个有摘要的示例且符合 Schema；错误一律是 ErrorDetail，会话类错误每个查询都有，带路径 id 的查询有 404。
            var schema = contract.ResponseSchema(operation, "200");
            Assert.True(schema is not null, path + " 缺 200 响应 Schema");
            var samples = Resolve(contract, operation["responses"]!["200"]!)["content"]!["application/json"]!["examples"]?.AsObject();
            Assert.True(samples is { Count: > 0 }, path + " 缺响应示例");
            // 派生查询与成员检索至少两个示例：典型情形之外再给一个边界（空缺、找不到主管、按 id 批量）。
            if (path.EndsWith("/superiors", StringComparison.Ordinal) || path.EndsWith("/manager", StringComparison.Ordinal) || path.EndsWith("/head", StringComparison.Ordinal) || path == "/api/directory/members")
                Assert.True(samples!.Count >= 2, path + " 只有一个响应示例");
            foreach (var sample in samples!)
            {
                Assert.False(string.IsNullOrWhiteSpace(sample.Value!["summary"]?.GetValue<string>()), path + " 的示例 " + sample.Key + " 缺摘要");
                var value = JsonSerializer.SerializeToElement(sample.Value!["value"]);
                Assert.True(contract.Validate(schema!, value) is [], path + " 的示例 " + sample.Key + " 不符合 Schema：" + string.Join(" | ", contract.Validate(schema!, value)));
                // 反证校验不是空转：根对象多一个字段、少一个必填字段都被拒。
                var extra = JsonNode.Parse(value.GetRawText())!.AsObject(); extra["unexpected"] = 1;
                Assert.NotEmpty(contract.Validate(schema!, JsonSerializer.SerializeToElement(extra)));
                var missing = JsonNode.Parse(value.GetRawText())!.AsObject(); missing.Remove(missing.First().Key);
                Assert.NotEmpty(contract.Validate(schema!, JsonSerializer.SerializeToElement(missing)));
                examples++;
            }
            var statuses = operation["responses"]!.AsObject().Select(response => response.Key).Where(status => status != "200").Order(StringComparer.Ordinal).ToArray();
            Assert.Contains("401", statuses); Assert.Contains("403", statuses);
            Assert.Equal(path.Contains("{id}", StringComparison.Ordinal), statuses.Contains("404"));
            foreach (var status in statuses)
                Assert.Equal(DirectoryAgentContract.ErrorDetailId, contract.ResponseSchema(operation, status, "application/problem+json")?["$ref"]?.GetValue<string>());
        }
        Assert.True(examples >= 17, "响应示例数：" + examples);
    }

    [Fact]
    public void 目录契约Schema与目录记录的序列化逐字段一致_派生查询的空缺与可选字段()
    {
        var contract = DirectoryAgentContract.Load();
        var member = new Membership(A, B, C, "张三", AccountStatuses.Active, true) { Account = "zhangsan", IdentityRevision = 3 };
        // 每条记录把可选字段都填上，序列化后的字段集合必须与契约 Schema 的 properties 完全相同，并通过严格校验。
        (string Schema, object Value)[] samples =
        [
            ("OrganizationType", new OrganizationType("dept", "部门")),
            ("Organization", new Organization(B, "研发中心", "dept", A, $"/{A:D}/{B:D}/", 2, C, D)),
            ("Position", new DirectoryPosition(B, "研发总监", A, C, $"/{C:D}/{B:D}/", 2, true, true, D)),
            ("Group", new DirectoryGroup(A, "项目组")),
            ("Membership", member),
            ("DirectoryMember", new DirectoryMember(member, false)),
            ("MemberRelation", new MemberRelation("position", A, true)),
            ("UserProfile", new UserProfile(A, "张三", "zhangsan", "+86138****5678", "z***@example.test", JsonSerializer.SerializeToElement(new { locale = "zh-CN" }))),
            ("Superior", new DirectorySuperior(1, A, B, member, DirectoryVacancies.Inactive)),
            ("Superiors", new DirectorySuperiors(A, B, C, [new DirectorySuperior(1, A, B, null, DirectoryVacancies.Unassigned)])),
            ("Manager", new DirectoryManager(A, member, B)),
            ("OrganizationHead", new OrganizationHead(A, B, member, DirectoryVacancies.Unassigned, member)),
        ];
        foreach (var (name, value) in samples)
        {
            var json = JsonSerializer.SerializeToElement(value, value.GetType(), IdentityDirectoryEndpoints.Json);
            var schema = contract.Document["components"]!["schemas"]![name]!.AsObject();
            Assert.False(schema["additionalProperties"]!.GetValue<bool>(), name);
            Assert.Equal(schema["properties"]!.AsObject().Select(property => property.Key).Order(StringComparer.Ordinal), json.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
            Assert.True(contract.ValidateComponent(name, json) is [], name + "：" + string.Join(" | ", contract.ValidateComponent(name, json)));
        }
        // 空缺只有两种原因；空值字段省略，不写 null。
        Assert.Equal([DirectoryVacancies.Inactive, DirectoryVacancies.Unassigned], contract.Document["components"]!["schemas"]!["Vacancy"]!["enum"]!.AsArray().Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal));
        var vacant = JsonSerializer.SerializeToElement(new DirectorySuperior(1, A, B, null, DirectoryVacancies.Unassigned), IdentityDirectoryEndpoints.Json);
        Assert.False(vacant.TryGetProperty("responsible", out _));
        Assert.Empty(contract.ValidateComponent("Superior", vacant));
        Assert.NotEmpty(contract.ValidateComponent("Superior", JsonSerializer.SerializeToElement(new { level = 1, position_id = A, organization_id = B, vacancy = "retired" })));
        Assert.NotEmpty(contract.ValidateComponent("Superior", JsonSerializer.SerializeToElement(new { level = 0, position_id = A, organization_id = B })));
        Assert.NotEmpty(contract.ValidateComponent("Membership", JsonSerializer.SerializeToElement(new { id = "not-a-uuid", person_id = B, tenant_id = C, display_name = "张三", status = "active", is_tenant_administrator = false, account = "a", identity_revision = 0 })));
    }

    [Fact]
    public void yue业务组件契约的目录部分与目录契约逐字段一致()
    {
        var identity = DirectoryAgentContract.Load();
        var biz = DirectoryAgentContract.Load("packages/yue/biz/contracts/biz.openapi.json");
        var shared = biz.Operations().Where(operation => operation.Path.StartsWith("/api/directory/", StringComparison.Ordinal)).ToArray();
        Assert.True(shared.Length >= 4, "业务组件契约的目录路径数：" + shared.Length);
        foreach (var (path, method, operation) in shared)
        {
            var counterpart = identity.Document["paths"]?[path]?[method]?.AsObject();
            Assert.True(counterpart is not null, "目录契约里没有 " + method + " " + path);
            Assert.Equal(ParameterShapes(identity, counterpart!), ParameterShapes(biz, operation));
            Assert.Equal(counterpart!["responses"]!.AsObject().Select(response => response.Key).Order(StringComparer.Ordinal), operation["responses"]!.AsObject().Select(response => response.Key).Order(StringComparer.Ordinal));
            Assert.Equal(Shape(identity, identity.ResponseSchema(counterpart!, "200")!), Shape(biz, biz.ResponseSchema(operation, "200")!));
        }
    }

    private static JsonNode Resolve(DirectoryAgentContract contract, JsonNode node) => contract.Resolve(node);

    private static string[] ParameterShapes(DirectoryAgentContract contract, JsonObject operation) =>
        [.. contract.Parameters(operation).Select(parameter =>
        {
            var location = parameter["in"]!.GetValue<string>();
            var style = parameter["style"]?.GetValue<string>() ?? (location == "query" ? "form" : "simple");
            var explode = parameter["explode"]?.GetValue<bool>() ?? style == "form";
            return $"{location}:{parameter["name"]!.GetValue<string>()}:{parameter["required"]?.GetValue<bool>() ?? false}:{style}:{explode}:{Shape(contract, parameter["schema"]!)}";
        }).Order(StringComparer.Ordinal)];

    // 结构化比较用的规范形：展开引用，只保留影响线上形状的关键字（类型、格式、枚举、范围、必填、字段、数组项、缺省值），去掉描述与示例。
    private static readonly string[] Structural = ["type", "format", "enum", "minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems", "default", "pattern"];

    private static string Shape(DirectoryAgentContract contract, JsonNode schema) => Canonical(contract, schema, 0).ToJsonString();

    private static JsonObject Canonical(DirectoryAgentContract contract, JsonNode schema, int depth)
    {
        Assert.True(depth < 32, "Schema 嵌套过深或存在环");
        var resolved = contract.Resolve(schema).AsObject();
        var shape = new JsonObject();
        foreach (var keyword in Structural)
            if (resolved[keyword] is { } value) shape[keyword] = value.DeepClone();
        if (resolved["required"] is JsonArray required) shape["required"] = new JsonArray([.. required.Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal).Select(item => (JsonNode)JsonValue.Create(item))]);
        if (resolved["properties"] is JsonObject properties)
        {
            var fields = new JsonObject();
            foreach (var property in properties.OrderBy(property => property.Key, StringComparer.Ordinal)) fields[property.Key] = Canonical(contract, property.Value!, depth + 1);
            shape["properties"] = fields;
        }
        if (resolved["items"] is { } items) shape["items"] = Canonical(contract, items, depth + 1);
        return shape;
    }
}
