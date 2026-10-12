using System.Text.Json;

namespace Qian.Data;

internal sealed record DataConnectionBinding(Type ContextType, string? Reference);

internal static class DataConfiguration
{
    internal static string? ReadConnection(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object)
            throw new QianDataMisconfiguredException("Data 配置必须为对象。");
        var names = new HashSet<string>(StringComparer.Ordinal);
        string? reference = null;
        foreach (var property in config.EnumerateObject())
        {
            if (!names.Add(property.Name)) throw new QianDataMisconfiguredException("Data 配置不能重复声明字段。");
            switch (property.Name)
            {
                case "dialect" when property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() == "pg":
                    break;
                case "connection" when property.Value.ValueKind == JsonValueKind.String:
                    reference = property.Value.GetString();
                    ValidateReference(reference!);
                    break;
                case "rls" when property.Value.ValueKind == JsonValueKind.Object:
                    var entries = property.Value.EnumerateObject().ToArray();
                    if (entries.Length != 1 || entries[0].Name != "enforce" || entries[0].Value.ValueKind != JsonValueKind.True)
                        throw new QianDataMisconfiguredException("RLS 必须保持 enforce=true。");
                    break;
                default:
                    throw new QianDataMisconfiguredException("Data 配置包含未知字段、无效方言或不受支持的值。");
            }
        }
        return reference;
    }

    internal static void ValidateReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith("secretRef://", StringComparison.Ordinal) ||
            reference.Any(char.IsWhiteSpace) || !Uri.TryCreate(reference, UriKind.Absolute, out var uri) ||
            uri.Host is not ("platform" or "dev") || uri.AbsolutePath.Length <= 1 ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || !uri.IsDefaultPort)
            throw new QianDataMisconfiguredException("S1 业务库连接只能使用宿主登记的 platform/dev secretRef，租户自带业务库不在本期范围内。");
    }
}
