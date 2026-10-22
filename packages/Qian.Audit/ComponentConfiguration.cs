namespace Qian.Audit;

// 配置声明的代码真相源；组件目录中的 Schema 由 qian-plugin sync 派生。
internal static class ComponentConfiguration
{
    internal const string Schema = """
        {
          "type": "object",
          "properties": {
            "chain": {
              "type": "object",
              "properties": {
                "store": {
                  "type": "string",
                  "enum": [
                    "pg"
                  ]
                },
                "platformRole": {
                  "type": "string",
                  "pattern": "^[a-z][a-z0-9_]{0,62}$",
                  "maxLength": 63,
                  "description": "可选的独立平台审计登录角色，仅允许 global Provider；凭据由宿主单独绑定，不写入配置。",
                  "not": {
                    "pattern": "[^a-z0-9_]"
                  }
                }
              },
              "additionalProperties": false
            }
          },
          "additionalProperties": false,
          "$schema": "https://json-schema.org/draft/2020-12/schema"
        }
        """;
}
