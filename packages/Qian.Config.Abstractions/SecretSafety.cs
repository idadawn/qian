using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Qian.Config;

/// <summary>不含原值的配置扫描诊断。</summary>
public sealed record SecretDiagnostic(string Code, string Location, string Reason);

/// <summary>配置契约级敏感数据扫描，可供发布门禁与 Apollo 快照验证共用。</summary>
public static partial class SecretSafety
{
    // 动态键模式只来自配置契约，进程内条目有限；超过上限整表清空重建，内存有界，已取出的实例不受影响。
    internal const int PatternCacheSize = 128;
    private static readonly ConcurrentDictionary<string, Regex> Patterns = new(StringComparer.Ordinal);

    /// <summary>扫描 JSON；本地 Schema 引用和组合分支中的敏感标记都参与检查。</summary>
    public static IReadOnlyList<SecretDiagnostic> Scan(JsonElement value, JsonElement? schema = null)
    {
        var diagnostics = new List<SecretDiagnostic>();
        Visit(value, "", "#", schema is { } root ? [root] : [], schema, diagnostics, 0);
        return diagnostics.Distinct().ToArray();
    }

    /// <summary>扫描标准 IConfiguration 的扁平层次键，冒号分隔路径；不把原值放进诊断。</summary>
    public static IReadOnlyList<SecretDiagnostic> ScanConfiguration(IReadOnlyDictionary<string, string?> values, JsonElement? schema = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new List<SecretDiagnostic>();
        foreach (var pair in values)
        {
            var path = pair.Key.Split(':');
            IReadOnlyList<JsonElement> candidates = schema is { } root ? [root] : [];
            foreach (var segment in path)
            {
                if (candidates.SelectMany(item => Expand(item, schema, result, "#", new HashSet<string>(StringComparer.Ordinal), 0))
                    .Any(IsSensitive)) result.Add(new("CONFIG_SECRET_EXPOSED", "#/" + Token(pair.Key), "sensitive-parent"));
                candidates = Children(candidates, segment, schema, result, "#", 0);
            }
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(pair.Value));
            Visit(json.RootElement, path[^1], "#/" + Token(pair.Key), candidates, schema, result, 0);
        }
        return result.Distinct().ToArray();
    }

    private static void Visit(JsonElement value, string name, string pointer, IReadOnlyList<JsonElement> schemas,
        JsonElement? root, List<SecretDiagnostic> output, int depth)
    {
        if (depth > 64) { Add("schema-depth-limit"); return; }
        var expanded = schemas.SelectMany(item => Expand(item, root, output, pointer, new HashSet<string>(StringComparer.Ordinal), 0)).ToArray();
        var sensitive = expanded.Any(IsSensitive);
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        var reference = text is not null && (SecretReference.IsValid(text) || name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) &&
            text.StartsWith("Bearer ", StringComparison.Ordinal) && SecretReference.IsValid(text[7..]));
        if (sensitive && !reference) { Add("sensitive-schema"); return; }
        if (SensitiveName(name) && !reference) { Add("sensitive-key"); return; }
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
                Visit(property.Value, property.Name, pointer + "/" + Token(property.Name), Children(expanded, property.Name, root, output, pointer, depth), root, output, depth + 1);
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in value.EnumerateArray())
                Visit(child, name, pointer + "/" + index, Children(expanded, (index++).ToString(System.Globalization.CultureInfo.InvariantCulture), root, output, pointer, depth), root, output, depth + 1);
        }
        else if (text is not null && !reference)
        {
            if (text.Contains("PRIVATE KEY-----", StringComparison.Ordinal) || Jwt().IsMatch(text)) { Add("credential-material"); return; }
            if (text.Split(';').Any(part => part.Contains('=') && SensitiveName(part.Split('=')[0].Trim())))
            { Add("credential-connection-string"); return; }
            if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            {
                if (uri.UserInfo.Length != 0 || uri.Query.TrimStart('?').Split('&').Any(part =>
                    SensitiveName(Uri.UnescapeDataString(part.Split('=')[0])) ||
                    part.Contains('=') && HighEntropy(Uri.UnescapeDataString(part[(part.IndexOf('=') + 1)..]))) ||
                    uri.AbsolutePath.Split('/').Any(part => HighEntropy(Uri.UnescapeDataString(part))) || HighEntropy(uri.Fragment.TrimStart('#')))
                    Add("credential-uri");
                return;
            }
            if (text.TrimStart().StartsWith('{') || text.TrimStart().StartsWith('['))
            {
                try { using var embedded = JsonDocument.Parse(text); Visit(embedded.RootElement, "", pointer, [], null, output, depth + 1); return; }
                catch (JsonException) { }
            }
            if (HighEntropy(text)) { Add("high-entropy"); return; }
        }
        void Add(string reason) => output.Add(new("CONFIG_SECRET_EXPOSED", pointer, reason));
    }

    private static List<JsonElement> Children(IReadOnlyList<JsonElement> schemas, string key, JsonElement? root,
        List<SecretDiagnostic> output, string pointer, int depth)
    {
        var result = new List<JsonElement>();
        foreach (var schema in schemas.SelectMany(item => Expand(item, root, output, pointer, new HashSet<string>(StringComparer.Ordinal), depth)))
        {
            if (schema.ValueKind != JsonValueKind.Object) continue;
            var matched = false;
            if (schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
                foreach (var property in properties.EnumerateObject())
                    if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase)) { result.Add(property.Value); matched = true; }
            if (schema.TryGetProperty("patternProperties", out var patterns) && patterns.ValueKind == JsonValueKind.Object)
                foreach (var pattern in patterns.EnumerateObject())
                {
                    try
                    {
                        if (Pattern(pattern.Name).IsMatch(key)) { result.Add(pattern.Value); matched = true; }
                    }
                    catch (Exception error) when (error is ArgumentException or NotSupportedException or RegexMatchTimeoutException)
                    { output.Add(new("CONFIG_SECRET_SCHEMA_INVALID", pointer, "unsupported-pattern")); }
                }
            if (!matched && schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.Object) result.Add(additional);
            if (int.TryParse(key, out var index) && index >= 0)
            {
                if (schema.TryGetProperty("prefixItems", out var prefix) && prefix.ValueKind == JsonValueKind.Array && index < prefix.GetArrayLength()) result.Add(prefix[index]);
                else if (schema.TryGetProperty("items", out var items)) result.Add(items);
            }
        }
        return result;
    }

    private static IEnumerable<JsonElement> Expand(JsonElement schema, JsonElement? root, List<SecretDiagnostic> output,
        string pointer, HashSet<string> references, int depth)
    {
        if (depth > 64) { output.Add(new("CONFIG_SECRET_SCHEMA_INVALID", pointer, "schema-depth-limit")); yield break; }
        yield return schema;
        if (schema.ValueKind != JsonValueKind.Object) yield break;
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var key = reference.ValueKind == JsonValueKind.String ? reference.GetString() : null;
            if (key is null || !key.StartsWith("#/", StringComparison.Ordinal) || root is null)
                output.Add(new("CONFIG_SECRET_SCHEMA_INVALID", pointer, "unsupported-reference"));
            else if (references.Add(key))
            {
                var target = root.Value;
                foreach (var segment in key[2..].Split('/').Select(part => part.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)))
                {
                    if (target.ValueKind != JsonValueKind.Object || !target.TryGetProperty(segment, out target))
                    { output.Add(new("CONFIG_SECRET_SCHEMA_INVALID", pointer, "unresolved-reference")); break; }
                }
                foreach (var resolved in Expand(target, root, output, pointer, references, depth + 1)) yield return resolved;
                references.Remove(key);
            }
        }
        foreach (var keyword in new[] { "allOf", "anyOf", "oneOf" })
            if (schema.TryGetProperty(keyword, out var branches) && branches.ValueKind == JsonValueKind.Array)
                foreach (var branch in branches.EnumerateArray())
                    foreach (var nested in Expand(branch, root, output, pointer, references, depth + 1)) yield return nested;
        foreach (var keyword in new[] { "if", "then", "else", "dependentSchemas" })
            if (schema.TryGetProperty(keyword, out var branch))
            {
                if (keyword == "dependentSchemas" && branch.ValueKind == JsonValueKind.Object)
                {
                    foreach (var dependent in branch.EnumerateObject())
                        foreach (var nested in Expand(dependent.Value, root, output, pointer, references, depth + 1)) yield return nested;
                }
                else foreach (var nested in Expand(branch, root, output, pointer, references, depth + 1)) yield return nested;
            }
    }

    private static bool IsSensitive(JsonElement schema) => schema.ValueKind == JsonValueKind.Object &&
        ((schema.TryGetProperty("x-qian-sensitive", out var marker) && marker.ValueKind == JsonValueKind.True) ||
         (schema.TryGetProperty("sensitive", out marker) && marker.ValueKind == JsonValueKind.True));

    private static bool SensitiveName(string name)
    {
        var normalized = name.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        return normalized is "authorization" or "cookie" or "setcookie" or "key" or "apikey" or "accesskey" or "secretkey" or "privatekey" or "signingkey" ||
            normalized.EndsWith("password", StringComparison.Ordinal) || normalized.EndsWith("secret", StringComparison.Ordinal) || normalized.EndsWith("token", StringComparison.Ordinal);
    }
    private static bool HighEntropy(string value)
    {
        if (value.Length < 32 || value.Length > 8192 || value.Any(char.IsWhiteSpace)) return false;
        if (value.StartsWith("sha256:", StringComparison.Ordinal) && value.Length == 71 && value[7..].All(Uri.IsHexDigit)) return false;
        var classes = (value.Any(char.IsUpper) ? 1 : 0) + (value.Any(char.IsLower) ? 1 : 0) + (value.Any(char.IsDigit) ? 1 : 0);
        var entropy = value.GroupBy(character => character).Sum(group =>
        { var probability = (double)group.Count() / value.Length; return -probability * Math.Log2(probability); });
        return classes >= 2 && entropy >= 4.0 || value.Length >= 40 && value.All(Uri.IsHexDigit) && entropy >= 3.2;
    }
    private static string Token(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    // NonBacktracking 线性匹配，过大的自动机在构造时即拒绝；超时只兜底超长输入，触发即报 CONFIG_SECRET_SCHEMA_INVALID（失败关闭）。
    // 墙钟只在物化新 DFA 转移和长输入分段时检查：复用实例后，常规长度的热匹配不受调度或 GC 停顿影响；首次 JIT 与建图仍可能被拖长，1 秒上限为此留出余量。
    // 静态 Regex.IsMatch 只靠框架共享的 15 项缓存复用实例，会被其他代码挤出，所以单独缓存；构造失败直接抛给调用方，不进缓存。
    internal static Regex Pattern(string pattern)
    {
        if (Patterns.TryGetValue(pattern, out var cached)) return cached;
        var regex = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(1));
        if (Patterns.Count >= PatternCacheSize) Patterns.Clear();
        return Patterns.GetOrAdd(pattern, regex);
    }
    internal static int CachedPatterns => Patterns.Count;
    [GeneratedRegex(@"\AeyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex Jwt();
}
