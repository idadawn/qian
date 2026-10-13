using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema;
using Qian.Runtime.Errors;

namespace Qian.Audit;

/// <summary>由可信能力契约构造的输入审计策略；元数据不能来自调用请求。脱敏后才计算输入哈希。</summary>
public sealed class AuditInputPolicy
{
    /// <summary>所有敏感值使用同一标记，不保存原值的哈希、长度或类型。</summary>
    public const string Redacted = "[REDACTED]";
    private const string Draft = "https://json-schema.org/draft/2020-12/schema";
    private readonly JsonElement _source;
    private readonly JsonSchema _validator;

    /// <summary>复制并校验可信 Schema。命令默认开启、查询默认关闭；audit 是可信契约的显式覆盖。</summary>
    public AuditInputPolicy(JsonElement schema, bool isCommand, bool? audit = null)
    {
        ShouldRecord = audit ?? isCommand;
        if (schema.ValueKind is not (JsonValueKind.Object or JsonValueKind.True or JsonValueKind.False) || schema.GetRawText().Length > 131072) throw InvalidSchema();
        _source = schema.Clone();
        try
        {
            ValidateShape(_source, true, new Budget(), 0, []);
            var registry = new SchemaRegistry { Fetch = (_, _) => null };
            var meta = (JsonSchema)registry.Get(new Uri(Draft))!;
            if (!meta.Evaluate(_source).IsValid) throw InvalidSchema();
            _validator = JsonSchema.FromText(_source.GetRawText(), new BuildOptions { Dialect = Dialect.Draft202012, SchemaRegistry = registry });
        }
        catch (Exception error) when (error is JsonException or JsonSchemaException or ArgumentException or InvalidOperationException or RegexMatchTimeoutException)
        { throw InvalidSchema(); }
    }
    /// <summary>是否记录此能力调用；它不替代权限判定或业务输入验证。</summary>
    public bool ShouldRecord { get; }
    /// <summary>关闭审计时为 null，否则返回脱敏投影的规范化 SHA-256；不保留原始输入。</summary>
    public string? InputHash(JsonElement input) => ShouldRecord
        ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(AuditCanonicalizer.Canonicalize(Redact(input))))) : null;
    /// <summary>生成脱敏投影；未知字段值保守遮盖，原输入不会被修改。只支持离线本地 JSON Pointer 引用。</summary>
    public JsonElement Redact(JsonElement input)
    {
        if (input.ValueKind == JsonValueKind.Undefined || input.GetRawText().Length > 65536) throw InvalidInput();
        InspectInput(input, 0);
        var buffer = new ArrayBufferWriter<byte>();
        // 先检查结构/引用和重复键，不让恶意同层引用循环进入 Schema 求值器。
        using (var writer = new Utf8JsonWriter(buffer)) Write(writer, input, [_source], new Budget(), 0);
        try
        {
            if (!_validator.Evaluate(input, new EvaluationOptions { OutputFormat = OutputFormat.Flag }).IsValid) throw InvalidInput();
        }
        catch (Exception error) when (error is JsonException or JsonSchemaException or ArgumentException or InvalidOperationException or RegexMatchTimeoutException)
        { throw InvalidSchema(); }
        using var document = JsonDocument.Parse(buffer.WrittenMemory); return document.RootElement.Clone();
    }

    private void Write(Utf8JsonWriter writer, JsonElement input, IReadOnlyList<JsonElement> schemas, Budget budget, int depth)
    {
        budget.Touch(depth);
        var expanded = schemas.SelectMany(schema => Expand(schema, new HashSet<string>(StringComparer.Ordinal), budget, 0)).ToArray();
        if (expanded.Length == 0 || expanded.Any(Sensitive) || expanded.All(IsUnspecified)) { writer.WriteStringValue(Redacted); return; }
        if (input.ValueKind == JsonValueKind.Object)
        {
            // 名称本身被标敏感时遮盖整个对象，不把动态敏感键带入投影。
            if (expanded.Where(s => s.ValueKind == JsonValueKind.Object && s.TryGetProperty("propertyNames", out _))
                .SelectMany(s => Expand(s.GetProperty("propertyNames"), new HashSet<string>(StringComparer.Ordinal), budget, 0)).Any(Sensitive))
            { writer.WriteStringValue(Redacted); return; }
            var fields = input.EnumerateObject().ToArray();
            if (fields.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length) throw InvalidInput();
            writer.WriteStartObject();
            foreach (var field in fields)
            {
                writer.WritePropertyName(field.Name);
                Write(writer, field.Value, ObjectSchemas(expanded, field.Name, budget), budget, depth + 1);
            }
            writer.WriteEndObject();
        }
        else if (input.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray(); var index = 0;
            foreach (var item in input.EnumerateArray()) Write(writer, item, ArraySchemas(expanded, index++), budget, depth + 1);
            writer.WriteEndArray();
        }
        else input.WriteTo(writer);
    }
    private static List<JsonElement> ObjectSchemas(IReadOnlyList<JsonElement> schemas, string name, Budget budget)
    {
        var result = new List<JsonElement>();
        foreach (var schema in schemas.Where(s => s.ValueKind == JsonValueKind.Object))
        {
            var matched = false;
            if (schema.TryGetProperty("properties", out var properties) && properties.TryGetProperty(name, out var property)) { result.Add(property); matched = true; }
            if (schema.TryGetProperty("patternProperties", out var patterns))
                foreach (var pattern in patterns.EnumerateObject())
                {
                    budget.Touch(0);
                    if (Matches(pattern.Name, name)) { result.Add(pattern.Value); matched = true; }
                }
            if (!matched && schema.TryGetProperty("additionalProperties", out var extra) && extra.ValueKind == JsonValueKind.Object) result.Add(extra);
            if (schema.TryGetProperty("unevaluatedProperties", out var remaining) && remaining.ValueKind == JsonValueKind.Object) result.Add(remaining);
        }
        return result;
    }
    private static List<JsonElement> ArraySchemas(IReadOnlyList<JsonElement> schemas, int index)
    {
        var result = new List<JsonElement>();
        foreach (var schema in schemas.Where(s => s.ValueKind == JsonValueKind.Object))
        {
            if (schema.TryGetProperty("prefixItems", out var prefix) && index < prefix.GetArrayLength()) result.Add(prefix[index]);
            else if (schema.TryGetProperty("items", out var item)) result.Add(item);
            if (schema.TryGetProperty("contains", out var contains)) result.Add(contains);
            if (schema.TryGetProperty("unevaluatedItems", out var remaining) && remaining.ValueKind == JsonValueKind.Object) result.Add(remaining);
        }
        return result;
    }
    private IEnumerable<JsonElement> Expand(JsonElement schema, HashSet<string> references, Budget budget, int depth)
    {
        budget.Touch(depth); yield return schema;
        if (schema.ValueKind != JsonValueKind.Object) yield break;
        if (schema.TryGetProperty("$ref", out var reference))
        {
            if (reference.ValueKind != JsonValueKind.String) throw InvalidSchema();
            var pointer = reference.GetString()!;
            if (!references.Add(pointer)) throw InvalidSchema();
            foreach (var target in Expand(Resolve(pointer), references, budget, depth + 1)) yield return target;
            references.Remove(pointer);
        }
        foreach (var name in new[] { "allOf", "anyOf", "oneOf" })
            if (schema.TryGetProperty(name, out var list))
            {
                if (list.ValueKind != JsonValueKind.Array) throw InvalidSchema();
                foreach (var branch in list.EnumerateArray()) foreach (var node in Expand(branch, references, budget, depth + 1)) yield return node;
            }
        foreach (var name in new[] { "if", "then", "else", "not" })
            if (schema.TryGetProperty(name, out var branch)) foreach (var node in Expand(branch, references, budget, depth + 1)) yield return node;
        if (schema.TryGetProperty("dependentSchemas", out var dependencies))
        {
            if (dependencies.ValueKind != JsonValueKind.Object) throw InvalidSchema();
            foreach (var dependency in dependencies.EnumerateObject())
                foreach (var node in Expand(dependency.Value, references, budget, depth + 1)) yield return node;
        }
    }
    private JsonElement Resolve(string reference)
    {
        if (reference == "#") return _source;
        if (!reference.StartsWith("#/", StringComparison.Ordinal)) throw InvalidSchema();
        var target = _source;
        foreach (var raw in Uri.UnescapeDataString(reference[2..]).Split('/'))
        {
            for (var i = 0; i < raw.Length; i++)
                if (raw[i] == '~' && (++i == raw.Length || raw[i] is not ('0' or '1'))) throw InvalidSchema();
            var token = raw.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (target.ValueKind == JsonValueKind.Object && target.TryGetProperty(token, out var field)) target = field;
            else if (target.ValueKind == JsonValueKind.Array && int.TryParse(token, out var index) && index >= 0 && index < target.GetArrayLength()) target = target[index];
            else throw InvalidSchema();
        }
        return target;
    }
    private void ValidateShape(JsonElement schema, bool root, Budget budget, int depth, HashSet<JsonElement> visited)
    {
        if (!visited.Add(schema)) return;
        budget.Touch(depth);
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False) return;
        if (schema.ValueKind != JsonValueKind.Object) throw InvalidSchema();
        var fields = schema.EnumerateObject().ToArray();
        if (fields.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length) throw InvalidSchema();
        foreach (var name in new[] { "sensitive", "x-qian-sensitive" })
            if (schema.TryGetProperty(name, out var flag) && flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw InvalidSchema();
        if ((!root && schema.TryGetProperty("$id", out _)) || schema.TryGetProperty("$dynamicRef", out _) || schema.TryGetProperty("$recursiveRef", out _) ||
            schema.TryGetProperty("$anchor", out _) || schema.TryGetProperty("$dynamicAnchor", out _) || schema.TryGetProperty("$vocabulary", out _)) throw InvalidSchema();
        if (schema.TryGetProperty("$schema", out var dialect) && (dialect.ValueKind != JsonValueKind.String || dialect.GetString() != Draft)) throw InvalidSchema();
        _ = Expand(schema, new HashSet<string>(StringComparer.Ordinal), budget, 0).ToArray();
        if (schema.TryGetProperty("$ref", out var reference)) ValidateShape(Resolve(reference.GetString()!), false, budget, depth + 1, visited);
        foreach (var name in new[] { "$defs", "definitions", "properties", "patternProperties", "dependentSchemas" })
            if (schema.TryGetProperty(name, out var map))
            {
                if (map.ValueKind != JsonValueKind.Object) throw InvalidSchema();
                if (map.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != map.EnumerateObject().Count()) throw InvalidSchema();
                foreach (var child in map.EnumerateObject())
                { if (name == "patternProperties") _ = Pattern(child.Name); ValidateShape(child.Value, false, budget, depth + 1, visited); }
            }
        foreach (var name in new[] { "allOf", "anyOf", "oneOf", "prefixItems" })
            if (schema.TryGetProperty(name, out var array))
            {
                if (array.ValueKind != JsonValueKind.Array) throw InvalidSchema();
                foreach (var child in array.EnumerateArray()) ValidateShape(child, false, budget, depth + 1, visited);
            }
        foreach (var name in new[] { "items", "contains", "additionalProperties", "unevaluatedProperties", "unevaluatedItems", "propertyNames", "if", "then", "else", "not" })
            if (schema.TryGetProperty(name, out var child)) ValidateShape(child, false, budget, depth + 1, visited);
    }
    private static void InspectInput(JsonElement input, int depth)
    {
        if (depth > 32) throw InvalidInput();
        if (input.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in input.EnumerateObject())
            { if (!names.Add(field.Name)) throw InvalidInput(); InspectInput(field.Value, depth + 1); }
        }
        else if (input.ValueKind == JsonValueKind.Array)
            foreach (var item in input.EnumerateArray()) InspectInput(item, depth + 1);
    }
    private static bool Matches(string pattern, string value)
    {
        try { return Pattern(pattern).IsMatch(value); }
        catch (RegexMatchTimeoutException) { throw InvalidSchema(); }
    }
    private static bool Sensitive(JsonElement schema) => schema.ValueKind == JsonValueKind.Object &&
        ((schema.TryGetProperty("sensitive", out var value) && value.ValueKind == JsonValueKind.True) ||
         (schema.TryGetProperty("x-qian-sensitive", out value) && value.ValueKind == JsonValueKind.True));
    private static bool IsUnspecified(JsonElement schema) => schema.ValueKind == JsonValueKind.True || schema.ValueKind == JsonValueKind.Object && !schema.EnumerateObject().Any();
    private static Regex Pattern(string pattern)
    {
        try { return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100)); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException) { throw InvalidSchema(); }
    }
    private static QianErrorException InvalidSchema() => new(new QianError(AuditErrors.SchemaInvalid));
    private static QianErrorException InvalidInput() => new(new QianError(PlatformErrors.InputInvalid));
    private sealed class Budget
    {
        private int _remaining = 100000;
        public void Touch(int depth) { if (depth > 32 || --_remaining < 0) throw InvalidSchema(); }
    }
}
