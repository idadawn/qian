using System.Text.Json.Nodes;

namespace Qian.Sample.Crm.Tests;

public sealed class TenantContractSchemaTests
{
    [Theory]
    [InlineData("tenantId", "query")]
    [InlineData("tenant_id", "path")]
    [InlineData("Tenant-ID", "query")]
    public void 参数名称和嵌套Schema同样拒绝租户输入(string field, string location)
    {
        var document = JsonNode.Parse("""{"paths":{"/probe":{"get":{"parameters":[]}}},"components":{"schemas":{"Admin":{"type":"object","properties":{}}}}}""")!;
        var parameters = document["paths"]!["/probe"]!["get"]!["parameters"]!.AsArray();
        parameters.Add(new JsonObject { ["name"] = field, ["in"] = location, ["schema"] = new JsonObject { ["type"] = "string" } });
        Assert.Contains(TenantFields(document), value => value.EndsWith("/" + field, StringComparison.Ordinal));
        parameters[0]!["name"] = "administrator";
        parameters[0]!["schema"] = new JsonObject { ["$ref"] = "#/components/schemas/Admin" };
        document["components"]!["schemas"]!["Admin"]!["properties"]![field] = new JsonObject { ["type"] = "string" };
        Assert.Contains(TenantFields(document), value => value.EndsWith("/properties/" + field, StringComparison.Ordinal));
        document["components"]!["schemas"]!["Admin"]!["properties"]!.AsObject().Remove(field);
        document["components"]!["schemas"]!["Admin"]!["examples"] = new JsonArray(new JsonObject { ["properties"] = new JsonObject { [field] = "literal" } });
        Assert.Empty(TenantFields(document));
    }
    [Theory]
    [InlineData("tenantId")]
    [InlineData("tenant_id")]
    [InlineData("tenant")]
    [InlineData("Tenant-ID")]
    public void 契约测试解析ref并拒绝任一请求体中的租户字段(string field)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(root!.FullName, "plugins/qian.tenancy/contracts/http-v1.openapi.json")))!;
        Assert.Empty(TenantFields(original));
        var tested = 0;
        foreach (var path in original["paths"]!.AsObject())
            foreach (var operation in path.Value!.AsObject())
                if (operation.Value?["requestBody"] is not null)
                {
                    var candidate = original.DeepClone();
                    var schema = Resolve(candidate, candidate["paths"]![path.Key]![operation.Key]!["requestBody"]!["content"]!["application/json"]!["schema"]!);
                    schema["properties"]![field] = new JsonObject { ["type"] = "string" };
                    Assert.Contains(TenantFields(candidate), pointer => pointer.EndsWith("/" + field, StringComparison.Ordinal));
                    schema["properties"]!.AsObject().Remove(field);
                    schema["properties"]!["nested"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { [field] = new JsonObject { ["type"] = "string" } } };
                    Assert.Contains(TenantFields(candidate), pointer => pointer.EndsWith("/nested/properties/" + field, StringComparison.Ordinal));
                    tested++;
                }
        Assert.Equal(4, tested); // 创建、PUT 编辑、激活、PUT 设置。
    }

    internal static IReadOnlyList<string> TenantFields(JsonNode document)
    {
        var found = new List<string>();
        var root = System.Text.Json.JsonSerializer.SerializeToElement(document);
        foreach (var path in document["paths"]!.AsObject())
            foreach (var operation in path.Value!.AsObject())
            {
                if (operation.Value?["requestBody"] is { } body) Scan(body, path.Key + "/" + operation.Key + "/requestBody");
                if (operation.Value?["parameters"] is JsonArray parameters)
                    foreach (var parameter in parameters)
                    {
                        var resolved = Resolve(document, parameter!);
                        var name = resolved["name"]!.GetValue<string>();
                        if (Qian.Capabilities.TenantInputContract.IsTenantName(name)) found.Add(path.Key + "/" + operation.Key + "/parameters/" + name);
                        if (resolved["schema"] is { } schema) Scan(schema, path.Key + "/" + operation.Key + "/parameters/" + name);
                    }
            }
        return found;
        void Scan(JsonNode node, string prefix)
        {
            foreach (var field in Qian.Capabilities.TenantInputContract.FindFields(System.Text.Json.JsonSerializer.SerializeToElement(node), root))
                found.Add(prefix + "/" + string.Join('/', field));
        }
    }

    internal static JsonNode Resolve(JsonNode document, JsonNode schema)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (schema is JsonObject obj && obj["$ref"] is { } reference)
        {
            var path = reference.GetValue<string>(); Assert.StartsWith("#/", path);
            Assert.True(seen.Add(path), "引用循环不能解析成请求 schema。");
            schema = document;
            foreach (var segment in path[2..].Split('/')) schema = schema[segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)]!;
        }
        return schema;
    }
}
