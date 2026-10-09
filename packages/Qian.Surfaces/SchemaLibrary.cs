using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces;

internal sealed class SchemaLibrary
{
    private readonly Dictionary<string, JsonSchema> _schemas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> _raw = new(StringComparer.Ordinal);
    private readonly BuildOptions _options = new() { SchemaRegistry = new SchemaRegistry { Fetch = (_, _) => null } };
    internal SchemaLibrary()
    {
        foreach (var name in new[] { "capability-entry", "permissions", "plugin", "skill", "agent", "command", "hooks", "mcp", "tool", "patch", "profile" })
        {
            using var stream = typeof(SchemaLibrary).Assembly.GetManifestResourceStream("schemas/" + name + ".schema.json")
                ?? throw new InvalidOperationException("缺少内置 Schema：" + name);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            using var json = JsonDocument.Parse(text);
            var raw = json.RootElement.Clone();
            _raw[raw.GetProperty("$id").GetString()!] = raw;
            _schemas[name] = JsonSchema.FromText(text, _options);
        }
    }

    internal IReadOnlyList<SurfaceDiagnostic> Validate(string name, string file, JsonElement data, IReadOnlyDictionary<string, int> lines, string traceId)
    {
        var results = _schemas[name].Evaluate(data, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        if (results.IsValid) return [];
        var errors = new Dictionary<string, SurfaceDiagnostic>(StringComparer.Ordinal);
        foreach (var item in Flatten(results))
        {
            if (item.IsValid || item.Errors is null) continue;
            var pointer = item.InstanceLocation.ToString();
            foreach (var keyword in item.Errors.Keys)
            {
                if (keyword is "allOf" or "anyOf" or "oneOf" or "if" or "then" or "else" or "$ref" or "$dynamicRef" or "properties" or "items" or "prefixItems" or "patternProperties") continue;
                var schema = AtSchema(item.SchemaLocation);
                var instance = At(data, pointer);
                if (keyword == "required" && schema is { } schemaValue && schemaValue.TryGetProperty("required", out var required) && instance is { ValueKind: JsonValueKind.Object } parent)
                {
                    foreach (var field in required.EnumerateArray().Select(e => e.GetString()!))
                        if (!parent.TryGetProperty(field, out _)) Add(pointer + "/" + Diagnostics.Token(field), "缺少必填字段。");
                }
                else if (keyword == "additionalProperties" && schema is { } objectSchema && instance is { ValueKind: JsonValueKind.Object } value)
                {
                    foreach (var field in value.EnumerateObject())
                    {
                        var known = objectSchema.TryGetProperty("properties", out var properties) && properties.TryGetProperty(field.Name, out _);
                        var pattern = objectSchema.TryGetProperty("patternProperties", out var patterns) && patterns.EnumerateObject().Any(p => Regex.IsMatch(field.Name, p.Name, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));
                        if (!known && !pattern) Add(pointer + "/" + Diagnostics.Token(field.Name), "存在未声明的字段。");
                    }
                }
                else Add(pointer, "字段不符合 Schema 约束（" + keyword + "）。");
            }
        }
        if (errors.Count == 0) Add("", "文档不符合 Schema。");
        return errors.Values.OrderBy(e => e.Error.Pointer, StringComparer.Ordinal).ToArray();
        void Add(string pointer, string message) => errors.TryAdd(pointer, Diagnostics.Create(file, pointer, Diagnostics.Line(lines, pointer), SurfaceErrors.Schema, message, traceId));
    }

    internal bool IsSchema(JsonElement schema)
    {
        if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("$schema", out var version) && version.GetString() != "https://json-schema.org/draft/2020-12/schema") return false;
        var meta = (JsonSchema)_options.SchemaRegistry.Get(new Uri("https://json-schema.org/draft/2020-12/schema"))!;
        return meta.Evaluate(schema).IsValid;
    }
    private JsonElement? AtSchema(Uri location)
    {
        var key = location.GetLeftPart(UriPartial.Path);
        if (!_raw.TryGetValue(key, out var root)) return null;
        var pointer = Uri.UnescapeDataString(location.Fragment.TrimStart('#'));
        var value = At(root, pointer);
        // JsonSchema.Net 9 的条件子约束会在 then/else 后增加内部 /0；它不是源 Schema 的数组。
        return value ?? At(root, pointer.Replace("/then/0/", "/then/", StringComparison.Ordinal).Replace("/else/0/", "/else/", StringComparison.Ordinal));
    }
    internal static JsonElement? At(JsonElement data, string pointer)
    {
        foreach (var raw in pointer.Split('/').Skip(1))
        {
            var token = raw.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(token, out var child)) data = child;
            else if (data.ValueKind == JsonValueKind.Array && int.TryParse(token, out var index) && index >= 0 && index < data.GetArrayLength()) data = data[index];
            else return null;
        }
        return data;
    }
    private static IEnumerable<EvaluationResults> Flatten(EvaluationResults results)
    {
        if (results.IsValid) yield break;
        yield return results;
        foreach (var child in results.Details ?? []) foreach (var item in Flatten(child)) yield return item;
    }
}
