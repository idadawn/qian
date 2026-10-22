using System.Globalization;
using System.Text.Json;

namespace Qian.Config.Tool;

// 组件 config.schema 的键结构。支持 properties / required / additionalProperties / items / type / enum / const / default /
// x-qian-sensitive 与文档内 $ref；oneOf / anyOf / allOf 的分支属性并入（只有 allOf 与 $ref 的 required 计入），
// patternProperties、if/then/else、dependentSchemas 让该层视为开放，不判多键。
internal sealed class SchemaNode
{
    private const int MaxDepth = 32;
    private static readonly string[] s_branches = ["allOf", "anyOf", "oneOf"];
    private static readonly string[] s_open = ["patternProperties", "if", "then", "else", "dependentSchemas"];
    private readonly HashSet<string> _types = new(StringComparer.Ordinal);
    internal string Pointer { get; private init; } = "#";
    internal Dictionary<string, (string Name, SchemaNode Node)> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal HashSet<string> Required { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal bool Closed { get; private set; }
    internal SchemaNode? Additional { get; private set; }
    internal SchemaNode? Items { get; private set; }
    internal bool IsArray { get; private set; }
    internal bool Sensitive { get; private set; }
    internal JsonElement? Default { get; private set; }
    // 声明了类型（或 enum / const 只有标量）且不含 object / array：其下不能再有子键。
    internal bool IsScalar => Properties.Count == 0 && !IsArray && Additional is null && _types.Count != 0 && !_types.Contains("object") && !_types.Contains("array");

    internal static SchemaNode Parse(JsonElement root) => Parse(root, root, "#", 0);

    private static SchemaNode Parse(JsonElement schema, JsonElement root, string pointer, int depth)
    {
        if (depth > MaxDepth) throw new InvalidDataException("配置 Schema 嵌套或引用过深。");
        var node = new SchemaNode { Pointer = pointer };
        if (schema.ValueKind == JsonValueKind.False) { node.Closed = true; return node; }
        if (schema.ValueKind != JsonValueKind.Object) return node;
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var target = reference.GetString() ?? "";
            if (!target.StartsWith('#')) throw new InvalidDataException("配置 Schema 只能引用本文档内的位置。");
            node.Merge(Parse(Resolve(root, target), root, target, depth + 1), required: true, closedness: true);
        }
        if (schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
            foreach (var property in properties.EnumerateObject())
                node.Properties.TryAdd(property.Name, (property.Name, Parse(property.Value, root, pointer + "/properties/" + Token(property.Name), depth + 1)));
        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
            foreach (var name in required.EnumerateArray()) if (name.GetString() is { } text) node.Required.Add(text);
        if (schema.TryGetProperty("additionalProperties", out var additional))
        {
            if (additional.ValueKind == JsonValueKind.False) node.Closed = true;
            else if (additional.ValueKind == JsonValueKind.Object) node.Additional = Parse(additional, root, pointer + "/additionalProperties", depth + 1);
        }
        if (schema.TryGetProperty("items", out var items) && items.ValueKind is JsonValueKind.Object or JsonValueKind.True or JsonValueKind.False)
        { node.IsArray = true; node.Items = Parse(items, root, pointer + "/items", depth + 1); }
        if (schema.TryGetProperty("type", out var type))
        {
            if (type.ValueKind == JsonValueKind.String) node._types.Add(type.GetString()!);
            else if (type.ValueKind == JsonValueKind.Array) foreach (var item in type.EnumerateArray()) if (item.GetString() is { } text) node._types.Add(text);
        }
        if (node._types.Count == 0) node.Infer(schema);
        if (node._types.Contains("array")) node.IsArray = true;
        if (schema.TryGetProperty("x-qian-sensitive", out var sensitive) && sensitive.ValueKind == JsonValueKind.True) node.Sensitive = true;
        // 骨架只写 Schema 给定的取值：default 优先，其次 const。
        if (schema.TryGetProperty("default", out var value) || schema.TryGetProperty("const", out value)) node.Default = value.Clone();
        foreach (var keyword in s_branches)
        {
            if (!schema.TryGetProperty(keyword, out var branches) || branches.ValueKind != JsonValueKind.Array) continue;
            var parsed = branches.EnumerateArray().Select((branch, index) => Parse(branch, root, pointer + "/" + keyword + "/" + index.ToString(CultureInfo.InvariantCulture), depth + 1)).ToArray();
            // 分支各自封闭且本层没有自己的 properties 时，本层只接受分支里出现过的键；任一分支开放即本层开放。
            var allClosed = parsed.Length != 0 && parsed.All(branch => branch.Closed);
            foreach (var branch in parsed) node.Merge(branch, required: keyword == "allOf", closedness: false);
            if (node.Properties.Count != 0 && allClosed && !schema.TryGetProperty("properties", out _)) node.Closed = true;
        }
        foreach (var open in s_open)
            if (schema.TryGetProperty(open, out _)) node.Closed = false;
        return node;
    }

    // 没有 type 时按 enum / const 的取值推断；混有对象或数组则不推断，视为开放。
    private void Infer(JsonElement schema)
    {
        var samples = new List<JsonElement>();
        if (schema.TryGetProperty("const", out var constant)) samples.Add(constant);
        if (schema.TryGetProperty("enum", out var values) && values.ValueKind == JsonValueKind.Array) samples.AddRange(values.EnumerateArray());
        if (samples.Count == 0 || samples.Any(sample => sample.ValueKind is JsonValueKind.Object or JsonValueKind.Array)) return;
        _types.Add("scalar");
    }

    private void Merge(SchemaNode other, bool required, bool closedness)
    {
        foreach (var (key, property) in other.Properties) Properties.TryAdd(key, property);
        if (required) foreach (var name in other.Required) Required.Add(name);
        if (closedness) Closed |= other.Closed;
        Additional ??= other.Additional; Items ??= other.Items;
        IsArray |= other.IsArray; Sensitive |= other.Sensitive; Default ??= other.Default;
        _types.UnionWith(other._types);
    }

    private static JsonElement Resolve(JsonElement root, string pointer)
    {
        var current = root;
        foreach (var raw in pointer.TrimStart('#').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = Uri.UnescapeDataString(raw).Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(token, out var next)) current = next;
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < current.GetArrayLength()) current = current[index];
            else throw new InvalidDataException("配置 Schema 的 $ref 指向不存在的位置。");
        }
        return current;
    }

    private static string Token(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}

