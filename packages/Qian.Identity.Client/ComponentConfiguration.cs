namespace Qian.Identity.Client;

// 配置声明的代码真相源；组件目录中的 Schema 由 qian-plugin sync 派生。
internal static class ComponentConfiguration
{
    internal const string Schema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": {
            "issuer": {
              "type": "string",
              "description": "发行方的规范 issuer，必须与宿主 IdentityClientOptions.Authority 一致（末尾斜杠不计）；只用于核对，不从配置推导发行方地址。"
            },
            "clientId": {
              "type": "string",
              "minLength": 1,
              "maxLength": 200,
              "description": "在发行方登记的客户端标识，必须与宿主 IdentityClientOptions.ClientId 一致。"
            },
            "clientSecret": {
              "type": "string",
              "description": "机密客户端的密钥引用（secretRef，platform 或 dev），第一次换码时经 Config 的密钥策略解析，不写进组件配置；公开客户端省略。",
              "pattern": "^secretRef://(platform|dev)/[^\\s?#]+$",
              "x-qian-sensitive": true
            }
          },
          "required": [
            "issuer",
            "clientId"
          ],
          "additionalProperties": false
        }
        """;
}
