# 身份 Definition 约定

- 此包仅契约，不依赖身份 Provider、OpenIddict、EF 或宿主框架。
- claim 的 S2S 真相源是 contracts/v0；用户令牌词汇单列，不能把 sid/amr/acr/profile 塞进冻结 S2S。
- 事件 ID 与 Schema 同步，首个发布版本之后破坏性载荷变化必须升版本；不复制 Kun 的 IntegrationEventId 标记。
- 载荷不声明 event_id、occurred_at、tenant_id 等信封字段，也不实现 Kun 的 IIntegrationEvent；这些值只由 Qian.Events 写入端生成在信封上（S1-DEC-020）。
- 不记录密码、OTP、原始 sid；临时认证材料需要释放。
- 变更公共 API 必须登记；测试覆盖真实已发布框架端口与 Schema 一致性。
