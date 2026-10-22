namespace Qian.Capabilities;

// 配置声明的代码真相源；组件目录中的 Schema 由 qian-plugin sync 派生。
internal static class ComponentConfiguration
{
    internal const string Schema = """
        {
          "type": "object",
          "properties": {
            "catalogTtlMs": {
              "type": "integer",
              "minimum": 0,
              "maximum": 3000,
              "default": 1000,
              "description": "能力目录响应的 ttlMs：客户端可缓存目录列表的毫秒数。上限与权限变更 3 秒内生效的口径一致；执行入口每次重新判定，缓存不是执行授权。"
            },
            "receiptMaxBytes": {
              "type": "integer",
              "minimum": 0,
              "maximum": 1048576,
              "default": 65536,
              "description": "通用执行入口幂等回执保存响应体的上限（UTF-8 字节）。超出时回执只记已执行、不存响应体，同键重放返回 409 CAPABILITY_REPLAY_UNAVAILABLE；0 表示从不保存响应体。上限 1 MiB 与回执表的检查约束一致；数据分级为 pii、finance、secret 的能力不论大小都不保存响应体。"
            }
          },
          "additionalProperties": false,
          "$schema": "https://json-schema.org/draft/2020-12/schema"
        }
        """;
}