// 叶子键：标量、数组或映射（additionalProperties / 无属性对象）。Defaults 是 Schema default（或 const）展开后的配置键，先到的祖先默认值优先。
internal sealed record SkeletonKey(string Path, string Pointer, bool Required, bool Sensitive, string Kind, IReadOnlyList<KeyValuePair<string, string>> Defaults);

internal sealed record KeyFinding(string Kind, string Profile, string Namespace, string Key, string Pointer);

// 一个 namespace 的期望键集与线上键比对。namespace qian.plugin.<组件 id> 的键写成「qian.plugin.<组件 id>:<属性路径>」：
// Kun 的 Apollo 客户端不转换键名、各 namespace 合并进同一个 IConfiguration，带上 namespace 名才不会与其它组件或 application 撞键，
// 组件以 ReadSection(namespace 名) 读取。输出只有键名与 Schema 指针，不含线上值。
internal sealed class NamespaceSkeleton
{
    private readonly SchemaNode _root;
    private NamespaceSkeleton(string name, SchemaNode root, IReadOnlyList<SkeletonKey> keys) { Name = name; _root = root; Keys = keys; }
    internal string Name { get; }
    internal IReadOnlyList<SkeletonKey> Keys { get; }
    private string Prefix => Name + ":";

    internal static NamespaceSkeleton Create(string name, JsonElement schema)
    {
        var root = SchemaNode.Parse(schema);
        var leaves = new List<(string Path, SchemaNode Node, bool Required, bool Sensitive)>();
        var defaults = new List<KeyValuePair<string, string>>();
        Flatten(root, "", true, false, leaves, defaults);
        var keys = leaves.Select(leaf => new SkeletonKey(leaf.Path, leaf.Node.Pointer, leaf.Required, leaf.Sensitive,
            leaf.Node.IsArray ? "array" : leaf.Node.IsScalar ? "scalar" : "map",
            defaults.Where(item => item.Key.Equals(leaf.Path, StringComparison.OrdinalIgnoreCase) ||
                item.Key.StartsWith(leaf.Path + ":", StringComparison.OrdinalIgnoreCase)).ToArray())).ToArray();
        return new NamespaceSkeleton(name, root, keys);
    }

