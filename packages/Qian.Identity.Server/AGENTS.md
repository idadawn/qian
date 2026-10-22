# Identity Provider（协议面 + 账号模型）

- ADR-005、PRD 7.4.1 / 7.4.2、S1-IDP-002 / 003 / 004 / 007 是边界；MFA 因子仍由后续任务交付，在此之前不得降级或伪造。
- 连接器登录（`IdentityAccountService.Connectors.cs`）：state、nonce、PKCE verifier 只由本组件生成，`connector_flows` 只存哈希，verifier 由浏览器密钥派生、不落库；回调幂等靠条件更新抢占（pending → processing），改动回调或绑定的顺序必须配并发测试。上游 `error_description` 是不可信数据，不进文案。绑定必须「新鲜认证 + 显式确认」，冲突不透露对方账号；JIT 缺省关闭、读不到租户策略即视为关闭。会话只经统一流水线与一次性连接器票据签发，不在别处建会话。`connector_flows` 是全局表（回调时租户未知），不要给它加非空 `tenant_id`。
- OpenIddict 保持持久令牌记录、PKCE、非对称签名与 HTTPS 校验，不使用 degraded 模式；它是唯一的令牌发行者。
- 在线存储继承 QianDbContext，不用超级用户；Membership、ExternalIdentity 是租户表，只能在目标租户作用域内读写，不得为登录绕过 RLS（登录前靠 `persons.tenant_ids` 索引逐租户进入作用域）。
- 凭据只存 Argon2id；不记录、不返回密码、验证码、票据、原始标识与原始 sid（日志与审计只留类型 + 掩码 / sid 哈希）。对外投影只含掩码。
- 登录失败对外一律 `INVALID_CREDENTIALS`；账号状态只在凭据验证通过之后暴露。一次登录调用恰好一条 `LoginAudit`。
- 限流是「占位在先、验证在后」；锁定事件只在验证确认失败之后发。改动限流、票据或会话建立顺序必须配并发测试。
- 会话的事实来源是 PostgreSQL 的 `sessions` 表；Redis 索引与墓碑是派生数据，只用来加速，绝不据它放行，Redis 故障必须退回数据库。新增任何吊销来源都走 `RevokeSessionRowsAsync`（即时写 `revoked_at`，与引起它的变更同事务），协议族、后通道、索引与事件由 `IdentitySessionProcessor` 幂等收尾，不要在请求路径里同步做。
- 旁路写事件必须在会话所属租户的作用域内（`TenantScope` 加 `EventEnvelopeScope`），行为主体取吊销者；后台工作（旁路、对账）用 `_background` 互斥并在卸载时排空（`CloseAsync`），新增后台工作必须遵守，否则卸载会撞上被释放的数据会话。
- 日志点只用 `[LoggerMessage]`，参数不得是会话标识或 Guid（`SessionLogHygieneTests` 扫描把关）；管理面的判定是内置规则，集中在 `ResolveCallerAsync` 与端点的 `Scope`，不要在别处加分支，待 PEP 接入后统一换成权限点。
- 事件只经领域通知 → 集成事件总线 → 同事务 Outbox 发布（QIAN0006）；写事件的操作必须在 `EventEnvelopeScope` 内。
- 协议错误由 OpenIddict 按 OIDC/OAuth2 输出；账号接口与平台管理接口使用 ErrorDetail，错误码在 `IdentityErrors` 登记。
- 只依赖 Runtime*、`*.Abstractions` 与第三方协议库（不引用 Qian.Tenancy / Qian.Authz 等 Provider 包）；宿主组合 Data 与 Tenancy。公共 API 变更同步 `PublicAPI.Unshipped.txt`。
- 不复制其他产品的身份代码；绑定守卫、软删除绑定、掩码投影只借鉴设计思路，实现自写并配结构化错误测试。
