using System.Text.Json;

namespace Qian.Runtime.Errors.Tests;

/// <summary>contracts/v0 的冻结文件：Schema 本身合规、正例全过、反例全拒、语义反例过 Schema 但违反语义规则。</summary>
public sealed class ContractSchemaTests
{
    public static TheoryData<string> SchemaNames => new(Contracts.SchemaNames);

    public static TheoryData<string, string> ValidCases => Cases("valid");

    public static TheoryData<string, string> InvalidCases => Cases("invalid");

    public static TheoryData<string, string> SemanticInvalidCases => Cases("semantic-invalid");

    [Theory]
    [MemberData(nameof(SchemaNames))]
    public void Schema_is_a_valid_2020_12_schema_with_frozen_status(string name)
    {
        using var doc = Contracts.ReadJson(Path.Combine(Contracts.V0, $"{name}.schema.json"));
        var root = doc.RootElement;

        Assert.Empty(Contracts.Validate(Contracts.MetaSchema, root));
        Assert.Equal(Contracts.MetaSchemaUri, root.GetProperty("$schema").GetString());
        Assert.Equal($"https://qian.dev/contracts/v0/{name}.schema.json", root.GetProperty("$id").GetString());
        // R53 / R80 / R119：S1-AGT-005 已满足冻结前提，不得移除标记或退回 draft。
        Assert.Equal("frozen", root.GetProperty("x-status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("description").GetString()));
    }

    [Theory]
    [MemberData(nameof(SchemaNames))]
    public void Every_object_with_declared_properties_is_closed(string name)
    {
        using var doc = Contracts.ReadJson(Path.Combine(Contracts.V0, $"{name}.schema.json"));
        var open = new List<string>();
        Walk(doc.RootElement, "#", open);
        Assert.True(open.Count == 0, $"{name} 中声明了 properties 的 object 必须 additionalProperties: false（或 unevaluatedProperties: false）：{string.Join(", ", open)}");
    }

    [Theory]
    [MemberData(nameof(ValidCases))]
    public void Valid_fixture_passes_schema_and_semantics(string schema, string fixture)
    {
        var data = Find(schema, "valid", fixture);
        Assert.Empty(Contracts.Validate(schema, data));
        Assert.Empty(Contracts.CheckSemantics(schema, data));
    }

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void Invalid_fixture_is_rejected_by_schema(string schema, string fixture)
    {
        Assert.NotEmpty(Contracts.Validate(schema, Find(schema, "invalid", fixture)));
    }

    [Theory]
    [MemberData(nameof(SemanticInvalidCases))]
    public void Semantic_invalid_fixture_passes_schema_but_breaks_a_semantic_rule(string schema, string fixture)
    {
        var data = Find(schema, "semantic-invalid", fixture);
        Assert.Empty(Contracts.Validate(schema, data));
        Assert.NotEmpty(Contracts.CheckSemantics(schema, data));
    }

    [Fact]
    public void Schema_test_inventory_covers_every_v0_schema()
    {
        Assert.Equal(
            Directory.GetFiles(Contracts.V0, "*.schema.json").Select(Path.GetFileName).Order(StringComparer.Ordinal),
            Contracts.SchemaNames.Select(name => $"{name}.schema.json").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_schema_has_valid_and_invalid_fixtures()
    {
        foreach (var name in Contracts.SchemaNames)
        {
            Assert.NotEmpty(Contracts.Fixtures(name, "valid"));
            Assert.NotEmpty(Contracts.Fixtures(name, "invalid"));
        }
    }

    [Fact]
    public void Every_json_file_under_contracts_parses()
    {
        foreach (var path in Directory.EnumerateFiles(Contracts.V0, "*.json", SearchOption.AllDirectories))
        {
            using var _ = Contracts.ReadJson(path);
        }
    }

    private static TheoryData<string, string> Cases(string kind)
    {
        var data = new TheoryData<string, string>();
        foreach (var schema in Contracts.SchemaNames)
        {
            foreach (var (name, _) in Contracts.Fixtures(schema, kind))
                data.Add(schema, name);
        }
        return data;
    }

    private static JsonElement Find(string schema, string kind, string name)
        => Contracts.Fixtures(schema, kind).Single(f => f.Name == name).Data;

    private static void Walk(JsonElement node, string path, List<string> open)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                if (node.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "object"
                    && node.TryGetProperty("properties", out _)
                    && !IsFalse(node, "additionalProperties") && !IsFalse(node, "unevaluatedProperties"))
                {
                    open.Add(path);
                }
                foreach (var p in node.EnumerateObject())
                    Walk(p.Value, $"{path}/{p.Name}", open);
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in node.EnumerateArray())
                    Walk(item, $"{path}/{i++}", open);
                break;
        }
    }

    private static bool IsFalse(JsonElement node, string keyword)
        => node.TryGetProperty(keyword, out var value) && value.ValueKind == JsonValueKind.False;
}
