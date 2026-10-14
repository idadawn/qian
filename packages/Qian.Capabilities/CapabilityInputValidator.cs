using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Json.Schema;
using Qian.Runtime.Errors;

namespace Qian.Capabilities;

// 执行前按条目 inputSchema（JSON Schema 2020-12）校验输入；每个违反项映射为 contracts/v0 的字段级错误码与输入 JSON Pointer。
internal sealed class CapabilityInputValidator
{
    private const int CacheLimit = 1024;
    private static readonly Uri BaseUri = new("https://qian.dev/capabilities/input.schema.json");
    // 只强制与 System.Text.Json 解析口径一致的格式。date-time、time、uri 等交给绑定反序列化：System.Text.Json 接受不带时区偏移的
    // DateTime、TimeOnly 与相对 Uri，按 RFC 3339 / RFC 3986 校验会拒绝绑定本来能处理的输入。
    private static readonly HashSet<string> EnforcedFormats = new(StringComparer.Ordinal) { "uuid", "date" };
    // 取值是子 Schema 的关键字，按成员表、数组、单个 Schema 三种形状；其余成员（enum、const、default、examples 等）是数据。
    private static readonly HashSet<string> SchemaMaps = new(StringComparer.Ordinal) { "properties", "patternProperties", "dependentSchemas", "$defs", "definitions" };
    private static readonly HashSet<string> SchemaArrays = new(StringComparer.Ordinal) { "allOf", "anyOf", "oneOf", "prefixItems" };
    private static readonly HashSet<string> SchemaValues = new(StringComparer.Ordinal)
    { "items", "additionalProperties", "unevaluatedProperties", "unevaluatedItems", "propertyNames", "contains", "if", "then", "else", "not" };
    private static readonly string[] AnchorKeywords = ["$anchor", "$dynamicAnchor"];
    private readonly ConcurrentDictionary<string, JsonSchema> _schemas = new(StringComparer.Ordinal);

