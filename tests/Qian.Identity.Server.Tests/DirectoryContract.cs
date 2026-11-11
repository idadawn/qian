using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Qian.Identity.Server.Tests;

// contracts/identity/v1/directory.openapi.json 的读取与校验（S1-IDP-010 验收 4）。OpenAPI 3.1 的 Schema 对象就是 JSON Schema 2020-12：
// 校验时把 components.schemas 改写成 $defs 拼进同一份文档，ErrorDetail 按 $id 预先登记，格式（uuid 等）严格校验。
internal sealed class DirectoryContract
{
    internal const string RelativePath = "contracts/identity/v1/directory.openapi.json";
    internal const string ErrorDetailId = "https://qian.dev/contracts/v0/error-detail.schema.json";
    private static readonly EvaluationOptions Strict = new() { OutputFormat = OutputFormat.List, RequireFormatValidation = true };
    private readonly BuildOptions _options = new() { SchemaRegistry = new SchemaRegistry() };
    private readonly JsonObject _definitions;
    private int _built;

    private DirectoryContract(JsonNode document)
    {
        Document = document;
        _ = JsonSchema.FromText(File.ReadAllText(Path.Combine(Root, "contracts/v0/error-detail.schema.json")), _options);
        _definitions = Rewrite(document["components"]?["schemas"]?.DeepClone() ?? new JsonObject()).AsObject();
    }

    internal static string Root { get; } = FindRoot();
    internal JsonNode Document { get; }

    internal static DirectoryContract Load(string relativePath = RelativePath) => new(JsonNode.Parse(File.ReadAllText(Path.Combine(Root, relativePath)))!);
    internal static DirectoryContract From(JsonNode document) => new(document);

    internal IEnumerable<(string Path, string Method, JsonObject Operation)> Operations() =>
        Document["paths"]!.AsObject().SelectMany(path => path.Value!.AsObject().Select(operation => (path.Key, operation.Key, operation.Value!.AsObject())));

    // 解析文档内的 $ref（#/components/...），一直跟到非引用节点。
    internal JsonNode Resolve(JsonNode node)
    {
        for (var hops = 0; node is JsonObject { } item && item["$ref"]?.GetValue<string>() is { } reference && reference.StartsWith("#/", StringComparison.Ordinal); hops++)
        {
            Assert.True(hops < 16, "引用链过长：" + reference);
            node = reference[2..].Split('/').Aggregate(Document, (current, segment) => current[segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)]
                ?? throw new InvalidOperationException("引用不存在：" + reference));
        }
        return node;
    }

    internal IEnumerable<JsonObject> Parameters(JsonObject operation) =>
        (operation["parameters"]?.AsArray() ?? []).Select(parameter => Resolve(parameter!).AsObject());

    internal JsonNode? ResponseSchema(JsonObject operation, string status, string mediaType = "application/json") =>
        operation["responses"]?[status] is { } response ? Resolve(response)["content"]?[mediaType]?["schema"] : null;

    // 按契约里的某个 Schema 节点校验实例；返回空数组即通过，否则给出每处错误的实例位置。
    internal string[] Validate(JsonNode schema, JsonElement instance)
    {
        var root = Rewrite(schema.DeepClone()).AsObject();
        root["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        root["$id"] = $"https://qian.dev/tests/identity/directory-contract/{++_built}.json";
        root["$defs"] = _definitions.DeepClone();
        var result = JsonSchema.FromText(root.ToJsonString(), _options).Evaluate(instance, Strict);
        return result.IsValid ? [] : (result.Details ?? []).Where(detail => detail.Errors is { Count: > 0 })
            .Select(detail => detail.InstanceLocation + ": " + string.Join("; ", detail.Errors!.Values)).DefaultIfEmpty("invalid").ToArray();
    }

    internal string[] ValidateComponent(string name, JsonElement instance) => Validate(new JsonObject { ["$ref"] = "#/components/schemas/" + name }, instance);

    private static JsonNode Rewrite(JsonNode node)
    {
        switch (node)
        {
            case JsonObject item:
                foreach (var property in item.ToArray())
                    if (property.Key == "$ref" && property.Value?.GetValue<string>() is { } reference && reference.StartsWith("#/components/schemas/", StringComparison.Ordinal))
                        item["$ref"] = "#/$defs/" + reference["#/components/schemas/".Length..];
                    else if (property.Value is not null) Rewrite(property.Value);
                break;
            case JsonArray array:
                foreach (var element in array) if (element is not null) Rewrite(element);
                break;
        }
        return node;
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录（Qian.slnx）。");
    }
}
