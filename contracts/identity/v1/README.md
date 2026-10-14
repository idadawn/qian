# 身份事件 payload v1

五份 Schema 对应 `IdentityEventIds` 和类型上的稳定 `[IntegrationEventId]`，由 `Qian.Identity.Abstractions` 嵌入分发。外层信封沿用 `contracts/v0/event-envelope.schema.json`；UUID 直接引用冻结 S2S Schema 的定义，不再抄写另一份规则。

事件为登录、退出、改密、锁定和会话吊销。只发送身份投影与修订信息，不发送密码、哈希、OTP 或令牌。OccurredAt 使用 UTC；会话标识是内部失效关联字段，日志只允许哈希或脱敏标识。

字段、属性标记和源生成 JSON 由 `Qian.Identity.Abstractions.Tests` 逐一校验。破坏性变化新增 v2 类型与 Schema，不覆盖 v1。

已知问题：`event_id`、`occurred_at`、`tenant_id` 与外层信封的同名字段重复且不同源，消费端以信封为准；去留见 [S1-DEC-020](../../../pm/tasks/S1-DEC-020.md)。
