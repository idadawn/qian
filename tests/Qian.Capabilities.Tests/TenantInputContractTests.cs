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
            {"paths": {"/probe": {"post": {"requestBody": {"required": true, "content": {"application/json": {"schema": {"$ref": "#/components/schemas/Body"}, "example": {"tenantId": "x"}}}}}}},
             "components": {"schemas": {"Body": {"type": "object", "properties": {"enum": {"type": "object", "required": ["tenantId"]}}}}}}
            """);
        var body = document.RootElement.GetProperty("paths").GetProperty("/probe").GetProperty("post").GetProperty("requestBody");
        var found = Assert.Single(TenantInputContract.FindFields(body, document.RootElement));
        Assert.Equal(["content", "application/json", "schema", "properties", "enum", "required", "0"], found);
    }

    [Theory]
    [InlineData("Tenant-ID", true)]
    [InlineData("tenantid", true)]
    [InlineData("tenant\n", false)]
    [InlineData("tenants", false)]
    public void 参数名按TenantFieldName整体匹配(string name, bool expected) => Assert.Equal(expected, TenantInputContract.IsTenantName(name));
}
