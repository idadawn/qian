namespace Qian.Data;

// 配置声明的代码真相源；组件目录中的 Schema 由 qian-plugin sync 派生。
internal static class ComponentConfiguration
{
    internal const string Schema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": {
            "dialect": {
              "type": "string",
              "const": "pg",
              "default": "pg"
            },
            "connection": {
              "type": "string",
              "description": "选择由宿主显式绑定此引用的业务上下文；省略时使用各上下文自身的连接。凭据按上下文创建读取，不写入清单。",
              "pattern": "^secretRef://(platform|dev)/[^\\s?#]+$",
              "x-qian-sensitive": true
            },
            "rls": {
              "type": "object",
              "properties": {
                "enforce": {
                  "type": "boolean",
                  "const": true
                }
              },
              "required": [
                "enforce"
              ],
              "additionalProperties": false
            }
          },
          "additionalProperties": false
        }
        """;
}
