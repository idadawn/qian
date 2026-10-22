namespace Qian.Config.Apollo;

// 配置声明的代码真相源；组件目录中的 Schema 由 qian-plugin sync 派生。
internal static class ComponentConfiguration
{
    internal const string Schema = """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false,
          "$schema": "https://json-schema.org/draft/2020-12/schema"
        }
        """;
    internal const string ApolloSchema = """
        {
          "type": "object",
          "properties": {
            "meta": {
              "type": "string"
            },
            "appId": {
              "type": "string"
            },
            "namespaces": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "env": {
              "type": "string"
            }
          },
          "additionalProperties": false,
          "$schema": "https://json-schema.org/draft/2020-12/schema"
        }
        """;
}
