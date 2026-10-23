using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using Json.Schema.Keywords;
using Qian.Common;
using Qian.Profiles.Abstractions;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;
using VersionRange = SemanticVersioning.Range;
using SemanticVersion = SemanticVersioning.Version;

namespace Qian.Profiles;

/// <summary>组合包 → profile patch → home patch → CLI overlay 的静态装配器。</summary>
public sealed class ProfileLoader(ISurfaceReader reader) : IProfileLoader
{
    /// <inheritdoc />
    public ProfileLoadResult Load(ProfileLoadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new Composition(reader, options).Load();
    }

    private sealed class Composition(ISurfaceReader reader, ProfileLoadOptions options)
    {
        private readonly string _root = Path.GetFullPath(options.WorkingDirectory);
        private readonly string _trace = Guid.NewGuid().ToString("N");
        private readonly List<SurfaceDiagnostic> _diagnostics = [];
        private readonly List<ProfileLayer> _layers = [];
        private readonly Dictionary<string, WorkingEntry> _entries = new(StringComparer.Ordinal);
        private readonly List<Component> _components = [];
        private readonly ErrorCatalog _catalog = CreateCatalog();
        private readonly Dictionary<string, SurfaceSyntax> _sources = new(StringComparer.Ordinal);
        private JsonElement _profile;
        private string _profileFile = "";
        private static ErrorCatalog CreateCatalog() { var catalog = ErrorCatalog.CreateDefault(); catalog.Register(ProfileErrors.Invalid); return catalog; }

        internal ProfileLoadResult Load()
        {
            try
            {
                if (!Regex.IsMatch(options.Profile, @"^[a-z][a-z0-9-]*\z", RegexOptions.CultureInvariant)) throw new ArgumentException("profile 名无效。");
                _profileFile = Path.Combine(_root, "profiles", options.Profile, "qian.profile.yml");
                _profile = Read(_profileFile, "profile");
                if (Text(_profile, "name") != options.Profile) Fail(_profileFile, "/name", "profile 名必须与目录一致。");
                if (options.Profile is "gateway" or "identity" && Flag(_profile, "hooks", "allowCommand")) Fail(_profileFile, "/hooks/allowCommand", "此 profile 不允许 command hook。");
                Discover(Path.Combine(_root, "plugins"));
                Discover(Path.Combine(_root, ".qian", "plugins"));
                foreach (var directory in options.ComponentDirectories) Discover(Path.GetFullPath(directory, _root));
                foreach (var reference in _profile.GetProperty("bundles").EnumerateArray().Select(e => e.GetString()!))
                {
                    var bundle = Resolve(reference, _profileFile, "/bundles");
                    var patch = bundle.Manifest.TryGetProperty("surfaces", out var surfaces) && surfaces.TryGetProperty("patch", out var custom)
                        ? custom.GetString()! : "./config/qian.patch.yml";
                    var file = Within(bundle.Directory, patch);
                    AddLayer("bundle", file, bundle.Id + "@" + bundle.Version, required: true);
                }
                if (_profile.TryGetProperty("patch", out var profilePatch)) AddLayer("profile-patch", Within(Path.GetDirectoryName(_profileFile)!, profilePatch.GetString()!), null, required: true);
                if (options.IncludeHomePatch)
                {
                    var home = options.HomeDirectory ?? Environment.GetEnvironmentVariable("QIAN_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qian");
                    AddLayer("home-patch", Path.Combine(Path.GetFullPath(home, _root), "qian.patch.yml"), null, required: false);
                }
                foreach (var patch in options.Patches) AddLayer("overlay", Path.GetFullPath(patch, _root), null, required: true);
                foreach (var patch in options.ScopePatches) AddScopeLayer(patch);
                var entries = ResolveEntries();
                var order = Order(entries);
                return new ProfileLoadResult(new ProfileSnapshot(options.Profile, _layers.ToArray(), entries, order)
                {
                    DisabledEntries = _entries.Values.Where(e => e.Disabled).Select(e => new DisabledProfileEntry(
                        e.Id, e.Scope, e.Plugin, e.Layer, Display(e.SourceFile) + "#" + e.SourcePointer)).ToArray()
                }, _diagnostics.ToArray());
            }
            catch (CompositionFailed) { return new ProfileLoadResult(null, _diagnostics.ToArray()); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException or FormatException or JsonSchemaException or RefResolutionException or RegexMatchTimeoutException)
            {
                Error(_profileFile.Length == 0 ? "qian.profile.yml" : _profileFile, "", "profile 或组件内容无法解析。", "error");
                return new ProfileLoadResult(null, _diagnostics.ToArray());
            }
        }

        private JsonElement Read(string file, string schema)
        {
            var display = Display(file);
            if (!File.Exists(file) || new FileInfo(file).Length > 1_048_576) Fail(file, "", "文件不存在或超过大小限制。");
            var parsed = reader.Parse(display, File.ReadAllText(file, new System.Text.UTF8Encoding(false, true)), traceId: _trace);
            _diagnostics.AddRange(parsed.Diagnostics);
            if (parsed.Syntax is null || parsed.Diagnostics.Any(d => d.Severity == "error")) throw new CompositionFailed();
            _sources[display] = parsed.Syntax;
            var errors = reader.ValidateSchema(schema, display, parsed.Syntax.Data, _trace);
            foreach (var diagnostic in errors)
            {
                var pointer = diagnostic.Error.Pointer!.Split('#')[1];
                _diagnostics.Add(diagnostic with { Line = Line(parsed.Syntax.Lines, pointer) });
            }
            if (errors.Any(d => d.Severity == "error")) throw new CompositionFailed();
            return parsed.Syntax.Data;
        }

        private void Discover(string directory, int depth = 0)
        {
            if (!Directory.Exists(directory)) return;
            if (depth > 32) Fail(directory, "", "组件目录深度超过限制。");
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) Fail(directory, "", "组件目录不能是符号链接。");
            var manifestFile = Path.Combine(directory, ".qian-plugin", "plugin.json");
            if (File.Exists(manifestFile))
            {
                Within(directory, "./.qian-plugin/plugin.json");
                var manifest = Read(manifestFile, "plugin");
                var component = new Component(Text(manifest, "id"), Text(manifest, "version"), directory, manifest);
                if (_components.Any(c => c.Id == component.Id && c.Version == component.Version)) Fail(manifestFile, "/id", "同一组件版本在目录中重复。");
                _components.Add(component);
                if (_components.Count > 1024) Fail(manifestFile, "", "组件数量超过限制。");
                Discover(Path.Combine(directory, "components"), depth + 1);
                return;
            }
            foreach (var child in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
                if (Path.GetFileName(child) is not (".git" or "node_modules" or "bin" or "obj" or "dist")) Discover(child, depth + 1);
        }

