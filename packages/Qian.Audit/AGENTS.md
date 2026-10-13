# Audit Provider

- 权威设计为 ADR-009、PRD §7.11.1 与 S1-AUD-001；主文档为 README.md。
- 记录和链头必须原子提交；独立追加事务不能用来关闭 CRM 业务回滚验收。
- 不丢弃 capability_id、contract_version、input_hash、channel、on_behalf_of、payload；全部纳入哈希，持久化后仍须可重放。
- 使用 Data 的受限角色、安全上下文及租户隔离；缺少租户不能隐式开启平台旁路。
- 运行时监听器须通过真实能力 Definition 与 ctx.Effect 注册撤销，不能用仅测试定义或手写业务回调冒充最终装配。
- 不提供删除审计的业务 API，不引用已弃用的事后审计拦截器。
