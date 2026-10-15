using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle;

// 安装前从制品读出的清单要点；完整的静态校验与登记仍由 Registry 完成。
internal sealed partial class LifecycleArtifact
{
    internal const string ManifestPath = ".qian-plugin/plugin.json";
    internal const string Declarative = "declarative";
    internal required string Digest { get; init; }
    internal required string PluginId { get; init; }
    internal required string Version { get; init; }
    internal required string Form { get; init; }
    internal required bool Required { get; init; }
    internal string? Image { get; init; }
    internal int Port { get; init; } = 8080;
    internal string HealthPath { get; init; } = "/health";

    // D4 目录摘要：UTF-8、LF、序数路径排序的「路径\t字节数\tSHA-256」清单再取 SHA-256，与 CLI 和 Registry 同一算法。
    internal static string ComputeDigest(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files)
    {
        var inventory = string.Concat(files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file =>
            file.Key + "\t" + file.Value.Length.ToString(CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(SHA256.HashData(file.Value.Span)) + "\n"));
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory)));
    }

    internal static LifecycleArtifact Read(PluginArtifact artifact)
    {
        if (!TrustRoots.Fingerprint(artifact.Digest)) throw Invalid("artifact.json#/digest", "制品摘要必须是 sha256:<64 位小写十六进制>。");
        if (artifact.Files.Count is 0 or > 10000) throw Invalid("artifact.json#/files", "制品文件数量无效。");
        if (ComputeDigest(artifact.Files) != artifact.Digest) throw Invalid("artifact.json#/digest", "制品内容与声明摘要不一致。");
        if (!artifact.Files.TryGetValue(ManifestPath, out var bytes)) throw Invalid(ManifestPath + "#", "制品缺少组件清单。");
        JsonElement manifest;
        try { manifest = JsonSerializer.Deserialize<JsonElement>(bytes.Span); }
        catch (JsonException) { throw Invalid(ManifestPath + "#", "组件清单不是合法 JSON。"); }
        if (manifest.ValueKind != JsonValueKind.Object || !Text(manifest, "id", out var id) || !PluginIdPattern().IsMatch(id) || id.Length > 128)
            throw Invalid(ManifestPath + "#/id", "组件标识无效。");
        if (!Text(manifest, "version", out var version) || version.Length > 200 || !SemanticVersioning.Version.TryParse(version, out _))
            throw Invalid(ManifestPath + "#/version", "组件版本必须是语义化版本。");
        var required = manifest.TryGetProperty("policy", out var policy) && policy.ValueKind == JsonValueKind.Object &&
            policy.TryGetProperty("required", out var flag) && flag.ValueKind == JsonValueKind.True;
        if (!manifest.TryGetProperty("runtime", out var runtime) || runtime.ValueKind != JsonValueKind.Object || !Text(runtime, "form", out var form))
            return new LifecycleArtifact { Digest = artifact.Digest, PluginId = id, Version = version, Form = Declarative, Required = required };
        if (form != "service") return new LifecycleArtifact { Digest = artifact.Digest, PluginId = id, Version = version, Form = form, Required = required };
        // service 形态：runtime.image 只允许 digest 引用；runtime.service.port / health 缺省 8080、/health。
        if (!Text(runtime, "image", out var image) || !ImagePattern().IsMatch(image)) throw Invalid(ManifestPath + "#/runtime/image", "service 组件必须声明 oci://…@sha256: 镜像。");
        var port = 8080; var health = "/health";
        if (runtime.TryGetProperty("service", out var service) && service.ValueKind == JsonValueKind.Object)
        {
            if (service.TryGetProperty("port", out var value))
                port = value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number is > 0 and <= 65535
                    ? number : throw Invalid(ManifestPath + "#/runtime/service/port", "端口必须是 1–65535 的整数。");
            if (service.TryGetProperty("health", out value))
                health = value.ValueKind == JsonValueKind.String && value.GetString() is { } path && HealthPattern().IsMatch(path)
                    ? path : throw Invalid(ManifestPath + "#/runtime/service/health", "健康检查路径必须以 / 开头且不含查询。");
        }
        return new LifecycleArtifact
        {
            Digest = artifact.Digest, PluginId = id, Version = version, Form = form, Required = required, Image = image["oci://".Length..], Port = port, HealthPath = health
        };
    }

    private static bool Text(JsonElement element, string name, out string value)
    {
        value = element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString()! : "";
        return value.Length != 0;
    }

    internal static QianErrorException Invalid(string pointer, string reason) => new(new QianError(PlatformErrors.InputInvalid)
    { Errors = [new QianFieldError(LifecycleErrors.Invalid, pointer, new Dictionary<string, string> { ["reason"] = reason })] });

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$", RegexOptions.CultureInvariant)] internal static partial Regex PluginIdPattern();
    [GeneratedRegex("^oci://[A-Za-z0-9.-]+(:[0-9]+)?(/[a-z0-9._-]+)+@sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)] private static partial Regex ImagePattern();
    [GeneratedRegex("^/[A-Za-z0-9._~/-]*$", RegexOptions.CultureInvariant)] private static partial Regex HealthPattern();
}
