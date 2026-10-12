using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Qian.Plugin.Sdk;

namespace Qian.Capabilities;

// 来自可执行绑定的实际 HTTP 入口，不从预装清单推测可用路径。
internal static class CapabilityOpenApi
{
    internal static JsonElement Build(IEnumerable<JsonElement> entries)
    {
        var paths = new JsonObject(); var schemas = new JsonObject();
        foreach (var entry in entries)
        {
            var id = entry.GetProperty("id").GetString()!;
            var key = "C" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
            schemas[key + "Input"] = JsonSchemaReferences.RebaseToComponent(entry.GetProperty("inputSchema"), key + "Input");
            var result = entry.TryGetProperty("outputSchema", out var output) ? JsonSchemaReferences.RebaseToComponent(output, key + "Output") : JsonValue.Create(true)!;
            schemas[key + "Output"] = result;
            paths["/api/capabilities/" + id + "/invoke"] = new JsonObject
            {
                ["post"] = new JsonObject
                {
                    ["operationId"] = key, ["description"] = entry.GetProperty("description").GetString(),
                    ["x-qian-capability"] = id, ["x-qian-required-scopes"] = JsonNode.Parse(entry.GetProperty("requiredScopes").GetRawText()),
                    ["requestBody"] = new JsonObject { ["required"] = true, ["content"] = Content(key + "Input") },
                    ["responses"] = new JsonObject
                    {
                        ["200"] = new JsonObject { ["description"] = "执行成功", ["content"] = Content(key + "Output") },
                        ["default"] = new JsonObject { ["description"] = "ErrorDetail 结构化错误" }
                    },
                    ["security"] = new JsonArray(new JsonObject { ["bearerAuth"] = new JsonArray() })
                }
            };
        }
        var document = new JsonObject
        {
            ["openapi"] = "3.1.1", ["info"] = new JsonObject { ["title"] = "Qian 能力接口", ["version"] = "1.0.0" },
            ["paths"] = paths, ["components"] = new JsonObject
            {
                ["schemas"] = schemas,
                ["securitySchemes"] = new JsonObject { ["bearerAuth"] = new JsonObject { ["type"] = "http", ["scheme"] = "bearer", ["bearerFormat"] = "JWT" } }
            }
        };
        var text = document.ToJsonString();
        var parsed = OpenApiDocument.Parse(text, "json");
        if (parsed.Document is null || parsed.Diagnostic?.Errors.Count > 0) throw new InvalidOperationException("生成的能力 OpenAPI 契约无效。");
        using var json = JsonDocument.Parse(text); return json.RootElement.Clone();
    }
    private static JsonObject Content(string schema) => new()
    {
        ["application/json"] = new JsonObject { ["schema"] = new JsonObject { ["$ref"] = "#/components/schemas/" + schema } }
    };
}
