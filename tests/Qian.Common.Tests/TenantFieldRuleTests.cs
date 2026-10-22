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
              "default": {"properties": {"tenantId": {}}}, "const": {"required": ["tenant"]}, "enum": [{"properties": {"tenant": {}}}],
              "example": {"required": ["tenant"]},
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

    [Theory]
    [InlineData("#/$defs/x/default", true)]
    [InlineData("#/$defs/x/examples/0", true)]
    [InlineData("#/properties/a/example", true)]
    [InlineData("#/enum/0", true)]
    [InlineData("#/const", true)]
    [InlineData("scope.json#/$defs/x/default", true)]
    [InlineData("#/$defs/x/%64efault", true)]
    [InlineData("#/$defs/x/%63%6F%6e%73%74", true)]
    [InlineData("#/$defs/x/properties", true)]
    [InlineData("#/$defs", true)]
    [InlineData("#/properties/a/dependencies", true)]
    [InlineData("#/$defs/x%2Fproperties/default", true)]
    [InlineData("#%2F$defs%2Fx", true)]
    [InlineData("#/$defs/x%2fy", true)]
    [InlineData("#/$defs/default", false)]
    [InlineData("#/properties/default/properties/enum", false)]
    [InlineData("#/$defs/x", false)]
    [InlineData("#/components/schemas/Body", false)]
    [InlineData("#/$defs/%E5%AE%A2%E6%88%B7", false)]
    [InlineData("#/$defs/x/%6", false)]
    [InlineData("#/$defs/x/de~0fault", false)]
    [InlineData("#keyed", false)]
    [InlineData("#", false)]
    [InlineData("scope.json", false)]
    public void 引用片段按遍历口径逐段判定_途经实例数据或停在名称表本身即越出Schema位置_编码的斜杠按无法判定(string reference, bool expected) =>
        Assert.Equal(expected, TenantFieldRule.OutsideSchema(reference));

    [Fact]
    public void 越出Schema位置的引用报在引用本身_给出解析时也不跟进去_内嵌资源里的同名路径同样拦住()
    {
        // 执行侧按 $id 把 d 里的引用解析到 d.json 自己的 $defs，按根解析会落到无害的 $defs/x/default 上（S1-AGT-007 之前能力契约就这样漏过）。
        using var schema = JsonDocument.Parse("""
            {"type": "object", "properties": {
              "a": {"$ref": "#/$defs/x/default"},
              "b": {"$dynamicRef": "#/$defs/x/default"},
              "c": {"$ref": "#/$defs/x"},
              "d": {"$id": "d.json", "$defs": {"x": {"default": {"required": ["tenant"]}}}, "properties": {"v": {"$ref": "#/$defs/x/default"}}}
            }, "$defs": {"x": {"type": "object", "default": {"required": ["tenantId"]}}}}
            """);
        var root = schema.RootElement;
        JsonElement Resolve(string reference) => reference[2..].Split('/').Aggregate(root, (node, segment) => node.GetProperty(segment));
        foreach (var found in new[] { TenantFieldRule.Find(root), TenantFieldRule.Find(root, Resolve) })
        {
            Assert.Equal(["properties/a/$ref", "properties/b/$dynamicRef", "properties/d/properties/v/$ref"], found.Select(item => string.Join('/', item.Path)));
            Assert.All(found, item => Assert.Equal(TenantFieldRule.ReferenceMessage, item.Message));
        }
    }
}
