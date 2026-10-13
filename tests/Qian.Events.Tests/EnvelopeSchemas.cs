using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Qian.Events.Tests;

/// <summary>contracts/v0 的信封 Schema 与 iClaw EventEnvelope 摘录；iClaw 侧按对齐口径去掉 subject 域名白名单。</summary>
internal static class EnvelopeSchemas
{
    private static readonly Lazy<(JsonSchema Qian, JsonSchema IClaw)> Loaded = new(Load);
    private static readonly EvaluationOptions Strict = new() { OutputFormat = OutputFormat.List, RequireFormatValidation = true };

    public static string RepositoryRoot { get; } = FindRoot();

    public static string[] ValidateQian(JsonElement envelope) => Validate(Loaded.Value.Qian, envelope);

    public static string[] ValidateIClaw(JsonElement envelope) => Validate(Loaded.Value.IClaw, envelope);

    public static IEnumerable<(string Name, JsonElement Data)> Fixtures(string kind)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "contracts/v0/fixtures", "event-envelope." + kind + ".json")));
        foreach (var item in document.RootElement.EnumerateArray())
            yield return (item.GetProperty("name").GetString()!, item.GetProperty("data").Clone());
    }

    private static string[] Validate(JsonSchema schema, JsonElement instance)
    {
        var result = schema.Evaluate(instance, Strict);
        return result.IsValid ? [] : (result.Details ?? []).Where(detail => detail.Errors is { Count: > 0 })
            .Select(detail => detail.InstanceLocation + ": " + string.Join("; ", detail.Errors!.Values)).DefaultIfEmpty("invalid").ToArray();
    }

    private static (JsonSchema, JsonSchema) Load()
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        var v0 = Path.Combine(RepositoryRoot, "contracts/v0");
        _ = JsonSchema.FromText(File.ReadAllText(Path.Combine(v0, "s2s-claims.schema.json")), options);
        var qian = JsonSchema.FromText(File.ReadAllText(Path.Combine(v0, "event-envelope.schema.json")), options);
        // iclaw-alignment.md 第 3 节：subject 的四个域名白名单是两边已知不兼容项，校验租户事件时去掉。
        var iclaw = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts/iclaw-event-envelope.schema.json")))!.AsObject();
        iclaw["properties"]!["subject"]!.AsObject().Remove("pattern");
        return (qian, JsonSchema.FromText(iclaw.ToJsonString(), new BuildOptions { SchemaRegistry = new SchemaRegistry() }));
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录（Qian.slnx）。");
    }
}
