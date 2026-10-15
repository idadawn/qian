using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qian.Plugin.Sdk;

/// <summary>源生成与实际 HTTP 描述共用的 JSON Schema 引用处理。</summary>
public static class JsonSchemaReferences
{
    /// <summary>嵌入 OpenAPI components 时重定位本地引用并等价展开布尔 Schema，保持实例 examples/default 原样。</summary>
    public static JsonNode RebaseToComponent(JsonElement schema, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))) throw new ArgumentException("Schema 组件名无效。", nameof(key));
        return Rewrite(JsonNode.Parse(schema.GetRawText())!, "#/components/schemas/" + key);
    }
    private static JsonNode Rewrite(JsonNode node, string root)
    {
        // JsonElement 属性会导出 true；锁定的 OpenAPI 读取器不支持嵌套布尔 Schema，等价改为对象形状。
        if (node is JsonValue boolean && boolean.TryGetValue<bool>(out var allowed))
            return allowed ? new JsonObject() : new JsonObject { ["not"] = new JsonObject() };
        if (node is not JsonObject value) return node;
        if (value["$ref"] is JsonValue reference && reference.TryGetValue<string>(out var text) && (text == "#" || text.StartsWith("#/", StringComparison.Ordinal)))
            value["$ref"] = root + text[1..];
        foreach (var property in value.ToArray())
        {
            // 只访问 Schema 位置，不能改写 default/examples 或业务数据中的同名成员。
            if (property.Key is "properties" or "patternProperties" or "$defs" or "definitions" or "dependentSchemas" && property.Value is JsonObject map)
            {
                foreach (var child in map.ToArray()) if (child.Value is not null)
                { var changed = Rewrite(child.Value, root); if (!ReferenceEquals(changed, child.Value)) map[child.Key] = changed; }
            }
            else if (property.Key is "allOf" or "anyOf" or "oneOf" or "prefixItems" && property.Value is JsonArray list)
            {
                for (var i = 0; i < list.Count; i++) if (list[i] is { } child)
                { var changed = Rewrite(child, root); if (!ReferenceEquals(changed, child)) list[i] = changed; }
            }
            else if (property.Key is "items" or "additionalProperties" or "unevaluatedProperties" or "unevaluatedItems" or "contains" or "propertyNames" or "not" or "if" or "then" or "else" && property.Value is not null)
            {
                // additionalProperties 的布尔形式被 OpenAPI 读取器原生支持，保留已有发布形状。
                if (property.Key == "additionalProperties" && property.Value is JsonValue flag && flag.TryGetValue<bool>(out _)) continue;
                var changed = Rewrite(property.Value, root); if (!ReferenceEquals(changed, property.Value)) value[property.Key] = changed;
            }
        }
        return node;
    }
}
