using System.Text.Json;
using System.Text.RegularExpressions;
using Qian.Runtime.Errors;

namespace Qian.Files;

internal sealed record FilesSettings(string Kind, JsonElement Storage, long QuotaPerTenantBytes, IReadOnlyList<UploadPolicy> Policies);

// 组件配置（plugins/qian.files/schemas/config.schema.json）：storage 必填，kind 按名称选用 Backends 里的后端；quota 缺省 10 GiB；policies 为平台默认上传策略。
internal static partial class FilesConfiguration
{
    internal const long DefaultQuotaBytes = 10L * 1024 * 1024 * 1024;
    internal const long MaxPolicySize = 1L << 40;

    internal static FilesSettings Read(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object) throw Invalid("");
        string? kind = null; JsonElement storage = default; var quota = DefaultQuotaBytes; var policies = new List<UploadPolicy>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in config.EnumerateObject())
        {
            if (!names.Add(property.Name)) throw Invalid(property.Name);
            switch (property.Name)
            {
                case "storage" when property.Value.ValueKind == JsonValueKind.Object:
                    storage = property.Value.Clone();
                    kind = property.Value.TryGetProperty("kind", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                    if (kind is null) throw Invalid("storage/kind");
                    break;
                case "quota" when property.Value.ValueKind == JsonValueKind.Object:
                    foreach (var item in property.Value.EnumerateObject())
                        quota = item.Name == "perTenantBytes" && item.Value.ValueKind == JsonValueKind.Number && item.Value.TryGetInt64(out var bytes) && bytes >= 0
                            ? bytes : throw Invalid("quota/" + item.Name);
                    break;
                case "policies" when property.Value.ValueKind == JsonValueKind.Array:
                    var index = 0;
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        UploadPolicy? policy;
                        try { policy = item.Deserialize(FilesContractJson.UploadPolicyTypeInfo); }
                        catch (JsonException) { throw Invalid("policies/" + index); }
                        if (policy is null || ValidatePolicy(policy) is not null || policies.Any(existing => existing.Purpose == policy.Purpose)) throw Invalid("policies/" + index);
                        policies.Add(policy); index++;
                    }
                    break;
                default:
                    throw Invalid(property.Name);
            }
        }
        if (kind is null) throw Invalid("storage");
        return new FilesSettings(kind, storage, quota, policies.AsReadOnly());
    }

    // 返回第一个不合法字段名；合法返回 null。用途形如 <组件 ID>/<名称>，组件 ID 进入存储键。
    internal static string? ValidatePolicy(UploadPolicy policy)
    {
        if (policy.Purpose is null || policy.Purpose.Length > 128 || !Purpose().IsMatch(policy.Purpose)) return "purpose";
        if (policy.AllowedTypes is null || policy.AllowedTypes.Count is 0 or > 64 ||
            policy.AllowedTypes.Any(type => type is null || (ContentSniffer.Normalize(type) != type && !(type.EndsWith("/*", StringComparison.Ordinal) && ContentSniffer.Normalize(type[..^1] + "x") == type[..^1] + "x"))))
            return "allowedTypes";
        if (policy.MaxSize is < 1 or > MaxPolicySize) return "maxSize";
        if (policy.MaxCount is < 1) return "maxCount";
        return null;
    }

    internal static string OwnerOf(string purpose) => purpose.Split('/', 2)[0];

    private static QianErrorException Invalid(string member) =>
        new(new QianError(PlatformErrors.InputInvalid) { Pointer = member.Length == 0 ? "#" : "#/" + member });

    [GeneratedRegex(@"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+/[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Purpose();
}
