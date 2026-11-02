# 租户自助开通 Provider

- 主文档为 README.md，规格为 docs/prd/specs/tenant-signup.md；接口路径、字段名、错误码以规格为准，偏离要在 README 写明。
- 只依赖 Runtime、SDK 与 Definition（Tenancy / Identity / Authz / Data / Events / Capabilities 的 Abstractions）；不引用 Tenancy、Identity 或 Authz 的 Provider，不读写它们的表。
- 账号只经 `IIdentityAccounts`，开通只经 `ITenantProvisioning` 与 `ITenantLifecycle`；不改 qian.tenancy 的契约与行为。
- 三张表都是全局表，没有租户列；一律拒绝租户上下文。手机号原值只为通知保存，接口一律掩码；验证码、会话令牌只存哈希；密码不经本组件落库或进日志。
- 运营端点按 R113 同序，超管旁路缺席即 404；申请人端点写请求先核对同源，再校验输入。
- 迁移由 tests/Qian.Tenancy.Signup.Tests 的设计时入口生成（internal），不手改。
