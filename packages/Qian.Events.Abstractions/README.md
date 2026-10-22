# Qian.Events.Abstractions

Events seam 的 Definition：集成事件信封 v1、事件来源作用域、消费端的投递上下文、事件类型与 MQ 拓扑的命名规则、事件目录与订阅目录、服务键和错误契约。业务组件只依赖本包，Provider 位于 [Qian.Events](../Qian.Events/README.md)。权威设计见 [PRD §7.10](../../docs/prd/07-s1-foundation.md) 与 [ADR-008](../../docs/adr/ADR-008-events-messaging.md)，任务见 [S1-EVT-001](../../pm/tasks/S1-EVT-001.md) 与 [S1-EVT-002](../../pm/tasks/S1-EVT-002.md)。

## 信封

`QianEventEnvelope` 是 [event-envelope.schema.json](../../contracts/v0/event-envelope.schema.json) 的 .NET 形状：version、event_id（UUID v7）、subject、type（`<subject>.v<N>`）、occurred_at（必须带时区偏移，写出为 UTC）、tenant_id（租户 UUID，平台级事件为 null）、producer、actor（kind 为 user / service / agent，可带 on_behalf_of）、trace_parent、payload，以及可选的 correlation_id、causation_id。

`Parse` 拒绝未知字段，并做 Schema 表达不了的语义校验：type 与 subject 一致、tenant_id 不是空串、producer 是组件 id、traceparent 格式正确、payload 是对象。任何一项不满足都抛 EVENTS_PUBLISH_REJECTED。UUID 一律写成小写规范格式，同一条信封往返后逐字段不变；iClaw 对齐口径见 [iclaw-alignment.md](../../contracts/v0/iclaw-alignment.md) 第 3 节。

`IntegrationEventTypes` 解析事件类型：subject 至少两段、全小写，版本号从 1 开始。`QianEventTopology` 给出 topic exchange `qian.events`、死信 exchange `qian.dlx`、路由键（等于 subject），以及队列名：

- `q.<service>.<subscription>`：竞争消费；
- `q.<service>.<instance>`：广播；
- `DeadLetterQueue`：`q.<service>.<subscription>-dlq`，每订阅一个，也是发往 `qian.dlx` 的路由键；
- `RetryQueue`：`<主队列>-retry-<n>-<毫秒>`，第 n 次失败后的退避队列。

`-dlq` 与 `-retry-<n>-<毫秒>` 两种后缀保留给消费端，订阅名、实例名以它们结尾即被拒绝，免得与别的订阅的死信或重试队列同名。

## 事件来源

`EventEnvelopeScope.Begin(EventOrigin)` 在当前异步流上声明由谁发事件：producer（组件 id）、actor，以及可选的 correlation_id、causation_id 和 traceparent。作用域按打开的逆序恢复外层，重复释放没有副作用。

SDK 命令管道在每次写命令外开启这个作用域：producer 取能力所属组件；actor 取已验证主体，委托调用时 actor 是代理、on_behalf_of 是被代表人；causation_id 是本次调用 id；correlation_id 沿用外层。来源只能由可信执行端开启，不从请求输入读取。不在作用域内发布的事件会被写入端拒绝。

## 事件目录

`EventsDefinition.Key` 拥有 Events 服务键，`ctx.Events()` 解析 `IEventsService`：

- `Catalog` 列出宿主登记的全部集成事件，以及由源生成 JsonTypeInfo 导出的 payload JSON Schema（2020-12）；
- `Find(type)` 按事件类型查找。

## 消费端

`QianEventDelivery` 是订阅器正在派发的一次投递：已校验、已与 AMQP 属性核对的信封，加上服务名、订阅名、模式、幂等记录的消费者名和第几次投递（首投为 1）。订阅器在每条消息的依赖注入作用域里提供它，handler 在构造函数里注入即可读到 producer、actor 与因果链；在订阅器作用域之外解析会抛 EVENTS_MISCONFIGURED。handler 本身实现 Kun 的 `IIntegrationEventHandler<TEvent>`，订阅登记入口 `Subscribe<TEvent, THandler>()` 在 Provider。

`EventSubscriptionMode` 区分竞争消费与广播。`IEventsService.Subscriptions` 列出宿主订阅器登记的全部订阅，每个（订阅, 事件 ID）一项 `EventSubscriptionDescriptor`：服务名、订阅名、模式、事件 ID、subject、版本、handler 类型全名、是否接受平台级事件，与 `Catalog` 按事件 ID 对齐，是事件目录「订阅者」的来源。

## 错误

| 错误码 | HTTP | 含义 |
|---|---|---|
| EVENTS_MISCONFIGURED | 500 | 装配或组件配置与契约不符 |
| EVENTS_PUBLISH_REJECTED | 500 | 一次发布不满足信封契约 |
| EVENTS_CONSUME_REJECTED | 500 | 消费端判定消息不可处理：不重投、不记幂等，直接进死信队列；handler 抛出它表示重试也不会成功 |

两者由 `QianEventsException` 携带，宿主统一映射为 ErrorDetail。

## 组件位置

本包适用于全部 .NET profile，只依赖 Qian.Runtime。它与 Qian.Runtime 的进程内事件（`ctx.On` / Dispatch）不混用：后者是组件之间的同步扩展点，这里是跨服务、经 Outbox 投递的集成事件。

## 已知限制与延期项

事件目录 HTTP 接口（含订阅者）、AsyncAPI 导出、主题 ACL 与面向 `NpgsqlTransaction` 的写入契约属于 [S1-EVT-003](../../pm/tasks/S1-EVT-003.md)。死信重放工具（ADR-008 D8.9）尚未提供。
