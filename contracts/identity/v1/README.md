# 身份事件 payload v1

五份 Schema 对应 `IdentityEventIds` 和类型上的稳定 `[IntegrationEventId]`，由 `Qian.Identity.Abstractions` 嵌入分发。外层信封沿用 `contracts/v0/event-envelope.schema.json`；UUID 直接引用冻结 S2S Schema 的定义，不再抄写另一份规则。

事件为登录、退出、改密、锁定和会话吊销。只发送身份投影与修订信息，不发送密码、哈希、OTP 或令牌。会话标识是内部失效关联字段，日志只允许哈希或脱敏标识。

载荷不含信封字段（[S1-DEC-020](../../../pm/tasks/S1-DEC-020.md)）：`event_id`、`occurred_at` 由 Qian.Events 写入端入队时生成，`tenant_id` 取发布时的租户上下文，三者只在信封上，消费端从信封读取。生产方在事件所属租户的作用域内入队；平台级身份事件在租户作用域外入队，信封 `tenant_id` 为 null。

字段、属性标记和源生成 JSON 由 `Qian.Identity.Abstractions.Tests` 逐一校验。自首个发布版本起，破坏性变化新增 v2 类型与 Schema，不覆盖 v1；发布前去掉信封字段这一次就地修正由 S1-DEC-020 裁决。