    private static void Flatten(SchemaNode node, string path, bool required, bool sensitive, List<(string, SchemaNode, bool, bool)> leaves, List<KeyValuePair<string, string>> defaults)
    {
        sensitive |= node.Sensitive;
        if (node.Default is { } value && path.Length != 0) Expand(value, path, defaults);
        if (node.Properties.Count == 0 || node.IsArray)
        {
            if (path.Length != 0) leaves.Add((path, node, required, sensitive));
            return;
        }
        foreach (var (key, (name, child)) in node.Properties.OrderBy(item => item.Key, StringComparer.Ordinal))
            Flatten(child, path.Length == 0 ? name : path + ":" + name, required && node.Required.Contains(key), sensitive, leaves, defaults);
    }

    private static void Expand(JsonElement value, string path, List<KeyValuePair<string, string>> defaults)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject()) Expand(property.Value, path + ":" + property.Name, defaults);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray()) Expand(item, path + ":" + (index++).ToString(CultureInfo.InvariantCulture), defaults);
                break;
            case JsonValueKind.String: Add(value.GetString()!); break;
            case JsonValueKind.True: Add("true"); break;
            case JsonValueKind.False: Add("false"); break;
            case JsonValueKind.Number: Add(value.GetRawText()); break;
        }
        void Add(string text)
        {
            if (!defaults.Any(item => item.Key.Equals(path, StringComparison.OrdinalIgnoreCase))) defaults.Add(new(path, text));
        }
    }

    // 多键：不在本 namespace 前缀下、封闭对象里没有的属性、标量下还有子键、对象或数组本身被赋值、数组下标不是非负整数。
    // 缺键：已出现的对象（根对象总是出现）里 required 的属性没有任何键；未出现的可选对象不检查其内部 required。
    internal IReadOnlyList<KeyFinding> Compare(string profile, IEnumerable<string> online)
    {
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var findings = new List<KeyFinding>();
        foreach (var key in online.Order(StringComparer.Ordinal))
        {
            if (!key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) || key.Length == Prefix.Length)
            { findings.Add(new KeyFinding("extra", profile, Name, key, "#")); continue; }
            var segments = key[Prefix.Length..].Split(':');
            for (var i = 1; i <= segments.Length; i++) present.Add(string.Join(':', segments.Take(i)));
            if (Reject(segments) is { } pointer) findings.Add(new KeyFinding("extra", profile, Name, key, pointer));
        }
        Missing(_root, "", present, profile, findings);
        return findings;
    }

    private string? Reject(string[] segments)
    {
        var node = _root;
        foreach (var segment in segments)
        {
            if (segment.Length == 0) return node.Pointer;
            if (node.IsArray)
            {
                if (!int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out _)) return node.Pointer;
                if (node.Items is not { } items) return null;
                node = items; continue;
            }
            if (node.Properties.TryGetValue(segment, out var property)) { node = property.Node; continue; }
            if (node.Additional is { } additional) { node = additional; continue; }
            return node.Closed || node.IsScalar ? node.Pointer : null;
        }
        return node.IsArray || node.Properties.Count != 0 ? node.Pointer : null;
    }

    private void Missing(SchemaNode node, string path, HashSet<string> present, string profile, List<KeyFinding> findings)
    {
        foreach (var (key, (name, child)) in node.Properties.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var childPath = path.Length == 0 ? name : path + ":" + name;
            if (!present.Contains(childPath))
            {
                if (node.Required.Contains(key)) findings.Add(new KeyFinding("missing", profile, Name, Prefix + childPath, child.Pointer));
            }
            else if (child.Properties.Count != 0 && !child.IsArray) Missing(child, childPath, present, profile, findings);
        }
        // required 里写了 properties 没有的名字：封闭对象永远无法满足，同样报缺键。
        foreach (var name in node.Required.Where(name => !node.Properties.ContainsKey(name)).Order(StringComparer.Ordinal))
        {
            var childPath = path.Length == 0 ? name : path + ":" + name;
            if (!present.Contains(childPath)) findings.Add(new KeyFinding("missing", profile, Name, Prefix + childPath, node.Pointer + "/required"));
        }
    }
}
