using System.Text.Json;

namespace Qian.Common.Tests;

// S1-AGT-004：输入 Schema 点名租户字段的写法。@qian/surfaces 的 tenantFields 逐条对应，两端由 tck/surfaces 的共同 fixture 比对。
public sealed class TenantFieldRuleTests
{
    [Theory]
    [InlineData("tenant", true)]
    [InlineData("tenantId", true)]
    [InlineData("TENANT_ID", true)]
    [InlineData("Tenant-Id", true)]
    [InlineData("tEnAnTiD", true)]
    [InlineData("tenants", false)]
    [InlineData("lieutenant", false)]
    [InlineData("tenant__id", false)]
    [InlineData("tenant id", false)]
    [InlineData("tenant\n", false)]
    [InlineData("tenant_id\n", false)]
    [InlineData("tenantİd", false)]
    [InlineData("tenantıd", false)]
    public void 名称按TenantFieldName整体匹配_只认ASCII大小写_末尾换行不算(string name, bool expected) => Assert.Equal(expected, TenantFieldRule.IsName(name));

    [Theory]
    [InlineData("^tenant", TenantFieldRule.PatternMessage)]
    [InlineData("tenant", TenantFieldRule.PatternMessage)]
    [InlineData("^TENANT[_-]?ID$", TenantFieldRule.PatternMessage)]
    [InlineData("^TeNaNt$", TenantFieldRule.PatternMessage)]
    [InlineData("^[a-z_]+$", TenantFieldRule.PatternMessage)]
    [InlineData("id$", TenantFieldRule.PatternMessage)]
    [InlineData("^t.n.nt", TenantFieldRule.PatternMessage)]
    [InlineData("", TenantFieldRule.PatternMessage)]
    [InlineData("^x-", null)]
    [InlineData("^tenants$", null)]
    [InlineData("^lieutenant", null)]
    [InlineData("^[0-9]+$", null)]
    [InlineData("^tenant$\n", null)]
    [InlineData(@"\bmeta\b", TenantFieldRule.UnsupportedMessage)]
    [InlineData(@"^(?i)x-", TenantFieldRule.UnsupportedMessage)]
    [InlineData(@"^\p{L}+$", TenantFieldRule.UnsupportedMessage)]
    [InlineData("^(x-", TenantFieldRule.UnsupportedMessage)]
    public void 模式不加锚点_能匹配任一租户名的任一ASCII大小写写法即违规_超出可移植子集无法判定同样违规(string pattern, string? expected) =>
        Assert.Equal(expected, TenantFieldRule.Pattern(pattern));

    [Fact]
    public void 各写法按成员顺序报告_名称表先查名称再往下_实例数据不查_名称表里叫default的条目照常往下()
    {
        using var schema = JsonDocument.Parse("""
            {
              "type": "object", "additionalProperties": false,
              "properties": {
                "default": {"type": "object", "properties": {"tenant_id": {"type": "string"}}},
                "tenant": {"type": "string"},
                "enum": {"type": "object", "required": ["TenantId"]}
              },
              "patternProperties": {"^x-": {}, "^Tenant": {}, "\\bmeta": {}},
              "required": ["name", "tenantId"],
              "dependentRequired": {"name": ["tenant-id"], "TENANT": []},
              "dependentSchemas": {"tenant": {"properties": {"tenantId": {}}}},
              "dependencies": {"tenant": {"required": ["tenant"]}, "name": ["tenantid"]},
              "$defs": {"const": {"properties": {"tenant": {}}}},
              "examples": [{"properties": {"tenant": {}}, "required": ["tenant"]}],
              "default": {"tenantId": "x"}, "const": {"required": ["tenant"]}, "enum": [{"properties": {"tenant": {}}}],
              "x-note": {"required": ["tenant"]}
            }
            """);
        var found = TenantFieldRule.Find(schema.RootElement).Select(item => string.Join('/', item.Path)).ToArray();
        Assert.Equal([
            "properties/tenant", "properties/default/properties/tenant_id", "properties/enum/required/0",
            "patternProperties/^Tenant", "patternProperties/\\bmeta",
            "required/1",
            "dependentRequired/name/0", "dependentRequired/TENANT",
            "dependentSchemas/tenant", "dependentSchemas/tenant/properties/tenantId",
            "dependencies/tenant", "dependencies/name/0", "dependencies/tenant/required/0",
            "$defs/const/properties/tenant",
            "x-note/required/0",
        ], found);
    }

    [Fact]
    public void propertyNames只在肯定位置用const或enum列出租户名时算点名_pattern与not和if之下不算_开放映射不查()
    {
        using var schema = JsonDocument.Parse("""
            {"type": "object", "properties": {
              "a": {"type": "object", "propertyNames": {"enum": ["region", "Tenant"], "const": "tenant_id"}, "additionalProperties": {"type": "string"}},
              "b": {"type": "object", "propertyNames": {"anyOf": [{"const": "x"}, {"allOf": [{"enum": ["tenant-id"]}]}], "oneOf": [{"then": {"const": "TENANT"}, "else": {"enum": ["tenantId"]}}]}},
              "c": {"type": "object", "propertyNames": {"pattern": "^[a-z]+$", "not": {"enum": ["tenant", "tenantId"]}, "if": {"const": "tenant"}, "then": {"maxLength": 20}}},
              "d": {"type": "object", "additionalProperties": {"type": "string"}},
              "e": {"type": "object", "propertyNames": {"$ref": "#/$defs/keys"}}
            }, "$defs": {"keys": {"enum": ["tenant"]}}}
            """);
        Assert.Equal([
            "properties/a/propertyNames/enum/1", "properties/a/propertyNames/const",
            "properties/b/propertyNames/anyOf/1/allOf/0/enum/0", "properties/b/propertyNames/oneOf/0/then/const", "properties/b/propertyNames/oneOf/0/else/enum/0",
        ], TenantFieldRule.Find(schema.RootElement).Select(item => string.Join('/', item.Path)));
    }

    [Fact]
    public void 给出解析时在引用处继续检查_位置记在引用处之下_循环引用只走一遍()
    {
        using var schema = JsonDocument.Parse("""
            {"type": "object", "properties": {"scope": {"$ref": "#/$defs/scope"}},
             "$defs": {"scope": {"type": "object", "required": ["tenant"], "properties": {"child": {"$ref": "#/$defs/scope"}}}}}
            """);
        var root = schema.RootElement;
        JsonElement Resolve(string reference) => root.GetProperty("$defs").GetProperty(reference["#/$defs/".Length..]);
        Assert.Equal(["properties/scope/required/0", "$defs/scope/required/0", "$defs/scope/properties/child/required/0"],
            TenantFieldRule.Find(root, Resolve).Select(item => string.Join('/', item.Path)));
        // 不解析时只在定义处报告。
        Assert.Equal(["$defs/scope/required/0"], TenantFieldRule.Find(root).Select(item => string.Join('/', item.Path)));
    }
}
