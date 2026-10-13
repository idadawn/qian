using System.Text.Json;
using System.Text.Json.Nodes;
using Qian.Plugin.Sdk;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;

namespace Qian.Plugin.Tool;

internal sealed record PlannedFile(string File, JsonNode? Content);
internal sealed record ManifestDrift(string File, string Pointer);

// 此计划只描述构建产物；它不会运行插件、安装组件或授予能力执行权限。
internal sealed class ManifestPlan(string root, IReadOnlyList<PlannedFile> files, IReadOnlyList<ManifestDrift> drift)
{
    internal string Root { get; } = root;
    internal IReadOnlyList<PlannedFile> Files { get; } = files;
    internal IReadOnlyList<ManifestDrift> Drift { get; } = drift;
    internal const string ManifestFile = ".qian-plugin/plugin.json";
    private const string Marker = "x-qian-generated";
    private static readonly JsonSerializerOptions Formatting = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static ManifestPlan Create(string directory, ComponentCodeMetadata code)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)); var validator = new SurfaceValidator();
        var manifest = Read(root, ManifestFile, validator)?.AsObject() ?? throw new InvalidDataException("缺少唯一 plugin.json。");
        if (manifest["id"]?.GetValue<string>() != code.ComponentId) throw new InvalidDataException("代码组件 ID 与清单不一致。");
        var runtime = manifest["runtime"]?.AsObject() ?? new JsonObject();
        if (runtime["form"] is not null && runtime["form"]!.GetValue<string>() != "module") throw new InvalidDataException("此生成入口只处理 module 代码组件。");
        var original = manifest.DeepClone();
        manifest["inject"] = Strings(code.Inject); manifest["provides"] = Strings(code.Provides);
        if (manifest["runtime"] is null) manifest["runtime"] = runtime;
        runtime["form"] = "module"; runtime["entry"] = code.RuntimeEntry;
        var pointer = manifest["surfaces"]?["tools"]?.GetValue<string>() ?? "./tools";
        if (!pointer.StartsWith("./", StringComparison.Ordinal)) throw new InvalidDataException("工具目录必须是组件内的相对路径。");
        var tools = pointer[2..].TrimEnd('/'); _ = Safe(root, tools);
        var files = new List<PlannedFile>(); var differences = new List<ManifestDrift>(); var generatedTools = new List<string>();
        foreach (var contract in code.Capabilities)
        {
            var file = tools + "/" + contract.Name + ".json"; var tool = JsonNode.Parse(contract.Metadata.GetRawText())!.AsObject();
            _ = Safe(root, file); var previous = Read(root, file, validator);
            if (previous is not null && !Owned(previous, code.ComponentId)) throw new InvalidDataException("不能覆盖手写的同名工具表面：" + file);
            tool["$schema"] = "https://qian.dev/schemas/tool/v2"; tool["inputSchema"] = contract.InputSchema; tool["outputSchema"] = contract.OutputSchema;
            tool["schemaSha256"] = ToolContractHash.Compute(Element(tool));
            tool[Marker] = new JsonObject { ["$generated"] = true, ["component"] = code.ComponentId };
            EnsureSchema(validator, "tool", file, tool);
            files.Add(new PlannedFile(file, tool)); generatedTools.Add(file);
            Difference(file, "", previous, tool, differences);
        }
        // 清理范围必须同时有清单索引和文件自身的所属标记，不能借索引删除手写文件。
        if (original[Marker]?["tools"] is JsonArray oldTools)
        {
            foreach (var item in oldTools)
            {
                var file = item?.GetValue<string>() ?? throw new InvalidDataException("派生文件索引无效。"); _ = Safe(root, file);
                if (generatedTools.Contains(file, StringComparer.Ordinal)) continue;
                var previous = Read(root, file, validator);
                if (previous is null) continue;
                if (!Owned(previous, code.ComponentId)) throw new InvalidDataException("旧派生文件失去所属标记，拒绝删除：" + file);
                files.Add(new PlannedFile(file, null)); differences.Add(new ManifestDrift(file, ""));
            }
        }
        manifest[Marker] = new JsonObject { ["$generated"] = true, ["fields"] = Strings(["/inject", "/provides", "/runtime/form", "/runtime/entry"]), ["tools"] = Strings(generatedTools) };
        EnsureSchema(validator, "plugin", ManifestFile, manifest);
        files.Add(new PlannedFile(ManifestFile, manifest)); Difference(ManifestFile, "", original, manifest, differences);
        var oldGenerated = original[Marker]?["tools"]?.AsArray().Select(n => n!.GetValue<string>()).ToHashSet(StringComparer.Ordinal) ?? [];
        var toolDirectory = Safe(root, tools);
        if (Directory.Exists(toolDirectory))
            foreach (var path in Directory.EnumerateFiles(toolDirectory, "*.json"))
            {
                var file = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (!generatedTools.Contains(file, StringComparer.Ordinal) && !oldGenerated.Contains(file) && Read(root, file, validator) is { } content && Owned(content, code.ComponentId))
                    throw new InvalidDataException("发现清单索引之外的遗留派生工具：" + file);
            }
        var plan = new ManifestPlan(root, files.AsReadOnly(), differences.AsReadOnly());
        plan.ValidateStaged(code, validator);
        return plan;
    }

    private void ValidateStaged(ComponentCodeMetadata code, SurfaceValidator validator)
    {
        var stage = Path.Combine(Path.GetTempPath(), "qian-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var count = 0; long bytes = 0;
        try
        {
            Copy(Root, stage);
            foreach (var file in Files)
            {
                var path = Path.Combine(stage, file.File);
                if (file.Content is null) File.Delete(path);
                else { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, file.Content.ToJsonString()); }
            }
            var validation = validator.ValidateComponent(stage, new SurfaceValidationOptions { Capabilities = code.Capabilities.Select(c => c.Id).ToArray() });
            if (!validation.IsValid) throw new InvalidDataException("派生后的完整组件校验失败：" + string.Join(", ", validation.Diagnostics.Where(d => d.Severity == "error").Select(d => d.Error.Pointer)));
        }
        finally { Directory.Delete(stage, true); }

        void Copy(string source, string destination)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(source))
            {
                var relative = Path.GetRelativePath(Root, entry).Replace('\\', '/'); _ = Safe(Root, relative);
                var name = Path.GetFileName(entry);
                if (Directory.Exists(entry))
                {
                    if (name is "bin" or "obj" or "node_modules" or ".git") continue;
                    var next = Path.Combine(destination, name); Directory.CreateDirectory(next); Copy(entry, next);
                }
                else
                {
                    if (++count > 4096 || (bytes += new FileInfo(entry).Length) > 64L * 1024 * 1024) throw new InvalidDataException("组件预检快照超过文件或体积限制。");
                    if (name == "qian-plugin.yaml" || (name.Contains(".generated.", StringComparison.Ordinal) && Path.GetExtension(name) is ".json" or ".yaml" or ".yml"))
                        throw new InvalidDataException("组件不能保留第二份派生清单。");
                    File.Copy(entry, Path.Combine(destination, name));
                }
            }
        }
    }

    internal void Apply()
    {
        var changed = Files.Where(file => Drift.Any(d => d.File == file.File)).ToArray();
        var before = changed.ToDictionary(file => file.File, file => File.Exists(Safe(Root, file.File)) ? File.ReadAllBytes(Safe(Root, file.File)) : null, StringComparer.Ordinal);
        var applied = new List<string>();
        try
        {
            foreach (var file in changed)
            {
                var target = Safe(Root, file.File); applied.Add(file.File);
                if (file.Content is null) File.Delete(target);
                else { Directory.CreateDirectory(Path.GetDirectoryName(target)!); Write(target, System.Text.Encoding.UTF8.GetBytes(file.Content.ToJsonString(Formatting) + "\n")); }
            }
        }
        catch
        {
            foreach (var file in applied.AsEnumerable().Reverse())
            {
                var target = Safe(Root, file);
                if (before[file] is { } bytes) Write(target, bytes); else File.Delete(target);
            }
            throw;
        }
    }
    private static void Write(string target, byte[] bytes)
    {
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, bytes); File.Move(temp, target, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static void EnsureSchema(SurfaceValidator validator, string schema, string file, JsonNode content)
    {
        var errors = validator.ValidateSchema(schema, file, Element(content)).Where(d => d.Severity == "error").ToArray();
        if (errors.Length != 0) throw new InvalidDataException("派生字段不符合表面契约：" + file + " " + string.Join(", ", errors.Select(e => e.Error.Pointer)));
    }
    private static JsonElement Element(JsonNode node) { using var json = JsonDocument.Parse(node.ToJsonString()); return json.RootElement.Clone(); }
    private static JsonArray Strings(IEnumerable<string> values) => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
    private static bool Owned(JsonNode node, string component) => node[Marker] is JsonObject marker && marker["$generated"]?.GetValue<bool>() == true && marker["component"]?.GetValue<string>() == component;
    private static JsonNode? Read(string root, string file, SurfaceValidator validator)
    {
        var path = Safe(root, file); if (!File.Exists(path)) return null;
        var parsed = validator.Parse(file, File.ReadAllText(path));
        if (parsed.Syntax is null || parsed.Diagnostics.Any(d => d.Severity == "error")) throw new InvalidDataException("文件语法或重复键无效：" + file);
        return JsonNode.Parse(parsed.Syntax.Data.GetRawText());
    }
    private static string Safe(string root, string file)
    {
        if (Path.IsPathRooted(file) || file.Contains('\\') || file.Split('/').Any(part => part is "" or "." or "..")) throw new InvalidDataException("派生路径不能越出组件目录。");
        var target = Path.GetFullPath(Path.Combine(root, file));
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException("派生路径不能越出组件目录。");
        for (FileSystemInfo? current = new FileInfo(target); current is not null; current = current is FileInfo info ? info.Directory : ((DirectoryInfo)current).Parent)
            if (current.LinkTarget is not null) throw new InvalidDataException("派生路径不能经过符号链接。");
        return target;
    }
    private static void Difference(string file, string pointer, JsonNode? before, JsonNode? after, List<ManifestDrift> output)
    {
        if (JsonNode.DeepEquals(before, after)) return;
        if (before is JsonObject oldObject && after is JsonObject newObject)
        {
            foreach (var name in oldObject.Select(p => p.Key).Union(newObject.Select(p => p.Key), StringComparer.Ordinal).Order(StringComparer.Ordinal))
                Difference(file, pointer + "/" + name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal), oldObject[name], newObject[name], output);
        }
        else output.Add(new ManifestDrift(file, pointer));
    }
}
