using System.Text.Json;
using Qian.Common;

namespace Qian.Capabilities;

/// <summary>能力与 HTTP 输入共用的租户字段规则；字段必须来自可信上下文。</summary>
public static class TenantInputContract
{
    /// <summary>按 contracts/v0 的 TenantFieldName 判断参数或属性名：tenant、tenantId、tenant_id、tenant-id，整体匹配、ASCII 大小写不敏感。</summary>
    public static bool IsTenantName(string name) => TenantFieldRule.IsName(name);
    /// <summary>
    /// 返回全部点名租户字段的位置段：properties、dependentSchemas、dependentRequired 的名称，required 等数组项，propertyNames 用 const / enum 列出的名称，以及能匹配租户名或无法判定的 patternProperties 模式
    /// （与表面校验器同一规则，见 packages/Common/TenantFieldRule.cs）。解析文档内引用，在引用处继续检查；途经实例数据或停在名称表本身上的引用返回引用本身的位置，
    /// 不跟进去。跳过示例、默认值与常量实例。
    /// </summary>
    public static IReadOnlyList<string[]> FindFields(JsonElement schema, JsonElement? document = null)
    {
        var root = document ?? schema;
        return TenantFieldRule.Find(schema, Resolve).Select(violation => violation.Path).ToList().AsReadOnly();
        JsonElement Resolve(string reference)
        {
            if (!reference.StartsWith("#/", StringComparison.Ordinal)) throw new ArgumentException("输入契约只允许文档内引用。", nameof(schema));
            var resolved = root;
            foreach (var segment in reference[2..].Split('/')) resolved = resolved.GetProperty(segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal));
            return resolved;
        }
    }
}