    internal void Validate(JsonElement entry, JsonElement input)
    {
        var source = entry.GetProperty("inputSchema");
        if (Unreadable(input, 0)) throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
        var schema = Compile(entry.GetProperty("schemaSha256").GetString()!, source);
        EvaluationResults results;
        try { results = schema.Evaluate(input, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical, RequireFormatValidation = true }); }
        catch (Exception error) when (error is KeyNotFoundException or ArgumentException or InvalidOperationException or JsonException)
        { throw new InvalidOperationException("能力输入 Schema 无法求值。", error); }
        if (results.IsValid) return;
        var found = new Findings();
        new Walker(source, input).Visit(results, source, BaseUri, null, [], [], found);
        // 只有组合关键字失败、没有更具体的子项时，整个输入按取值不允许处理。
        if (found.Errors.Count == 0) found.Add(PlatformErrors.ValueNotAllowed, []);
        throw new QianErrorException(new QianError(PlatformErrors.InputInvalid)
        { Errors = found.Errors.Values.OrderBy(error => error.Pointer, StringComparer.Ordinal).ThenBy(error => error.Code.Code, StringComparer.Ordinal).Take(100).ToArray() });
    }

    // 子 Schema 沿结果树从父 Schema 按关键字逐级定位，$ref 按引用值与当前基准 URI 自行解析，不读结果的 SchemaLocation：JsonSchema.Net 9.4
    // 对 items、additionalProperties、unevaluatedProperties、propertyNames 的子节点会在该位置末尾多拼一段，对带 $id 的内嵌资源给出的片段也不在
    // 该资源内，按它回查源 Schema 会落空或落到别处。
    private sealed class Walker(JsonElement source, JsonElement input)
    {
        internal void Visit(EvaluationResults node, JsonElement? schema, Uri scope, string? via, string[] parent, (int Depth, int Offset)[] shifts, Findings found)
        {
            var location = Shift(Segments(node.InstanceLocation.ToString()), shifts);
            if (schema?.ValueKind == JsonValueKind.False)
            {
                // 布尔 false 子 Schema：多出的成员是未声明属性，多出的数组元素是超出长度。
                if (via is "additionalProperties" or "unevaluatedProperties") found.Add(PlatformErrors.UnknownProperty, location);
                else if (via is "items" or "unevaluatedItems") found.Add(PlatformErrors.OutOfRange, parent);
                else found.Add(PlatformErrors.ValueNotAllowed, location);
                return;
            }
            if (schema is { ValueKind: JsonValueKind.Object } resource && resource.TryGetProperty("$id", out var id) && id.ValueKind == JsonValueKind.String &&
                Uri.TryCreate(scope, id.GetString(), out var based)) scope = Document(based);
            var instance = At(input, location);
            var children = (node.Details ?? []).Select(child => (Node: child, Path: Relative(node, child))).Where(child => child.Path.Length != 0).ToArray();
            foreach (var keyword in node.Errors?.Keys ?? Enumerable.Empty<string>())
            {
                switch (keyword)
                {
                    case "anyOf" or "oneOf": Choose(keyword, schema, scope, location, shifts, children, found); break;
                    // 子 Schema 关键字的具体原因在子节点上；没有失败子节点时才在本位置报告。
                    case "allOf" or "then" or "else" or "properties" or "patternProperties" or "additionalProperties" or "unevaluatedProperties"
                        or "items" or "prefixItems" or "unevaluatedItems" or "dependentSchemas" or "propertyNames" or "$ref" or "$dynamicRef":
                        if (!children.Any(child => !child.Node.IsValid && child.Path[0] == keyword)) found.Add(PlatformErrors.ValueNotAllowed, location);
                        break;
                    case "required" or "dependentRequired":
                        foreach (var field in Missing(keyword, schema, instance)) found.Add(PlatformErrors.Required, [.. location, field]);
                        break;
                    case "type": found.Add(PlatformErrors.TypeMismatch, location); break;
                    case "enum" or "const": found.Add(PlatformErrors.ValueNotAllowed, location, Allowed(schema, keyword), discriminator: true); break;
                    case "minimum" or "maximum" or "exclusiveMinimum" or "exclusiveMaximum" or "multipleOf" or "minLength" or "maxLength"
                        or "minItems" or "maxItems" or "minProperties" or "maxProperties" or "minContains" or "maxContains":
                        found.Add(PlatformErrors.OutOfRange, location); break;
                    // 匹配元素的个数受 minContains / maxContains 约束时是数量越界，否则是没有任何元素满足。
                    case "contains":
                        found.Add(schema is { ValueKind: JsonValueKind.Object } rule && (rule.TryGetProperty("minContains", out _) || rule.TryGetProperty("maxContains", out _))
                            ? PlatformErrors.OutOfRange : PlatformErrors.ValueNotAllowed, location);
                        break;
                    case "pattern": found.Add(PlatformErrors.PatternMismatch, location); break;
                    case "format": found.Add(PlatformErrors.FormatInvalid, location); break;
                    // not 的子 Schema 匹配、uniqueItems 等其余情况：本位置的取值不允许。
                    default: found.Add(PlatformErrors.ValueNotAllowed, location); break;
                }
            }
            foreach (var (child, path) in children)
            {
                var keyword = path[0];
                // if、not、contains 的子 Schema 只是逻辑测试，失败不是输入错误；anyOf / oneOf 的分支由 Choose 取舍。
                if (child.IsValid || keyword is "if" or "not" or "contains" or "anyOf" or "oneOf") continue;
                if (keyword is not ("$ref" or "$dynamicRef") && node.Errors?.ContainsKey(keyword) != true) continue;
                var next = shifts;
                // JsonSchema.Net 9.4 对 prefixItems 之后的 items 子节点给的是相对前缀的下标，换回数组里的实际下标。
                if (keyword == "items" && schema is { ValueKind: JsonValueKind.Object } owner && owner.TryGetProperty("prefixItems", out var prefix) &&
                    prefix.ValueKind == JsonValueKind.Array && prefix.GetArrayLength() > 0)
                    next = [.. shifts, (location.Length, prefix.GetArrayLength())];
                var (target, targetScope) = Child(schema, path, scope);
                Visit(child, target, targetScope, keyword, location, next, found);
            }
        }

        // anyOf / oneOf 失败：oneOf 有分支通过说明同时匹配了多个分支，只在本位置报取值不允许。全部分支失败时只报告最接近的分支——先排除
        // 本位置类型就不符的分支，再看直接成员的判别字段（const / enum，如多态类型的 $type）不符最少，其次违反项最少；并列分支结论不同时
        // 只在本位置报告，不把各分支的要求混在一起；并列分支在同一判别字段上都不符时只报该字段并合并允许值。
        private void Choose(string keyword, JsonElement? schema, Uri scope, string[] location, (int Depth, int Offset)[] shifts,
            (EvaluationResults Node, string[] Path)[] children, Findings found)
        {
            var branches = children.Where(child => child.Path[0] == keyword).ToArray();
            if (branches.Length == 0 || branches.Any(branch => branch.Node.IsValid)) { found.Add(PlatformErrors.ValueNotAllowed, location); return; }
            var root = Pointer(location);
            var outcomes = branches.Select(branch =>
            {
                var own = new Findings();
                var (target, targetScope) = Child(schema, branch.Path, scope);
                Visit(branch.Node, target, targetScope, keyword, location, shifts, own);
                var mismatch = own.Errors.TryGetValue(root, out var error) && error.Code.Code == PlatformErrors.TypeMismatch.Code;
                return (Findings: own, Rank: (Mismatch: mismatch ? 1 : 0, Discriminators: own.Discriminators(location.Length + 1), Count: own.Errors.Count));
            }).OrderBy(outcome => outcome.Rank).ToArray();
            var best = outcomes[0];
            var tied = outcomes.TakeWhile(outcome => outcome.Rank == best.Rank).Select(outcome => outcome.Findings).ToArray();
            if (best.Findings.Errors.Count == 0) found.Add(PlatformErrors.ValueNotAllowed, location);
            else if (tied.All(own => own.SameAs(best.Findings))) found.Merge(best.Findings);
            else if (!Findings.TryUnion(tied, found)) found.Add(PlatformErrors.ValueNotAllowed, location);
        }

        private (JsonElement? Schema, Uri Scope) Child(JsonElement? schema, string[] path, Uri scope)
        {
            var keyword = path[0];
            if (schema is not { ValueKind: JsonValueKind.Object } rule || !rule.TryGetProperty(keyword, out var value)) return (null, scope);
            if (keyword is "$ref" or "$dynamicRef") return value.ValueKind == JsonValueKind.String ? Resolve(scope, value.GetString()!) : (null, scope);
            if (SchemaMaps.Contains(keyword))
                return (path.Length > 1 && value.ValueKind == JsonValueKind.Object && value.TryGetProperty(path[1], out var named) ? named : null, scope);
            if (SchemaArrays.Contains(keyword))
                return (path.Length > 1 && value.ValueKind == JsonValueKind.Array && int.TryParse(path[1], NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
                    index < value.GetArrayLength() ? value[index] : null, scope);
            return (value, scope);
        }

        // 引用相对当前基准 URI 解析到本文档或带 $id 的内嵌资源，片段是 JSON Pointer 或 $anchor 名；外部引用在编译时已禁用。
        // $dynamicRef 按同名锚点静态解析：只用于定位违反项，是否通过已由库判定。
        private (JsonElement? Schema, Uri Scope) Resolve(Uri scope, string reference)
        {
            if (!Uri.TryCreate(scope, reference, out var target)) return (null, scope);
            var document = Document(target);
            if ((document == BaseUri ? source : Embedded(source, BaseUri, document)) is not { } resource) return (null, document);
            var fragment = Uri.UnescapeDataString(target.Fragment.TrimStart('#'));
            return (fragment.Length == 0 || fragment[0] == '/' ? At(resource, Segments(fragment)) : Anchor(resource, fragment, true), document);
        }
    }

    private sealed class Findings
    {
        // 判别字段（const / enum）不符的位置及其深度：选 anyOf / oneOf 分支时用来判断分支对不对得上。
        private readonly Dictionary<string, int> _discriminators = new(StringComparer.Ordinal);
        internal Dictionary<string, QianFieldError> Errors { get; } = new(StringComparer.Ordinal);

        internal void Add(ErrorCode code, string[] segments, IReadOnlyList<JsonElement>? allowed = null, bool discriminator = false) =>
            Add(new QianFieldError(code, Pointer(segments)) { Allowed = allowed }, segments.Length, discriminator);

        // 同一位置只记最先发现（外层优先）的违反项。
        internal void Add(QianFieldError error, int depth, bool discriminator)
        {
            if (Errors.TryAdd(error.Pointer, error) && discriminator) _discriminators[error.Pointer] = depth;
        }

        internal void Merge(Findings other)
        {
            foreach (var (pointer, error) in other.Errors)
                if (Errors.TryAdd(pointer, error) && other._discriminators.TryGetValue(pointer, out var depth)) _discriminators[pointer] = depth;
        }

        internal int Discriminators(int depth) => _discriminators.Values.Count(value => value <= depth);

        internal bool SameAs(Findings other) => Errors.Count == other.Errors.Count && Errors.All(pair => other.Errors.TryGetValue(pair.Key, out var error) &&
            error.Code.Code == pair.Value.Code.Code && Raw(error.Allowed).SequenceEqual(Raw(pair.Value.Allowed), StringComparer.Ordinal));

        // 并列分支在同一位置都有判别字段（const / enum）不符：只报该位置，允许值取各分支的并集（任一分支未列出或超过上限时不列出）；
        // 各分支其余的要求取决于选哪个分支，不报。
        internal static bool TryUnion(Findings[] tied, Findings into)
        {
            var common = tied[0]._discriminators.Keys.Where(pointer => tied.All(own => own._discriminators.ContainsKey(pointer))).ToArray();
            foreach (var pointer in common)
            {
                var lists = tied.Select(own => own.Errors[pointer].Allowed).ToArray();
                var values = lists.Any(list => list is null) ? [] : lists.SelectMany(list => list!).DistinctBy(value => value.GetRawText(), StringComparer.Ordinal).ToArray();
                into.Add(new QianFieldError(PlatformErrors.ValueNotAllowed, pointer) { Allowed = values.Length is > 0 and <= AllowedValues.MaxCount ? values : null },
                    tied[0]._discriminators[pointer], discriminator: true);
            }
            return common.Length != 0;
        }

        private static IEnumerable<string> Raw(IReadOnlyList<JsonElement>? values) => (values ?? []).Select(value => value.GetRawText());
    }

    // contracts/v0 的 pointer 最长 1024 字符；超出时退到最近的合法上级位置，不因输入的属性名过长而变成服务端错误。
    private static string Pointer(string[] segments)
    {
        for (var count = segments.Length; count > 0; count--)
        {
            var pointer = ErrorPointer.Input([.. segments.Take(count).Cast<object>()]);
            if (ErrorPointer.IsValid(pointer)) return pointer;
        }
        return ErrorPointer.Input();
    }

    private JsonSchema Compile(string key, JsonElement source)
    {
        if (_schemas.TryGetValue(key, out var cached)) return cached;
        if (_schemas.Count >= CacheLimit) _schemas.Clear();
        // JsonSchema.Net 9.4 按 JSON Pointer 转义后的键回查 patternProperties 与 dependentSchemas：键含 / 或 ~ 时前者每次求值都抛异常，
        // 后者被静默跳过。这类 Schema 无法可靠校验，按配置错误拒绝执行，不放过约束。
        if (Unsupported(source)) throw new InvalidOperationException("能力输入 Schema 的 patternProperties / dependentSchemas 键含 / 或 ~，无法可靠校验。");
        try
        {
            var options = new BuildOptions { Dialect = Dialect.Draft202012, SchemaRegistry = new SchemaRegistry { Fetch = (_, _) => null } };
            return _schemas.GetOrAdd(key, JsonSchema.Build(Sanitize(source), options, BaseUri));
        }
        catch (Exception error) when (error is JsonSchemaException or JsonException or ArgumentException or InvalidOperationException)
        { throw new InvalidOperationException("能力输入 Schema 无法编译。", error); }
    }

    // 编译用副本：去掉不强制的 format。只在 Schema 位置上处理，名为 format 的属性与 enum、const 等数据原样保留；结构不变，定位仍按源 Schema。
    private static JsonElement Sanitize(JsonElement schema)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer)) WriteSchema(writer, schema);
        using var document = JsonDocument.Parse(buffer.WrittenMemory, new JsonDocumentOptions { MaxDepth = 1000 });
        return document.RootElement.Clone();
    }

    private static void WriteSchema(Utf8JsonWriter writer, JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object) { schema.WriteTo(writer); return; }
        writer.WriteStartObject();
        foreach (var member in schema.EnumerateObject())
        {
            if (member.Name == "format" && member.Value.ValueKind == JsonValueKind.String && !EnforcedFormats.Contains(member.Value.GetString()!)) continue;
            writer.WritePropertyName(member.Name);
            if (SchemaMaps.Contains(member.Name) && member.Value.ValueKind == JsonValueKind.Object)
            {
                writer.WriteStartObject();
                foreach (var item in member.Value.EnumerateObject()) { writer.WritePropertyName(item.Name); WriteSchema(writer, item.Value); }
                writer.WriteEndObject();
            }
            else if (SchemaArrays.Contains(member.Name) && member.Value.ValueKind == JsonValueKind.Array)
            {
                writer.WriteStartArray();
                foreach (var item in member.Value.EnumerateArray()) WriteSchema(writer, item);
                writer.WriteEndArray();
            }
            else if (SchemaValues.Contains(member.Name)) WriteSchema(writer, member.Value);
            else member.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    private static bool Unsupported(JsonElement schema) =>
        schema.ValueKind == JsonValueKind.Object && (schema.EnumerateObject().Any(member => member.Name is "patternProperties" or "dependentSchemas" &&
            member.Value.ValueKind == JsonValueKind.Object && member.Value.EnumerateObject().Any(item => item.Name.AsSpan().IndexOfAny('/', '~') >= 0)) ||
        Subschemas(schema).Any(Unsupported));

    private static IEnumerable<JsonElement> Subschemas(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object) yield break;
        foreach (var member in schema.EnumerateObject())
        {
            if (SchemaMaps.Contains(member.Name) && member.Value.ValueKind == JsonValueKind.Object)
                foreach (var item in member.Value.EnumerateObject()) yield return item.Value;
            else if (SchemaArrays.Contains(member.Name) && member.Value.ValueKind == JsonValueKind.Array)
                foreach (var item in member.Value.EnumerateArray()) yield return item;
            else if (SchemaValues.Contains(member.Name)) yield return member.Value;
        }
    }

    // 按 $id 找内嵌资源（含带 $id 的根），$id 相对外层资源解析。
    private static JsonElement? Embedded(JsonElement schema, Uri scope, Uri target)
    {
        if (schema.ValueKind != JsonValueKind.Object) return null;
        if (schema.TryGetProperty("$id", out var id) && id.ValueKind == JsonValueKind.String && Uri.TryCreate(scope, id.GetString(), out var based))
        {
            scope = Document(based);
            if (scope == target) return schema;
        }
        foreach (var child in Subschemas(schema)) if (Embedded(child, scope, target) is { } found) return found;
        return null;
    }

    // 资源内按 $anchor / $dynamicAnchor 名找子 Schema，不进入另带 $id 的内嵌资源。
    private static JsonElement? Anchor(JsonElement schema, string name, bool root)
    {
        if (schema.ValueKind != JsonValueKind.Object || (!root && schema.TryGetProperty("$id", out _))) return null;
        foreach (var keyword in AnchorKeywords)
            if (schema.TryGetProperty(keyword, out var anchor) && anchor.ValueKind == JsonValueKind.String && anchor.GetString() == name) return schema;
        foreach (var child in Subschemas(schema)) if (Anchor(child, name, false) is { } found) return found;
        return null;
    }

    private static Uri Document(Uri uri) => new(uri.GetLeftPart(UriPartial.Query));

    // 子节点相对父节点的求值路径：首段是产生它的关键字，其后是成员名或下标。
    private static string[] Relative(EvaluationResults parent, EvaluationResults child)
    {
        var outer = Segments(parent.EvaluationPath.ToString()); var inner = Segments(child.EvaluationPath.ToString());
        return inner.Length > outer.Length && outer.SequenceEqual(inner.Take(outer.Length), StringComparer.Ordinal) ? inner[outer.Length..] : [];
    }

    private static string[] Shift(string[] segments, (int Depth, int Offset)[] shifts)
    {
        foreach (var (depth, offset) in shifts)
            if (depth < segments.Length && int.TryParse(segments[depth], NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                segments[depth] = (index + offset).ToString(CultureInfo.InvariantCulture);
        return segments;
    }

    private static IEnumerable<string> Missing(string keyword, JsonElement? schema, JsonElement? instance)
    {
        if (schema is not { ValueKind: JsonValueKind.Object } rule || instance is not { ValueKind: JsonValueKind.Object } value || !rule.TryGetProperty(keyword, out var spec)) yield break;
        if (keyword == "required" && spec.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in spec.EnumerateArray().Select(item => item.GetString()!)) if (!value.TryGetProperty(field, out _)) yield return field;
            yield break;
        }
        if (spec.ValueKind != JsonValueKind.Object) yield break;
        foreach (var dependency in spec.EnumerateObject().Where(item => value.TryGetProperty(item.Name, out _) && item.Value.ValueKind == JsonValueKind.Array))
            foreach (var field in dependency.Value.EnumerateArray().Select(item => item.GetString()!)) if (!value.TryGetProperty(field, out _)) yield return field;
    }

    private static JsonElement[]? Allowed(JsonElement? schema, string keyword)
    {
        if (schema is not { ValueKind: JsonValueKind.Object } rule || !rule.TryGetProperty(keyword, out var spec)) return null;
        if (keyword == "const") return [spec.Clone()];
        var values = spec.ValueKind == JsonValueKind.Array ? spec.EnumerateArray().Select(item => item.Clone()).ToArray() : [];
        return values.Length is > 0 and <= AllowedValues.MaxCount ? values : null;
    }

    // 输入读不成确定的 JSON 值时按无法解析的请求处理：嵌套超过 64 层、成员重名，或成员名 / 字符串含孤立的 UTF-16 代理项（如 \ud800 转义）。
    private static bool Unreadable(JsonElement value, int depth)
    {
        if (depth > 64) return true;
        try
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Array: return value.EnumerateArray().Any(item => Unreadable(item, depth + 1));
                case JsonValueKind.String: _ = value.GetString(); return false;
                case JsonValueKind.Object:
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var member in value.EnumerateObject())
                        if (!names.Add(member.Name) || Unreadable(member.Value, depth + 1)) return true;
                    return false;
                default: return false;
            }
        }
        catch (InvalidOperationException) { return true; }
    }

    private static string[] Segments(string pointer) => pointer.Length == 0 ? [] :
        pointer.Split('/').Skip(1).Select(raw => raw.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)).ToArray();

    private static JsonElement? At(JsonElement root, string[] segments)
    {
        var current = root;
        foreach (var token in segments)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(token, out var child)) current = child;
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < current.GetArrayLength()) current = current[index];
            else return null;
        }
        return current;
    }
}
