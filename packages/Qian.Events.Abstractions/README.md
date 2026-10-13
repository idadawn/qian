# Qian.Events.Abstractions

Events seam 的 Definition：集成事件信封 v1、事件来源作用域、事件类型与 MQ 拓扑的命名规则、事件目录服务键和错误契约。业务组件只依赖本包，Provider 位于 [Qian.Events](../Qian.Events/README.md)。权威设计见 [PRD §7.10](../../docs/prd/07-s1-foundation.md) 与 [ADR-008](../../docs/adr/ADR-008-events-messaging.md)，任务见 [S1-EVT-001](../../pm/tasks/S1-EVT-001.md)。

## 信封

`QianEventEnvelope` 是 [event-envelope.schema.json](../../contracts/v0/event-envelope.schema.json) 的 .NET 形状：version、event_id（UUID v7）、subject、type（`<subject>.v<N>`）、occurred_at（必须带时区偏移，写出为 UTC）、tenant_id（租户 UUID，平台级事件为 null）、producer、actor（kind 为 user / service / agent，可带 on_behalf_of）、trace_parent、payload，以及可选的 correlation_id、causation_id。

`Parse` 拒绝未知字段，并做 Schema 表达不了的语义校验：type 与 subject 一致、tenant_id 不是空串、producer 是组件 id、traceparent 格式正确、payload 是对象。任何一项不满足都抛 EVENTS_PUBLISH_REJECTED。UUID 一律写成小写规范格式，同一条信封往返后逐字段不变；iClaw 对齐口径见 [iclaw-alignment.md](../../contracts/v0/iclaw-alignment.md) 第 3 节。

`IntegrationEventTypes` 解析事件类型：subject 至少两段、全小写，版本号从 1 开始。`QianEventTopology` 给出 topic exchange `qian.events`、死信 exchange `qian.dlx`、路由键（等于 subject），以及队列名 `q.<service>.<subscription>`（竞争消费）和 `q.<service>.<instance>`（广播）。

## 事件来源

`EventEnvelopeScope.Begin(EventOrigin)` 在当前异步流上声明由谁发事件：producer（组件 id）、actor，以及可选的 correlation_id、causation_id 和 traceparent。作用域按打开的逆序恢复外层，重复释放没有副作用。

SDK 命令管道在每次写命令外开启这个作用域：producer 取能力所属组件；actor 取已验证主体，委托调用时 actor 是代理、on_behalf_of 是被代表人；causation_id 是本次调用 id；correlation_id 沿用外层。来源只能由可信执行端开启，不从请求输入读取。不在作用域内发布的事件会被写入端拒绝。

## 事件目录

`EventsDefinition.Key` 拥有 Events 服务键，`ctx.Events()` 解析 `IEventsService`：

- `Catalog` 列出宿主登记的全部集成事件，以及由源生成 JsonTypeInfo 导出的 payload JSON Schema（2020-12）；
- `Find(type)` 按事件类型查找。

## 错误

| 错误码 | HTTP | 含义 |
|---|---|---|
| EVENTS_MISCONFIGURED | 500 | 装配或组件配置与契约不符 |
| EVENTS_PUBLISH_REJECTED | 500 | 一次发布不满足信封契约 |

两者由 `QianEventsException` 携带，宿主统一映射为 ErrorDetail。

## 组件位置

本包适用于全部 .NET profile，只依赖 Qian.Runtime。它与 Qian.Runtime 的进程内事件（`ctx.On` / Dispatch）不混用：后者是组件之间的同步扩展点，这里是跨服务、经 Outbox 投递的集成事件。

## 已知限制与延期项

消费端契约（订阅、ack、幂等与租户回灌）属于 [S1-EVT-002](../../pm/tasks/S1-EVT-002.md)；事件目录 HTTP 接口、AsyncAPI 导出与面向 `NpgsqlTransaction` 的写入契约属于 [S1-EVT-003](../../pm/tasks/S1-EVT-003.md)。