        private Component Resolve(string reference, string file, string pointer)
        {
            Component? candidate;
            try { candidate = Find(reference); }
            catch (Exception error) when (error is ArgumentException or FormatException)
            { Fail(file, pointer, "组件版本范围无效。"); throw; }
            if (candidate is null) Fail(file, pointer, "本地目录没有满足版本范围的组件；先安装或提供组件目录。");
            return candidate!;
        }
        private Component? Find(string reference)
        {
            var pieces = reference.Split('@', 2);
            var range = new VersionRange(pieces.Length == 2 ? pieces[1] : "*");
            return _components.Where(c => c.Id == pieces[0] && range.IsSatisfied(new SemanticVersion(c.Version)))
                .OrderByDescending(c => new SemanticVersion(c.Version)).FirstOrDefault();
        }

        private void AddLayer(string kind, string file, string? reference, bool required)
        {
            var index = _layers.Count;
            var present = File.Exists(file);
            _layers.Add(new ProfileLayer(index, kind, Display(file), reference, present));
            if (!present && !required) return;
            AddEntries(Read(file, "patch"), file, index, null);
        }

        private void AddScopeLayer(ScopePatchLayer layer)
        {
            var scope = layer.Scope;
            if (scope.Kind is not ("tenant" or "app" or "agent-session") || string.IsNullOrWhiteSpace(scope.Id) ||
                scope.Id.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == ':'))
                Fail("registry/scope-patches.json", "/scope", "数据库层必须声明合法的非全局 scope。");
            var file = layer.Source;
            if (string.IsNullOrWhiteSpace(file) || Path.IsPathRooted(file) || file.Split('/', '\\').Any(p => p == "..") || file.Any(char.IsControl))
                Fail("registry/scope-patches.json", "", "数据库层来源必须为安全的相对名称。");
            if (layer.Patches.ValueKind != JsonValueKind.Array) Fail(file, "", "scope patch 必须为数组。");
            var raw = JsonSerializer.Serialize(layer.Patches);
            if (System.Text.Encoding.UTF8.GetByteCount(raw) > 1_048_576) Fail(file, "", "scope patch 超过大小限制。");
            var parsed = reader.Parse(Display(file), raw, traceId: _trace);
            _diagnostics.AddRange(parsed.Diagnostics);
            if (parsed.Syntax is null || parsed.Diagnostics.Any(d => d.Severity == "error")) throw new CompositionFailed();
            _sources[Display(file)] = parsed.Syntax;
            var errors = reader.ValidateSchema("patch", Display(file), parsed.Syntax.Data, _trace);
            _diagnostics.AddRange(errors);
            if (errors.Any(d => d.Severity == "error")) throw new CompositionFailed();
            var index = _layers.Count;
            _layers.Add(new ProfileLayer(index, "scope-patch", Display(file), null, true));
            AddEntries(parsed.Syntax.Data, file, index, scope.Kind + ":" + scope.Id);
        }

