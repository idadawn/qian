> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# ADR-008 事件与消息

> 本页由构建脚本镜像自仓库 [`docs/adr/ADR-008-events-messaging.md`](https://github.com/idadawn/qian/blob/main/docs/adr/ADR-008-events-messaging.md)，请修改源文件，不要改本页。

## 1 背景与问题（Context）

Kun 的 `Kun.EventBus` + `.RabbitMQ` 提供 Outbox 表 / 发布器 / 锁（默认 Null 锁，可 Redis）、稳定事件 ID 注册表 + JSON Schema 导出工具、幂等装饰器、RabbitMQ topic 收发 + DLX，属于「可用但薄」（基线 K §1 EventBus 行）。已确认缺口：① **Outbox 丢消息**——`KunMediator.Publish` 新开 scope，通知 handler 里发出的集成事件写进另一个 `IntegrationEventBuffer`，随 scope 释放丢弃，框架默认的领域事件派发器同样走此路径，Saga 步骤内发事件同样不落库（推断，代码路径明确，无测试覆盖）；② 消费端「先标记后处理」实际是 **at-most-once**，对应集成测试被 Skip；③ **多副本不安全**——Outbox 默认 Null 锁、Saga 无锁、多副本并发 `MigrateAsync` 无协调；④ 消费端与 Saga 执行器**不回灌租户**；`IOutboxMessageStore` 无框架实现、`RetryBaseDelay` 未实现、无 publisher confirm（基线 K §2.4、§2.1 差距②）。

现网 K3s 上**没有 RabbitMQ**，Outbox 处于空转（基线 K §6.1）。iClaw 已冻结事件 envelope 契约 `version / event_id / subject / occurred_at / tenant_id / payload`（基线 P §8.6）。参考平台 RabbitMQ 模式单队列、非持久化、先 ack 后处理、多实例竞争消费不能广播（基线 D §6 第 2 条）。事件目录（`IEventTypeRegistry.GetAll()` + SchemaGen）是 Kun 全仓唯一机器可读契约产物（基线 K §1）。

## 2 决策（Decision）

- **D8.1 RabbitMQ 作为 L1 共享组件部署到 K3s**（持久化队列、DLX、HA 拓扑随三节点现实配置）。
- **D8.2 修复 Outbox 丢消息**：领域事件派发改为提交前同 scope（或在 `EfUnitOfWork` 内直接 drain 子 scope 的 buffer）；在修复合入前，Qian 编码规范强制「集成事件只能在命令 handler 的同一 scope 发出」；为该路径补 Testcontainers 集成测试（建聚合后断言 `outbox_messages` 行数）。
- **D8.3 消费端改至少一次**：处理成功后标记（或标记 + 失败回滚）；失败重投不再被当作重复 Ack。
- **D8.4 幂等**：消费端 Redis 幂等表（按 `event_id`）默认装配；写类处理器必须幂等。（2026-10-10 S1-DEC-013：上游 Kun 的 `IProcessedMessageStore` 只有 Redis 实现，PRD §07 7.10 原写的 PG `processed_messages` 兜底表不作默认，留作 S1-EVT-001 的可选项；PRD 已同步。）
- **D8.5 多副本安全**：Outbox 轮询用 `FOR UPDATE SKIP LOCKED` 或 Redis 锁且默认开启；publisher confirm；`RetryBaseDelay` 指数退避落实；`IOutboxMessageStore` 的 EF 默认实现。
- **D8.6 租户回灌**：`MessageContext.TenantId`；订阅器建 scope 后调用 `ITenantContext.BeginScope`；Saga 执行器按 `SagaInstance.TenantId` 开作用域；Outbox 发布器带 `tenant_id`。
- **D8.7 事件信封对齐 iClaw**：必填 `version`（常量 v1）、`event_id`、`subject`（`<domain>.<entity>.<verb>`）、`occurred_at`、`tenant_id`、`payload`；可选 `correlation_id`、`causation_id`、`producer`；事件 ID 带版本后缀（如 `tenant.created.v1`）。（2026-10-14 S1-DEC-020：信封字段只在信封上。`event_id`、`occurred_at`、`tenant_id` 由 Qian.Events 写入端入队时生成，`[IntegrationEventId]` 标在载荷类型上，载荷不再声明独立的事件标识或发生时间；载荷因业务需要重复租户或主体时（如 `qian.record.changed.v1` 的 tenantId、actor），消费端须与信封核对一致。身份事件 v1 尚无生产方与已发布包，按此就地删去载荷里的这三项。）
- **D8.8 事件目录升格为能力目录的一部分**：稳定 ID + JSON Schema 导出 + AsyncAPI 描述；主题 ACL 由插件清单 `provides.events.publish/subscribe` 生成；事件契约化为 S4 Agent 触发器预留。
- **D8.9 两种订阅模式**：广播（每消费者组一份）与竞争消费；死信队列与重放工具。
- **D8.10 S2 动态数据事件**用一个静态注册的通用信封事件 `qian.record.changed.v1`（EntityCode / RecordId / Op / TenantId / Payload），必须在命令 handler 同 scope 发出；需要面向 `NpgsqlTransaction` 的 `IOutboxWriter` 实现使动态记录与 `outbox_messages` 同事务。
- **D8.11 Saga**：租户上下文回灌（D8.6）为 S1 P0；修锁、Scoped 工厂、上下文回写、步骤重试为 S1 P1 可选，修补后只用于平台内部长事务（安装 / 卸载、发布）；S1 的租户开通编排由 `qian.kernel` 生命周期控制器（v2 后 `qian.kernel` 拆为 `Qian.Runtime` + 平台插件，生命周期控制器即 `platform` profile base 组合包里的 `qian.lifecycle`，见 ADR-020）以幂等步骤表承担，不用 Saga（ADR-003、PRD §07 7.3）；不作业务流程引擎。
- **D8.12 进程内状态外置**：实时推送、延时队列、重试一律走 Redis / 消息总线背板，不依赖单实例内存。

## 3 理由（Rationale）

- 这些缺口是「K3s 多副本 + 多租户」场景下必然触发的问题，不先修则多租户在后台链路泄漏（基线 K §7.1 第 2 条）。
- 对齐 iClaw 信封让两个产品共用 Kun 事件基座，S4 Agent 触发器可直接消费（基线 P §8.6）。
- 至少一次 + 幂等是分布式消息的标准组合；MCP 2026-07-28 删除了 SSE 断点续传，写类工具必须幂等的要求与此同构（基线 P §8.2 调用）。

## 4 备选方案与放弃原因（Alternatives）

- **Kafka**：团队既有 ADR 选 RabbitMQ 不选 Kafka；三节点集群运维面更重。放弃。
- **Dapr pub/sub**：见 ADR-002，与 Kun EventBus/Outbox 大面积重叠。放弃。
- **进程内事件总线**（参考平台默认）：多副本下不成立。放弃。
- **保留 at-most-once 并由业务补偿**：把框架缺口转嫁给每个插件作者。放弃。

## 5 后果与代价（Consequences）

- 正面：插件间集成有可靠的主路径；事件目录成为 Agent 素材；Saga 与 Outbox 可多副本运行。
- 负面：至少一次要求所有消费者幂等，增加插件作者负担（由 SDK 默认装配幂等装饰器缓解）；RabbitMQ 成为新的 L1 运维对象；丢消息修复若走上游 PR 受 Kun 响应节奏制约。
- 配套：事件 Schema 的 CI 兼容性检查（AsyncAPI diff）；死信监控与告警；消费端跨租户消息的 TCK 用例。

过渡态（2026-10-06）：K3s 上无 RabbitMQ，Outbox 空转；RabbitMQ K8s 化与 D8.2–D8.6 修复是 S1 第一批基础设施任务。

## 6 对各阶段的影响

- **S1**：RabbitMQ K8s 化；D8.2–D8.9 全部；事件目录 v1；Saga 租户回灌 P0、其余修补 P1 可选。
- **S2**：`qian.record.changed.v1`、`IOutboxWriter` 的 Npgsql 实现；流程实例 / 节点 / 工作项生命周期事件；服务端 before / after Hook 与数据变更事件经 Outbox 投递；实时推送与调度的背板。
- **S3**：事件主题 ACL 对第三方插件生效；授权变更事件推送。
- **S4**：事件作为 Agent 触发器；CloudEvents 兼容投影。

## 7 待确认事项

- 丢消息路径为推断，需先用最小集成测试实证，再决定走上游修复还是 Qian 侧编码规定（基线 K 附录 B-6）。
- 三节点集群上 RabbitMQ 的 HA 拓扑（镜像队列 / quorum queue）与存储类（现网唯一 `local-path`）。
- 消费端幂等键存储选 Redis 还是 PG（`IProcessedMessageStore` 两个同名接口需选定一套）。

## 8 参考

- 相关 ADR：ADR-001、ADR-003、ADR-009、ADR-011、ADR-012、ADR-016、ADR-017。
- PRD：`PRD §07`、`PRD §12`。
- 基线：K §1 EventBus / Saga 行、§2.4、§5.1 第 2/3 条、§6.1、附录 B-6；P §5 事件总线行、§8.6；D §6 第 1/2 条；W §6.2。
