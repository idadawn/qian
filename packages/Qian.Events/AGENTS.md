# Qian.Events

- 权威设计为 PRD §7.10、ADR-008 与 S1-EVT-001；主文档为 README.md。
- 宿主装配调用 AddQianEvents / AddQianEventsBroker；业务组件只依赖 Qian.Events.Abstractions。
- IOutboxWriter 与 IIntegrationEventBus 必须解析到同一个 scoped 信封写入端；Outbox 行的 payload 就是完整 v1 信封，不另存半成品。
- 投递以「确认后立即逐条标记」为前提：不要改回整批事务提交，也不要在租约到期后继续发布已领取的行，否则停机或 broker 变慢时会重复发布。
- 真实数据库与 RabbitMQ 测试复用 Qian.Testing.Containers，按非超级用户角色验证；拓扑声明以 RabbitMQ 4 的默认限制为准。
- 消费端属于 S1-EVT-002，目录接口与 ACL 属于 S1-EVT-003；未交付项不得写成已实现。
