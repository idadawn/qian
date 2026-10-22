# Qian.Events

Events seam 的 Provider：v1 信封写入端、RabbitMQ 发布端、按租约领取的 Outbox 投递后台服务、消费端订阅器、payload Schema 导出，以及组件入口 `EventsPlugin`。契约在 [Qian.Events.Abstractions](../Qian.Events.Abstractions/README.md)，任务见 [S1-EVT-001](../../pm/tasks/S1-EVT-001.md)（写入与投递）与 [S1-EVT-002](../../pm/tasks/S1-EVT-002.md)（消费端）。

## 宿主装配

```csharp
builder.Services.AddKunEventBus(events => events.Register(OrderEvents.Created, OrderJson.Default.OrderCreated));
builder.Services.AddQianData<OrderDbContext>((sp, options) => options.UseNpgsql(connection).UseKunSchema("plugin_order"));
builder.Services.AddQianEvents();
builder.Services.AddQianEventsBroker(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Events")!);
```

- `AddQianEvents` 把数据会话的 `IOutboxWriter` 与 `IIntegrationEventBus` 换成同一个 scoped 信封写入端，只写 Outbox、不投递。
- `AddQianEventsBroker` 再登记 RabbitMQ 发布端和投递后台服务。AMQP 地址在首次连接时读取，不进组件清单。只订阅、没有业务上下文的宿主（例如网关）也经它登记 broker，投递服务这时没有可领取的 Outbox。
- `AddQianEventsSubscriber` 登记消费端，见下文「消费」。
- 三者与 `AddQianData` 的调用顺序无关。

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

## 消费

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redis));
builder.Services.AddSingleton<ITenantContextPropagator>(sp => new TenantContextPropagator(sp.GetRequiredService<ITenantContext>()));
builder.Services.AddQianEventsSubscriber("qian.identity", subscriber => subscriber
    .Subscribe<TenantSuspended, TenantSuspendedSessionHandler>("session-revocation")
    .Subscribe<TenantExpired, TenantExpiredSessionHandler>("session-revocation"));
