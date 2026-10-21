# Qian.Identity.Abstractions

身份 seam 的 Definition，依据组件模型 §6.3、ADR-005 与 S1-IDP-001。文件夹只有词汇、端口、JSON 元数据与事件契约，不包含用户数据库、发行器、Cookie 会话或认证算法。

## 装配位置

所有 profile 都可引用 Definition。`IdentityDefinition.Key` 拥有 `Identity` 服务键，`ctx.Identity()` 按当前 scope 注入 `IIdentityService`；协议 Provider 与内置的 Person / Membership 账号模型、统一登录流水线已由 [Qian.Identity.Server](../Qian.Identity.Server/README.md) 提供（S1-IDP-002 / 003 / 004，含会话中心）；依赖方（RP）Provider 由 [Qian.Identity.Client](../Qian.Identity.Client/README.md) 提供（S1-IDP-005：服务端换码与 `qian.sid` 会话、刷新、下游 S2S 校验），它只回答当前主体，管理操作返回 `IDENTITY_OPERATION_UNSUPPORTED`。连接器组件在自己的 Apply 里 `ctx.IdentityConnectors().Register(name, connector)` 挂载（见下文「连接器 SPI 与清单」），注册句柄进入该组件自己的效果栈，同名独占、scope 遮蔽与 restriction 过滤由运行时统一执行。

`qian add identity` 安装的是组件与所选 Provider，本包不自行创建身份服务。扩展只需依赖 Definition 即可调用当前主体、Person/Membership 投影、会话与连接器目录。

## 框架依赖

直接依赖 `Qian.Runtime` 与 `Kun.EventBus.Abstractions`，复用原命名空间下的稳定 `[IntegrationEventId]`、事件注册端口与源生成 `JsonTypeInfo` 重载。该框架包只依赖零外部依赖的 Kun.Core，身份契约不再经事件入口传递引入 Redis、broker 或后台发布器。全部 Kun 包版本仍统一取根目录 KunVersion。

## 账号与登录端口

内置 Provider 额外提供三个端口，服务键在 `IdentityDefinition` 上，`ctx.IdentityAccounts()` / `ctx.IdentitySignIn()` / `ctx.IdentityFactors()` 按当前 scope 注入：

- `IIdentityAccounts`：创建 Person（多标识，初始密码，可选平台资格）、按标识查找、在当前租户建立成员（经租户配额判定，幂等）、绑定 / 解绑 / 换绑标识、自助改密、运营重置密码、状态变更、外部身份绑定，以及 `BumpIdentityRevisionAsync(personId, reason)`——授权变更后递增该 Person 在当前租户的身份修订（返回新的 `identity_revision`），旧令牌随即被服务端拒绝。`sub`（Person.Id）创建后永不改写；解绑最后一个已验证标识返回结构化的 `LAST_VERIFIED_IDENTIFIER`。返回值只含掩码后的标识。
- `ISignInPipeline`：`SignInAsync(LoginInput, LoginContext)`、`SelectAsync`（用 5 分钟选择令牌换具体 Membership 或平台身份，切换租户同样走这里）、`ChangeInitialPasswordAsync`、`BeginSwitchAsync`。结果 `SignInResult` 的 `Status` 为 `Authenticated`（带 `AccountSession`）/ `SelectionRequired`（带票据与可选身份）/ `PasswordChangeRequired` / `Failed`（带 `QianError`，锁定时带 `RetryAfter`）。所有登录方式构造同一个 `LoginInput`，临时证明（`AuthenticationProof`）使用后由调用方释放。
- `IAuthenticatorFactorRegistry`：认证因子注册表，同名独占、可撤销；内置的 `password` 由身份 Provider 占用，短信 OTP、TOTP 由各自组件注册。

`IdentityErrors` 是身份领域稳定错误码的唯一定义处，由 Provider 在挂载时登记到 `ErrorCatalog`。

## claim 边界

- `IdentityClaimNames.IdentityToken` 表示用户令牌词汇：sub、tenant_id、membership_id、scope、identity_revision、sid、amr、acr，以及尚未启用的 profile/act。
- `S2sRequired` / `S2sOptional` 与冻结 `contracts/v0/s2s-claims.schema.json` 精确一致。S2S 使用 session_id，不加入 sid/amr/acr/profile；权限点集合不进入令牌，roles 仅是已裁决的粗粒度角色例外。
- `Principal` 是 S2S payload DTO，平台 tenant_id 显式序列化为 null；`IdentityJson.PrincipalTypeInfo` 提供源生成元数据。它不是令牌签发器或验证器：调用方仍需按 Schema 验证条件字段、时间窗、UUID、服务 session_id=jti，并执行 JWT 验签、aud/issuer 与当前有效性校验。
- PrincipalKind 与 ClientType 均预留 agent。S1 不因此允许发行 Agent 或委托令牌；S4 启用需策略实现。

## SPI 与事件

`IIdentityConnector` 使用规格中的 BeginSignInAsync/CompleteSignInAsync，分别对应 PRD 的挑战／回调语义；可选免登与目录同步拆为扩展接口。连接器只输出 ExternalAssertion；state/nonce 关联、绑定、JIT 建号、合并与令牌签发由身份 Provider 统一处理。

