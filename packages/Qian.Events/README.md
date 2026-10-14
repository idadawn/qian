# Qian.Events

Events seam 的 Provider：v1 信封写入端、RabbitMQ 发布端、按租约领取的 Outbox 投递后台服务、payload Schema 导出，以及组件入口 `EventsPlugin`。契约在 [Qian.Events.Abstractions](../Qian.Events.Abstractions/README.md)，任务见 [S1-EVT-001](../../pm/tasks/S1-EVT-001.md)。

## 宿主装配

```csharp
builder.Services.AddKunEventBus(events => events.Register(OrderEvents.Created, OrderJson.Default.OrderCreated));
builder.Services.AddQianData<OrderDbContext>((sp, options) => options.UseNpgsql(connection).UseKunSchema("plugin_order"));
builder.Services.AddQianEvents();
builder.Services.AddQianEventsBroker(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Events")!);
```

- `AddQianEvents` 把数据会话的 `IOutboxWriter` 与 `IIntegrationEventBus` 换成同一个 scoped 信封写入端，只写 Outbox、不投递。
- `AddQianEventsBroker` 再登记 RabbitMQ 发布端和投递后台服务。AMQP 地址在首次连接时读取，不进组件清单。
- 两者与 `AddQianData` 的调用顺序无关。

启动检查有三条，任一不满足宿主拒绝启动：

- 宿主必须用 `AddKunEventBus` 显式登记事件类型；
- 每个类型都带源生成 `JsonTypeInfo`，并声明与登记一致的 `[IntegrationEventId]`；
- Outbox 写入端没有被其他装配覆盖。

## 写入

业务代码照旧调用 `session.Outbox.Enqueue(event)` 或 `IIntegrationEventBus.Publish(event)`，写入端负责补齐信封：

- 来源取自 `EventEnvelopeScope`；
- tenant_id 取自租户上下文，必须是小写规范 UUID；没有租户即平台级事件，tenant_id 为 null；
- event_id 是以事件发生时间生成的 UUID v7；
- 完整 v1 信封作为 Outbox 行的 payload，与业务数据在同一事务提交。

登记与发布的类型就是信封里的 payload，不要把整封信封登记成事件类型，载荷也不声明自己的 event_id 或发生时间（写入端不读，ADR-008 D8.7 / S1-DEC-020）。消费方需要信封上下文时，从解析后的信封派生视图，例如 Qian.Data 记录事件的 `RecordChangedEvent.FromEnvelope`。

以下情况抛 EVENTS_PUBLISH_REJECTED，缓冲里不会留下半条：

- 不在事件作用域内；
- 类型未登记，或缺少 JsonTypeInfo；
- 租户不是 UUID。

编译期规则 QIAN0006 限定发布位置：只允许写命令 handler（Kun `ICommand`，或 `[Capability]` 中 SideEffect 不为 read）与领域事件通知 handler。在查询、后台服务等处调用 `Publish` / `Enqueue` 直接编译失败；直接向 `IntegrationEventBuffer.Add` 写入只允许出现在写入端实现里。规则与正反例见 [tests/README.md](../../tests/README.md#集成事件发布位置门禁s1-evt-001)。

## 投递

投递后台服务逐个处理已登记的业务上下文。每一轮：

1. 一条语句以 `FOR UPDATE SKIP LOCKED` 领取最多 BatchSize 行到期的 Outbox，把 next_attempt_at 推到租约到期时刻，随即提交。到期判断与租约都用数据库时钟，副本之间的时钟偏差不影响接管时机。
2. 逐条发布，等 publisher confirm 后立刻标记已发布。
3. 剩余租约不够再等一次确认时（broker 变慢、整批发不完），把本批未发的行交还，而不是拖到租约到期。

各种情况下的行为：

- 副本收到停机信号：先完成手上这一条，再交还本批其余行，另一副本可以马上接手。
- 副本崩溃：它领取的行在租约到期后被其他副本接管。
- 交还：只放回仍由本次领取持有的行。
- 已确认的消息不会因停机、回滚或 broker 变慢而重发。

发布失败按 `RetryBaseDelay × 2^(n-1)`（封顶 60 秒）退避，并记入 next_attempt_at；达到 MaxAttempts 次后标记失败。信封与行不一致（id、type、租户）属于不可重试的错误，直接标记失败。

组件配置 `outbox.lock: redis` 时，在领取之外再按上下文 schema 取 Kun 的 Redis 锁，宿主须登记 `IConnectionMultiplexer`。meter `Qian.Events` 输出 confirmed、retried、failed 三个计数，confirmed 只在 broker 确认后加一。

| 选项 | 默认 | 说明 |
|---|---|---|
| BatchSize | 100 | 每轮最多领取的行数 |
| PollInterval | 1 秒 | 一轮没有领到消息时的等待 |
| LeaseDuration | 30 秒 | 领取后的租约，须长于 PublishTimeout |
| PublishTimeout | 10 秒 | 单条等待 broker 确认的上限 |
| MaxAttempts | 5 | 达到后行标为失败 |
| RetryBaseDelay | 1 秒 | 退避起点，按 2 的幂增长、封顶一分钟 |
| OutboxLock | SkipLocked | 必须与组件配置 `outbox.lock` 一致 |
| RelayInstance | 机器名-进程号 | 只用于日志与指标标签 |

## RabbitMQ 拓扑

- exchange：topic 类型的 `qian.events`，路由键是 subject，消息体就是 Outbox 行里的信封原文。
- 消息属性：MessageId 为 event_id，Type 为 type，AppId 为 producer，持久化投递；头部带 traceparent、tracestate 与 tenant_id。
- 没有订阅者的事件由 broker 丢弃（mandatory=false）。
- 竞争消费：每个订阅一个 quorum 队列 `q.<service>.<subscription>`，死信转 `qian.dlx`。
- 广播：每个实例一个经典自动删除队列 `q.<service>.<instance>`。RabbitMQ 4 默认禁止非持久的非独占队列，所以广播队列声明为持久，并设 30 分钟闲置过期兜底。

发布端没有复用 Kun.EventBus.RabbitMQ 的发布器，原因有三：它以带版本的 type 作路由键，与 PRD 约定的 subject 路由不符；每条消息新建一个通道；并以 mandatory 发布，在确认跟踪下没有订阅者的事件会确认失败，行会反复重试直到标记失败。这里复用一条确认通道，失败时重建。

## 组件入口

`EventsPlugin`（组件 `qian.events`）可以挂在 global 或 tenant scope。挂载时核对组件配置与宿主装配一致：

- `broker` 只能是宿主登记过的 secretRef；
- `outbox.lock`（skip-locked 或 redis）必须与宿主选项相同。

核对通过后登记错误码并提供 Events 服务键，卸载时撤销。

## 已知限制与延期项

- 消费端（订阅、处理成功后 ack、Redis 幂等键、按 tenant_id 回灌租户、DLX）由 [S1-EVT-002](../../pm/tasks/S1-EVT-002.md) 交付，本包只声明拓扑。
- 事件目录 HTTP 接口、AsyncAPI 导出、主题 ACL 与面向 `NpgsqlTransaction` 的写入端由 [S1-EVT-003](../../pm/tasks/S1-EVT-003.md) 交付。
- S1-DEC-013 列为可选项的 PG `processed_messages` 兜底表没有做。
- `plugins/qian.events` 仍保留骨架标记：SDK 发布与 `qian add` 安装验收通过前，不把本包写成完整的组件交付。
