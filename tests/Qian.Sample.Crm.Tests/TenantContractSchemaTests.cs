using System.Text.Json.Nodes;

namespace Qian.Sample.Crm.Tests;

public sealed class TenantContractSchemaTests
{
    private static readonly HashSet<string> TenantNames = new(StringComparer.OrdinalIgnoreCase) { "tenant", "tenantId", "tenant_id", "tenant-id" };
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
                    tested++;
                }
        Assert.Equal(4, tested); // 创建、PUT 编辑、激活、PUT 设置。
    }

    internal static IReadOnlyList<string> TenantFields(JsonNode document)
    {
        var found = new List<string>();
        foreach (var path in document["paths"]!.AsObject())
            foreach (var operation in path.Value!.AsObject())
                if (operation.Value?["requestBody"] is { } body)
                    Visit(body, path.Key + "/" + operation.Key + "/requestBody", []);
        return found;
        void Visit(JsonNode? node, string pointer, HashSet<string> seen)
        {
            if (node is JsonArray array)
                for (var index = 0; index < array.Count; index++) Visit(array[index], pointer + "/" + index, seen);
            if (node is not JsonObject obj) return;
            if (obj["$ref"] is { } reference)
            {
                var id = reference.GetValue<string>();
                if (seen.Add(id)) Visit(Resolve(document, obj), id, seen);
            }
            if (obj["properties"] is JsonObject properties)
                foreach (var field in properties)
                    if (TenantNames.Contains(field.Key)) found.Add(pointer + "/properties/" + field.Key);
            foreach (var item in obj)
                if (item.Key is not ("$ref" or "example" or "examples" or "default")) Visit(item.Value, pointer + "/" + item.Key, seen);
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
