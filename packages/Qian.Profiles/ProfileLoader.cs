using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
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
                if (!Regex.IsMatch(options.Profile, "^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)) throw new ArgumentException("profile 名无效。");
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
                var entries = ResolveEntries();
                var order = Order(entries);
                return new ProfileLoadResult(new ProfileSnapshot(options.Profile, _layers.ToArray(), entries, order), _diagnostics.ToArray());
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
            var patch = Read(file, "patch"); var position = 0; var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in patch.EnumerateArray())
            {
                var pointer = "/" + position++;
                var id = Text(value, "id"); var scope = Text(value, "scope"); if (scope.Length == 0) scope = "global";
                var key = scope + "/" + id;
                if (!seen.Add(key)) Fail(file, pointer + "/id", "同一层不能重复声明同一 scope/id。");
                var plugin = Text(value, "plugin");
                if (!_entries.TryGetValue(key, out var current))
                {
                    if (plugin.Length == 0) Fail(file, pointer + "/plugin", "新条目必须声明 plugin。");
                    if (!value.TryGetProperty("config", out _)) Fail(file, pointer + "/config", "新条目必须声明 config，可为空对象。");
                    current = new WorkingEntry(id, scope, plugin, new JsonObject(), index, file, pointer + "/config");
                    if (scope != "global" && _entries.TryGetValue("global/" + id, out var global)) Protect(global, plugin, false, file, pointer);
                }
                var disabled = value.TryGetProperty("disabled", out var flag) ? flag.GetBoolean() : current.Disabled;
                Protect(current, plugin.Length == 0 ? current.Plugin : plugin, disabled, file, pointer);
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
                    Strings(component.Manifest, "inject"), Strings(component.Manifest, "provides"), entry.Note)
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
            var schemaNode = JsonNode.Parse(File.ReadAllText(schemaPath))!;
            var config = Defaults(schemaNode, entry.Config.DeepClone())!;
            var schema = JsonSchema.FromText(schemaNode.ToJsonString(), new BuildOptions { Dialect = Dialect.Draft202012, SchemaRegistry = new SchemaRegistry { Fetch = (_, _) => null } });
            var data = JsonSerializer.SerializeToElement(config);
            var evaluation = schema.Evaluate(data, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
            if (!evaluation.IsValid)
            {
                var errors = Leaves(evaluation).Where(e => e.Errors is { Count: > 0 }).ToArray();
                foreach (var error in errors) Error(entry.ConfigFile, entry.ConfigPointer + error.InstanceLocation, "条目配置不符合组件 config.schema。", "error");
                throw new CompositionFailed();
            }
            Sensitive(config, schemaNode, entry.ConfigFile, entry.ConfigPointer);
            return data;
        }

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
                        if (!target.ContainsKey(item.Key) && item.Value is JsonObject property && property["default"] is { } fallback) target[item.Key] = fallback.DeepClone();
                        if (target.TryGetPropertyValue(item.Key, out var child) && item.Value is not null) target[item.Key] = Defaults(item.Value, child?.DeepClone(), root, depth + 1);
                    }
            }
            return value;
        }
        private static JsonNode? LocalReference(JsonObject schema, JsonNode root)
        {
            if (schema["$ref"] is not JsonValue reference || !reference.TryGetValue<string>(out var path)) return null;
            if (!path.StartsWith("#/", StringComparison.Ordinal)) throw new JsonException("配置 Schema 仅接受文档内 JSON Pointer 引用。");
            JsonNode? result = root;
            foreach (var segment in path[2..].Split('/')) result = result?[Uri.UnescapeDataString(segment).Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)];
            return result ?? throw new JsonException("配置 Schema 引用不存在。");
        }
        private void Sensitive(JsonNode? value, JsonNode? schema, string file, string pointer, JsonNode? root = null, int depth = 0)
        {
            if (depth > 64) throw new JsonException("配置 Schema 引用深度超过限制。");
            root ??= schema;
            if (schema is not JsonObject rule) return;
            if (LocalReference(rule, root!) is { } referenced) Sensitive(value, referenced, file, pointer, root, depth + 1);
            if (rule["x-qian-sensitive"]?.GetValue<bool>() == true &&
                (value is not JsonValue scalar || !scalar.TryGetValue<string>(out var text) || !text.StartsWith("secretRef://", StringComparison.Ordinal)))
                Fail(file, pointer, "敏感配置必须为 secretRef，不能打印或保存明文。");
            foreach (var name in new[] { "allOf", "anyOf", "oneOf" })
                if (rule[name] is JsonArray branches) foreach (var branch in branches) Sensitive(value, branch, file, pointer, root, depth + 1);
            if (value is JsonArray array && rule["items"] is { } items)
                for (var i = 0; i < array.Count; i++) Sensitive(array[i], items, file, pointer + "/" + i, root, depth + 1);
            if (value is JsonObject map) foreach (var item in map)
            {
                var found = false;
                if (rule["properties"] is JsonObject properties && properties.TryGetPropertyValue(item.Key, out var property)) { Sensitive(item.Value, property, file, pointer + "/" + Escape(item.Key), root, depth + 1); found = true; }
                if (rule["patternProperties"] is JsonObject patterns) foreach (var pattern in patterns)
                    if (Regex.IsMatch(item.Key, pattern.Key, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) { Sensitive(item.Value, pattern.Value, file, pointer + "/" + Escape(item.Key), root, depth + 1); found = true; }
                if (!found) Sensitive(item.Value, rule["additionalProperties"], file, pointer + "/" + Escape(item.Key), root, depth + 1);
            }
        }

        private List<string> Order(ResolvedProfileEntry[] entries)
        {
            var providers = new Dictionary<string, ResolvedProfileEntry>(StringComparer.Ordinal);
            foreach (var entry in entries) foreach (var key in entry.Provides)
                if (!providers.TryAdd(entry.Scope + "/" + key, entry)) Fail(_profileFile, "/bundles", "同 scope 的服务键存在重复 Provider。");
            var state = new Dictionary<string, int>(); var order = new List<string>();
            foreach (var entry in entries) Visit(entry);
            return order;
            void Visit(ResolvedProfileEntry entry)
            {
                var id = entry.Scope + "/" + entry.Id;
                if (state.GetValueOrDefault(id) == 2) return;
                if (state.GetValueOrDefault(id) == 1) Fail(_profileFile, "/bundles", "插件依赖存在环路。");
                state[id] = 1;
                foreach (var key in entry.Inject)
                {
                    if (!providers.TryGetValue(entry.Scope + "/" + key, out var provider) && !providers.TryGetValue("global/" + key, out provider))
                        Fail(_profileFile, "/bundles", "缺少 inject 所需的 Provider。");
                    Visit(provider!);
                }
                state[id] = 2; order.Add(id);
            }
        }
        private static IEnumerable<EvaluationResults> Leaves(EvaluationResults value)
        {
            if (value.IsValid) yield break;
            var children = (value.Details ?? []).Where(d => !d.IsValid).ToArray();
            if (children.Length == 0) yield return value;
            foreach (var child in children) foreach (var leaf in Leaves(child)) yield return leaf;
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
}