```

- 第一个参数是订阅方服务名，与订阅名一起决定队列名和幂等记录的消费者名。同一宿主可以多次调用以追加订阅，服务名必须相同。
- `Subscribe<TEvent, THandler>(subscription, mode, acceptPlatformEvents)` 把一个 Kun `IIntegrationEventHandler<TEvent>` 挂到订阅上，并以 scoped 登记该 handler 类型。宿主已经用 `AddKunEventBus(b => b.AddHandler<TEvent, THandler>())` 登记过的 handler（如身份组件的 `AddQianIdentitySessionHandlers()`）照样可以订阅。
- 一个订阅可以覆盖多个事件，但同一事件在一个订阅里只有一个 handler；同一事件在一个服务里只能有一个广播订阅（广播订阅共用实例队列）。
- 消费连接与发布端分开（ClientProvidedName 为 `qian.events.subscriber`），AMQP 地址复用 `AddQianEventsBroker` 登记的引用。

启动检查不满足即拒绝启动，错误码 EVENTS_MISCONFIGURED：

- 宿主登记了 `AddQianEventsBroker`；
- 有幂等存储：宿主登记的 `IProcessedMessageStore`，否则用已登记的 `IConnectionMultiplexer` 建 Kun 的 `RedisProcessedMessageStore`；两者都没有即拒绝。`UseInMemoryIdempotency()` 只能用于测试，启动时记一条警告；
- 每个订阅的事件都已在 `AddKunEventBus` 以源生成 JsonTypeInfo 登记；
- 订阅名、实例名合法，实例名不与订阅名相同。

### 一条消息怎么处理

每个队列一个消费通道，手动确认，同一通道上的消息依次处理（PrefetchCount 只是预取）。处理顺序：

1. 消息体按 `QianEventEnvelope.Parse` 解析并校验，再与 AMQP 属性核对：MessageId 等于 event_id，Type 等于 type，头 `tenant_id` 与信封 tenant_id 一致（平台级事件没有这个头），从 `qian.events` 直接投来的消息路由键等于 subject；
2. 按信封 type 找到本队列里的订阅与 handler；
3. 查幂等记录（Kun `IProcessedMessageStore`，Redis 键 `idemp:{consumer}:{event_id}`），命中即确认、计 duplicate，不再执行 handler；
4. 新建依赖注入作用域，按信封租户进入租户上下文（见「租户回灌」），从信封 trace_parent 与头 tracestate 恢复 `consume <subject>` 的 Consumer Activity；
5. 载荷从信封 payload 按登记的 JsonTypeInfo 反序列化，从不把整封信封当载荷；
6. 调用 handler，成功后写幂等记录（保留 IdempotencyTtl，默认 7 天），然后才确认。

消费者名：竞争订阅为 `<service>.<subscription>`，广播订阅为 `<service>.<subscription>@<instance>`（每个实例各记各的）。幂等记录写入失败按瞬时失败处理，handler 会在重试时再次执行；写类 handler 必须业务幂等（ADR-008 D8.4）。

### 失败分类

| 分类 | 情形 | 处置 | 计数 |
|---|---|---|---|
| 契约不满足（handler 之前） | 信封解析或校验失败（含缺少 tenant_id）、与 AMQP 属性不一致、本队列没有订阅这个事件 ID、平台级事件而订阅不接受、租户进入被拒（传播器判定或非法租户 id）、载荷反序列化失败 | 不重投、不记幂等，原样转入订阅的死信队列 | rejected |
| handler 确定性失败 | `InvalidOperationException`（含 `QianEventsException` 与 `RecordChangedEvent.FromEnvelope` 的派生失败）、`ArgumentException`、`JsonException`、`NotSupportedException`、错误码不可重试的 `QianErrorException` | 不重投、不记幂等，转入死信队列 | dead_lettered |
| handler 瞬时失败 | 其余异常 | 退避后重投；用尽后转入死信队列 | retried，用尽时 dead_lettered |
| 宿主停机 | 停机令牌触发后 handler 抛出的任何异常 | 放回队列，不计一次尝试 | 无 |

handler 想明确表示「重试也不会成功」时，抛携带 EVENTS_CONSUME_REJECTED 的 `QianEventsException`。广播队列里连事件 ID 都无法归属订阅的消息（信封无法解析、没有订阅这个事件）没有死信队列可去，记 rejected 与 Error 日志后丢弃。

### 重试与死信

- 首投 1 次 + 重试 MaxRetries 次（默认 5），第 6 次仍失败进死信队列。第 n 次失败后退避 `RetryBaseDelay × 2^(n-1)`，封顶 60 秒，与 Outbox 投递侧对称。
- 退避状态在 broker 上：失败的消息带着计数头 `x-qian-attempts` 复制进重试队列 `<主队列>-retry-<n>-<毫秒>`，副本被 broker 确认（mandatory）后才确认原消息；重试队列没有消费者，到期后经默认 exchange 回到原主队列，不经 `qian.events`，不会扇出到其他订阅。计数头只认经默认 exchange 回流的副本，直接发到 `qian.events` 的同名头一律忽略。
- 竞争订阅的重试队列是 quorum + at-least-once，跨副本有效；广播实例的重试队列是带闲置过期的经典队列，实例下线、主队列删除后回流的消息无处可投即被丢弃。
- 死信队列每订阅一个：`q.<service>.<subscription>-dlq`（quorum，跨实例共享），在 `qian.dlx` 上按自己的队列名精确绑定，死信也以该队列名为路由键发出。不按 subject 绑定：`qian.dlx` 是 topic exchange，按 subject 绑定时订阅同一事件的各个订阅会收到彼此的死信。
- 死信消息体与原消息逐字节相同，保留 MessageId、Type、AppId 与发布端写出的头（traceparent、tracestate、tenant_id），去掉 broker 生成的 x-death 等记录，另加 `x-qian-attempts`、`x-qian-reason`、`x-qian-consumer`、`x-qian-subscription`、`x-qian-instance`、`x-qian-subject`，以及有异常时的 `x-qian-error`（异常类型全名，不含异常消息）。
- `x-qian-reason` 取值：exhausted、handler_rejected、invalid_envelope、property_mismatch、tenant_mismatch、routing_mismatch、unsubscribed_type、platform_event、tenant_rejected、invalid_payload。
- 告警：每进一条死信，dead_lettered 计数加一并记一条 Error 日志（event_id、type、消费者、第几次投递与原因，不含载荷）；Alertmanager 规则归 S1-DEP-006。
- broker 侧还有一道投递上限：竞争队列声明 `x-delivery-limit=6`。RabbitMQ 4 的 quorum 队列只对「消费者消失」（连接或通道断开时消息未确认）计次，basic.nack 放回不计，所以本订阅器的重试与停机放回都不会触到它；只有崩溃循环（处理中途进程退出）会在第 7 次投递后由 broker 按死信队列名转入死信队列。这类死信不经过上面的计数与日志，由死信队列深度告警兜底。
- 竞争队列用 at-least-once 死信策略：死信目标不可达时消息滞留在源队列（不计入 messages，计入 messages_dlx）而不是丢失，所以死信队列先于主队列声明。

### 两种订阅

- 竞争消费：每订阅一个 quorum 队列 `q.<service>.<subscription>`，同一服务的全部实例共享，每条消息只由一个实例处理。
- 广播：本实例的全部广播订阅共用一个经典自动删除队列 `q.<service>.<instance>`，按事件 ID 分派到所属订阅；每个在线实例各处理一次，实例的最后一个消费者断开即删除。
- 实例名默认取机器名加进程号并规范化：转小写，非 `[a-z0-9-]` 换成短横线，首字符不是字母时补 `i-` 前缀。

### 租户回灌

租户只取已校验信封的 tenant_id（contracts/v0 §5 第 3 条），不读载荷（S1-DEC-020）；每条消息在全新作用域里进入，消费循环运行在空执行上下文里，宿主启动时的租户或事件作用域不会带进消息。

- 宿主登记了 `ITenantContextPropagator` 时经它进入：carrier 是消息体（信封原文），authorizedTenantId 是解析、校验后且与 AMQP 头 tenant_id 核对一致的信封租户。传播器在这里提供缺失、重复、不一致与租户冲突的 fail-closed 判定；消费端没有独立于消息的令牌，AMQP 头由发布端从同一信封写出，只能近似满足传播器「authorizedTenantId 不能取自同一输入」的契约（S1-EVT-002 日志的建议裁决）。
- 没有登记传播器时退回 Kun `ITenantContext.BeginScope`，先按同样规则核对原文里恰有一个与授权租户一致的 tenant_id，并在启动时记一条日志。
- 平台级事件（tenant_id 为 null）默认拒绝；订阅显式 `acceptPlatformEvents: true` 时派发，不开租户作用域。宿主的租户上下文不是多租户时不回灌。

### handler 拿到什么

- Kun `MessageContext` 由信封填充：MessageId 为 event_id，EventType 为 type，TraceParent 为 trace_parent，TenantId 为 tenant_id，OccurredOn 为 occurred_at。
- 需要 producer、actor、correlation_id、causation_id 等完整信封时，在 handler 构造函数里注入 `QianEventDelivery`（订阅器在每条消息的作用域里提供，另带订阅名、消费者名与第几次投递）。
- handler 里不要直接调用 `IIntegrationEventBus.Publish` 或 `IOutboxWriter.Enqueue`：QIAN0006 只放行命令 handler 与领域事件通知 handler，要发后续事件经 `ISender` 发命令。

### 指标与选项

meter `Qian.Events` 的消费侧计数，每条消息恰落入其中一个：`qian.events.consumer.processed`、`duplicate`、`retried`、`dead_lettered`、`rejected`。标签为 `qian.events.consumer`（消费者名）、`qian.events.type`（已归属订阅时）与 `qian.events.reason`（rejected、dead_lettered）。

| 选项 | 默认 | 说明 |
|---|---|---|
| Instance | 规范化后的机器名-进程号 | 广播队列名与广播幂等记录的实例段 |
| PrefetchCount | 16 | 每个消费通道未确认消息的上限 |
| MaxRetries | 5 | 首投之外的重试次数（0–20）；与 Outbox 侧「总共尝试次数」的 MaxAttempts 不同义 |
| RetryBaseDelay | 1 秒 | 退避起点，按 2 的幂增长、封顶一分钟 |
| IdempotencyTtl | 7 天 | 幂等记录保留时长 |

订阅目录经 `IEventsService.Subscriptions` 枚举，每个（订阅, 事件 ID）一项，字段与事件目录按事件 ID 对齐。

## RabbitMQ 拓扑

- exchange：topic 类型的 `qian.events`，路由键是 subject，消息体就是 Outbox 行里的信封原文。
- 消息属性：MessageId 为 event_id，Type 为 type，AppId 为 producer，持久化投递；头部带 traceparent、tracestate 与 tenant_id。
- 没有订阅者的事件由 broker 丢弃（mandatory=false）。
- 竞争消费：每个订阅一个 quorum 队列 `q.<service>.<subscription>`，broker 侧死信以 `q.<service>.<subscription>-dlq` 为路由键转 `qian.dlx`，投递上限 6。
- 广播：每个实例一个经典自动删除队列 `q.<service>.<instance>`。RabbitMQ 4 默认禁止非持久的非独占队列，所以广播队列声明为持久，并设 30 分钟闲置过期兜底。
- 死信队列 `q.<service>.<subscription>-dlq` 与重试队列 `<主队列>-retry-<n>-<毫秒>` 由消费端声明；这两种后缀保留，订阅名与实例名不能以它们结尾。

发布端没有复用 Kun.EventBus.RabbitMQ 的发布器，原因有三：它以带版本的 type 作路由键，与 PRD 约定的 subject 路由不符；每条消息新建一个通道；并以 mandatory 发布，在确认跟踪下没有订阅者的事件会确认失败，行会反复重试直到标记失败。这里复用一条确认通道，失败时重建。

## 组件入口

`EventsPlugin`（组件 `qian.events`）可以挂在 global 或 tenant scope。挂载时核对组件配置与宿主装配一致：

- `broker` 只能是宿主登记过的 secretRef；
- `outbox.lock`（skip-locked 或 redis）必须与宿主选项相同。

核对通过后登记错误码并提供 Events 服务键，卸载时撤销。

## 已知限制与延期项

- 事件目录 HTTP 接口（含「订阅者」一列，数据来自 `IEventsService.Subscriptions`）、AsyncAPI 导出、主题 ACL（订阅侧的 `provides.events.subscribe` 钩子）与面向 `NpgsqlTransaction` 的写入端由 [S1-EVT-003](../../pm/tasks/S1-EVT-003.md) 交付。
- 拆分部署下身份侧订阅 `authz.grant.changed.v1` → `BumpIdentityRevisionAsync` 由 [S1-IDP-012](../../pm/tasks/S1-IDP-012.md)（随 S1-DEC-027 建档）用本订阅器接线；同进程宿主不登记该订阅。
- 死信队列的重放工具（ADR-008 D8.9）没有做；死信消息保留原文与原属性，重放时去掉 `x-qian-*` 头再发回 `qian.events`。
- 消费端 baggage（`tenant.id` 等）由 S1-AUD-003 在订阅器的 Activity 上补齐，本包只恢复 trace_parent；死信告警规则归 S1-DEP-006。
- S1-DEC-013 列为可选项的 PG `processed_messages` 兜底表没有做，幂等记录只在 Redis。
- broker 侧投递上限触发的死信（崩溃循环）不经过消费端的计数与日志。
- `plugins/qian.events` 已通过 SDK 发布与私有源 `qian add events` 两门，S1-RT-033 按 R100 移除骨架标记，并补齐配置 Schema 的代码派生。

`qian.events` 的 `runtime.configSchema` 和 Schema 文件由入口的 `QianConfigSchema` 引用 `ComponentConfiguration.Schema` 派生；修改配置时先更新实现和代码声明，再同步唯一清单。该组件已通过既有真实安装门，清单不再带骨架标记。
