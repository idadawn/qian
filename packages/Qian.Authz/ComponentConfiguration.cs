namespace Qian.Authz;

// 配置声明的代码真相源；组件目录中的 Schema 由 qian-plugin sync 派生。
internal static class ComponentConfiguration
{
    internal const string Schema = """
        {
          "type": "object",
          "properties": {
            "advancedMode": {
              "type": "boolean",
              "const": false,
              "description": "S1 没有高级模式（数据 / 字段权限编译属 S2），Provider 只接受 false。"
            },
            "platformRole": {
              "type": "string",
              "pattern": "^[a-z][a-z0-9_]{0,62}$",
              "maxLength": 63,
              "description": "可选的独立平台授权登录角色，配置后提供平台超管显式旁路；仅 global Provider；凭据由宿主单独绑定，不写入配置。",
              "not": {
                "pattern": "[^a-z0-9_]"
              }
            },
            "decisionLogSampling": {
              "type": "number",
              "minimum": 0,
              "maximum": 1,
              "default": 1,
              "description": "放行判定写入决策审计（decision_logs）的采样比例，0–1，缺省 1 全记；拒绝不受采样影响、必记（PRD 7.5）。"
            }
          },
          "additionalProperties": false,
          "$schema": "https://json-schema.org/draft/2020-12/schema"
        }
        """;
}
