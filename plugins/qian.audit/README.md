# qian.audit

此目录仍为组件安装骨架（x-skeleton: true），提供清单与配置校验。实际 Provider 与读写服务已经在 [Qian.Audit](../../packages/Qian.Audit/README.md) 中实现，但真实能力事务、waterfall、SDK 发布及 qian add 安装仍未完成，不能把骨架视为完整就绪的组件。

默认配置沿用 chain.store: pg。可选的 chain.platformRole 只允许 global Provider 使用，角色须由宿主单独绑定为受限平台登录凭据；配置里不存口令。完整权限、迁移和验收边界以 Provider 主文档为准。
