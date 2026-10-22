using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Qian.Common;

// 输入 Schema 不得点名租户字段（PRD 7.18：租户只来自令牌；S1-AGT-004）。以内部源文件编译进 Qian.Surfaces（工具语义校验）与
// Qian.Capabilities.Abstractions（TenantInputContract），须同时链接 SchemaPattern.cs；@qian/surfaces 的 tenantFields 逐条对应，改动须三处同步。
// 名称按 contracts/v0 的 TenantFieldName 整体匹配：tenant、tenantId、tenant_id、tenant-id 的 ASCII 大小写变体。点名的写法有 properties、dependentSchemas、
// dependentRequired 与旧式 dependencies 的名称，required 与 dependentRequired / dependencies 的数组项，以及 patternProperties 的模式：
// 按 SchemaPattern 的可移植子集能匹配任一租户名即违规，超出子集无法判定，同样违规。
// 遍历沿用「未知成员照常往下查」：示例、默认值、常量与枚举是实例数据，不当 Schema 检查；名称表（properties、$defs 等）的条目是名称，叫 default、enum 也照常往下查。
internal static class TenantFieldRule
{
    internal const string NameMessage = "输入 Schema 不允许租户字段。";
    internal const string PatternMessage = "patternProperties 的模式能匹配租户字段名。";
    internal const string UnsupportedMessage = "patternProperties 的模式超出可移植子集，无法判定是否匹配租户字段名。";
    private static readonly string[] Names = ["tenant", "tenantid", "tenant_id", "tenant-id"];
    // 四个名称的全部 ASCII 大小写写法（832 个），模式逐个试配：与 TenantFieldName 的定义逐字对应，不依赖宿主的大小写折叠表。
    private static readonly Lazy<string[]> Spellings = new(() => [.. Names.SelectMany(Variants)]);
    private static readonly HashSet<string> Instances = new(StringComparer.Ordinal) { "example", "examples", "default", "const", "enum" };

    // 违规位置（相对传入 Schema 的段）与原因。
    internal readonly record struct Violation(string[] Path, string Message);

    internal static bool IsName(string name) => Names.Any(candidate => name.Length == candidate.Length && Ascii.EqualsIgnoreCase(name, candidate));

    // 匹配同 JSON Schema，不隐式加锚点；不能匹配任何租户名时返回 null。翻译拒绝与 .NET 自身的拒绝都按无法判定处理。
    internal static string? Pattern(string pattern)
    {
        try
        {
            var regex = SchemaPattern.Compile(pattern);
            return Spellings.Value.Any(regex.IsMatch) ? PatternMessage : null;
        }
        catch (Exception error) when (error is NotSupportedException or ArgumentException or RegexMatchTimeoutException) { return UnsupportedMessage; }
    }

    // 按成员出现顺序报告；名称表先查全部名称，再逐条往下。resolve 不为空时解析 $ref，在引用处继续检查（同一条引用链里重复的引用跳过）。
    internal static List<Violation> Find(JsonElement schema, Func<string, JsonElement>? resolve = null)
    {
        var found = new List<Violation>();
        Walk(schema, [], new HashSet<string>(StringComparer.Ordinal));
        return found;

        void Walk(JsonElement node, string[] path, HashSet<string> active)
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in node.EnumerateArray()) Walk(item, [.. path, Index(index++)], active);
                return;
            }
            if (node.ValueKind != JsonValueKind.Object) return;
            foreach (var member in node.EnumerateObject())
            {
                if (Instances.Contains(member.Name)) continue;
                var value = member.Value;
                if (member.Name == "$ref" && resolve is not null && value.ValueKind == JsonValueKind.String)
                {
                    var reference = value.GetString()!;
                    if (!active.Add(reference)) continue;
                    Walk(resolve(reference), path, active); active.Remove(reference); continue;
                }
                string[] at = [.. path, member.Name];
                switch (member.Name)
                {
                    case "properties" or "dependentSchemas" when value.ValueKind == JsonValueKind.Object: Entries(value, at, active, name => IsName(name) ? NameMessage : null); break;
                    case "patternProperties" when value.ValueKind == JsonValueKind.Object: Entries(value, at, active, Pattern); break;
                    case "$defs" or "definitions" when value.ValueKind == JsonValueKind.Object: Entries(value, at, active, _ => null); break;
                    case "required" when value.ValueKind == JsonValueKind.Array: Items(value, at); break;
                    case "dependentRequired" or "dependencies" when value.ValueKind == JsonValueKind.Object:
                        foreach (var entry in value.EnumerateObject())
                        {
                            if (IsName(entry.Name)) found.Add(new([.. at, entry.Name], NameMessage));
                            if (entry.Value.ValueKind == JsonValueKind.Array) Items(entry.Value, [.. at, entry.Name]);
                        }
                        // 旧式 dependencies 的值也可以是 Schema。
                        foreach (var entry in value.EnumerateObject())
                            if (entry.Value.ValueKind != JsonValueKind.Array) Walk(entry.Value, [.. at, entry.Name], active);
                        break;
                    default: Walk(value, at, active); break;
                }
            }
        }

        void Entries(JsonElement map, string[] at, HashSet<string> active, Func<string, string?> check)
        {
            foreach (var entry in map.EnumerateObject())
                if (check(entry.Name) is { } message) found.Add(new([.. at, entry.Name], message));
            foreach (var entry in map.EnumerateObject()) Walk(entry.Value, [.. at, entry.Name], active);
        }

        void Items(JsonElement items, string[] at)
        {
            var index = 0;
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && IsName(item.GetString()!)) found.Add(new([.. at, Index(index)], NameMessage));
                index++;
            }
        }
    }

    private static string Index(int index) => index.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<string> Variants(string name)
    {
        var letters = name.Count(char.IsAsciiLetter);
        for (var mask = 0; mask < 1 << letters; mask++)
        {
            var chars = name.ToCharArray();
            for (int i = 0, bit = 0; i < chars.Length; i++)
                if (char.IsAsciiLetter(chars[i]) && (mask >> bit++ & 1) == 1) chars[i] = char.ToUpperInvariant(chars[i]);
            yield return new string(chars);
        }
    }
}
