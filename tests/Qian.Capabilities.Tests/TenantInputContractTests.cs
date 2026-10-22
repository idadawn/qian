using System.Text.Json;

namespace Qian.Capabilities.Tests;

// S1-AGT-004：能力输入契约与表面校验器同一规则（packages/Common/TenantFieldRule.cs），properties 之外点名租户字段的写法同样返回位置段。
public sealed class TenantInputContractTests
{
    [Fact]
    public void properties之外点名租户字段的写法都返回位置段_按成员顺序_实例数据不误报()
    {
        using var schema = JsonDocument.Parse("""
            {"type": "object", "additionalProperties": false,
             "properties": {"name": {"type": "string"}, "default": {"type": "object", "properties": {"Tenant": {}}}},
             "required": ["name", "tenant_id"],
             "patternProperties": {"^x-": {}, "(?i)tenant": {}, "^TENANT": {}},
             "dependentRequired": {"name": ["tenant-id"]}, "dependentSchemas": {"tenantId": {}},
             "examples": [{"tenantId": "x", "required": ["tenant"]}], "default": {"properties": {"tenant": {}}}}
            """);
        Assert.Equal(["properties/default/properties/Tenant", "required/1", "patternProperties/(?i)tenant", "patternProperties/^TENANT", "dependentRequired/name/0",
            "dependentSchemas/tenantId"], TenantInputContract.FindFields(schema.RootElement).Select(path => string.Join('/', path)));
    }

    [Fact]
    public void HTTP请求体经文档内引用检查_名为enum的属性照常往下_媒体类型的示例不当Schema()
    {
        using var document = JsonDocument.Parse("""
            {"paths": {"/probe": {"post": {"requestBody": {"required": true, "content": {"application/json": {"schema": {"$ref": "#/components/schemas/Body"}, "example": {"required": ["tenantId"]}}}}}}},
             "components": {"schemas": {"Body": {"type": "object", "properties": {"enum": {"type": "object", "required": ["tenantId"]}}}}}}
            """);
        var body = document.RootElement.GetProperty("paths").GetProperty("/probe").GetProperty("post").GetProperty("requestBody");
        var found = Assert.Single(TenantInputContract.FindFields(body, document.RootElement));
        Assert.Equal(["content", "application/json", "schema", "properties", "enum", "required", "0"], found);
    }

    [Fact]
    public void propertyNames用const或enum列出租户名算点名_not之下的排除与开放映射不算()
    {
        using var schema = JsonDocument.Parse("""
            {"type": "object", "properties": {
              "labels": {"type": "object", "propertyNames": {"enum": ["region", "tenantId"]}, "additionalProperties": {"type": "string"}},
              "tags": {"type": "object", "propertyNames": {"pattern": "^[a-z]+$", "not": {"enum": ["tenant"]}}, "additionalProperties": {"type": "string"}},
              "metadata": {"type": "object", "additionalProperties": {"type": "string"}}}}
            """);
        Assert.Equal(["properties/labels/propertyNames/enum/1"], TenantInputContract.FindFields(schema.RootElement).Select(path => string.Join('/', path)));
    }

    [Fact]
    public void 引用途经实例数据或停在名称表本身即返回引用位置_不跟进去_内嵌资源里的同名路径与数组下标同样拦住()
    {
        // S1-AGT-007 之前：inner 的引用按根解析到无害的 $defs/x/default，执行侧却按 inner.json 解析、要求 tenantId；数组下标与百分号编码让解析抛异常，
        // 只能整个契约按 contract-invalid 拒绝；table 跟进去后报在 properties/table/properties/tenantId。
        using var schema = JsonDocument.Parse("""
            {"type": "object", "additionalProperties": false,
             "$defs": {"x": {"default": {"type": "object"}, "examples": [{"required": ["tenantId"]}]}, "t": {"type": "object", "properties": {"properties": {"tenantId": {}}}}},
             "properties": {
               "inner": {"$id": "inner.json", "$defs": {"x": {"default": {"required": ["tenantId"]}}}, "properties": {"q": {"$ref": "#/$defs/x/default"}}},
               "item": {"$ref": "#/$defs/x/examples/0"}, "encoded": {"$ref": "#/$defs/x/%64efault"}, "table": {"$ref": "#/$defs/t/properties"}}}
            """);
        Assert.Equal(["properties/inner/properties/q/$ref", "properties/item/$ref", "properties/encoded/$ref", "properties/table/$ref"],
            TenantInputContract.FindFields(schema.RootElement).Select(path => string.Join('/', path)));
    }

    [Theory]
    [InlineData("Tenant-ID", true)]
    [InlineData("tenantid", true)]
    [InlineData("tenant\n", false)]
    [InlineData("tenants", false)]
    public void 参数名按TenantFieldName整体匹配(string name, bool expected) => Assert.Equal(expected, TenantInputContract.IsTenantName(name));
}
