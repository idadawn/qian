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
    // 回执没有保存响应体时同键请求不可回放；reason 取值即回执表 withheld 列的取值。
    private const string NotReplayable = "同键请求已执行成功但回执没有保存响应体时返回 409 CAPABILITY_REPLAY_UNAVAILABLE（reason 为 " +
        CapabilitiesDbContext.WithheldTooLarge + "：响应超过组件配置 receiptMaxBytes；" + CapabilitiesDbContext.WithheldClassification +
        "：能力的 dataClassification 不是 public 或 internal），不再执行，应通过查询确认结果，换用新键会再执行一次";

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
            // 只读能力忽略幂等键，不声明该参数与回放响应头。
            var keyed = entry.GetProperty("sideEffect").GetString() != "read";
            var success = new JsonObject { ["description"] = keyed ? "执行成功，或同一幂等键的首次成功响应回放" : "执行成功", ["content"] = Content(key + "Output") };
            if (keyed)
                success["headers"] = new JsonObject
                {
                    ["Idempotent-Replayed"] = new JsonObject
                    { ["description"] = "回放首次成功响应时为 true；本次执行时不出现。", ["schema"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("true") } }
                };
            var operation = new JsonObject
            {
                ["operationId"] = key, ["description"] = entry.GetProperty("description").GetString(),
                ["x-qian-capability"] = id, ["x-qian-required-scopes"] = JsonNode.Parse(entry.GetProperty("requiredScopes").GetRawText()),
                ["requestBody"] = new JsonObject { ["required"] = true, ["content"] = Content(key + "Input") },
                ["responses"] = new JsonObject
                {
                    ["200"] = success,
                    ["default"] = new JsonObject
                    {
                        ["description"] = "ErrorDetail 结构化错误：输入不符 400 INPUT_INVALID（errors 带 JSON Pointer），不可见或不存在 404 NOT_FOUND" +
                            (keyed ? "，幂等键冲突 409 CONFLICT，" + NotReplayable : "")
                    }
                },
                ["security"] = new JsonArray(new JsonObject { ["bearerAuth"] = new JsonArray() })
            };
            if (keyed)
                operation["parameters"] = new JsonArray(new JsonObject
                {
                    ["name"] = "Idempotency-Key", ["in"] = "header", ["required"] = false,
                    ["description"] = "可选幂等键（UUID）：同一主体、同一能力、同一键在回放窗口内重复请求时返回首次成功响应且不再执行；" +
                        "同键请求仍在执行或键相同而输入不同时返回 409 CONFLICT；" + NotReplayable + "。",
                    ["schema"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" }
                });
            paths["/api/capabilities/" + id + "/invoke"] = new JsonObject { ["post"] = operation };
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
