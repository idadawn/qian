using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qian.Surfaces.Tests;

public sealed class MarketplaceTests
{
    [Fact]
    public void SharedMarketplaceVectors_UseTheSameDiagnostics()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "tck/marketplaces/cases.json")));
        var validator = new SurfaceValidator(); var report = new JsonObject();
        foreach (var vector in vectors.RootElement.EnumerateArray())
        {
            var name = vector.GetProperty("name").GetString()!;
            var result = validator.ValidateMarketplace(".qian/marketplace.json", vector.GetProperty("content").GetString()!, new string('0', 32));
            var diagnostics = new JsonArray(result.Diagnostics.Select(d => (JsonNode)new JsonObject
            { ["file"] = d.File, ["line"] = d.Line, ["severity"] = d.Severity, ["pointer"] = d.Error.Pointer, ["code"] = d.Error.Errors![0].Code }).ToArray());
            var normalized = new JsonObject { ["valid"] = result.IsValid, ["diagnostics"] = diagnostics };
            Assert.True(result.IsValid == vector.GetProperty("valid").GetBoolean(), name + ": " + normalized.ToJsonString());
            Assert.DoesNotContain("DO-NOT-LEAK", JsonSerializer.Serialize(result));
            report[name] = normalized;
        }
        if (Environment.GetEnvironmentVariable("QIAN_MARKET_REPORT") is { } output)
        { Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.WriteAllText(output, report.ToJsonString()); }
    }
}