        private void AddEntries(JsonElement patch, string file, int index, string? owningScope)
        {
            var position = 0; var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in patch.EnumerateArray())
            {
                var pointer = "/" + position++;
                var id = Text(value, "id"); var scope = Text(value, "scope"); if (scope.Length == 0) scope = owningScope ?? "global";
                if (owningScope is not null && scope != owningScope) Fail(file, pointer + "/scope", "patch 不能改写本层所属 scope。");
                var key = scope + "/" + id;
                if (!seen.Add(key)) Fail(file, pointer + "/id", "同一层不能重复声明同一 scope/id。");
                var plugin = Text(value, "plugin");
                if (!_entries.TryGetValue(key, out var current))
                {
                    if (owningScope is not null && _entries.TryGetValue("global/" + id, out var inherited))
                    {
                        current = new WorkingEntry(id, scope, inherited.Plugin, inherited.Config.DeepClone().AsObject(), index, inherited.ConfigFile, inherited.ConfigPointer)
                        { Disabled = inherited.Disabled, Note = inherited.Note };
                    }
                    else
                    {
                        if (plugin.Length == 0) Fail(file, pointer + "/plugin", "新条目必须声明 plugin。");
                        if (!value.TryGetProperty("config", out _)) Fail(file, pointer + "/config", "新条目必须声明 config，可为空对象。");
                        current = new WorkingEntry(id, scope, plugin, new JsonObject(), index, file, pointer + "/config");
                    }
                }
                var disabled = value.TryGetProperty("disabled", out var flag) ? flag.GetBoolean() : current.Disabled;
                Protect(current, plugin.Length == 0 ? current.Plugin : plugin, disabled, file, pointer);
                if (scope != "global" && _entries.TryGetValue("global/" + id, out var global))
                    Protect(global, plugin.Length == 0 ? current.Plugin : plugin, disabled, file, pointer);
                if (plugin.Length > 0) current.Plugin = plugin;
                if (value.TryGetProperty("config", out var config))
                {
                    current.Config = JsonNode.Parse(config.GetRawText())!.AsObject();
                    current.ConfigFile = file; current.ConfigPointer = pointer + "/config";
                }
                current.Disabled = disabled; current.Layer = index; current.SourceFile = file; current.SourcePointer = pointer;
                if (value.TryGetProperty("note", out var note)) current.Note = note.GetString();
                _entries[key] = current;
            }
        }

        private void Protect(WorkingEntry current, string replacement, bool disabled, string file, string pointer)
        {
            Component? component;
            try { component = Find(current.Plugin); }
            catch (Exception error) when (error is ArgumentException or FormatException) { return; }
            // 中间层可能被后层替换；仅最终活动条目要求可解析。
            if (component is null) return;
            if (component.Manifest.GetProperty("policy").TryGetProperty("required", out var required) && required.GetBoolean() &&
                (disabled || replacement.Split('@')[0] != component.Id)) Fail(file, pointer, "required 组件不能禁用或替换为另一个组件。");
        }

        private ResolvedProfileEntry[] ResolveEntries()
        {
            var result = new List<ResolvedProfileEntry>();
            foreach (var entry in _entries.Values.Where(e => !e.Disabled))
            {
                var component = Resolve(entry.Plugin, entry.SourceFile, entry.SourcePointer + "/plugin");
                var kind = entry.Scope == "global" ? "global" : entry.Scope.Split(':', 2)[0];
                if (!Strings(component.Manifest, "scopes").Contains(kind, StringComparer.Ordinal)) Fail(entry.SourceFile, entry.SourcePointer + "/scope", "组件不允许此 scope 种类。");
                if (Flag(_profile, "build", "aot") && component.Manifest.TryGetProperty("runtime", out var runtime) &&
                    (Text(runtime, "form") == "function" || !component.Manifest.TryGetProperty("x-aot", out var aot) || aot.ValueKind != JsonValueKind.True))
                    Fail(entry.SourceFile, entry.SourcePointer + "/plugin", "AOT profile 含未声明 AOT 兼容的代码组件。");
                var config = ValidateConfig(component, entry);
                if (component.Manifest.TryGetProperty("x-skeleton", out var skeleton) && skeleton.ValueKind == JsonValueKind.True)
                    Error(entry.SourceFile, entry.SourcePointer + "/plugin", "配置骨架不代表业务 Provider 已实现；启动宿主仍需真实代码。", "warning");
                result.Add(new ResolvedProfileEntry(entry.Id, entry.Scope, component.Id + "@" + component.Version, config, entry.Layer,
                    Strings(component.Manifest, "inject"), Strings(component.Manifest, "optionalInject"), Strings(component.Manifest, "provides"), entry.Note)
                    { ComponentDirectory = component.Directory, Manifest = component.Manifest });
            }
            return result.ToArray();
        }

        private JsonElement ValidateConfig(Component component, WorkingEntry entry)
        {
            var relative = component.Manifest.TryGetProperty("runtime", out var runtime) ? Text(runtime, "configSchema") : "";
            var schemaPath = Within(component.Directory, relative.Length == 0 ? "./schemas/config.schema.json" : relative);
            if (!File.Exists(schemaPath))
            {
                if (entry.Config.Count > 0) Fail(entry.ConfigFile, entry.ConfigPointer, "有配置的组件必须提供 config.schema.json。");
                return JsonSerializer.SerializeToElement(entry.Config);
            }
            if (new FileInfo(schemaPath).Length > 1_048_576) Fail(schemaPath, "", "Schema 超过大小限制。");
            // 与 profiles.ts 的 config 同序：数值归一、预检、元 Schema、默认值、验证。数值先换成双精度（同 CLI 的 JSON.parse），两端按同一数值判断。
            var schemaNode = Doubles(JsonNode.Parse(Utf8Text(schemaPath)));
            var declared = Check(schemaNode, schemaPath);
            if (!MetaSchemas.Draft202012.Evaluate(JsonSerializer.SerializeToElement(schemaNode), new EvaluationOptions { OutputFormat = OutputFormat.Flag }).IsValid)
                Fail(schemaPath, "", "组件 config.schema 不符合 JSON Schema 2020-12 的元 Schema。");
            var config = Defaults(schemaNode!, entry.Config.DeepClone())!;
            // 子 Schema 上的 $schema 会让验证器换回内置方言、绕过 AjvSemantics 替换的关键字；预检已确认只声明 2020-12，去掉不改变含义。
            foreach (var rule in declared) rule.Remove("$schema");
            var schema = JsonSchema.FromText(schemaNode!.ToJsonString(), new BuildOptions { Dialect = AjvSemantics, SchemaRegistry = new SchemaRegistry { Fetch = (_, _) => null } });
            UsePortablePatterns(schema.Root, new HashSet<JsonSchemaNode>(ReferenceEqualityComparer.Instance));
            var data = JsonSerializer.SerializeToElement(config);
            var evaluation = schema.Evaluate(JsonSerializer.SerializeToElement(Doubles(config)), new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical, IncludeApplicatorErrors = true });
            if (!evaluation.IsValid)
            {
                var locations = new SortedSet<string>(StringComparer.Ordinal);
                Locations(evaluation, locations);
                foreach (var location in locations) Error(entry.ConfigFile, entry.ConfigPointer + location, "条目配置不符合组件 config.schema。", "error");
                throw new CompositionFailed();
            }
            Sensitive(config, schemaNode, entry.ConfigFile, entry.ConfigPointer);
            return data;
        }

        // 含子 Schema 的关键字与 $ref，按序数排列；值为映射或数组的分别列出。
        private static readonly string[] SchemaKeywords = ["$defs", "$ref", "additionalProperties", "allOf", "anyOf", "contains", "contentSchema", "definitions", "dependentSchemas",
            "else", "if", "items", "not", "oneOf", "patternProperties", "prefixItems", "properties", "propertyNames", "then"];
        private static readonly HashSet<string> SchemaMaps = new(StringComparer.Ordinal) { "$defs", "definitions", "dependentSchemas", "patternProperties", "properties" };
        private static readonly HashSet<string> SchemaLists = new(StringComparer.Ordinal) { "allOf", "anyOf", "oneOf", "prefixItems" };
        // 在同一实例上求值子 Schema 的关键字，按序数排列。
        private static readonly HashSet<string> InPlace = new(StringComparer.Ordinal) { "$ref", "allOf", "anyOf", "dependentSchemas", "else", "if", "not", "oneOf", "then" };
        // 两端验证器解释不同、或会让文档内 JSON Pointer 引用失去单一含义的关键字，按序数排列：标识与动态引用、2019-09 与 draft-07 的旧写法、Ajv 自有的 $async 与 nullable，
        // 以及 unevaluatedItems、unevaluatedProperties（Ajv 统计已求值的项与属性时与规范不同，见 README）。
        private static readonly string[] Unsupported = ["$anchor", "$async", "$dynamicAnchor", "$dynamicRef", "$id", "$recursiveAnchor", "$recursiveRef", "$vocabulary", "dependencies",
            "nullable", "unevaluatedItems", "unevaluatedProperties"];
        private static readonly System.Text.UTF8Encoding StrictUtf8 = new(false, true);

        private sealed class SchemaWalk(JsonNode? root, string file)
        {
            internal readonly JsonNode? Root = root;
            internal readonly string SchemaFile = file;
            internal readonly Dictionary<string, JsonObject> Visited = new(StringComparer.Ordinal);
            internal readonly List<string> Order = [];
            internal readonly Dictionary<string, string> References = new(StringComparer.Ordinal);
            internal readonly List<JsonObject> Declared = [];
        }

        // 与 profiles.ts 的 check 同序同义的预检。从根开始按上表顺序走遍各子 Schema 位置，映射按键名序数、数组按下标。正则超出可移植子集、
        // $schema 不是 2020-12、出现 Unsupported 中的关键字、enum 不是非空数组、$ref 不指向子 Schema 位置（见 Target）时，在 Schema 文件的对应指针处失败。
        // $ref 的目标都在这次遍历之内，随后的元 Schema 校验也覆盖它们。走完后检查循环引用（见 Cycles）。返回声明了 $schema 的子 Schema。
        private List<JsonObject> Check(JsonNode? root, string file)
        {
            var walk = new SchemaWalk(root, file);
            Check(walk, root, "", 0);
            Cycles(walk);
            return walk.Declared;
        }

        private void Check(SchemaWalk walk, JsonNode? schema, string pointer, int depth)
        {
            if (depth > 64) throw new JsonException("配置 Schema 嵌套深度超过限制。");
            if (schema is not JsonObject rule || !walk.Visited.TryAdd(pointer, rule)) return;
            walk.Order.Add(pointer);
            if (rule.ContainsKey("$schema"))
            {
                if (rule["$schema"] is not JsonValue declared || !declared.TryGetValue<string>(out var dialect) ||
                    dialect is not ("https://json-schema.org/draft/2020-12/schema" or "https://json-schema.org/draft/2020-12/schema#"))
                    Fail(walk.SchemaFile, pointer + "/$schema", "组件 config.schema 只接受 JSON Schema 2020-12，$schema 须为 https://json-schema.org/draft/2020-12/schema。");
                walk.Declared.Add(rule);
            }
            foreach (var keyword in Unsupported) if (rule.ContainsKey(keyword)) Fail(walk.SchemaFile, pointer + "/" + keyword, "组件 config.schema 不支持 " + keyword + " 关键字。");
            if (rule.ContainsKey("enum") && rule["enum"] is not JsonArray { Count: > 0 }) Fail(walk.SchemaFile, pointer + "/enum", "组件 config.schema 的 enum 须为非空数组。");
            if (rule["pattern"] is JsonValue pattern && pattern.TryGetValue<string>(out var source)) Portable(source, walk.SchemaFile, pointer + "/pattern", "pattern");
            foreach (var keyword in SchemaKeywords)
            {
                var value = rule[keyword];
                if (keyword == "$ref")
                {
                    if (value is not JsonValue reference || !reference.TryGetValue<string>(out var path)) continue;
                    if (Target(walk.Root, path) is not { } target || !(target.Node is JsonObject || target.Node is JsonValue boolean && boolean.GetValueKind() is JsonValueKind.True or JsonValueKind.False))
                    { Fail(walk.SchemaFile, pointer + "/$ref", "组件 config.schema 的 $ref 须为 #/ 开头、指向本文档内子 Schema 位置（对象或布尔值）的 JSON Pointer。"); continue; }
                    walk.References[pointer] = target.Pointer;
                }
                else if (SchemaMaps.Contains(keyword))
                {
                    if (value is not JsonObject map) continue;
                    foreach (var item in map.OrderBy(item => item.Key, StringComparer.Ordinal))
                    {
                        var at = pointer + "/" + keyword + "/" + Escape(item.Key);
                        // Ajv 为防原型污染略过这两个映射里的 __proto__ 键，.NET 照常求值。
                        if (keyword is "properties" or "patternProperties" && item.Key == "__proto__") Fail(walk.SchemaFile, at, "组件 config.schema 的 " + keyword + " 不能以 __proto__ 为键名。");
                        if (keyword == "patternProperties") Portable(item.Key, walk.SchemaFile, at, keyword);
                        Check(walk, item.Value, at, depth + 1);
                    }
                }
                else if (SchemaLists.Contains(keyword))
                {
                    if (value is not JsonArray list) continue;
                    for (var i = 0; i < list.Count; i++) Check(walk, list[i], pointer + "/" + keyword + "/" + i, depth + 1);
                }
                else Check(walk, value, pointer + "/" + keyword, depth + 1);
            }
        }

        // 子 Schema 经 $ref 与 InPlace 中的关键字回到自身时会在同一实例上无限递归：JsonSchema.Net 在构造或求值时报循环，Ajv 在编译或验证时栈溢出，
        // 只在被引用时出错且两端时机不同。按预检的访问顺序逐个出发，沿 InPlace 的顺序深度优先（显式栈），遇到回到栈上 Schema 的边即在该边处失败。
        private void Cycles(SchemaWalk walk)
        {
            var done = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var start in walk.Order)
            {
                if (done.ContainsKey(start)) continue;
                var stack = new Stack<(string Pointer, List<(string At, string Target)> Edges, int Next)>();
                done[start] = false; stack.Push((start, Edges(walk, start), 0));
                while (stack.TryPop(out var frame))
                {
                    if (frame.Next == frame.Edges.Count) { done[frame.Pointer] = true; continue; }
                    stack.Push(frame with { Next = frame.Next + 1 });
                    var (at, target) = frame.Edges[frame.Next];
                    if (!walk.Visited.ContainsKey(target)) continue;
                    if (done.TryGetValue(target, out var finished))
                    {
                        if (!finished) Fail(walk.SchemaFile, at, "组件 config.schema 的引用构成循环：子 Schema 不经属性或数组项又回到了自身。");
                        continue;
                    }
                    done[target] = false; stack.Push((target, Edges(walk, target), 0));
                }
            }
        }

        private static List<(string At, string Target)> Edges(SchemaWalk walk, string pointer)
        {
            var rule = walk.Visited[pointer]; var edges = new List<(string At, string Target)>();
            foreach (var keyword in SchemaKeywords.Where(InPlace.Contains))
            {
                var at = pointer + "/" + keyword;
                if (keyword == "$ref") { if (walk.References.TryGetValue(pointer, out var target)) edges.Add((at, target)); }
                else if (SchemaLists.Contains(keyword)) { if (rule[keyword] is JsonArray list) for (var i = 0; i < list.Count; i++) edges.Add((at + "/" + i, at + "/" + i)); }
                else if (SchemaMaps.Contains(keyword))
                { if (rule[keyword] is JsonObject map) foreach (var item in map.OrderBy(item => item.Key, StringComparer.Ordinal)) edges.Add((at + "/" + Escape(item.Key), at + "/" + Escape(item.Key))); }
                else if (rule.ContainsKey(keyword)) edges.Add((at, at));
            }
            return edges;
        }

        // 文档内引用的目标与其规范指针，同 profiles.ts 的 target：只接受 #/ 开头、字符合乎 URI 片段的 JSON Pointer；百分号编码须为合法 UTF-8，
        // 解码后不含 /（JsonSchema.Net 先解码整个片段再分段，Ajv 先分段再解码），~ 只能出现在 ~0、~1 中。目标须为子 Schema 位置：从根起逐段经
        // SchemaKeywords 中的关键字到达（$ref 除外），映射再经键名，数组再经不带前导零的十进制下标。其他位置不在元 Schema 的校验范围内，
        // 两个验证器对其中不合规的写法处理不同；#/ 按 JSON Pointer 指向根下的空键名，Ajv 却当作根。正则以 \z 收尾：.NET 的 $ 在末尾的换行之前也成立。
        private static (JsonNode? Node, string Pointer)? Target(JsonNode? root, string reference)
        {
            if (!reference.StartsWith("#/", StringComparison.Ordinal) || !Regex.IsMatch(reference[1..], @"^(?:[A-Za-z0-9._~!$&'()*+,;=:@/?-]|%[0-9A-Fa-f]{2})*\z", RegexOptions.CultureInvariant)) return null;
            var node = root; var pointer = ""; string? container = null;
            foreach (var raw in reference[2..].Split('/'))
            {
                if (Unescape(raw) is not { } segment || segment.Contains('/', StringComparison.Ordinal) || Regex.IsMatch(segment, "~(?![01])", RegexOptions.CultureInvariant)) return null;
                segment = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
                // container 为 null 时位于子 Schema 上，否则位于该映射或数组关键字的值上。
                if (container is null && node is JsonObject rule && segment != "$ref" && SchemaKeywords.Contains(segment) && rule.TryGetPropertyValue(segment, out var value))
                { node = value; container = SchemaMaps.Contains(segment) || SchemaLists.Contains(segment) ? segment : null; }
                else if (container is not null && SchemaMaps.Contains(container) && node is JsonObject map && map.TryGetPropertyValue(segment, out var child)) { node = child; container = null; }
                else if (container is not null && SchemaLists.Contains(container) && node is JsonArray list && Regex.IsMatch(segment, @"^(?:0|[1-9][0-9]{0,8})\z", RegexOptions.CultureInvariant) &&
                    int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture) < list.Count) { node = list[int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture)]; container = null; }
                else return null;
                pointer += "/" + Escape(segment);
            }
            return container is null ? (node, pointer) : null;
        }

        // 同 JS 的 decodeURIComponent：%XX 序列按 UTF-8 严格解码，不合法时返回 null；其余字符原样保留。
        private static string? Unescape(string segment)
        {
            var text = new System.Text.StringBuilder(); var bytes = new List<byte>();
            for (var i = 0; i < segment.Length; i++)
            {
                if (segment[i] == '%')
                {
                    bytes.Add(Convert.ToByte(segment.Substring(i + 1, 2), 16)); i += 2;
                    continue;
                }
                if (!Flush()) return null;
                text.Append(segment[i]);
            }
            return Flush() ? text.ToString() : null;
            bool Flush()
            {
                if (bytes.Count == 0) return true;
                try { text.Append(StrictUtf8.GetString(bytes.ToArray())); }
                catch (System.Text.DecoderFallbackException) { return false; }
                bytes.Clear();
                return true;
            }
        }

        // 把 JSON 数值换成双精度值，同 JS 的 Number：超出范围的按无法解析处理，-0 记为 0（JS 的比较、相等与整除都不区分两者）。
        // 字符串与键名含残缺代理项时也按无法解析处理（JS 能表示，.NET 读取时失败）。
        private static JsonNode? Doubles(JsonNode? node) => node switch
        {
            JsonObject map => new JsonObject(map.Select(item => KeyValuePair.Create(WellFormed(item.Key), Doubles(item.Value)))),
            JsonArray list => new JsonArray(list.Select(Doubles).ToArray()),
            JsonValue value when value.GetValueKind() == JsonValueKind.Number => Double(value.GetValue<double>()),
            JsonValue value when value.GetValueKind() == JsonValueKind.String => JsonValue.Create(WellFormed(value.GetValue<string>())),
            _ => node?.DeepClone(),
        };
        private static JsonValue Double(double number) => double.IsFinite(number) ? JsonValue.Create(number == 0 ? 0 : number) : throw new JsonException("数值超出双精度范围。");
        private static string WellFormed(string text) { _ = StrictUtf8.GetByteCount(text); return text; }

        // 同 CLI 的 TextDecoder（fatal）：按 UTF-8 严格解码，只略去开头的一个 UTF-8 BOM。File.ReadAllText 遇到 UTF-16、UTF-32 的 BOM 会改按它们解码。
        private static string Utf8Text(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var skip = bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
            return StrictUtf8.GetString(bytes, skip, bytes.Length - skip);
        }

        private void Portable(string pattern, string file, string pointer, string keyword)
        {
            try { _ = SchemaPattern.Compile(pattern); }
            catch (Exception error) when (error is ArgumentException or NotSupportedException)
            { Fail(file, pointer, "组件 config.schema 的 " + keyword + " 含不支持的正则写法，只接受 ECMA-262 可移植子集。"); }
        }

        // 验证器默认按 .NET 的 ECMAScript 选项编译正则，判断与 CLI 不同；构造后把 pattern 与 patternProperties 的编译结果换成共享翻译器的实例，
        // 两端按同一语义匹配字符串值与键名。additionalProperties、unevaluatedProperties 读 patternProperties 的注解，随之一致。
        // 引用解析出的子 Schema 也在 Subschemas 里。预检已跟随 $ref 走遍验证器会求值的位置，这里遇到不支持的写法时仍按无法解析处理，与 CLI 的 Ajv 编译失败相同。
        // patternProperties 按 JSON Pointer 转义后的模式（~ 记为 ~0、/ 记为 ~1）取正则，字典的键也按此转义，否则含这两个字符的模式在求值时找不到实例（9.4.0）。
        private static void UsePortablePatterns(JsonSchemaNode node, HashSet<JsonSchemaNode> seen)
        {
            if (!seen.Add(node)) return;
            foreach (var keyword in node.Keywords)
            {
                if (keyword.Handler is PatternKeyword) keyword.Value = Engine(keyword.RawValue.GetString()!);
                else if (keyword.Handler is PatternPropertiesKeyword) keyword.Value = keyword.RawValue.EnumerateObject().ToDictionary(item => Escape(item.Name), item => Engine(item.Name), StringComparer.Ordinal);
                foreach (var child in keyword.Subschemas) UsePortablePatterns(child, seen);
            }
        }

        private static Regex Engine(string pattern)
        {
            try { return SchemaPattern.Compile(pattern); }
            catch (NotSupportedException) { throw new JsonException("配置 Schema 含不支持的正则写法。"); }
        }

        // 容器关键字只经由失败的子节点报告；Ajv 也不为它们输出自身的错误（additionalProperties、propertyNames 的错误落在键名上，同子节点位置）。
        private static readonly HashSet<string> Containers = new(StringComparer.Ordinal)
        {
            "$ref", "additionalProperties", "allOf", "dependentSchemas", "items", "patternProperties", "prefixItems", "properties", "propertyNames",
        };

        // 与 profiles.ts 的 locations 同义，把验证结果归一为出错实例位置：容器之外的失败关键字报自身位置，含 anyOf、oneOf、not、contains 与 then/else；
        // 只进入随关键字一起失败的子节点，if 的条件分支、通过的 anyOf 下失败的分支都不计。contains 不进入逐项子节点：Ajv 计数达到上限即停止逐项检查。
        // oneOf 因两个以上分支同时成立而失败时也不进入子节点：Ajv 第二个分支成立后不再求值其余分支。
        private static void Locations(EvaluationResults node, SortedSet<string> found)
        {
            var keys = node.Errors?.Keys.ToArray() ?? [];
            var ambiguous = (node.Details ?? []).Count(child => child.IsValid && Keyword(node, child) == "oneOf") > 1;
            var descended = false;
            foreach (var child in node.Details ?? [])
            {
                if (child.IsValid || child.EvaluationPath.SegmentCount <= node.EvaluationPath.SegmentCount) continue;
                var keyword = Keyword(node, child);
                if (keyword == "contains" || (keyword == "oneOf" && ambiguous) || (keyword != "$ref" && !keys.Contains(keyword))) continue;
                descended = true;
                Locations(child, found);
            }
            if (keys.Any(key => !Containers.Contains(key)) || (keys.Length == 0 && !descended)) found.Add(node.InstanceLocation.ToString());
        }
        private static string? Keyword(EvaluationResults node, EvaluationResults child) =>
            child.EvaluationPath.SegmentCount > node.EvaluationPath.SegmentCount ? child.EvaluationPath[node.EvaluationPath.SegmentCount].ToString() : null;

        private static JsonNode? Defaults(JsonNode schema, JsonNode? value, JsonNode? root = null, int depth = 0)
        {
            if (depth > 64) throw new JsonException("配置 Schema 引用深度超过限制。");
            root ??= schema;
            if (schema is not JsonObject rule) return value;
            if (LocalReference(rule, root) is { } referenced) value = Defaults(referenced, value, root, depth + 1);
            if (rule["allOf"] is JsonArray all) foreach (var child in all) if (child is not null) value = Defaults(child, value, root, depth + 1);
            if (value is JsonArray array && rule["items"] is { } items)
                for (var i = 0; i < array.Count; i++) array[i] = Defaults(items, array[i]?.DeepClone(), root, depth + 1);
            if (value is JsonObject target)
            {
                if (rule["default"] is JsonObject defaults) foreach (var item in defaults) if (!target.ContainsKey(item.Key)) target[item.Key] = item.Value?.DeepClone();
                if (rule["properties"] is JsonObject properties)
                    foreach (var item in properties)
                    {
                        // default 可为 JSON null（JsonNode 记作 null），同 CLI 照样写入。
                        if (!target.ContainsKey(item.Key) && item.Value is JsonObject property && property.TryGetPropertyValue("default", out var fallback)) target[item.Key] = fallback?.DeepClone();
                        if (target.TryGetPropertyValue(item.Key, out var child) && item.Value is not null) target[item.Key] = Defaults(item.Value, child?.DeepClone(), root, depth + 1);
                    }
            }
            return value;
        }
        // 预检已确认走到的 $ref 都能解析；目标为 JSON null 时同没有引用。
        private static JsonNode? LocalReference(JsonObject schema, JsonNode root)
        {
            if (schema["$ref"] is not JsonValue reference || !reference.TryGetValue<string>(out var path)) return null;
            return Target(root, path) is { } target ? target.Node : throw new JsonException("配置 Schema 仅接受文档内 JSON Pointer 引用。");
        }
        private void Sensitive(JsonNode? value, JsonNode? schema, string file, string pointer, JsonNode? root = null, int depth = 0)
        {
            if (depth > 64) throw new JsonException("配置 Schema 引用深度超过限制。");
            root ??= schema;
            if (schema is not JsonObject rule) return;
            if (LocalReference(rule, root!) is { } referenced) Sensitive(value, referenced, file, pointer, root, depth + 1);
            if (rule["x-qian-sensitive"]?.GetValue<bool>() == true && !IsSecretReference(value))
                Fail(file, pointer, "敏感配置必须为 secretRef，不能打印或保存明文。");
            foreach (var name in new[] { "allOf", "anyOf", "oneOf" })
                if (rule[name] is JsonArray branches) foreach (var branch in branches) Sensitive(value, branch, file, pointer, root, depth + 1);
            if (value is JsonArray array && rule["items"] is { } items)
                for (var i = 0; i < array.Count; i++) Sensitive(array[i], items, file, pointer + "/" + i, root, depth + 1);
            if (value is JsonObject map) foreach (var item in map.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                // 键名按两种读法判定，敏感标记取并集：IQianPlugin<TConfig> 按 Web 默认不区分大小写绑定（Password 会绑到 password），
                // 验证器与直接读 JsonElement 的插件区分大小写。所以属性名与动态键都按不区分大小写匹配，additionalProperties 只在按原样没有匹配时适用。
                // 配置键、属性名与模式都按序数顺序遍历：JS 对象把整数形式的键排在前面，按文档顺序时两端报告的首个违规会不同。
                var child = pointer + "/" + Escape(item.Key);
                var exact = false;
                if (rule["properties"] is JsonObject properties)
                    foreach (var property in properties.OrderBy(property => property.Key, StringComparer.Ordinal))
                        if (string.Equals(property.Key, item.Key, StringComparison.OrdinalIgnoreCase))
                        { Sensitive(item.Value, property.Value, file, child, root, depth + 1); exact |= property.Key == item.Key; }
                if (rule["patternProperties"] is JsonObject patterns && patterns.Count > 0)
                {
                    // 动态键按 ECMA-262 判定（见 SchemaPattern）。方言判断不同的键名不做匹配，值必须是 secretRef；模式仍逐个编译，不受支持的写法照常失败。
                    var stable = SchemaPattern.IsStable(item.Key);
                    foreach (var pattern in patterns.OrderBy(pattern => pattern.Key, StringComparer.Ordinal))
                    {
                        var folded = Pattern(pattern.Key, true, file, child);
                        if (!stable || !folded.IsMatch(item.Key)) continue;
                        Sensitive(item.Value, pattern.Value, file, child, root, depth + 1);
                        exact |= Pattern(pattern.Key, false, file, child).IsMatch(item.Key);
                    }
                    if (!stable && !IsSecretReference(item.Value)) Fail(file, child, "键名含各正则方言判断不同的字符，按敏感配置处理，必须为 secretRef。");
                }
                if (!exact) Sensitive(item.Value, rule["additionalProperties"], file, child, root, depth + 1);
            }
        }

        private static bool IsSecretReference(JsonNode? value) =>
            value is JsonValue scalar && scalar.TryGetValue<string>(out var text) && text.StartsWith("secretRef://", StringComparison.Ordinal);

        // 超出 ECMA-262 可移植子集或被 .NET 拒绝的写法让装配失败，不跳过敏感标记。
        private Regex Pattern(string pattern, bool foldCase, string file, string pointer)
        {
            try { return SchemaPattern.Compile(pattern, foldCase); }
            catch (Exception error) when (error is ArgumentException or NotSupportedException)
            { Fail(file, pointer, "组件 config.schema 的 patternProperties 含不支持的正则写法，只接受 ECMA-262 可移植子集。"); throw; }
        }

        private List<string> Order(ResolvedProfileEntry[] entries)
        {
            var providers = new Dictionary<string, ResolvedProfileEntry>(StringComparer.Ordinal);
            foreach (var entry in entries) foreach (var key in entry.Provides)
                if (!providers.TryAdd(entry.Scope + "/" + key, entry)) Fail(_profileFile, "/bundles", "同 scope 的服务键存在重复 Provider。");
            var state = new Dictionary<string, int>(); var order = new List<string>();
            foreach (var entry in entries) Visit(entry, false);
            return order;
            // optionalInject 是软边：Provider 在树里时先于消费方装载，缺席不报错；经软边回到装载中的条目不算环路，只是不再提前（与 TS 装配器一致）。
            bool Visit(ResolvedProfileEntry entry, bool soft)
            {
                var id = entry.Scope + "/" + entry.Id;
                var current = state.GetValueOrDefault(id);
                if (current == 2) return true;
                if (current == 1) { if (soft) return false; Fail(_profileFile, "/bundles", "插件依赖存在环路。"); }
                state[id] = 1;
                foreach (var key in entry.Inject)
                {
                    if (!providers.TryGetValue(entry.Scope + "/" + key, out var provider) && !providers.TryGetValue("global/" + key, out provider))
                        Fail(_profileFile, "/bundles", "缺少 inject 所需的 Provider。");
                    if (!Visit(provider!, soft)) { state[id] = 0; return false; }
                }
                foreach (var key in entry.OptionalInject)
                    if (providers.TryGetValue(entry.Scope + "/" + key, out var provider) || providers.TryGetValue("global/" + key, out provider)) Visit(provider!, true);
                state[id] = 2; order.Add(id);
                return true;
            }
        }
        private void Error(string file, string pointer, string message, string severity)
        {
            var display = Display(file); var prefix = string.Join('/', display.Split('/').Select(Uri.EscapeDataString));
            var error = new QianError(PlatformErrors.InputInvalid) { Errors = [new QianFieldError(ProfileErrors.Invalid, prefix + "#" + pointer, new Dictionary<string, string> { ["reason"] = message })] };
            _diagnostics.Add(new SurfaceDiagnostic(display, _sources.TryGetValue(display, out var source) ? Line(source.Lines, pointer) : 1, severity, _catalog.CreateDetail(error, _trace)));
        }
        private void Fail(string file, string pointer, string message) { Error(file, pointer, message, "error"); throw new CompositionFailed(); }
        private string Display(string path) => Path.GetRelativePath(_root, Path.GetFullPath(path, _root)).Replace('\\', '/');
        private static int Line(IReadOnlyDictionary<string, int> lines, string pointer) { while (!lines.ContainsKey(pointer) && pointer.Length > 0) pointer = pointer[..pointer.LastIndexOf('/')]; return lines.GetValueOrDefault(pointer, 1); }
        private static string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
        private static string Text(JsonElement data, string key) => data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
        private static string[] Strings(JsonElement data, string key) => data.TryGetProperty(key, out var value) ? value.EnumerateArray().Select(v => v.GetString()!).ToArray() : [];
        private static bool Flag(JsonElement data, string parent, string name) => data.TryGetProperty(parent, out var section) && section.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
        private static string Within(string directory, string relative)
        {
            var full = Path.GetFullPath(relative, directory);
            if (!full.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new IOException("路径越界。");
            var current = directory;
            foreach (var part in Path.GetRelativePath(directory, full).Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, part);
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("不允许符号链接。");
            }
            return full;
        }
        private sealed record Component(string Id, string Version, string Directory, JsonElement Manifest);
        private sealed class WorkingEntry(string id, string scope, string plugin, JsonObject config, int layer, string file, string pointer)
        {
            internal readonly string Id = id;
            internal readonly string Scope = scope;
            internal string Plugin = plugin;
            internal JsonObject Config = config;
            internal int Layer = layer;
            internal bool Disabled;
            internal string? Note;
            internal string ConfigFile = file;
            internal string ConfigPointer = pointer;
            internal string SourceFile = file;
            internal string SourcePointer = pointer[..pointer.LastIndexOf('/')];
        }
        private sealed class CompositionFailed : Exception;
    }

    // 以下替换 JsonSchema.Net 的部分关键字实现，按 CLI 的 Ajv 语义判断。只用公开扩展点；JsonSchema.Net 按处理器的具体类型解析关键字之间的依赖，
    // 这几个关键字没有被其他关键字依赖，可以整体替换（unevaluatedItems 按关键字名读取 items 的注解）。
    // - const、enum、uniqueItems 按 JS 的值相等：数值按双精度比较。默认实现经 decimal 比较，绝对值超过约 7.9e28 时抛错，极小的值舍入为 0。
    // - minLength、maxLength 按码点计数：代理对计 1，残缺代理项各计 1。默认实现按文本元素计数，e 加组合重音计 1。
    // - prefixItems 之后的 items 从前缀之后的下标起算。9.4.0 从 0 起算，求值路径也多一层 items（上游缺陷）。
    // - dependentSchemas 按原样的键名匹配实例属性。9.4.0 拿 JSON Pointer 转义后的段比较，键名含 ~ 或 / 时从不生效（上游缺陷）。
    private static readonly Dialect AjvSemantics = Dialect.Draft202012
        .Without([ConstKeyword.Instance, DependentSchemasKeyword.Instance, EnumKeyword.Instance, ItemsKeyword.Instance, MaxLengthKeyword.Instance, MinLengthKeyword.Instance,
            UniqueItemsKeyword.Instance])
        .With([new JsConst(), new RawNameDependentSchemas(), new JsEnum(), new PositionedItems(), new CodePointMaxLength(), new CodePointMinLength(), new JsUniqueItems()]);

    // JS 的值相等，同 Ajv 的 const、enum 与 uniqueItems：数值按双精度，字符串按码元，对象不计键序。
    private sealed class JsEquality : IEqualityComparer<JsonElement>
    {
        internal static readonly JsEquality Instance = new();
        public bool Equals(JsonElement x, JsonElement y) => x.ValueKind == y.ValueKind && x.ValueKind switch
        {
            JsonValueKind.Number => x.GetDouble() == y.GetDouble(),
            JsonValueKind.String => x.GetString() == y.GetString(),
            JsonValueKind.Array => x.GetArrayLength() == y.GetArrayLength() && x.EnumerateArray().Zip(y.EnumerateArray()).All(pair => Equals(pair.First, pair.Second)),
            JsonValueKind.Object => x.EnumerateObject().Count() == y.EnumerateObject().Count() &&
                x.EnumerateObject().All(item => y.TryGetProperty(item.Name, out var other) && Equals(item.Value, other)),
            _ => true,
        };
        public int GetHashCode(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble().GetHashCode(),
            JsonValueKind.String => StringComparer.Ordinal.GetHashCode(value.GetString()!),
            JsonValueKind.Array => value.EnumerateArray().Aggregate(17, (hash, item) => HashCode.Combine(hash, GetHashCode(item))),
            JsonValueKind.Object => value.EnumerateObject().Aggregate(0, (hash, item) => hash ^ HashCode.Combine(item.Name, GetHashCode(item.Value))),
            _ => (int)value.ValueKind,
        };
    }

    private sealed class JsConst : IKeywordHandler
    {
        public string Name => "const";
        public object? ValidateKeywordValue(JsonElement value) => null;
        public void BuildSubschemas(KeywordData keyword, BuildContext context) { }
        public KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context) => JsEquality.Instance.Equals(keyword.RawValue, context.Instance)
            ? new KeywordEvaluation { Keyword = Name, IsValid = true }
            : new KeywordEvaluation { Keyword = Name, IsValid = false, Error = "值与 const 不同。" };
    }

    private sealed class JsEnum : IKeywordHandler
    {
        public string Name => "enum";
        public object? ValidateKeywordValue(JsonElement value) => value.ValueKind == JsonValueKind.Array ? null : throw new JsonSchemaException("enum 须为数组。");
        public void BuildSubschemas(KeywordData keyword, BuildContext context) { }
        public KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context) => keyword.RawValue.EnumerateArray().Any(value => JsEquality.Instance.Equals(value, context.Instance))
            ? new KeywordEvaluation { Keyword = Name, IsValid = true }
            : new KeywordEvaluation { Keyword = Name, IsValid = false, Error = "值不在 enum 中。" };
    }

    private sealed class JsUniqueItems : UniqueItemsKeyword
    {
        public override KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context)
        {
            if (context.Instance.ValueKind != JsonValueKind.Array || keyword.RawValue.ValueKind == JsonValueKind.False) return KeywordEvaluation.Ignore;
            var seen = new HashSet<JsonElement>(JsEquality.Instance);
            return context.Instance.EnumerateArray().All(seen.Add)
                ? new KeywordEvaluation { Keyword = Name, IsValid = true }
                : new KeywordEvaluation { Keyword = Name, IsValid = false, Error = "数组含重复项。" };
        }
    }

    private sealed class CodePointMinLength : MinLengthKeyword
    {
        public override KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context) =>
            context.Instance.ValueKind != JsonValueKind.String ? KeywordEvaluation.Ignore
            : CodePoints(context.Instance.GetString()!) >= (long)keyword.Value! ? new KeywordEvaluation { Keyword = Name, IsValid = true }
            : new KeywordEvaluation { Keyword = Name, IsValid = false, Error = "字符串短于 minLength。" };
    }

    private sealed class CodePointMaxLength : MaxLengthKeyword
    {
        public override KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context) =>
            context.Instance.ValueKind != JsonValueKind.String ? KeywordEvaluation.Ignore
            : CodePoints(context.Instance.GetString()!) <= (long)keyword.Value! ? new KeywordEvaluation { Keyword = Name, IsValid = true }
            : new KeywordEvaluation { Keyword = Name, IsValid = false, Error = "字符串长于 maxLength。" };
    }

    // 同 Ajv 的 ucs2length。
    private static long CodePoints(string text)
    {
        long count = text.Length;
        for (var i = 0; i + 1 < text.Length; i++) if (char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i + 1])) { count--; i++; }
        return count;
    }

    // 子 Schema 与键名同序（BuildSubschemas 按 RawValue 的顺序构造）。
    private sealed class RawNameDependentSchemas : DependentSchemasKeyword
    {
        public override KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context)
        {
            if (context.Instance.ValueKind != JsonValueKind.Object) return KeywordEvaluation.Ignore;
            var names = keyword.RawValue.EnumerateObject().Select(item => item.Name).ToList();
            var details = context.Instance.EnumerateObject().Select(property => names.IndexOf(property.Name)).Where(index => index >= 0)
                .Select(index => keyword.Subschemas[index].Evaluate(context with { EvaluationPath = context.EvaluationPath.Combine(Name, names[index]) })).ToArray();
            var valid = Array.TrueForAll(details, result => result.IsValid);
            return new KeywordEvaluation { Keyword = Name, IsValid = valid, Details = details, Error = valid ? null : "属性依赖的 Schema 不成立。" };
        }
    }

    private sealed class PositionedItems : ItemsKeyword
    {
        public override KeywordEvaluation Evaluate(KeywordData keyword, EvaluationContext context)
        {
            if (context.Instance.ValueKind != JsonValueKind.Array) return KeywordEvaluation.Ignore;
            var skip = (int?)keyword.Value ?? 0;
            var path = context.EvaluationPath.Combine(Name);
            var details = context.Instance.EnumerateArray().Select((item, index) => (Item: item, Index: index)).Where(pair => pair.Index >= skip)
                .Select(pair => keyword.Subschemas[0].Evaluate(context with { InstanceLocation = context.InstanceLocation.Combine(pair.Index), Instance = pair.Item, EvaluationPath = path }))
                .ToArray();
            var valid = Array.TrueForAll(details, result => result.IsValid);
            return new KeywordEvaluation { Keyword = Name, IsValid = valid, Details = details, Annotation = JsonElementExtensions.True, Error = valid ? null : "数组项不符合 items。" };
        }
    }
}
