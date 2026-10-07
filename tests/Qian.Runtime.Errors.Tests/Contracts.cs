using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema;

namespace Qian.Runtime.Errors.Tests;

/// <summary>读取 contracts/v0：Schema（共用一个注册表以解析跨文件 $ref）、fixture，以及 Schema 表达不了的语义规则。</summary>
internal static partial class Contracts
{
    /// <summary>按跨文件引用的依赖顺序排列。</summary>
    public static readonly string[] SchemaNames = ["s2s-claims", "error-detail", "error-catalog", "event-envelope", "capability-entry"];

    public const string MetaSchemaUri = "https://json-schema.org/draft/2020-12/schema";

    private static readonly Lazy<(BuildOptions Options, Dictionary<string, JsonSchema> Schemas)> s_loaded = new(Load);

    public static string RepoRoot { get; } = FindRepoRoot();

    public static string V0 => Path.Combine(RepoRoot, "contracts", "v0");

    public static JsonSchema Schema(string name) => s_loaded.Value.Schemas[name];

    public static JsonSchema MetaSchema =>
        s_loaded.Value.Options.SchemaRegistry.Get(new Uri(MetaSchemaUri)) as JsonSchema ?? throw new InvalidOperationException("2020-12 元 Schema 未注册");

    public static JsonDocument ReadJson(string path) => JsonDocument.Parse(File.ReadAllText(path));

