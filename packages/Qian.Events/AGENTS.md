# Qian.Events

- 权威设计为 PRD §7.10、ADR-008、S1-EVT-001 与 S1-EVT-002；主文档为 README.md。
- 宿主装配调用 AddQianEvents / AddQianEventsBroker / AddQianEventsSubscriber；业务组件只依赖 Qian.Events.Abstractions。
- IOutboxWriter 与 IIntegrationEventBus 必须解析到同一个 scoped 信封写入端；Outbox 行的 payload 就是完整 v1 信封，不另存半成品。
- 投递以「确认后立即逐条标记」为前提：不要改回整批事务提交，也不要在租约到期后继续发布已领取的行，否则停机或 broker 变慢时会重复发布。
- 真实数据库与 RabbitMQ 测试复用 Qian.Testing.Containers，按非超级用户角色验证；拓扑声明以 RabbitMQ 4 的默认限制为准。
- 消费端以「handler 成功并写入幂等记录后才确认」为前提：重试一律复制进重试队列并在副本确认后确认原消息，不靠 nack 放回；重试队列经默认 exchange 回原队列，不经 qian.events 回投；失败分类与计数口径见 README「失败分类」，改动要同步测试。
- 重试或死信副本转发失败时原地退避重发、不放回原消息（quorum 队列的 nack 放回不计入投递上限，放回会让 handler 无上限地重跑），连续失败后 nack 不放回交 broker 死信；改动要同步 SubscriberTests 的转发失败用例。
- 租户只取已校验信封的 tenant_id，不读载荷；每条消息在全新作用域里进入，进入后核对当前租户等于信封租户（R122），消费循环不继承宿主启动时的执行上下文。
- 目录接口与 ACL 属于 S1-EVT-003，死信重放工具未做；未交付项不得写成已实现。
