namespace Qian.Tenancy;

// 配置声明的代码真相源；组件目录中的 Schema 由 qian-plugin sync 派生。
internal static class ComponentConfiguration
{
    internal const string Schema = """
        {
          "type": "object",
          "properties": {
            "resolver": {
              "type": "object",
              "properties": {
                "mode": {
                  "type": "string",
                  "const": "production"
                },
                "allowHeaderFallback": {
                  "type": "boolean",
                  "const": false
                }
              },
              "additionalProperties": false,
              "description": "严格生产解析边界的声明；仅支持已验证 tenant_id，禁止请求头兜底。"
            },
            "initializationCapabilities": {
              "type": "array",
              "items": {
                "type": "string",
                "pattern": "^[a-z][a-z0-9.-]*/[a-z][A-Za-z0-9_.-]*$",
                "maxLength": 384
              },
              "uniqueItems": true,
              "default": [],
              "description": "仅全局开户编排可以执行的初始化能力精确标识；不接受通配符。"
            }
          },
          "additionalProperties": false,
          "$schema": "https://json-schema.org/draft/2020-12/schema"
        }
        """;
}
