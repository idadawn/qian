using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qian.Plugin.Sdk;

/// <summary>源生成与实际 HTTP 描述共用的 JSON Schema 引用处理。</summary>
public static class JsonSchemaReferences
{
    /// <summary>嵌入 OpenAPI components 时重定位本地 Schema 引用，保持实例 examples/default 原样。</summary>
    public static JsonNode RebaseToComponent(JsonElement schema, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))) throw new ArgumentException("Schema 组件名无效。", nameof(key));
        var node = JsonNode.Parse(schema.GetRawText())!;
        // JsonTypeInfo 的递归引用以原 Schema 根为基准；嵌入 components 后必须重定位。
        Rewrite(node, "#/components/schemas/" + key);
        return node;
    }
    private static void Rewrite(JsonNode node, string root)
    {
        if (node is not JsonObject value) return;
        if (value["$ref"] is JsonValue reference && reference.TryGetValue<string>(out var text) && (text == "#" || text.StartsWith("#/", StringComparison.Ordinal)))
            value["$ref"] = root + text[1..];
        foreach (var property in value.ToArray())
        {
            // 只访问 Schema 位置，不能改写 default/examples 或业务数据中的同名成员。
            if (property.Key is "properties" or "patternProperties" or "$defs" or "definitions" or "dependentSchemas" && property.Value is JsonObject map)
            { foreach (var child in map) if (child.Value is not null) Rewrite(child.Value, root); }
            else if (property.Key is "allOf" or "anyOf" or "oneOf" or "prefixItems" && property.Value is JsonArray list)
            { foreach (var child in list) if (child is not null) Rewrite(child, root); }
            else if (property.Key is "items" or "additionalProperties" or "unevaluatedProperties" or "unevaluatedItems" or "contains" or "propertyNames" or "not" or "if" or "then" or "else" && property.Value is not null)
                Rewrite(property.Value, root);
        }
    }
}
