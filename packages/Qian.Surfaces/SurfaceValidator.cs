using System.Text;
using System.Text.Json;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Surfaces;

/// <summary>读取声明式组件，组合结构与语义校验；所有内容只解析，不执行。</summary>
public sealed partial class SurfaceValidator : ISurfaceValidator
{
    private readonly SchemaLibrary _schemas = new();
    private static readonly JsonElement Rules = ReadRules();

    /// <inheritdoc />
    public SurfaceParseResult Parse(string file, string content, bool markdown = false, string? traceId = null)
        => SurfaceReader.Parse(file, content, markdown, Trace(traceId));
    /// <inheritdoc />
    public IReadOnlyList<SurfaceDiagnostic> ValidateSchema(string schema, string file, JsonElement data, string? traceId = null)
        => _schemas.Validate(schema, file, data, new Dictionary<string, int> { [""] = 1 }, Trace(traceId));
    /// <inheritdoc />
    public SurfaceValidationResult ValidateComponent(string directory, SurfaceValidationOptions? options = null)
    {
        options ??= new SurfaceValidationOptions();
        var context = new Validation(this, Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)), options, Trace(options.TraceId));
        return context.Run();
    }
    private static string Trace(string? trace) => trace ?? Guid.NewGuid().ToString("N");
    private static JsonElement ReadRules()
    {
        using var stream = typeof(SurfaceValidator).Assembly.GetManifestResourceStream("schemas/surface-rules.json")!;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    private sealed partial class Validation(SurfaceValidator validator, string root, SurfaceValidationOptions options, string trace)
    {
        private const string ManifestFile = ".qian-plugin/plugin.json";
        private readonly List<SurfaceDiagnostic> _diagnostics = [];
        private readonly List<SurfaceDocument> _documents = [];
        private readonly Dictionary<string, SurfaceSyntax> _sources = new(StringComparer.Ordinal);
        private JsonElement _manifest;
        private string _id = "";
        private string _tier = "";

        internal SurfaceValidationResult Run()
        {
            var manifest = Read(ManifestFile, "plugin", false, ManifestFile, "");
            if (manifest is null || _diagnostics.Any(d => d.Severity == "error")) return Result(null);
            _manifest = manifest.Data;
            _id = Text(_manifest, "id");
            _tier = Text(_manifest.GetProperty("policy"), "tier");
            ManifestRules();
            foreach (var entry in Rules.GetProperty("defaults").EnumerateObject())
            {
                var declared = _manifest.TryGetProperty("surfaces", out var surfaces) && surfaces.TryGetProperty(entry.Name, out _);
                var path = declared ? Text(surfaces, entry.Name) : entry.Value.GetString()!;
                var full = Safe(path, ManifestFile, "/surfaces/" + entry.Name, required: declared);
                if (full is null) continue;
                if (entry.Name is "skills" or "agents" or "commands" or "tools")
                {
                    if (!Directory.Exists(full)) { Error(ManifestFile, "/surfaces/" + entry.Name, SurfaceErrors.Path, "表面指针必须指向目录。"); continue; }
                    var files = Directory.EnumerateFileSystemEntries(full).Order(StringComparer.Ordinal).ToArray();
                    if (files.Length > 1024) { Error(ManifestFile, "/surfaces/" + entry.Name, SurfaceErrors.Path, "表面目录条目过多。"); continue; }
                    foreach (var item in files)
                    {
                        var candidate = entry.Name == "skills" ? Path.Combine(item, "SKILL.md") : item;
                        var relative = Relative(candidate);
                        if (entry.Name == "skills")
                        {
                            if (!Directory.Exists(item) || !File.Exists(candidate)) continue;
                        }
                        else if (!item.EndsWith(entry.Name == "tools" ? ".json" : ".md", StringComparison.Ordinal)) continue;
                        var kind = entry.Name switch { "skills" => "skill", "agents" => "agent", "commands" => "command", _ => "tool" };
                        var source = Read(relative, kind, kind != "tool", ManifestFile, "/surfaces/" + entry.Name);
                        if (source is null) continue;
                        var name = kind == "skill" ? Path.GetFileName(Path.GetDirectoryName(candidate))! : Path.GetFileNameWithoutExtension(candidate);
                        if (kind == "tool") name = Text(source.Data, "name");
                        _documents.Add(new SurfaceDocument(kind, name, relative, source.Data, source.Body));
                    }
                }
                else
                {
                    var kind = entry.Name switch { "mcpServers" => "mcp", _ => entry.Name };
                    var source = Read(Relative(full), kind, false, ManifestFile, "/surfaces/" + entry.Name);
                    if (source is not null) _documents.Add(new SurfaceDocument(kind, kind, Relative(full), source.Data, source.Body));
                }
            }
            ReadMetadata();
            SemanticRules();
            return Result(new ParsedComponent(_id, _manifest, _documents.ToArray()) { Profile = options.Profile });
        }

        private SurfaceValidationResult Result(ParsedComponent? component) => new(component,
            _diagnostics.DistinctBy(d => (d.File, d.Error.Pointer, d.Error.Errors![0].Code, d.Severity))
                .OrderBy(d => d.Severity == "error" ? 0 : 1).ThenBy(d => d.File, StringComparer.Ordinal).ThenBy(d => d.Error.Pointer, StringComparer.Ordinal).Take(100).ToArray());

        private void ReadMetadata()
        {
            var declared = _manifest.TryGetProperty("surfaces", out var surfaces) && surfaces.TryGetProperty("metadata", out _);
            var full = Safe(declared ? Text(surfaces, "metadata") : "./metadata", ManifestFile, "/surfaces/metadata", required: declared);
            if (full is null) return;
            if (!Directory.Exists(full)) { Error(ManifestFile, "/surfaces/metadata", SurfaceErrors.Path, "metadata 表面必须指向目录。"); return; }
            var files = Directory.EnumerateFileSystemEntries(full).Order(StringComparer.Ordinal).ToArray();
            if (files.Length > 1024) { Error(ManifestFile, "/surfaces/metadata", SurfaceErrors.Path, "表面目录条目过多。"); return; }
            foreach (var item in files.Where(file => file.EndsWith(".json", StringComparison.Ordinal)))
            {
                var relative = Relative(item);
                // 通用层只负责 JSON 与路径；权限、角色等业务 Schema 属于对应 seam。
                var source = Read(relative, null, false, ManifestFile, "/surfaces/metadata");
                if (source is not null) _documents.Add(new SurfaceDocument("metadata", Path.GetFileNameWithoutExtension(item), relative, source.Data, source.Body));
            }
        }

        private SurfaceSyntax? Read(string file, string? schema, bool markdown, string diagnosticFile, string pointer)
        {
            var full = Safe(file, diagnosticFile, pointer, required: true);
            if (full is null) return null;
            try
            {
                if (!File.Exists(full) || new FileInfo(full).Length > 1_048_576) throw new IOException();
                var parsed = SurfaceReader.Parse(file, File.ReadAllText(full, new UTF8Encoding(false, true)), markdown, trace);
                _diagnostics.AddRange(parsed.Diagnostics);
                if (parsed.Syntax is not { } syntax || parsed.Diagnostics.Count > 0) return null;
                _sources[file] = syntax;
                var errors = schema is null ? Array.Empty<SurfaceDiagnostic>() : validator._schemas.Validate(schema, file, syntax.Data, syntax.Lines, trace);
                _diagnostics.AddRange(errors);
                return errors.Count == 0 ? syntax : null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                Error(file, "", SurfaceErrors.Path, "文件无法读取或超过大小限制。");
                return null;
            }
        }

        private string? Safe(string relative, string file, string pointer, bool required)
        {
            try
            {
                relative = relative.Replace('\\', '/');
                if (Path.IsPathRooted(relative) || relative.Contains(':', StringComparison.Ordinal) || relative.Contains('\0')) throw new IOException();
                var full = Path.GetFullPath(Path.Combine(root, relative));
                if (full != root && !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new IOException();
                var current = root;
                foreach (var segment in Path.GetRelativePath(root, full).Split(Path.DirectorySeparatorChar))
                {
                    if (segment == ".") continue;
                    if (!Directory.Exists(current) || !Directory.EnumerateFileSystemEntries(current).Any(p => Path.GetFileName(p) == segment))
                    {
                        if (required) throw new IOException();
                        return null;
                    }
                    current = Path.Combine(current, segment);
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException();
                }
                return full;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Error(file, pointer, SurfaceErrors.Path, "路径必须是组件内存在的相对路径，不能经过符号链接。");
                return null;
            }
        }
        private string Relative(string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
        private void Error(string file, string pointer, ErrorCode code, string reason, string severity = "error", int? line = null)
        {
            var lines = _sources.TryGetValue(file, out var source) ? source.Lines : new Dictionary<string, int>();
            _diagnostics.Add(Diagnostics.Create(file, pointer, line ?? Diagnostics.Line(lines, pointer), code, reason, trace, severity));
        }
        private static string Text(JsonElement element, string key) => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
        private static string[] Strings(JsonElement element, string key) => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).ToArray() : [];
    }
}
