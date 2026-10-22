namespace Qian.Identity.Server;

// 配置声明的代码真相源；组件目录中的 Schema 由 qian-plugin sync 派生。
internal static class ComponentConfiguration
{
    internal const string Schema = """
        {
          "type": "object",
          "properties": {
            "issuer": {
              "type": "string",
              "format": "uri"
            }
          },
          "additionalProperties": false,
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "required": [
            "issuer"
          ]
        }
        """;
}
