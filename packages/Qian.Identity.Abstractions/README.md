# Qian.Identity.Abstractions

身份 seam 的 Definition，依据组件模型 §6.3、ADR-005 与 S1-IDP-001。文件夹只有词汇、端口、JSON 元数据与事件契约，不包含用户数据库、发行器、Cookie 会话或认证算法。

## 装配位置

所有 profile 都可引用 Definition。`IdentityDefinition.Key` 拥有 `Identity` 服务键，`ctx.Identity()` 按当前 scope 注入 `IIdentityService`；真正 Provider 分别由后续 `Qian.Identity.Server` 与 `.Client` 提供。连接器通过 `IIdentityConnectorRegistry.Register` 挂载，释放句柄交给 `ctx.Effect`，同名独占与 scope 遮蔽由 Provider 遵守。

`qian add identity` 安装的是组件与所选 Provider，本包不自行创建身份服务。扩展只需依赖 Definition 即可调用当前主体、Person/Membership 投影、会话与连接器目录。

## 框架依赖

直接依赖 `Qian.Runtime` 与现有 `Kun.EventBus` 事件抽象入口；当前 Kun 2026.10.8 尚无 `Kun.EventBus.Abstractions` 包，稳定 `[IntegrationEventId]` 与 `IEventTypeRegistrar` 位于这个现有包，因此复用原类型而不复制另一份同名标记。只调用注册端口，不装配 broker、Redis 或后台发布器；不依赖 Identity Server/Client、OpenIddict、EF 或 ASP.NET Core。包版本仍统一取根目录 KunVersion。

## claim 边界

- `IdentityClaimNames.IdentityToken` 表示用户令牌词汇：sub、tenant_id、membership_id、scope、identity_revision、sid、amr、acr，以及尚未启用的 profile/act。
- `S2sRequired` / `S2sOptional` 与冻结 `contracts/v0/s2s-claims.schema.json` 精确一致。S2S 使用 session_id，不加入 sid/amr/acr/profile；权限点集合不进入令牌，roles 仅是已裁决的粗粒度角色例外。
- `Principal` 是 S2S payload DTO，平台 tenant_id 显式序列化为 null；`IdentityJson.PrincipalTypeInfo` 提供源生成元数据。它不是令牌签发器或验证器：调用方仍需按 Schema 验证条件字段、时间窗、UUID、服务 session_id=jti，并执行 JWT 验签、aud/issuer 与当前有效性校验。
- PrincipalKind 与 ClientType 均预留 agent。S1 不因此允许发行 Agent 或委托令牌；S4 启用需策略实现。

## SPI 与事件

`IIdentityConnector` 使用规格中的 BeginSignInAsync/CompleteSignInAsync，分别对应 PRD 的挑战／回调语义；可选免登与目录同步拆为扩展接口。连接器只输出 ExternalAssertion；state/nonce 关联、绑定、JIT 建号、合并与令牌签发由身份 Provider 统一处理。

`IAuthenticatorFactor` 提供挑战与验证端口。AuthenticationProof 复制临时材料、禁止默认打印／JSON 序列化并在 Dispose 时清零；失败携带 QianError，由宿主映射为 ErrorDetail，不自建错误体。

进程内事件为 signing-in（waterfall）、signed-in / signed-out（emit）、member-disabled（parallel）。跨进程五类事件有稳定 `[IntegrationEventId]`、源生成 JSON 与各自 payload Schema；通过 `RegisterIdentityEvents()` 显式登记，外层事件信封仍遵守 contracts/v0。事件和 Session 的 ToString 不打印 sid，载荷不含密码、认证答案或 access/refresh token。

## 模型体验

模型可读取 claim 名、SPI、客户端类型与事件 Schema；不会由 Definition 获得签发、重置密码或登录能力。Profile 里的 scope 与宿主权限仍是调用前提。

## 已知限制与延期项

本任务仅契约先行，不代表 OpenIddict 服务、SSO、连接器、三因子认证或会话失效链路已实现／上线。对应后续 IDP 任务负责真正的认证、权限与生命周期实现。
