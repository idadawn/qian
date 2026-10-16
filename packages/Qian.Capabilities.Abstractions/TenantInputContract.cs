using System.Globalization;
using System.Text.Json;

namespace Qian.Capabilities;

/// <summary>能力与 HTTP 输入共用的租户字段规则；字段必须来自可信上下文。</summary>
public static class TenantInputContract
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase) { "tenant", "tenantId", "tenant_id", "tenant-id" };
    /// <summary>按同一大小写不敏感词表判断参数或属性名。</summary>
    public static bool IsTenantName(string name) => Names.Contains(name);
    /// <summary>返回全部违规字段的位置段；解析文档内引用，跳过示例、默认值与常量实例。</summary>
    public static IReadOnlyList<string[]> FindFields(JsonElement schema, JsonElement? document = null)
    {
        var found = new List<string[]>();
        Visit(schema, [], new HashSet<string>(StringComparer.Ordinal));
        return found.AsReadOnly();
        void Visit(JsonElement node, string[] path, HashSet<string> active)
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in node.EnumerateArray()) Visit(item, [.. path, (index++).ToString(CultureInfo.InvariantCulture)], active);
                return;
            }
            if (node.ValueKind != JsonValueKind.Object) return;
            foreach (var property in node.EnumerateObject())
            {
                if (property.Name is "example" or "examples" or "default" or "const" or "enum") continue;
                if (property.Name == "$ref")
                {
                    var reference = property.Value.GetString()!;
                    if (!reference.StartsWith("#/", StringComparison.Ordinal)) throw new ArgumentException("输入契约只允许文档内引用。", nameof(schema));
                    if (!active.Add(reference)) continue;
                    var resolved = document ?? schema;
                    foreach (var segment in reference[2..].Split('/')) resolved = resolved.GetProperty(segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal));
                    Visit(resolved, path, active); active.Remove(reference); continue;
                }
                if (property.Name == "properties" && property.Value.ValueKind == JsonValueKind.Object)
                    foreach (var field in property.Value.EnumerateObject())
                        if (IsTenantName(field.Name)) found.Add([.. path, "properties", field.Name]);
                Visit(property.Value, [.. path, property.Name], active);
            }
        }
    }
}
