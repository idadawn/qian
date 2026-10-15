using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qian.Plugin.Sdk.Tests;

public sealed class JsonSchemaReferencesTests
{
    [Fact]
    public void 布尔Schema等价展开_默认实例与examples不改写_引用仍重定位()
    {
        using var schema = JsonDocument.Parse("""{"properties":{"config":true,"blocked":false,"self":{"$ref":"#"}},"additionalProperties":false,"default":{"$ref":"#/keep","properties":{"flag":true}},"examples":[false]}""");
        var changed = JsonSchemaReferences.RebaseToComponent(schema.RootElement, "Example");
        Assert.Empty(changed["properties"]!["config"]!.AsObject());
        Assert.Empty(changed["properties"]!["blocked"]!["not"]!.AsObject());
        Assert.Equal("#/components/schemas/Example", changed["properties"]!["self"]!["$ref"]!.GetValue<string>());
        Assert.False(changed["additionalProperties"]!.GetValue<bool>());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(schema.RootElement.GetProperty("default").GetRawText()), changed["default"]));
        Assert.False(changed["examples"]![0]!.GetValue<bool>());
    }
}
