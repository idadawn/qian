using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qian.Config.Apollo;

// 规则只含非敏感、平台级的放量策略；租户 ID 是定向条件，不是租户配置存储。
internal sealed record FeatureFlagRule(bool Default, FrozenSet<Guid> Tenants, int Percentage)
{
    internal bool Evaluate(string name, Guid tenantId)
    {
        if (Tenants.Contains(tenantId)) return true;
        if (Percentage > 0)
        {
            // 明确字节协议，不用进程随机化的 string.GetHashCode。增加百分比只扩大同一集合。
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name + "\n" + tenantId.ToString("D")));
            if (BinaryPrimitives.ReadUInt64BigEndian(hash) % 10_000 < (ulong)Percentage * 100) return true;
        }
        return Default;
    }
}

internal static class FeatureFlagRules
{
    internal const string Namespace = "qian.common.flags";
    internal const string Prefix = "flag.";
    internal const int MaximumRules = 1024;
    internal const int MaximumTenants = 256;

    internal static bool ValidName(string? name) => name is { Length: > 0 and <= 100 } &&
        name[0] is >= 'a' and <= 'z' && name.All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-' or '_');

    internal static FrozenDictionary<string, FeatureFlagRule> Read(IEnumerable<KeyValuePair<string, string?>> values)
    {
        var result = new Dictionary<string, FeatureFlagRule>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var name = key[Prefix.Length..];
            if (!key.StartsWith(Prefix, StringComparison.Ordinal) || !ValidName(name) || value is null ||
                result.Count >= MaximumRules || !result.TryAdd(name, Parse(value))) throw Invalid();
        }
        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }

    internal static bool Validate(IReadOnlyDictionary<string, string?> values)
    {
        try { _ = Read(values); return true; }
        catch (InvalidDataException) { return false; }
    }

    private static FeatureFlagRule Parse(string json)
    {
        if (json.Length is 0 or > 16_384) throw Invalid();
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw Invalid();
            bool? fallback = null;
            var percentage = 0;
            var tenants = new HashSet<Guid>();
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!fields.Add(property.Name)) throw Invalid();
                switch (property.Name)
                {
                    case "default" when property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                        fallback = property.Value.GetBoolean(); break;
                    case "percentage" when property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var amount) && amount is >= 0 and <= 100:
                        percentage = amount; break;
                    case "tenants" when property.Value.ValueKind == JsonValueKind.Array:
                        foreach (var value in property.Value.EnumerateArray())
                        {
                            if (value.ValueKind != JsonValueKind.String || !Guid.TryParseExact(value.GetString(), "D", out var tenant) ||
                                tenant == Guid.Empty || tenants.Count >= MaximumTenants || !tenants.Add(tenant)) throw Invalid();
                        }
                        break;
                    default: throw Invalid();
                }
            }
            if (fallback is null) throw Invalid();
            return new(fallback.Value, tenants.ToFrozenSet(), percentage);
        }
        catch (JsonException) { throw Invalid(); }
    }

    // JSON 异常可能包含原值、字节片段；只保留固定诊断，绝不把原异常挂到 InnerException。
    private static InvalidDataException Invalid() => new("特性开关规则不满足固定契约。");
}
