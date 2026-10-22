using System.Globalization;
using System.Text.Json;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using SemanticVersion = SemanticVersioning.Version;

namespace Qian.Files;

// 组件在 metadata/upload-policies.json 声明的上传策略（组件模型 §3：声明式贡献放 metadata/；PRD 7.7 的清单 contributes 按 S1-FILE-004 裁决落在这里）。
// 只能声明本组件的用途：用途的组件段就是存储键里的 plugin_id，冒名会写进别的组件的键前缀。同一组件预装多个版本时取最高版本的声明。
// 结构与 schemas/v2/files-upload-policies.schema.json 一致；用途归属与同文件不重复是 Schema 表达不了的语义，只在这里检查。
// 预装快照按版本不可变，取得 Registry 后编译一次并缓存；宿主没有 Registry（参考宿主、独立模式）时恒为空。
internal sealed class DeclaredUploadPolicies(Func<IRegistryService?> registry)
{
    internal const string MetadataPath = "metadata/upload-policies.json";
    // 与 schemas/v2/files-upload-policies.schema.json 的 maxItems 一致。
    internal const int MaxPolicies = 256;
    private static readonly IReadOnlyDictionary<string, UploadPolicy> None = new Dictionary<string, UploadPolicy>(StringComparer.Ordinal).AsReadOnly();
    private IReadOnlyDictionary<string, UploadPolicy>? _compiled;

    // 声明不合法时抛 INPUT_INVALID，指针形如 <组件 ID>/metadata/upload-policies.json#/0/purpose。
    internal IReadOnlyDictionary<string, UploadPolicy> Policies
    {
        get
        {
            if (Volatile.Read(ref _compiled) is { } compiled) return compiled;
            IRegistryService? source;
            try { source = registry(); }
            catch (ServiceNotReadyException) { return None; }
            if (source is null) return None;
            var result = Compile(source.GetInstalledComponents());
            return Interlocked.CompareExchange(ref _compiled, result, null) ?? result;
        }
    }

    internal static IReadOnlyDictionary<string, UploadPolicy> Compile(IEnumerable<InstalledComponentMetadata> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        var policies = new Dictionary<string, UploadPolicy>(StringComparer.Ordinal);
        foreach (var versions in components.GroupBy(component => component.PluginId, StringComparer.Ordinal))
        {
            var latest = versions.MaxBy(component => new SemanticVersion(component.Version))!;
            if (!latest.Metadata.TryGetValue(MetadataPath, out var document)) continue;
            // 用途按组件 ID 分段，不同组件的声明不会同名；同一文件内重复在 Read 里拒绝。
            foreach (var policy in Read(latest.PluginId, document)) policies.Add(policy.Purpose, policy);
        }
        return policies.AsReadOnly();
    }

    private static List<UploadPolicy> Read(string pluginId, JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Array || document.GetArrayLength() > MaxPolicies) throw Invalid(pluginId, "");
        var policies = new List<UploadPolicy>();
        foreach (var item in document.EnumerateArray())
        {
            var index = policies.Count.ToString(CultureInfo.InvariantCulture);
            UploadPolicy? policy;
            try { policy = item.Deserialize(FilesContractJson.UploadPolicyTypeInfo); }
            catch (JsonException) { throw Invalid(pluginId, index); }
            if (policy is null) throw Invalid(pluginId, index);
            if (FilesConfiguration.ValidatePolicy(policy) is { } member) throw Invalid(pluginId, index + "/" + member);
            if (FilesConfiguration.OwnerOf(policy.Purpose) != pluginId || policies.Any(existing => existing.Purpose == policy.Purpose))
                throw Invalid(pluginId, index + "/purpose");
            policies.Add(policy with { AllowedTypes = Array.AsReadOnly(policy.AllowedTypes.ToArray()) });
        }
        return policies;
    }

    private static QianErrorException Invalid(string pluginId, string member) =>
        new(new QianError(PlatformErrors.InputInvalid) { Pointer = pluginId + "/" + MetadataPath + "#" + (member.Length == 0 ? "" : "/" + member) });
}
