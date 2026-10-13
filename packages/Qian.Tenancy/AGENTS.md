# Tenancy Provider

- 权威设计为 ADR-004 和 PRD §7.3；当前工作是 TEN-001，不用主档创建冒充管理员开户或生命周期激活。
- 主文档为 README.md；每次更新能力边界和实际验证一起维护。
- 只使用 Kun.Core.MultiTenancy.ITenantContext；不能新增第二份 AsyncLocal 上下文、头兜底或 default 租户。
- 写入与迁移遵循 Data 的安全装配；在线代码继承 QianDbContext，受限角色，无跨插件表访问。
- 目录只有内部非敏感投影，不能把内部主档写入无授权地直接映射为 HTTP。
- 传播值不是授权证据；每一跳与可信认证/调度来源的租户匹配。完整适配器未验证前不关闭传播 IT。