## 连接器 SPI 与清单

- **注册表 seam（R38）**：`IdentityDefinition.ConnectorsKey` 即 `Identity.Connectors`；`ctx.IdentityConnectors()` 返回绑定当前上下文的注册表，`Register(name, connector)` 等价于提供具名服务键 `Identity.Connectors:<name>`，组件清单的 `provides` 写同一个键。注册名必须等于清单 `Id`。挂在租户 scope 的同名注册遮蔽全局项；`Find` 与 `List` 只看得到当前 scope 可见的，被 restriction 过滤的与不存在的同样查不到，非法名字直接返回 null。`IIdentityService.Connectors` 按调用时的租户上下文解析。
- **清单（connector.manifest）** 即 `ConnectorDescriptor`：`Id`（小写字母开头，小写字母、数字或连字符，最长 64，也是回调路径段）、`DisplayName`（1–64 字，无控制字符）、`Protocol`（小写短名，如 `oidc`）、`Terminals`（非空、不重复，取自 `ConnectorTerminals`：`pc` / `mobile` / `workbench`）、`ConfigSchema` 与 `CallbackTemplate`（固定为 `/connect/callback/{connectorId}`，回调由身份 Provider 统一接收）。`ConnectorManifest.Check` 逐项返回带 pointer 的字段错误，`Validate` 抛 INPUT_INVALID；注册时校验，TCK 用同一个校验器。
- **配置 Schema 规则**：根与每个嵌套 object 都是 `"type": "object"` 且 `additionalProperties: false`；名字像密钥的属性（secret、password、token、credential、private key、api key）必须带 `x-qian-sensitive: true`；敏感属性必须是 string，且 `pattern` 以 `^secretRef://` 开头，即只能填 secretRef。描述本身不含任何配置值。
- **上下文与失败**：`ConnectorContext` 带身份 Provider 生成的 state、nonce、PKCE `CodeVerifier` 与回调地址，连接器原样使用、不自己保存。协议失败抛 `QianErrorException`：nonce 不符用 `AUTH_CONNECTOR_STATE_INVALID`，上游响应无效或签名不符用 `BAD_GATEWAY`，上游超时用 `GATEWAY_TIMEOUT`；上游的 `error` 回调由 Provider 先行处理，不会传给连接器。`ExternalAssertion.Profile` 只放展示用资料，`ToString` 不打印内容。
- **错误码**：`IdentityErrors` 增加连接器登录的四个业务码 `AUTH_CONNECTOR_STATE_INVALID`（422）、`AUTH_CONNECTOR_DENIED`（422）、`AUTH_BINDING_CONFLICT`（409，不透露对方账号）、`AUTH_BINDING_EXPIRED`（422）；归属 `qian.identity`，随身份 Provider 挂载登记，不进平台内置目录。

通用 OIDC 连接器见 [Qian.Identity.Connector.Oidc](../Qian.Identity.Connector.Oidc/README.md)。

`IAuthenticatorFactor` 提供挑战与验证端口。AuthenticationProof 复制临时材料、禁止默认打印／JSON 序列化并在 Dispose 时清零；失败携带 QianError，由宿主映射为 ErrorDetail，不自建错误体。

进程内事件为 signing-in（waterfall）、signed-in / signed-out（emit）、member-disabled（parallel）。跨进程五类事件有稳定 `[IntegrationEventId]`、源生成 JSON 与各自 payload Schema；通过 `RegisterIdentityEvents()` 显式登记，外层事件信封仍遵守 contracts/v0。载荷只有身份投影：event_id、occurred_at 由 Qian.Events 写入端入队时生成，tenant_id 取发布时的租户上下文，三者只在信封上，载荷类型也不实现 Kun 的 `IIntegrationEvent`（[S1-DEC-020](../../pm/tasks/S1-DEC-020.md)）。事件和 Session 的 ToString 不打印 sid，载荷不含密码、认证答案或 access/refresh token。

## 模型体验

模型可读取 claim 名、SPI、客户端类型与事件 Schema；不会由 Definition 获得签发、重置密码或登录能力。Profile 里的 scope 与宿主权限仍是调用前提。

## 已知限制与延期项

本包仅契约；协议服务与账号模型的实现与验收见 Qian.Identity.Server 主文档，依赖方见 Qian.Identity.Client 主文档。它不代表外壳 SSO 或第二因子（TOTP / 短信）已实现／上线；外壳接线随 D5 / YUE-002，第二因子按 IDP-006 继续交付。连接器登录（统一回调、确认绑定、JIT）由 Qian.Identity.Server 交付，通用 OIDC 连接器在 Qian.Identity.Connector.Oidc，企业微信连接器属于 IDP-008。会话中心（Redis 索引、强制下线、身份修订递增）由 Qian.Identity.Server 交付。


## 授权主体类型透传

当前用户通过 Kun.Core.Security.ICurrentUser 被 Mediator 等入口消费时，PrincipalKind 从已验证的 Principal.Kind 派生。user/service/agent 保持原类型，不让服务身份落入旧适配器的 user 默认值；缺少身份快照返回空值并由鉴权入口拒绝。该契约不改变冻结 claim，也不因此开放 Agent 签发。