    /// <summary>按 Schema 校验，返回错误列表（空 = 通过）。</summary>
    public static IReadOnlyList<string> Validate(JsonSchema schema, JsonElement instance)
    {
        var results = schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (results.IsValid)
            return [];
        return (results.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .Select(d => $"{d.InstanceLocation} ({d.EvaluationPath}): {string.Join("; ", d.Errors!.Values)}")
            .DefaultIfEmpty("invalid")
            .ToArray();
    }

    public static IReadOnlyList<string> Validate(string schemaName, JsonElement instance) => Validate(Schema(schemaName), instance);

    /// <summary>fixture 文件 <c>fixtures/&lt;schema&gt;.&lt;kind&gt;.json</c>：<c>[{ name, data }]</c>。</summary>
    public static IEnumerable<(string Name, JsonElement Data)> Fixtures(string schemaName, string kind)
    {
        var path = Path.Combine(V0, "fixtures", $"{schemaName}.{kind}.json");
        if (!File.Exists(path))
            yield break;
        using var doc = ReadJson(path);
        foreach (var item in doc.RootElement.EnumerateArray())
            yield return (item.GetProperty("name").GetString()!, item.GetProperty("data").Clone());
    }

    /// <summary>
    /// JSON Schema 表达不了、由契约 README 规定的语义规则；校验器与生产方实现时以此为参考。
    /// 只对已通过 Schema 校验的实例调用。
    /// </summary>
    public static IReadOnlyList<string> CheckSemantics(string schemaName, JsonElement data)
    {
        var errors = new List<string>();
        switch (schemaName)
        {
            case "s2s-claims":
            {
                var iat = data.GetProperty("iat").GetInt64();
                var nbf = data.GetProperty("nbf").GetInt64();
                var exp = data.GetProperty("exp").GetInt64();
                if (exp - iat is <= 0 or > 300)
                    errors.Add($"exp − iat = {exp - iat}，须在 (0, 300] 秒内");
                if (nbf < iat || nbf >= exp)
                    errors.Add("须满足 iat ≤ nbf < exp");
                if (data.GetProperty("principal_kind").GetString() == "service"
                    && data.GetProperty("session_id").GetString() != data.GetProperty("jti").GetString())
                    errors.Add("service 主体的 session_id 须等于 jti");
                break;
            }
            case "event-envelope":
            {
                var subject = data.GetProperty("subject").GetString()!;
                var type = data.GetProperty("type").GetString()!;
                if (!VersionSuffix().IsMatch(type) || VersionSuffix().Replace(type, string.Empty) != subject)
                    errors.Add($"type '{type}' 须为 subject '{subject}' 加 .v<N>");
                break;
            }
            case "error-detail":
            {
                var code = data.GetProperty("code").GetString()!;
                if (!data.GetProperty("type").GetString()!.EndsWith("/" + code, StringComparison.Ordinal))
                    errors.Add("type 末段须等于 code");
                if (data.TryGetProperty("detail", out var detail) && detail.GetString() != data.GetProperty("message").GetString())
                    errors.Add("detail 须与 message 相同");
                if (data.TryGetProperty("errors", out var items))
                {
                    var first = items[0];
                    if (data.GetProperty("pointer").GetString() != first.GetProperty("pointer").GetString())
                        errors.Add("顶层 pointer 须等于 errors[0].pointer");
                    var top = data.TryGetProperty("allowed", out var a) ? a : (JsonElement?)null;
                    var firstAllowed = first.TryGetProperty("allowed", out var b) ? b : (JsonElement?)null;
                    if (top.HasValue != firstAllowed.HasValue || (top.HasValue && !JsonElement.DeepEquals(top.Value, firstAllowed!.Value)))
                        errors.Add("顶层 allowed 须等于 errors[0].allowed");
                }
                break;
            }
            case "error-catalog":
            {
                string? previous = null;
                foreach (var entry in data.GetProperty("codes").EnumerateArray())
                {
                    var code = entry.GetProperty("code").GetString()!;
                    if (previous is not null && string.CompareOrdinal(previous, code) >= 0)
                        errors.Add($"codes 须按 code 升序且不重复：{previous} → {code}");
                    previous = code;
                    var kind = entry.GetProperty("kind").GetString();
                    if (entry.TryGetProperty("type", out var type) && type.GetString() != $"{ErrorCode.TypeBaseUri}{kind}/{code}")
                        errors.Add($"{code} 的 type 须为 {ErrorCode.TypeBaseUri}{kind}/{code}");
                    var placeholders = entry.GetProperty("message").EnumerateObject()
                        .Select(p => string.Join(",", Placeholder().Matches(p.Value.GetString()!).Select(m => m.Value).Distinct().Order(StringComparer.Ordinal)))
                        .Distinct()
                        .Count();
                    if (placeholders > 1)
                        errors.Add($"{code} 各语言文案的占位符不一致");
                }
                break;
            }
            case "capability-entry":
            {
                var plugin = data.GetProperty("plugin").GetString();
                var name = data.GetProperty("name").GetString();
                if (data.GetProperty("id").GetString() != $"{plugin}/{name}")
                    errors.Add("id 须为 <plugin>/<name>");
                var expected = SchemaSha256(data);
                if (data.GetProperty("schemaSha256").GetString() != expected)
                    errors.Add($"schemaSha256 须为 {expected}");
                break;
            }
        }
        return errors;
    }

    /// <summary>contracts/v0 规定的 schemaSha256：对 {name, description, inputSchema} 做 RFC 8785 规范化后 SHA-256。</summary>
    public static string SchemaSha256(JsonElement capability)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"description\":"); Jcs.Write(sb, capability.GetProperty("description"));
        sb.Append(",\"inputSchema\":"); Jcs.Write(sb, capability.GetProperty("inputSchema"));
        sb.Append(",\"name\":"); Jcs.Write(sb, capability.GetProperty("name"));
        sb.Append('}');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static (BuildOptions, Dictionary<string, JsonSchema>) Load()
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        var schemas = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);
        foreach (var name in SchemaNames)
            schemas[name] = JsonSchema.FromText(File.ReadAllText(Path.Combine(V0, $"{name}.schema.json")), options);
        return (options, schemas);
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Qian.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("找不到仓库根目录（Qian.slnx）。");
    }

    [GeneratedRegex("\\.v[1-9][0-9]*$")]
    private static partial Regex VersionSuffix();

    [GeneratedRegex("\\{[A-Za-z_][A-Za-z0-9_]*\\}")]
    private static partial Regex Placeholder();
}

/// <summary>RFC 8785（JCS）规范化的测试用实现：成员按 UTF-16 码元排序、无空白、字符串按 ECMAScript 规则转义；只支持整数。</summary>
internal static class Jcs
{
    public static void Write(StringBuilder sb, JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                sb.Append('{');
                var first = true;
                foreach (var p in e.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!first)
                        sb.Append(',');
                    first = false;
                    WriteString(sb, p.Name);
                    sb.Append(':');
                    Write(sb, p.Value);
                }
                sb.Append('}');
                break;
            case JsonValueKind.Array:
                sb.Append('[');
                var i = 0;
                foreach (var item in e.EnumerateArray())
                {
                    if (i++ > 0)
                        sb.Append(',');
                    Write(sb, item);
                }
                sb.Append(']');
                break;
            case JsonValueKind.String:
                WriteString(sb, e.GetString()!);
                break;
            case JsonValueKind.Number:
                sb.Append(e.TryGetInt64(out var n) ? n.ToString(CultureInfo.InvariantCulture) : throw new NotSupportedException("测试用 JCS 只支持整数"));
                break;
            case JsonValueKind.True:
                sb.Append("true");
                break;
            case JsonValueKind.False:
                sb.Append("false");
                break;
            default:
                sb.Append("null");
                break;
        }
    }

    private static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}
