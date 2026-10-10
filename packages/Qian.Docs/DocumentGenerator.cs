using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Qian.Docs.Abstractions;
using Qian.Profiles.Abstractions;

namespace Qian.Docs;

/// <summary>从静态装配与源码派生 Markdown；不装载或执行插件。</summary>
public sealed class DocumentGenerator(IProfileLoader profiles) : IDocumentGenerator
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
    /// <inheritdoc />
    public DocumentResult Generate(DocumentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var diagnostics = new Diagnostics();
        var documents = new Dictionary<string, string>(StringComparer.Ordinal);
        EventMap[] events = [];
        try
        {
            var root = Path.GetFullPath(options.Profile.WorkingDirectory);
            var result = profiles.Load(options.Profile);
            diagnostics.Items.AddRange(result.Diagnostics);
            events = new EventAnalyzer(root, diagnostics).Analyze(options.SourceDirectories);
            documents["event-graph.md"] = EventGraph(options.Profile.Profile, events, diagnostics);
            if (result.IsValid)
            {
                documents["config-catalog.md"] = ConfigCatalog(root, result.Snapshot!);
                documents["module-graph.md"] = ModuleGraph(result.Snapshot!);
                documents["seam-graph.md"] = SeamGraph(result.Snapshot!);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            diagnostics.Add("docs/generated", 1, DocumentErrors.Source, "装配、源码或配置 Schema 无法安全读取，未生成可发布文档。");
        }
        return new DocumentResult(documents, events, diagnostics.Items);
    }
    private static string Header(string title, string profile) => $"# {title}\n\n由 `qian docs generate` 派生；profile：{Code(profile)}。请修改输入后重新生成。\n\n";
    private static string ConfigCatalog(string root, ProfileSnapshot snapshot)
    {
        var output = new StringBuilder(Header("配置目录", snapshot.Name));
        output.AppendLine("配置来自最终活动条目；整段替换后再补 Schema 默认值。secretRef 原样保留，敏感 Schema 的示例和默认值不导出。\n");
        foreach (var entry in snapshot.Entries)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"## {Text(entry.Scope + "/" + entry.Id)}\n");
            var layer = snapshot.Layers[entry.Layer];
            output.AppendLine(CultureInfo.InvariantCulture, $"组件：{Code(entry.Plugin)}；来源：{Code(layer.Kind)} / {Code(layer.Path)}。\n");
            if (Skeleton(entry)) output.AppendLine("此条目为业务配置骨架，未证明 Provider 已实现或 Ready。\n");
            output.AppendLine("### 生效配置\n");
            output.AppendLine(Fence(entry.Config.GetRawText()));
            var path = entry.Manifest.TryGetProperty("runtime", out var runtime) && runtime.TryGetProperty("configSchema", out var custom)
                ? custom.GetString()! : "./schemas/config.schema.json";
            var file = Paths.Within(entry.ComponentDirectory, path);
            if (!File.Exists(file)) { output.AppendLine("配置 Schema：未声明（仅允许空配置）。\n"); continue; }
            output.AppendLine(CultureInfo.InvariantCulture, $"### 配置 Schema\n\n来源：{Code(Path.GetRelativePath(root, file).Replace('\\', '/'))}。\n");
            var schema = JsonNode.Parse(File.ReadAllText(file))!;
            RedactSchema(schema);
            output.AppendLine(Fence(schema.ToJsonString(Pretty)));
        }
        if (snapshot.Entries.Count == 0) output.AppendLine("当前 profile 没有默认业务配置条目。");
        return output.ToString();
    }
    private static void RedactSchema(JsonNode? node)
    {
        if (node is JsonObject map)
        {
            if (map["x-qian-sensitive"] is JsonValue sensitive && sensitive.TryGetValue<bool>(out var flag) && flag)
                foreach (var name in new[] { "default", "examples", "const", "enum" }) map.Remove(name);
            foreach (var child in map) RedactSchema(child.Value);
        }
        else if (node is JsonArray array) foreach (var child in array) RedactSchema(child);
    }
    private static string ModuleGraph(ProfileSnapshot snapshot)
    {
        var output = new StringBuilder(Header("模块图", snapshot.Name));
        output.AppendLine("这是经过依赖校验的静态挂载计划，不是运行实例的就绪状态。\n");
        output.AppendLine("| 条目 | 组件 | 来源层 | 状态 |\n|---|---|---|---|");
        foreach (var entry in snapshot.Entries) output.AppendLine(CultureInfo.InvariantCulture, $"| {Code(entry.Scope + "/" + entry.Id)} | {Code(entry.Plugin)} | {entry.Layer}（{Text(snapshot.Layers[entry.Layer].Kind)}） | {(Skeleton(entry) ? "配置骨架" : "已解析，尚未执行")} |");
        output.AppendLine("\n挂载顺序：" + (snapshot.MountOrder.Count == 0 ? "空树。" : string.Join(" → ", snapshot.MountOrder.Select(Code))) + "\n");
        output.AppendLine(Graph(snapshot, seam: false));
        return output.ToString();
    }
    private static string SeamGraph(ProfileSnapshot snapshot)
    {
        var output = new StringBuilder(Header("seam 图", snapshot.Name));
        output.AppendLine("由清单 inject/provides 派生；同 scope 优先，其次 global。骨架的空键集合不代表未来没有依赖。\n");
        output.AppendLine("| 服务键 | Provider 条目 | Consumer 条目 |\n|---|---|---|");
        foreach (var provider in snapshot.Entries) foreach (var key in provider.Provides)
        {
            var consumers = snapshot.Entries.Where(c => c.Inject.Contains(key, StringComparer.Ordinal) && Provider(snapshot, c, key) == provider);
            output.AppendLine(CultureInfo.InvariantCulture, $"| {Code(key)} | {Code(provider.Scope + "/" + provider.Id)} | {string.Join("、", consumers.Select(c => Code(c.Scope + "/" + c.Id)))} |");
        }
        output.AppendLine("\n" + Graph(snapshot, seam: true));
        return output.ToString();
    }
    private static ResolvedProfileEntry? Provider(ProfileSnapshot snapshot, ResolvedProfileEntry consumer, string key) =>
        snapshot.Entries.FirstOrDefault(e => e.Scope == consumer.Scope && e.Provides.Contains(key, StringComparer.Ordinal)) ??
        snapshot.Entries.FirstOrDefault(e => e.Scope == "global" && e.Provides.Contains(key, StringComparer.Ordinal));
    private static string Graph(ProfileSnapshot snapshot, bool seam)
    {
        if (snapshot.Entries.Count == 0) return "当前 profile 为空树。\n";
        var output = new StringBuilder("```mermaid\nflowchart LR\n");
        var ids = snapshot.Entries.Select((entry, index) => (entry, id: "n" + index)).ToDictionary(x => x.entry, x => x.id);
        foreach (var entry in snapshot.Entries) output.AppendLine(CultureInfo.InvariantCulture, $"  {ids[entry]}[\"{Label(entry.Scope + "/" + entry.Id)}\"]");
        var index = 0;
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        if (seam) foreach (var provider in snapshot.Entries) foreach (var key in provider.Provides)
        {
            var service = "s" + index++;
            keys[provider.Scope + "/" + key] = service;
            output.AppendLine(CultureInfo.InvariantCulture, $"  {service}((\"{Label(key)}\"))\n  {ids[provider]} --> {service}");
        }
        foreach (var consumer in snapshot.Entries) foreach (var key in consumer.Inject)
        {
            var provider = Provider(snapshot, consumer, key);
            if (provider is null) continue;
            if (!seam) output.AppendLine(CultureInfo.InvariantCulture, $"  {ids[provider]} -->|\"{Label(key)}\"| {ids[consumer]}");
            else output.AppendLine(CultureInfo.InvariantCulture, $"  {keys[provider.Scope + "/" + key]} --> {ids[consumer]}");
        }
        return output.AppendLine("```").ToString();
    }
    private static string EventGraph(string profile, EventMap[] events, Diagnostics diagnostics)
    {
        var output = new StringBuilder(Header("事件图", profile));
        output.AppendLine("从选定 C# 源码目录的 QianEvent 定义与绑定调用派生，仅表示静态生产/消费关系；不代表当前 profile 已运行这些处理器。跨进程集成事件不在此图中。\n");
        output.AppendLine("| 事件 | 模式 | 声明位置 | 生产方 | 消费方 |\n|---|---|---|---|---|");
        foreach (var evt in events) output.AppendLine(CultureInfo.InvariantCulture, $"| {Code(evt.Name)} | {Code(evt.Mode)} | {Location(evt.Declaration)} | {string.Join("<br>", evt.Producers.Select(Location))} | {string.Join("<br>", evt.Consumers.Select(Location))} |");
        if (events.Length > 0)
        {
            output.AppendLine("\n```mermaid\nflowchart LR");
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                output.AppendLine(CultureInfo.InvariantCulture, $"  e{i}[\"{Label(evt.Name + " / " + evt.Mode)}\"]");
                for (var j = 0; j < evt.Producers.Count; j++) output.AppendLine(CultureInfo.InvariantCulture, $"  p{i}_{j}[\"{Label(evt.Producers[j].Symbol)}\"] --> e{i}");
                for (var j = 0; j < evt.Consumers.Count; j++) output.AppendLine(CultureInfo.InvariantCulture, $"  e{i} --> c{i}_{j}[\"{Label(evt.Consumers[j].Symbol)}\"]");
            }
            output.AppendLine("```");
        }
        if (events.Length == 0) output.AppendLine("\n没有可静态解析的事件声明。");
        output.AppendLine("\n## 校验诊断\n");
        foreach (var diagnostic in diagnostics.Items.Where(d => d.Error.Errors?.Any(e => e.Code.StartsWith("DOC_", StringComparison.Ordinal)) == true))
            output.AppendLine(CultureInfo.InvariantCulture, $"- {Text(diagnostic.Severity)}：{Code(diagnostic.File)}:{diagnostic.Line}，{Text(diagnostic.Error.Errors![0].Message)}");
        return output.ToString();
    }
    private static bool Skeleton(ResolvedProfileEntry entry) => entry.Manifest.TryGetProperty("x-skeleton", out var value) && value.ValueKind == JsonValueKind.True;
    private static string Location(EventSite site) => $"{Code(site.Symbol)}（{Code(site.File)}:{site.Line}）";
    private static string Text(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Replace("`", "&#96;", StringComparison.Ordinal);
    private static string Code(string value)
    {
        value = value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        var delimiter = "`"; while (value.Contains(delimiter, StringComparison.Ordinal)) delimiter += '`';
        return delimiter + " " + value + " " + delimiter;
    }
    private static string Label(string value) => Text(value).Replace("\"", "&quot;", StringComparison.Ordinal).Replace("[", "&#91;", StringComparison.Ordinal).Replace("]", "&#93;", StringComparison.Ordinal);
    private static string Fence(string json)
    {
        var delimiter = "```";
        while (json.Contains(delimiter, StringComparison.Ordinal)) delimiter += '`';
        using var document = JsonDocument.Parse(json);
        return delimiter + "json\n" + JsonSerializer.Serialize(document.RootElement, Pretty) + "\n" + delimiter + "\n";
    }
}
