# §07 S1 技术底座功能规格

> 状态：草案 v0（2026-10-06）。本章是 S1「技术底座期」的功能规格，写到可直接拆任务开工的粒度。决策只引用 `ADR-0xx`（见 `docs/decisions/key-decisions-v0.md`，拍板后逐条转为 ADR），不在本章重复论证；组件清单字段的完整定义见专题 spec `specs/component-model.md`（权威；`specs/plugin-manifest.md` 为 v1 字段迁移对照）。组件模型以 ADR-020（组件模型 v2）与 ADR-019（库优先）为准：可部署单元 = profile；规范术语「平台插件」指 `policy.tier: system`、属于 `platform` profile base 组合包的组件（原称「系统插件」，本章此后不再使用旧称；R30）。
> 口径：规范描述目标态；凡涉及 Kun 当前缺口的地方，用「过渡态（2026-10）」块说明现状与过渡路径。被借鉴的商业低代码产品一律称「参考平台」。

### 7.0 本章阅读说明

#### 7.0.1 S1 目标与退出标准

S1 的唯一目标是把 Qian 做成一个**开发人员可以在其上用代码写出多租户应用的平台底座**。按 ADR-018，S1 的退出标准有两条，缺一不可：

1. 开发人员用代码在 Qian 上写出一个多租户应用，其中登录/SSO、鉴权、数据存储、文件上传、配置、网关、事件、审计**全部由平台提供**，应用代码里不出现这些横切关注点的自实现。
2. 一个官方平台插件（`runtime.form: module`）能**独立出一个修订、逐租户迁移、回切**——即组件机制不是纸面设计，而是被 `platform` profile 的平台插件日常使用。
3. （ADR-020 §6 新增）在一个空白 ASP.NET Core 项目里执行 `qian add identity authz files`，30 分钟内跑通登录、权限检查与上传，不部署 Qian 平台——即通用组件「别人安装一个包就能直接用」。

与之配套的硬性要求（多租户、SSO、鉴权、微服务、K3s、Apollo）在本章全部落为功能项。S1 不做低代码构建（§08 的 S2 范围）、不面向外部客户、不做 Agent 运行时；但 S1 起所有能力都必须带机器可读契约与 Agent 预留字段（见 7.18）。

S1 的验收载体是一个由 `qian init --template component-dotnet` 生成、再由开发人员补齐业务代码的**参考应用**（暂名 `qian.sample.crm`，一个带表单、列表、文件附件和审批占位的客户管理模块）。本章所有「验收标准」里写的「参考应用」均指它；它在两个租户上独立开通、独立配置、独立升级与回切，是 S1 的集成验收场景。

#### 7.0.2 功能项写法

每个功能项固定六个小节：

- **设计理念**：为什么这样做，一到三句，含对参考平台教训的回应。
- **范围与边界**：✅ 做 / ❌ 不做，每条「不做」附一句理由或所属阶段。
- **技术落点**：依赖的 Kun 模块、组件 ID、seam 三角色包名（Definition `Qian.<X>.Abstractions` 与服务键 `ctx.<X>` / Provider / Consumer）、默认 profile 与是否可独立安装（`qian add <x>`）、`runtime.form` 与 `policy.tier`、关键契约或接口名、数据表或端点约定。涉及 Kun 缺口处写「过渡态（2026-10）」。
- **插件化定位**：`Inject` 哪些服务键（依赖哪些组件的 Definition）、向外 `provides` 哪些服务键、事件、扩展点或契约；S1 的 scope 种类（租户、应用、Agent 会话）下哪些注册可被遮蔽或 `restrict`。
- **Agent 契约**：对应的清单字段、是否暴露为工具（S4 派生，S1 只落元数据）、人在回路要求。
- **验收标准**：`- [ ]` 可观察行为 + 测法/环境。测法分四类：单元测试（UT）、Testcontainers 集成测试（IT，带真实 PostgreSQL / Redis / RabbitMQ）、K3s staging 端到端（E2E）、TCK（插件一致性套件）。

功能标题形如 `### 7.3 qian.tenancy 租户目录与生命周期（P0 · S1）`。一节内若含多个独立交付的功能项，用 `#### 7.x.y` 再分，每个 `7.x.y` 仍是完整六小节。

#### 7.0.3 优先级口径

- **P0 必做**：不做则 S1 退出标准不成立，或者是「之后几乎改不了」的契约（ADR-014、ADR-018 列出的防返工项）。
- **P1 应做**：S1 内应完成；若资源不足可延到 S2 首个迭代，但接口与数据表约定必须在 S1 冻结。
- **P2 可选**：S1 有余力再做；不做不影响退出标准，所列项在 S2/S3 另有归属。

阶段与优先级以 `key-decisions-v0.md` 为唯一权威。基线之间或基线与决策之间的冲突，按决策写入正文，并在章末「待确认」逐条注明。

#### 7.0.4 S1 profile、组合包与平台插件总览

**组件模型 v2 口径（ADR-020，取代 ADR-003 D3.1–D3.4 的固定清单）**。可部署单元 = profile：一个 profile 是一份具名组装文件，列出要装载的组合包与自己的 `qian.patch.yml`，S1 随发行版交付 `platform`、`gateway`、`identity`、`standalone`、`minimal` 五个 profile；新增一个可部署单元只是新增一个 profile 文件，不再改 ADR。原 18 个平台插件 = `platform` profile 的 base 组合包内容，数量由组合包定义而不是决策；每个组件按 seam 三角色拆为 `Qian.<X>.Abstractions`（Service Definition，拥有服务键 `ctx.<X>` 与词汇类型）/ `Qian.<X>[.<Provider>]`（Service Provider）/ Consumer 三类包，扩展只依赖 Definition、绝不依赖具体 Provider（换一个 Provider 就改变整个产品，消费者不变）。租户启用一个组件 = 在租户 scope 挂载它的配置条目（scope patch）；租户定制 = 带作用域的遮蔽注册；「预装全部 + 数据库开关」不再是唯一路径（S1 预装仍是 `platform` 的默认组合包，但开关改为 scope patch）。运行时只剩极小的 `Qian.Runtime`（7.2.1），原 `qian.kernel` 的注册表 / 生命周期 / 能力目录改为普通插件 `qian.registry` / `qian.lifecycle` / `qian.capabilities`，每一项都能从配置替换。

S1 的 profile 与组合包落位（R31；三节点 K3s 的现实仍然成立：S1 默认只出三个 .NET profile 镜像）：

| profile（可部署单元） | 形态 | 承载内容（组合包） | 优先级 |
|---|---|---|---|
| `platform`（镜像 `qian-platform`，原 `qian-platform` 单元） | 独立 Pod（JIT，不承诺 AOT） | base 组合包：7.2.1 `qian.registry` / `qian.lifecycle` / `qian.capabilities` + 7.3 / 7.5 / 7.6 / 7.7 / 7.8 / 7.10 / 7.11 / 7.12 / 7.13 / 7.14 的平台插件（下表全部 `platform` 行）；`shell` 组合包（7.15 yue 外壳的静态前端制品，经 `gateway` 提供）；`model-gateway` 组合包（7.16，默认挂此处） | P0 |
| `gateway`（镜像 `qian-gateway`，原 `qian-gateway` 单元） | 独立 Pod，可 AOT（Kun.Gateway 以库方式引用并加固） | 7.9 网关；Cookie 会话换 S2S；OpenAPI 聚合与能力目录端点代理；`Qian.Identity.Client`；对外提供 `shell` 组合包的静态制品 | P0 |
| `identity`（镜像 `qian-identity`，原 `qian-identity` 单元） | 独立 Pod（独立安全边界） | 7.4 身份与 SSO（`Qian.Identity.Server`，OpenIddict 唯一 issuer）、连接器组件、组织目录最小集 | P0 |
| `standalone` | 消费者自己的 ASP.NET Core 宿主，不出镜像 | 装最小组合包：`Qian.Runtime` + `qian add <x>` 按需装入的组件，不装 `qian.registry` / `qian.lifecycle` / `qian.capabilities`，租户上下文由宿主提供或显式 `SingleTenant`（ADR-019 D19.4 独立模式） | P0（退出标准第 3 条的载体） |
| `minimal` | 测试与 SDK 用最小树，不出镜像 | 只装 `Qian.Runtime` 与声明式表面装载器，无业务组件；TCK 运行时语义套件、校验器与 `dump-config` 的基准宿主 | P0 |
| `model-gateway`（可选独立 profile，镜像 `qian-model-gateway-adapter`） | 独立 Pod（可选；`runtime.form: service`） | `model-gateway` 组合包（7.16 GPUStack ↔ Higress 控制面适配）从 `platform` 拆出独立运行时使用 | P1 |

> 注：profile 可被 patch 合并或拆分，表中为 S1 默认（R31）。原 `qian-shell` 单元不是 profile：yue 外壳是 `platform` 的 `shell` 组合包（静态前端制品，经 `gateway` 提供）；原 `qian-model-gateway-adapter` 单元是 `model-gateway` 组合包，默认挂在 `platform`，可选独立 profile。

平台插件全部 `policy.tier: system`、`policy.required: true`（不可从 `platform` profile 卸载，取代 v1 `alwaysEnabled`，R34），默认随 `platform` profile 装载（租户不可卸载平台插件，但可用租户 scope patch 替换其配置条目或遮蔽其注册），按同一份 `.qian-plugin/plugin.json` 清单、同一运行时语义、同一 TCK 交付（ADR-020）。本章各节、组件 ID 与 seam 包的对应（包名按 ADR-019 D19.3；原「宿主」列改为「默认 profile」，原「形态」列改为 `runtime.form`）：

| 节 | 组件 ID（平台插件） | seam 包（Definition + Provider） | `runtime.form` | 默认 profile | 优先级 |
|---|---|---|---|---|---|
| 7.1 | （Kun 上游修复，非组件） | — | — | — | P0 |
| 7.2 | `Qian.Runtime`、`qian` CLI、`Qian.Plugin.Sdk`；插件 `qian.registry`、`qian.lifecycle`、`qian.capabilities` | `Qian.Runtime`（.NET）/ `@qian/runtime`（TS）；`Qian.Plugin.Sdk` + `.Generators`；`Qian.Registry`（`ctx.Registry`）/ `Qian.Lifecycle`（`ctx.Lifecycle`）/ `Qian.Capabilities`（`ctx.Capabilities`），各含 `.Abstractions`；只进 `platform` profile，不供消费者独立安装（R34） | 运行时 / module / 非运行时 | 全部 profile（Runtime、SDK）；`platform`（三个插件） | P0 |
| 7.3 | `qian.tenancy` | `Qian.Tenancy.Abstractions`（`ctx.Tenancy`）+ `Qian.Tenancy` | module | `platform` | P0 |
| 7.4 | `qian.identity`、`qian.identity.connector.oidc`、`qian.identity.connector.wecom` | `Qian.Identity.Abstractions`（`ctx.Identity`）+ `Qian.Identity.Server`、`Qian.Identity.Client`、`Qian.Identity.Connector.Oidc` / `.WeCom` | module（Server / Client）；连接器 = 为 `ctx.Identity` seam 提供 Provider 的组件（`provides[]` + `policy.egress`，`connector` 非 `runtime.form`，R31） | `identity`（Server、连接器）；`platform` / `gateway` / `standalone`（Client） | P0 |
| 7.5 | `qian.authz` | `Qian.Authz.Abstractions`（`ctx.Authz`）+ `Qian.Authz` | module | `platform` | P0 |
| 7.6 | `qian.data`、`qian.data.dialect.postgres` | `Qian.Data.Abstractions`（`ctx.Data`）+ `Qian.Data`、`Qian.Data.Dialect.Pg` | module | 全部 .NET profile（随 SDK） | P0 |
| 7.7 | `qian.files`、`qian.files.provider.{oss,s3,local}` | `Qian.Files.Abstractions`（`ctx.Files`）+ `Qian.Files.Oss` / `.S3` / `.Local` | module | `platform` | P0 |
| 7.8 | `qian.config` | `Qian.Config.Abstractions`（`ctx.Config`）+ `Qian.Config.Apollo` | module | 全部 .NET profile（随 SDK） | P0 |
| 7.9 | `qian.gateway` | `Qian.Gateway.Abstractions` + `Qian.Gateway` | module（独立 profile） | `gateway` | P0 |
| 7.10 | `qian.events` | `Qian.Events.Abstractions`（`ctx.Events`）+ `Qian.Events` | module | 全部 .NET profile（随 SDK） | P0 |
| 7.11 | `qian.audit`、`qian.observability` | `Qian.Audit.Abstractions`（`ctx.Audit`）+ `Qian.Audit`；`Qian.Observability.Abstractions` + `Qian.Observability` | module | 全部 .NET profile（随 SDK） | P0 |
| 7.12 | `qian.notify`、`qian.notify.channel.{inbox,smtp,sms}` | `Qian.Notify.Abstractions`（`ctx.Notify`）+ `Qian.Notify`、`Qian.Notify.Channel.Inbox` / `.Email` / `.Sms` | module | `platform` | P0 |
| 7.13 | `qian.scheduler`、`qian.realtime` | `Qian.Scheduler.Abstractions`（`ctx.Scheduler`）+ `Qian.Scheduler`；`Qian.Realtime.Abstractions`（`ctx.Realtime`）+ `Qian.Realtime` | module | `platform` | P0 / P1 |
| 7.14 | `qian.entitlement` | `Qian.Entitlement.Abstractions`（`ctx.Entitlement`）+ `Qian.Entitlement` | module | `platform` | P0 |
| 7.15 | `qian.shell` + yue | npm `@qian/runtime`、`@qian/plugin-sdk`、`@yue/*` | ui | `platform` 的 `shell` 组合包（静态前端制品，经 `gateway` 提供；非 profile，R31） | P0 |
| 7.16 | `qian.model-gateway` | `Qian.ModelGateway.Abstractions`（`ctx.ModelGateway`）+ `Qian.ModelGateway` | service | `model-gateway` 组合包，默认挂 `platform`；可选独立 profile `model-gateway`（镜像 `qian-model-gateway-adapter`，R31） | P1 |
| 7.17 | 部署物料（非组件） | — | — | — | P0 |

> 注：ADR-003 曾裁决 S1 平台插件为 18 个（R1）；按 ADR-020 D20.7，数量不再是决策，本表所列即 S1 `platform` profile base 组合包里的平台插件。`qian.notify`（P0）、`qian.scheduler`（P0）、`qian.realtime`（P1）的优先级依据不变：ADR-012 的流程前置能力（可靠延时调度、消息模板注册放 S1）与补充裁决「短信渠道 S1 必做」。组织目录最小集作为 `qian.identity` 的子模块交付（7.4.3），不独立成组件。每个组件可单独 `qian add <x>` 装入 `standalone` 宿主（ADR-019 库优先），独立模式与平台模式同一份代码。

### 7.1 Kun 缺口修复（P0 · S1）

**设计理念**。Kun 是「微服务开发框架」而非平台内核（ADR-001）；它的四层依赖、`Result` 错误模型、显式注册的 Mediator、Outbox 表结构与稳定事件 ID、schema-per-service、RLS 双保险、PublicAPI 门禁全部继承。但 Kun 关键路径上有一组**在「K3s 多副本 + 多租户」下必然触发**的正确性缺口，不先修则多租户会在后台链路漏。本节把 K 基线 §5.1 的 13 项修补（12 项必修 + 第 3 项 Saga 其余修补 P1 可选）写成可验收条目。

**修复通道**（ADR-001，Q7 默认）：上游 PR 优先，每项设 **1 个迭代**的回合入期限；超期未合入则由 Qian 维护 `Kun.<Module>.Patches` 补丁包（同名命名空间 + `[Obsolete]` 转发），并记录回合入日期。无论哪条通道，Qian 以 **NuGet + CalVer 锁版本**消费 Kun，禁止把 Kun 源码复制进 Qian 仓；Kun 需提供预发布 feed、公共 API 兼容承诺、≥2 个小版本的弃用窗口与完整变更日志。

**范围与边界**。✅ 做：下表 13 项（第 3 项 Saga 修补为 P1 可选，其租户回灌已含在第 1 项 P0，R13）。❌ 不做：推翻 Kun 核心设计；把「AOT 兼容」写成平台承诺（业务服务全 JIT，AOT 只留网关/gRPC 边缘）；为低代码动态实体改造 EF 模型（S2 走非 AOT 孤岛，ADR-011）。

**技术落点与验收标准**（每项：改什么 → 怎么证明）：

| # | 修复项 | 目标态（改什么） | 验收（- [ ] 可观察行为 + 测法） | 优先级 |
|---|---|---|---|---|
| 1 | 多租户后台链路 | `ITenantContext` 增加 `BeginScope(tenantId)`；`MessageContext.TenantId` 从信封读取并在订阅器建 scope 后回灌；Saga 执行器按 `SagaInstance.TenantId` 开作用域；`AddKunTenantRls` 自动挂连接版 + 事务版拦截器并强制 `KunMultiTenantModelCacheKeyFactory`；`DefaultTenantResolver` 增加 `Production` 模式：禁用 `X-Tenant-Id` 头兜底与 `FallbackTenantId`，租户 id 过白名单正则；两套 `ITenantContext` 收敛为 `AsyncLocalTenantContext` 唯一实现，`JwtTenantContext` 退化为它的解析源；claim 名统一 `tenant_id` | - [ ] IT：租户 A 发出的集成事件，在消费端 handler 内 `ITenantContext.TenantId == A`，且 handler 内 EF 查询自动带 `tenant_id = A` 过滤（Testcontainers PG + RabbitMQ）<br>- [ ] IT：生产模式下带 `X-Tenant-Id` 头但无合法令牌的请求返回 401，不落到 `default`<br>- [ ] UT：同时注册 `AddKunMultiTenancy` 与 `AddKunSecurity` 只解析出一个 `ITenantContext` 实现<br>- [ ] IT：忘记 `ReplaceService<IModelCacheKeyFactory>` 的 DbContext 在 `AddQianData` 装配下被拒绝启动（见 7.6） | P0 |
| 2 | Outbox / 事件语义 | 领域事件派发改为提交前同 scope（或 `EfUnitOfWork` 直接 drain 子 scope 的 `IntegrationEventBuffer`），并加分析器规则「集成事件只能在命令 handler 同 scope 发出」；消费端改「处理成功后才标记」（手动 ack），失败按 `RetryBaseDelay` 指数退避重投，超过 N 次进 DLX；`IOutboxMessageStore` 提供 EF 默认实现；Outbox 锁默认开（Redis 锁或 `FOR UPDATE SKIP LOCKED` 轮询）；publisher confirm | - [ ] IT：命令 handler 内经通知 handler 发出的集成事件，提交后 `outbox_messages` 行数 +1（当前路径会静默丢弃）<br>- [ ] IT：消费端 handler 第一次抛异常、第二次成功，事件最终恰被处理一次且 `processed_messages` 只有一行；RabbitMQ 容器重启后未 ack 消息重投不丢<br>- [ ] IT：两个发布器副本同时轮询同一 Outbox 表，每条消息只发布一次（publisher confirm 计数）<br>- [ ] UT：被 Skip 的 at-least-once 集成测试恢复为必跑并通过 | P0 |
| 3 | Saga | `TSaga` 从单例改为 Scoped 工厂、去掉启动期临时容器；步骤租约/行锁；上下文回写；步骤重试；名字用 `ISaga.Name` | - [ ] IT：两个副本同时运行同一 Saga 实例，每步只执行一次<br>- [ ] 开通编排已裁决由 `qian.lifecycle` 插件（原 `qian.kernel` 生命周期控制器）以幂等步骤表承担（7.3，R13），本项不升 P0 | P1 |
| 4 | 网关加固 | 回退认证策略默认「要求已认证」；路由级 `RateLimiterPolicy` 与全局限流器真正绑定到路由；可信代理网段（只信任 Traefik 的 `X-Forwarded-For`）；私钥热轮换（JWKS 双钥重叠，`kid` 切换不重启）；网关宿主 `AddKunObservability`；`/healthz` `/readyz`；`client_credentials` / 机器身份签发端点（白皮书声称有、代码没有） | - [ ] E2E：未带令牌请求任一业务路由返回 401（除显式匿名白名单）<br>- [ ] IT：对限流路由超阈值返回 429 且响应头含 `Retry-After`<br>- [ ] IT：轮换签名私钥后，旧 `kid` 令牌在重叠窗口内仍可验证，窗口外被拒绝；过程不重启进程<br>- [ ] E2E：伪造 `X-Forwarded-For` 的请求（非可信网段发出）不改变限流分区 | P0 |
| 5 | gRPC 流式 | Tracing / Auth / Result 三个拦截器补齐 server streaming、client streaming、duplex | - [ ] IT：无 `tenant_id` 的流式调用在首帧前被拒绝（`Unauthenticated`）；流式调用出现在 trace 中 | P0 |
| 6 | Identity 客户端链路（G3/G4/G5） | `ExchangeCodeAsync` 发送 `code_verifier`；校验 `nonce`；补 refresh、client_credentials、discovery 定期刷新；会话 user → sessions 索引、后通道登出、日志只记 sid 哈希；CSRF 防护；`SignInHelper` 的 DI 注册 | - [ ] IT：对强制 PKCE 的 IdP（用 7.4 的 `qian-identity` 本身）完成授权码换取<br>- [ ] IT：篡改 `nonce` 的 id_token 被拒绝<br>- [ ] IT：吊销某用户全部会话后，其任一 sid 的请求返回 401<br>- [ ] 代码审查：日志中不出现明文 sid（grep 门禁） | P0 |
| 7 | S2S 签发器收敛 + 去产品化 | 三套签发器保留网关 `S2sTokenIssuer`（短 TTL、JWKS）一套，吸收 `scope / session_id / membership_id / project_id` claim；`Kun.Identity.KunJwtIssuer`、`Kun.Security.S2SJwt` 标 `[Obsolete]`；claim 形状对齐 iClaw 契约 `iss / aud[] / sub / tenant_id / project_id? / roles[] / scope / session_id / iat / nbf / exp / jti`，`exp − iat ≤ 300s`；issuer 默认值、Sandbox 扩展等约 30 处下游产品字样改为可配置或移出 | - [ ] UT：Qian 代码树中引用废弃签发器即编译告警升错误<br>- [ ] IT：网关签出的 S2S 令牌通过 iClaw `contracts/v0/s2s-jwt-claims` JSON Schema 校验<br>- [ ] 代码审查：`Kun.*` 发布包中 grep 不到下游产品名的默认值 | P0 |
| 8 | 审计收敛 | 废弃 `Kun.Audit.EntityFrameworkCore.AuditSaveChangesInterceptor`（事后、不同事务）；实体级以同事务写 `AuditLog` + Outbox 事件为准；`IAuditChainState` 提供 PostgreSQL 实现（可串行化读 tip，`content_hash` UNIQUE）；`IAuditReader` EF 实现；`AuditActorType` 增 `Agent`；`TenantId / UserId` 从上下文填充 | - [ ] IT：业务事务回滚时 `audit_logs` 无残留行；提交时审计行与业务行同事务可见<br>- [ ] IT：两个副本并发追加同一租户链，链无分叉（`prev_hash` 严格串联，重放校验通过）<br>- [ ] UT：`AuditEntry.ActorType` 可取 `Agent` | P0 |
| 9 | 数据约定 | 审计字段自动填充拦截器（`ICurrentUser.UserId` 与 `TUserKey` 类型转换约定）；`CreatedBy` 可空；并发令牌（`xmin`）；snake_case 命名约定；`Guid.CreateVersion7()` 默认主键 | - [ ] IT：新增实体 `created_at/created_by` 非空，更新时 `updated_*` 刷新、`created_*` 不变<br>- [ ] IT：两个并发更新中一个因 `xmin` 变化抛 `DbUpdateConcurrencyException` 并映射为 `Result` 冲突错误 | P0 |
| 10 | 配置 | `Kun.Configuration.Apollo` provider（自研轻量、AOT 友好，详见 7.8）；热更新「校验失败保留上一份有效值并告警」；options JSON Schema 导出；修正 AGENTS.md 中关于热重载的错误描述 | - [ ] IT：发布一份非法配置后 `IOptionsMonitor<T>.CurrentValue` 仍返回上一份有效值，且发出告警指标<br>- [ ] UT：`AddKunOptions<T>` 可导出 JSON Schema 2020-12 | P0 |
| 11 | 鉴权端口 `Kun.Authorization` | 只放端口不放实现：`IPermissionChecker { Check, BatchCheck, ListAllowedActions }`、`IDataPermissionFilter`（输出 EF 表达式或 SQL 谓词）、`IFieldPolicy`；ASP.NET Core 策略 / Mediator 行为 / gRPC 拦截器三处集成 | - [ ] UT：未注册实现时任何受保护端点缺省拒绝（fail-closed）<br>- [ ] IT：Mediator 行为对标记了所需权限的命令在 handler 前调用 `Check` | P0 |
| 12 | 能力目录 | `ICapabilityRegistry` 公开：`AddRequestHandler` 注册时同时收 `JsonTypeInfo` 并可枚举；与 `IEventTypeRegistry.GetAll()` 同款 | - [ ] UT：注册一个命令后，目录中出现其 ID、请求/响应 JSON Schema | P0 |
| 13 | 其他 | `Kun.Infrastructure` 死接口废弃；旧线 `Kun.Storage` 废弃（只保留 `Kun.Storage.Abstractions/Local/S3/Oss` 新线）；`AddKunDapper` 回调无法赋值的 API 瑕疵；`PerformanceBehavior` 阈值可配 | - [ ] UT：Qian 代码树引用 `Kun.Infrastructure.IRepository` 或旧线 `Kun.Storage.IFileStorage` 即编译错误（Banned API 分析器） | P0 |

**过渡态（2026-10）**：上述缺口均已在源码确认（a2/a3/d2 笔记）；其中第 2 项「领域事件→集成事件路径丢消息」是按代码路径推断、无测试覆盖，S1 第一周先用一个最小集成测试实证（Catalog 建商品后断言 `outbox_messages` 行数），再决定走上游修复还是 Qian 侧规定「集成事件只在命令 handler 内发」。Kun 事实上单人维护、提交节奏自 2026-07 起放缓，所以每项都要在补丁包通道上留退路。

**插件化定位**：本节不是组件，但它决定了 7.2–7.14 全部平台插件的「canonical API 集」：`ITenantContext` 用 AsyncLocal 版；存储用新线；审计用同事务路径 + PG 哈希链；签发器用网关一套；限流服务侧用 `Kun.RateLimit`、网关侧用 Gateway 三策略 + Redis 共享计数。这些选择写进 `Qian.Plugin.Sdk` 的分析器，插件作者用错即编译失败。

**Agent 契约**：第 7、8、11、12 项直接服务 7.18 的防返工清单（统一主体模型、审计含主体链、可解释判定、能力目录）；不单独暴露工具。

### 7.2 Qian.Runtime 运行时、qian CLI 与目录市场（P0 · S1）

> 本节按 ADR-020 重写，取代 v1 的「`qian.kernel` 插件内核 + `qian.plugin-sdk` 开发套件」。v1 中仍有效的技术要点（清单校验、注册表 PG schema、修订回切、强制验签、GitOps 执行器、源生成器、CLI 模板、Aspire 本地编排、TCK）已按下表搬入新结构，不再另立「内核」：
>
> | v1 要点 | 新落点 |
> |---|---|
> | 清单解析与 JSON Schema / 语义校验、注册表 PG schema、修订保留与回切、租户安装记录 | 7.2.1 `qian.registry` 插件 |
> | 两层生命周期控制器、幂等钩子编排、强制验签、GitOps / Local 部署执行器 | 7.2.1 `qian.lifecycle` 插件 |
> | 能力目录、扩展点 / 契约仓库、OpenAPI 聚合、可发布元数据与导入导出框架契约、Operation / App 契约预留 | 7.2.1 `qian.capabilities` 插件 |
> | 源生成器、分析器、SDK 默认安全装配、CLI 项目模板、`qian dev` Aspire 本地编排、TCK | 7.2.3 `qian` CLI 与 `Qian.Plugin.Sdk` |
> | OCI 制品打包 / 签名 / 发布（`pack` / `sign` / `publish`） | 7.2.3（只用于代码部分）与 7.2.4（声明式表面随 marketplace 分发） |

#### 7.2.1 `Qian.Runtime` / `@qian/runtime` 运行时与 `qian.registry` / `qian.lifecycle` / `qian.capabilities` 插件（P0 · S1）

**设计理念**。一切皆插件，没有特权内核（ADR-020 D20.1）：运行时只剩极小的 `Qian.Runtime`——共享上下文 `ctx`、服务注册、类型化事件、可逆副作用、装载器；租户、身份、鉴权、文件、配置、网关、事件、审计都是向 `ctx` 贡献服务键的插件，原 `qian.kernel` 承担的注册表、生命周期控制器、能力目录、契约仓库改为普通插件 `qian.registry` / `qian.lifecycle` / `qian.capabilities`，每一项都能从配置替换。插件的最小接口是 `Inject` + `Apply`（D20.2），装载顺序由依赖关系表达而非手工编排；事件即扩展点（D20.3）；任一注册要么全局、要么归属恰好一个 scope（D20.5）；运行中的应用是 profile、组合包与 patch 层叠出的一棵插件树，一条命令能打印整棵树并替换任意条目（D20.7）。清单先于代码、静态可索引的原则不变：宿主不执行代码就能据 `plugin.json` 与声明式表面生成路由、权限点、菜单、Apollo namespace、OpenAPI 聚合与（S4）MCP 工具。参考平台的 11 万行内核、公共层反向依赖业务实体、启动期一次性加载外部程序集且无人使用，仍是反例；「只能打补丁才能改的内核」本身也是反例。

**范围与边界**。✅ 做：
- `Qian.Runtime`（.NET）与 `@qian/runtime`（TypeScript，yue 外壳也是一棵插件树）：`IQianPlugin { Inject; Apply(ctx, config) }`；服务键注册与解析（`ctx.<Key>`）；五种事件分发模式 `emit` / `waterfall` / `parallel` / `serial` / `bail`；可逆副作用 `ctx.Effect()`；scope 两层扁平（全局 | 恰好一个 scope key；S1 scope 种类为租户、应用、Agent 会话）与遮蔽 / `restrict`；profile → 组合包 → profile patch → home 级 patch → `--patch` overlay 的分层装载器；`qian --profile <p> --dump-config`；声明式表面（skills、commands、hooks、MCP、元数据、patch）运行中热重载；两侧语义相同并由同一组 TCK fixture 固化。
- `qian.registry` 插件（原 `qian.kernel` 注册表）：`.qian-plugin/plugin.json` 解析与 JSON Schema 2020-12 校验 + 语义校验；注册表（PG，schema `registry`，原 `kernel`）；租户 scope patch 的持久化（`scope_patches`，与文件 patch 同格式、同校验器）；scope 视图查询（「租户 T 挂载了什么」）；修订保留与回切记录。
- `qian.lifecycle` 插件（原 `qian.kernel` 生命周期控制器）：平台级制品 → 修订 → 激活；租户级授权 → 开通 → 升级 → 卸载 → 清除（`Enabled ⇄ Disabled` 不再是状态，改为租户 scope 的挂载 / 卸载 patch）；逐租户幂等钩子编排；强制验签（未签名或签名不符直接安装失败）；GitOps / Local 部署执行器。
- `qian.capabilities` 插件（原 `qian.kernel` 能力目录与契约仓库）：能力目录（命令 / 查询 / 事件 / 实体四类）与鉴权 + scope 过滤后的查询端点；扩展点 / 插槽注册表与契约仓库（`.proto` / OpenAPI 契约包，带 `/vN`）；OpenAPI 聚合；可发布元数据框架与导入导出框架的**契约**（P1，供 S2 消费）；Operation/Task 与 App 实体契约预留。
❌ 不做：为第三方代码与可 AOT 的边缘 profile 做运行时热插 DLL（永不）；官方代码插件在 `platform` profile 的热装卸（collectible `AssemblyLoadContext`）为 S2 可选，S1 默认进程重启（R27，ADR-020 §7 第 5 条）；第二套清单格式（手写 YAML 与派生文件并存）；按物理形态做顶层分类（`kind` 退为 `runtime.form`）；scope 继承（永不：子 scope 行为用 lineage 数据表达）；`package` / `function` / `connector` 运行时（S2）；逐租户批量迁移的批次调度（S2，S1 只对少量租户串行执行）；升级同意规则与推送升级（S2/S3）；工具描述哈希钉死与复审（S3）；每租户 MCP 门面（S4）；`extensions`（模型自行挂载 / 卸载插件，S4，S1 装载器只开放给人类 CLI）。

**技术落点**。

- `Qian.Runtime`：组件库唯一必需的 Qian 依赖（D20.10），极小、无业务语义，依赖 `Kun.Core`、`Kun.Mediator`、.NET DI keyed services。接口：`IQianPlugin { IReadOnlyList<ServiceKey> Inject { get; } ValueTask Apply(IPluginContext ctx, JsonElement config, CancellationToken ct); }`；`IPluginContext`：`Get<T>(ServiceKey)`、`Provide<T>(ServiceKey, T)`（keyed service 注册）、`On<TEvent>(DispatchMode, handler)`、`Emit / Waterfall / Parallel / Serial / Bail`、`Effect(Action dispose)`、`Scope`（当前 scope key，全局为 null）、`Restrict(scopeKey, predicate)`。装载器按 `inject[]` / `provides[]` 做拓扑排序，依赖缺失或成环在启动期以结构化错误失败，不做运行期懒加载。
- 五种分发模式：`emit`（观察，忽略返回）、`waterfall`（环绕中间件，监听器必须调用 `next()` 才委托）、`parallel`（并行扇出）、`serial`（按注册顺序）、`bail`（停在首个非空返回值）。事件在定义处以 `[QianEvent(Mode = ...)]` 标注，CI 校验声明与调用点一致并生成「事件生产方与消费方」映射（7.2.2 文档生成器）。拦截与策略优先用事件（鉴权、租户、审计三个强制执行点是 base 组合包注册的 `waterfall` 监听器，ADR-020 对 ADR-003 的修订），直接能力调用优先用服务方法。与 7.10 的分界：这里是进程内扩展事件，`ctx.Events` 是跨进程集成事件，两者不混用。
- 可逆副作用：`Provide`、`On`、路由与菜单贡献、工具注册等一切注册都经 `ctx.Effect()` 返回释放函数；卸载、重载与 teardown 按注册的逆序撤销；重载 = teardown 受影响插件子树 + 重新 `Apply`。
- scope：scope key 形如 `tenant:<id>`、`app:<id>`、`agent-session:<id>`；带作用域的注册在该 scope 内遮蔽同名全局项；`Restrict` 为某个 scope 过滤全局集合，多个 restriction 取交集；被过滤掉的项既不出现在目录与提示词里，也拒绝执行，与不存在无法区分。**租户启用一个组件 = 在租户 scope 挂载它的配置条目；租户定制 = 带作用域的遮蔽注册**（取代 v1 的租户级 `Enabled ⇄ Disabled` 状态与数据库开关）。
- profile / 组合包 / patch 装载器：条目形状 `{ id, plugin, config, scope? }`；层叠顺序 profile 文件（S1 随发行版交付 `platform`、`gateway`、`identity`、`standalone`、`minimal`）→ profile 列出的组合包（配置条目 + 挂载代码的分发格式）→ profile 自己的 `config/qian.patch.yml` → home 级 patch（`$QIAN_HOME/patch.yml`）→ 命令行 `--patch` overlay；一条 patch 按 `id` 定位条目并替换其整个 `config`，或插入新条目；条目 `config` 按该组件 `config.schema`（7.8）校验。租户层是租户 scope 的 patch 层：存 PG `scope_patches`，格式与校验器同文件 patch，由 `qian.registry` 在启动与变更事件时合入。`qian --profile platform --dump-config` 打印整棵树并标注每条目来源层；打印出的任何条目都可被 patch 替换。
- 热重载：开发态文件监视、生产态注册表变更事件触发；只 teardown 受影响表面的 effects → 重新解析与校验 → 重新注册；`src/` 代码变更不触发热重载，提示走滚动重启（7.17）。
- `qian.registry`（Provider 包 `Qian.Registry`，服务键 `ctx.Registry`）：`IPluginManifestLoader` / `PluginManifestValidator` 读取 `.qian-plugin/plugin.json`，按 JSON Schema 2020-12 校验，再做语义校验（`id` 反向域名且发布后不可改、`runtime.nuget[] / npm[]` 版本可解析、OCI 引用只接受 `@sha256:`、`inputSchema` 不含租户字段、`inject[]` 全部可被某个 `provides[]` 满足、`scopes[]` 只含 S1 合法种类、`policy.tier` 与 `policy.sandbox` 符合 ADR-002 信任等级规则、`policy.required: true` 只允许出现在 `policy.tier: system` 的平台插件上（取代 v1 `alwaysEnabled`，R34））；错误带 JSON Pointer。表：`plugin_artifacts`（digest、清单快照、签名验证结果）、`plugin_revisions`（插件 × 修订号 × 制品 digest × 状态 Installed / Active / Deprecated / Retired）、`tenant_plugin_installs`（tenant × plugin × 状态 Entitled / Provisioning / Installed / Upgrading / Uninstalled / Purged × 当前版本 × 迁移游标；v1 的 Enabled / Disabled 已删除，挂载状态只在 `scope_patches`，R34）、`scope_patches`（scope_kind、scope_key、entry_id、config jsonb、revision、applied_by、applied_at）、`extension_points`、`contracts`、`capabilities`、`event_types`、`entity_types`、`lifecycle_runs`（每次钩子执行的幂等记录）。scope 视图：`GET /api/registry/scopes/{scopeKey}/entries`。管理面端点统一为 `/api/registry/*`（注册表与 scope）、`/api/lifecycle/*`（生命周期操作）、`/api/capabilities/*`（能力目录、事件目录、OpenAPI 聚合），v1 的 `/api/kernel/*` 在 S1 内保留为别名并带 `Deprecation` 响应头，S2 移除（R33）。未挂载 / 未授权 / 被 restrict 的条目：面向业务与模型的表面（网关路由、`ctx.Tools`、能力目录、菜单）一律 404，与不存在无法区分；只有 `/api/registry/*` 在调用者持有 `qian.registry:scope:read` 时返回 403 `plugin_not_mounted` 并附原因（`not_entitled` / `not_mounted` / `restricted`）（R32，ADR-020 D20.5）。
- `qian.lifecycle`（Provider 包 `Qian.Lifecycle`，服务键 `ctx.Lifecycle`）：`IPluginLifecycleController` 平台级 `Install(artifact) / Activate(revision) / Rollback(toRevision) / Deprecate / Retire`；租户级 `Provision(tenant, plugin) / Upgrade(from, to) / Uninstall / Purge`，`Mount(scope, entry) / Unmount(scope, entryId)` 写租户 scope patch（取代 `Enable / Disable`）。钩子调用契约：`module` 实现进程内 `IPluginLifecycleHooks`，`service` 实现 gRPC `qian.lifecycle.v1.Lifecycle`；所有钩子带 `idempotencyKey = (tenant, plugin, hook, toVersion)`，结果入 `lifecycle_runs`，可重试；底层开户原语用 Kun `ITenantProvisioner`。`module` 的「安装」= 随 profile 镜像发布；回切 = 回切镜像修订 + 租户迁移保持前向兼容（扩展 → 迁移 → 收缩，不承诺自动回滚数据）。「未挂载」在网关路由（7.9）、SDK 请求入口（`PluginEnabledMiddleware` 读 `ctx.Registry` 的 scope 视图）、前端外壳（7.15.1）三处同时拦截，三处对业务与模型表面一律 404（R32）。验签：代码制品 = `plugin.json` 快照 + 文件摘要 + SBOM 打成 OCI 制品，cosign 分离签名；声明式元数据包用「文件 URI + SHA-256 + 字节数」清单 + Ed25519 分离签名；信任根（平台公钥，可多把并轮换）经 Apollo `qian.common.trust` 引用 K8s Secret。`IDeploymentExecutor`：`GitOps`（为 `runtime.form: service` 组件生成 Kustomize 片段提交到部署仓库，Argo 收敛，健康 = K8s 状态 + 组件 `/health` 双条件回写注册表）与 `Local`（开发态）两种实现。
- `qian.capabilities`（Provider 包 `Qian.Capabilities`，服务键 `ctx.Capabilities`）：`ICapabilityRegistry` 注册时收 `JsonTypeInfo` 生成请求 / 响应 JSON Schema；`GET /api/capabilities?kind=command|query|event|entity&q=` 按调用者权限与当前 scope 的 restrict 过滤（被过滤项不出现，R32），响应带 `ttlMs` 与 `cacheScope: private`；`GET /api/capabilities/openapi.json` 聚合全部已挂载组件的 OpenAPI 3.1（需认证，**不等于**公开 OpenAPI）。`IExtensionPointRegistry` / `IContractRepository`：扩展点 ID 形如 `qian.shell.nav/v1`；挂载组件前做契约一致性探测（gRPC 反射 / OpenAPI diff）。可发布元数据框架契约（P1）：`IPublishable<TDraft, TSnapshot>` + 状态 `draft / published / archived` + 当前生效指针 + 不可变快照表约定；导入导出框架契约（P1）：`IPackageSerializer`、判重策略 `skip | append`、id 重映射表、依赖重连清单。只定接口与表约定，S2 的表单 / 列表 / 流程落实现。
- Operation/Task 契约预留（R8；S1 可选实现，但契约必须预留；S2 P0）：`qian.operations` 的资源契约由 `qian.capabilities` 在 SDK 契约包中定义——`Operation { id, kind, status: working | input_required | completed | failed | cancelled, progress, inputRequest?, result?, error?, startedBy, tenantId, idempotencyKey, cancelable }` 与 `GET /operations/{id}`、`:cancel`、`:provideInput` 的端点形状；能力条目 `async: task` 即指向该契约。S1 内导入导出等长操作一律按此契约把返回类型定义为 Operation 资源（实现可到 S2），不得另造「任务」模型；完整实现见 §08 8.12。
- App 实体契约预留（R26；S1 契约，S2 升为一等元数据对象）：App = `runtime.form: package` 组件在租户内的安装记录，持有默认首页、菜单根与权限点归属，契约由 `qian.capabilities` 定义（S1 落在注册表 `tenant_plugin_installs` 的应用视图，应用默认首页 = 菜单 + 功能列表）；S2 由 `qian.metadata` 升为一等元数据对象（§08 8.10）。`qian.navigation` 在 S1 不是独立组件，而是 `qian.capabilities` 内「菜单由清单贡献 + URN 目录 + 用户偏好」能力的逻辑名（菜单是可按 scope 遮蔽的注册），S2 允许拆出为独立组件。

过渡态（2026-10）：Kun 没有任何插件机制、注册表或源生成器（文档声称的 Mediator 源生成器在代码中不存在）；.NET 也没有现成的「可逆副作用 + scope 过滤分发」运行时，`Qian.Runtime` 全部新建并在 S1 第一波用 TCK 固化语义（语义以 ADR-020 与 specs/component-model.md 为准，不以某个实现为准）。可借用的素材只有 `AddKunX().UseY()` 模块化形态（在 Qian 里被包装为插件的 `Apply` 内部细节）、`[IntegrationEventId]` 稳定 ID、SchemaGen 工具与 `Error.Code` 约定。Kun 严格 AOT 约束与动态插件树矛盾的部分：`gateway` 等边缘 profile 可 AOT，`platform` profile 不承诺 AOT。

**插件化定位**。`Qian.Runtime` 不依赖任何插件，被全部组件库依赖；`qian.registry` / `qian.lifecycle` / `qian.capabilities` 是只进 `platform` profile 的平台插件（Provider 包 `Qian.Registry` / `Qian.Lifecycle` / `Qian.Capabilities`，服务键 `ctx.Registry` / `ctx.Lifecycle` / `ctx.Capabilities`，各含 `.Abstractions`；不是供消费者独立安装的库，R34），`standalone` / `minimal` profile 不装载它们，组件库不得依赖它们（CI 依赖图断言）。向外提供扩展点 `qian.lifecycle.hook/v1`、`qian.capabilities.capability/v1`、`qian.lifecycle.deployment-executor/v1`（原 `qian.kernel.*`）；发布集成事件 `lifecycle.plugin.activated.v1`、`registry.scope.mounted.v1`、`registry.scope.unmounted.v1`、`lifecycle.tenant-plugin.upgraded.v1`（原 `kernel.*`）。

**Agent 契约**。消费 `plugin.json` 的 `tools` 表面与能力条目的 Agent 字段（`sideEffect / policyHint / requiredScopes / schemaSha256 / agent.expose`）、事件与资源声明，S1 只落库不派生 MCP 工具；所有模型可见的注册（工具、提示词片段、skills）进 `ctx.Tools` 等注册表并受 scope 过滤（D20.9），能力目录查询端点在 S4 直接成为 Agent 发现入口，每租户 MCP 门面由 `ctx.Tools` 的租户 scope 视图派生。生命周期操作（激活、回切、清除、挂载到租户 scope）全部标 `sideEffect: destructive`、`policyHint: confirm`，Agent 只能发起申请；`extensions`（模型在策略下挂载 / 卸载插件）建立在同一套装载器上，S1 只开放给人类 CLI。

**验收标准**
- [ ] TCK（运行时语义套件，.NET 与 TS 同一组 fixture）：`Inject` 的服务键无人提供 → 启动失败并指出缺失键与请求方；依赖成环 → 结构化错误列出环路；`Apply` 中注册的全部 effect 在卸载时按逆序撤销，卸载后 `dump-config` 与服务容器中无残留。
- [ ] TCK：五种分发模式各一组用例——`waterfall` 监听器不调用 `next()` 时下游不执行；`bail` 停在首个返回值；`parallel` 任一失败不影响其余；事件声明的模式与调用点不一致时 CI 失败并生成生产方 / 消费方映射。
- [ ] TCK：scope 遮蔽与 `restrict` 交集——租户 A scope 注册同名服务后，A 内解析到遮蔽项，B 与全局不受影响；两条 restriction 的交集生效；被过滤项在目录、提示词与执行三处都「不存在」（目录不列、调用返回与不存在相同的错误）。
- [ ] E2E：`qian --profile platform --dump-config` 打印整棵插件树并标注来源层；任取打印出的一条条目，用 `--patch` 替换其 `config` 后生效（声明式条目热重载、代码条目重启后），再次 `dump-config` 显示该条目来源为 overlay。
- [ ] IT：对租户 A 写一条 scope patch 挂载参考应用、B 不挂载：A 可用，B 的网关 / SDK / 外壳三处同时 404（与不存在无法区分）；持 `qian.registry:scope:read` 的运营者查 `GET /api/registry/scopes/tenant:B/entries/<id>` 得 403 `plugin_not_mounted`、`reason = not_mounted`，无该权限者 404（R32）；撤销 patch 后 ≤ 5s 恢复拦截；整个过程不重启进程、不改数据库开关。
- [ ] CI：依赖图断言——任何 `Qian.<X>` / `Qian.<X>.Abstractions` 包的传递依赖中不出现 `Qian.Registry`、`Qian.Lifecycle`、`Qian.Capabilities` 与任何其他 Provider 包，只允许 `Qian.Runtime`、`Kun.*` 与 `Qian.*.Abstractions`（`dotnet list package --include-transitive`）。
- [ ] TCK：缺任一 S1 必填字段、OCI 引用用 tag 而非 digest、`inputSchema` 含 `tenantId`、`inject[]` 引用无人提供的服务键、`scopes[]` 含非法种类的 `plugin.json`，安装失败并返回带 JSON Pointer 的结构化错误。
- [ ] E2E：参考应用在租户 A、B 分别 `Provision` 并挂载，`lifecycle_runs` 各有一条成功记录；对租户 A 重复执行 `Provision` 不产生副作用（幂等）。
- [ ] E2E：发布参考应用修订 n+1 并激活，租户 A 升级、租户 B 保持 n；对 A 回切到 n 后 A 的请求由修订 n 处理且数据可读（前向兼容）。
- [ ] IT：未签名制品与签名不匹配制品均 `Install` 失败；信任根轮换后旧签名在重叠期内有效。
- [ ] IT：`GET /api/capabilities` 对无权限用户不返回其不可调用的能力，对被租户 scope `restrict` 掉的能力同样不返回；响应含 `cacheScope: private`；v1 别名 `GET /api/kernel/capabilities` 返回同一结果并带 `Deprecation` 响应头（R33）。
- [ ] E2E：修改已挂载组件的 `skills/<x>/SKILL.md` 或 `hooks/hooks.json` 后 ≤ 5s 新内容生效、无进程重启；修改其 `src/` 不触发热重载并在 `qian doctor` 中提示需滚动重启。

#### 7.2.2 声明式表面：skills / agents / commands / hooks / MCP（P0 · S1）

**设计理念**。组件 = 文件夹 + 清单 + 声明式表面 + 可选代码（ADR-020 D20.6）；无代码的组件（只有 skill、只有配置）是常态而非例外。skill、agent、command、hook、MCP 服务器、tool、提示词片段从 S1 起就是受支持的表面（D20.9），不是 S4 才有：它们是 Markdown 与 JSON，Agent 能直接读写，设计器与 Agent 的产出物因此天然就是组件，可以进 git、进市场、被 patch。Qian 只负责定义、解析、校验、按 scope 注册与分发这些表面；执行它们的 Agent 运行时仍是 iClaw（ADR-016），不在 Qian 内写 Agent loop。

**范围与边界**。✅ 做：五种表面的解析器与校验器——`skills/<name>/SKILL.md`（front matter：`name`、`description`、`allowed-tools`、`metadata`）、`agents/<name>.md`（front matter + 系统提示）、`commands/<name>.md`（人类斜杠命令，命令平面，不进入模型消息；支持 `$ARGUMENTS` 与内联 shell）、`hooks/hooks.json`（Qian 事件与 Agent 宿主事件如 `PreToolUse` 的桥接）、`.mcp.json`（MCP 服务器声明）；另加 `tools/`（面向模型的工具描述，由代码派生或手写）与 `config/qian.patch.yml`（组合包对其他条目的 patch）；表面注册进 `ctx.Skills` / `ctx.Agents` / `ctx.Commands` / `ctx.Hooks` / `ctx.McpServers` / `ctx.Tools` 注册表并受 scope 过滤；`AGENTS.md` 进每个仓库与每个包；包 README 契约（含「模型体验」节与「已知限制与延期项」节）；文档生成器（配置目录、模块图、事件图、能力 seam 图由代码生成）与 CI 新鲜度门禁；校验器以库形式交付，被 `qian doctor`、CI 与 S4 的 Agent 生成闭环共用。❌ 不做：执行 skills / agents 的 Agent 运行时（iClaw）；每租户 MCP 门面与由能力目录派生工具（S4）；`forms/` `lists/` `flows/` `pages/` 低代码表面（S2，§08）；Skills over MCP 分发（S4）；`extensions`（S4）；服务端 hooks 执行任意 shell（永不：服务端 hooks 只能桥接到 Qian 事件与能力 URI，内联 shell 仅限 `commands/` 在人类 CLI 侧执行）；提示词片段的灰度与 A/B（S3 可选）。

**技术落点**。
- 目录约定与清单字段族以 ADR-020 D20.6 为准，权威规格为 specs/component-model.md（v1 specs/plugin-manifest.md 保留为 `kind` → `runtime.form` 字段迁移对照）。`plugin.json` 的表面指针（`skills`、`agents`、`commands`、`hooks`、`mcpServers`、`tools`、`ui`、`metadata`）缺省指向约定目录，可覆盖。
- 校验器：每种表面一份 JSON Schema 2020-12（front matter 解析为 JSON 后校验）+ 语义校验（`allowed-tools` 引用的工具必须存在于 `ctx.Tools` 或本组件 `tools/`；hooks 引用的 Qian 事件必须在事件目录、能力 URI 必须在能力目录；`.mcp.json` 的服务器名在组件内唯一；`commands/` 内联 shell 仅允许在人类 CLI 侧执行）。库 `Qian.Surfaces`（.NET）与 `@qian/surfaces`（TS）跑同一组 fixture；错误格式 = `ErrorDetail`（7.18 第 2 项）+ 文件路径 + 行号 + JSON Pointer。
- 注册：装载器读组件文件夹 → 解析表面 → 以 `ctx.Effect()` 注册，scope 取自挂载该组件的条目（`scope?`）并受 `plugin.json.scopes[]` 约束；表面变更走 7.2.1 的热重载路径。
- hooks：一条 hook = 触发源（Qian 事件 ID 或 Agent 宿主事件名）× 动作（能力 URI 或 `commands/` 命令名）× 分发模式；服务端触发的 hook 以 `channel = hook` 进审计；精确 Schema 在 specs/component-model.md 定稿。
- MCP：`.mcp.json` 在 S1 只被校验、登记、按 scope 过滤，并由 `qian add` 装进开发者的 Agent 宿主配置；平台侧运行 MCP 服务器与每租户门面是 S4（§10）。
- `AGENTS.md` 与 README：模板随 `qian init` 生成；CI 检查存在性与必需节；文档生成器 `qian docs generate` 输出 `docs/generated/{config-catalog,module-graph,event-graph,seam-graph}.md`，CI 比对生成结果与提交内容，漂移即失败。
- 官方市场（7.2.4）全部组件必须过校验器，作为市场条目的准入门禁。

**插件化定位**。表面解析与校验是 `Qian.Runtime` 装载器的一部分：无代码组件不需要任何插件就能被 `standalone` / `minimal` profile 装载；`ctx.Skills` 等注册表的服务键由 `Qian.Runtime` 定义，持久化视图由 `qian.registry` 提供；iClaw 经 7.2.4 市场或 `qian add` 获取表面；S2 的 `forms/` `lists/` `flows/` `pages/` 复用同一解析器骨架与校验器接口（§08 8.1）。

**Agent 契约**。表面本身就是 Agent 契约：SKILL.md 的 `description` 是模型选择技能的依据，`allowed-tools` 与 `ctx.Tools` 的 scope 视图取交集；`commands/` 不进入模型消息；hooks 可把 Qian 事件桥接到 Agent 宿主、也可把宿主事件桥接回 Qian 能力；每租户 MCP 门面（S4）从 `ctx.Tools` 的 scope 视图派生；Agent 生成的组件（S4）落为草稿组件文件夹并经本节同一校验器。

**验收标准**
- [ ] CI：五种表面的校验器对官方市场仓库全部组件零错误；对 20 个坏样例（缺 front matter 字段、`allowed-tools` 引用不存在的工具、hooks 引用不存在的事件或能力、`.mcp.json` 格式错、`commands/` 内联 shell 出现在服务端 hook）逐一返回带文件路径与 JSON Pointer 的结构化错误。
- [ ] TCK：一个只含 `skills/` 与 `.qian-plugin/plugin.json`、没有 `src/` 的组件可被 `qian add` 装入 `minimal` profile 并出现在 `dump-config` 与 `ctx.Skills`；挂载到租户 A scope 后 B 不可见。
- [ ] CI：删除某组件 README 的「已知限制与延期项」节或仓库根 `AGENTS.md`，门禁失败；修改一个事件声明而不重新生成事件图，新鲜度门禁失败。
- [ ] E2E：修改已挂载组件的 SKILL.md 后 ≤ 5s 新描述出现在 `ctx.Skills` 与 `qian list`，无进程重启。
- [ ] IT：`hooks.json` 把 `tenant.created.v1` 桥接到一个能力 URI，租户创建时被触发且审计记录 `channel = hook`；桥接到不存在的能力在装载期被拒。

#### 7.2.3 `qian` CLI 与 `Qian.Plugin.Sdk`（P0 · S1）

**设计理念**。一条命令、不需要理解平台（ADR-020 D20.8）：开发者安装 Qian 的登录或文件组件时面对的是 `qian add identity`，而不是 Helm values、DI 扩展方法与 Apollo namespace；`AddQian<X>()` 只是 `Apply` 内部的装配细节，不是开发者入口（D20.10）。清单从代码派生、不手写：源生成器把代码派生的字段合并进唯一的 `.qian-plugin/plugin.json`，CI 比对清单与代码，漂移即失败（P 基线原则 P4）。SDK 是组件作者的唯一入口，Kun 的安全默认值在模板里是**默认装配**而不是 opt-in（iClaw 的教训：RLS 拦截器、多租户、限流、可观测「有而未被用上」）。

**范围与边界**。✅ 做：CLI `qian add <component>[@version] ...`（等价 `npx qian@latest --skill x --agent y --plugin z --yes`：声明式表面复制进项目 `.qian/` 目录、代码部分加为 NuGet / npm 依赖、配置条目写进当前 profile 的 patch）、`qian remove`、`qian list`、`qian doctor`（健康检查：清单校验、依赖图、未满足的 `inject`、过期版本、表面校验、profile 一致性、需滚动重启的代码变更）、`qian dump-config`、`qian init --template <stack>`（S1 模板：`component-dotnet`、`aspnetcore-standalone`、`react-ui`、`skill-only`；S2 加 `package`）；代码部分的子命令 `qian pack | sign | publish`（OCI 制品只用于代码部分；R33 统一去掉 v1 的 `plugin` 名词）与 `qian dev`（Aspire AppHost 本地编排：PG、Redis、RabbitMQ、Apollo 用容器）；NuGet `Qian.Plugin.Sdk`（`IQianPlugin` 基类、租户 / 身份 / 权限 / 配置 / 事件 / 存储 / 通知 / 调度客户端门面、生命周期钩子基类、默认安全装配）与 `Qian.Plugin.Sdk.Generators`（`IIncrementalGenerator`：`[QianPlugin]`、`[Capability]`、`[ExtensionPoint]`、`[Contributes]`、`[Permission]`、`[QianJob]`、`[QianEvent]` → DI 注册代码 + 合并进 `plugin.json` 的派生字段 + JSON Schema）；分析器（禁 `IgnoreQueryFilters`、禁废弃 API、集成事件只在命令 handler 发出、写命令必须有 Schema 与 `idempotencyKey`、`Apply` 内的注册必须经 `ctx.Effect()`、组件库引用 `Qian.Registry` / `Qian.Lifecycle` / `Qian.Capabilities` 或任何 Provider 包即报错）；npm `@qian/plugin-sdk`（注册远程模块、导航与插槽贡献、`PlatformClient`、拦截器链）；TCK 三套（运行时语义、独立模式、组件一致性）；`samples/standalone-minimal` 入源码仓。❌ 不做：开发者租户与远程调试通道（S2 可选）；Pact can-i-deploy（S3 可选）；C# 以外语言的 `service` 模板（S3 视第三方开放情况）；`Qian.All` / `Qian.Platform` 元包（待 ADR-019 §7）；`qian add --scope tenant:<id>` 之外的租户级运维 UI（S2 控制台）。

**技术落点**。
- `qian add` 流程：从已登记的市场（7.2.4）解析条目 → 取组件文件夹（`local` / `url` / `git-subdir`）→ 过 7.2.2 校验器与验签要求 → 表面复制到 `.qian/<id>/` → `runtime.nuget[]` 执行 `dotnet add package`（版本按市场条目钉）、`runtime.npm[]` 执行包管理器安装 → 在当前 profile 的 `config/qian.patch.yml`（`standalone` 为 `.qian/patch.yml`）追加条目 → 运行 `qian doctor`；`--yes` 跳过交互；`--profile <p>` 指定目标 profile；`--scope tenant:<id>` 写租户 scope patch（需平台连接与运营权限，S1 仅运营 CLI）；`.qian/lock.json` 记录每个组件的来源与 digest / commit。
- CLI 实现：Node（便于 `npx qian@latest`）并提供 `dotnet qian` 薄包装（ADR-020 §7 第 3 条默认）；子命令带 `--json` 机器可读输出。
- `qian init --template component-dotnet` 生成：`Program.cs` 组合根（装配 `Qian.Runtime` 并选择 profile）、一个 `IQianPlugin` 实现（`Inject` / `Apply`）与 `[QianPlugin]` 注解、一个 `IMultiTenant` 聚合、`MapCommand / MapQuery` 端点、EF 迁移、`.qian-plugin/plugin.json`（源生成器输出）、`skills/` 示例、`AGENTS.md` 与 README 模板、前端 MF2 远程、TCK 配置——它就是 7.0.1 参考应用的起点（原 v1 `qian plugin new --kind module --template crud`；CLI 动词按 R33 统一）。`aspnetcore-standalone` 生成不装 `qian.registry` 等插件、显式 `SingleTenant` 的独立宿主。
- 源生成器与 `plugin.json` 的合并规则：手写字段（`id`、`version`、`interface`、`policy`、表面指针）与派生字段（`inject[]`、`provides[]`、`tools`、`runtime`、事件声明、权限点）在同一份 JSON 中，派生字段带 `"$generated": true` 标记；CI 比对派生字段与代码，漂移即失败；仓库中不存在 `*.generated.yaml` 之类的第二份清单。
- `qian pack`：产出 OCI 制品（代码、`plugin.json` 快照、文件摘要、SBOM、Kustomize 片段、OpenAPI、事件 Schema、AsyncAPI）；`sign` 调 cosign / Ed25519；`publish` 推 Harbor 并在注册表登记 `Submitted`；声明式表面不进 OCI，随 git / marketplace 分发。
- TCK 三套：**运行时语义套件**（7.2.1 验收所列：`Inject` / `Apply`、五种分发、可逆副作用、scope 遮蔽与 `restrict` 交集、patch 替换；.NET 与 TS 同一组 fixture）；**独立模式套件**（ADR-019 D19.8：`standalone` profile 下不装 `qian.registry` / `qian.lifecycle` / `qian.capabilities`，租户上下文由宿主提供或 `SingleTenant` 显式选择，权限点经 `[Permission]` 登记，行为与平台模式一致）；**组件一致性套件**（清单校验、契约兼容性 OpenAPI diff / proto breaking / AsyncAPI、生命周期钩子幂等（对同一租户执行两次）、跨租户越权探测（两个租户，以 A 的令牌读 B 的记录必须 404/403）、写命令 Schema 存在、`inputSchema` 禁租户字段、性能基线（P1））。

过渡态（2026-10）：Kun 的样例 AppHost 连接串名与服务读取名不一致、样例测试被 CI 排除、未真接 RabbitMQ；Qian 不复用 Kun 样例，只借鉴分层与组合根写法。CLI 实现语言与清单文件名按 ADR-020 §7 第 2、3 条的默认写，拍板后若改动只影响本节与 7.2.4。

**插件化定位**。CLI 与 SDK 不是插件；SDK 只依赖 `Qian.Runtime` 与各 `Qian.<X>.Abstractions`，不依赖任何 Provider 或 `qian.registry` 等插件；向外提供「组件项目模板」扩展点 `qian.cli.template/v1`（原 `qian.plugin-sdk.template/v1`；S2 加 `package` 模板）。

**Agent 契约**。源生成器把 `[Capability]` 上的 `Description`（意图式）、`SideEffect`、`PolicyHint`、`RequiredScopes` 写入 `plugin.json` 的 `tools` 表面；TCK 在 S1 就强制每个能力有意图式描述与 JSON Schema，这是 S4 工具描述质量的来源；`qian` CLI 的 `--json` 输出与 `qian doctor` 的结构化错误可被 Agent 消费；S4 的 `extensions` 复用同一装载器，S1 只开放给人类。

**验收标准**
- [ ] E2E：空白 ASP.NET Core 项目执行 `qian add identity authz files`，照 README 在 30 分钟内跑通登录、权限检查与上传（独立模式，不部署 Qian 平台；`samples/standalone-minimal` 入源码仓，ADR-019 D19.8 / ADR-020 §6）。
- [ ] E2E：`qian init --template component-dotnet` 生成的模板不改一行代码即可 `qian dev` 启动、通过 TCK、`pack` 出制品并在 staging 对两个租户开通。
- [ ] UT：删除代码中的一个 `[Capability]` 但保留 `plugin.json` 中的派生条目，CI 比对失败并指出漂移项；仓库中出现第二份清单文件（手写 YAML 或 `*.generated.*`）时门禁失败。
- [ ] TCK：故意写一个跨租户读取的查询（`IgnoreQueryFilters`），分析器编译期报错；绕过分析器后 TCK 越权探测用例失败；在 `Apply` 内不经 `ctx.Effect()` 直接注册服务，分析器报错。
- [ ] TCK：同一组包在 `standalone` 与 `platform` 两种模式下 TCK 全部通过；独立模式下未显式选择 `SingleTenant` 且无宿主 `ITenantContext` 的应用拒绝启动。
- [ ] E2E：`qian dev` 一条命令拉起 PG / Redis / RabbitMQ / Apollo 容器与 `standalone` profile 宿主，开发者 10 分钟内看到参考应用登录页。
- [ ] UT：`qian doctor` 对缺失 `inject`、过期版本、坏表面、profile 条目引用不存在组件四类问题各给出一条可操作的修复建议，并以 `--json` 输出。

#### 7.2.4 官方目录市场：marketplace.json、多市场、GitHub topic 发现、目录网站（P0 · S1）

**设计理念**。市场是 git 仓库里的一份目录文件，不是服务（ADR-020 D20.8）：条目只写来源、安装与认证策略、分类与展示信息，不复制组件内容；装好的组件不依赖市场在线。S3 的商业市场（授权、计费、审核、ISV 入驻）叠在同一份文件之上，不另起格式（ADR-014 据此修订，§09 9.1）；S4 市场兼任私有 MCP registry 时 `.mcp.json` 表面即其数据源。

**范围与边界**。✅ 做：`.qian/marketplace.json`（`name`、`interface.displayName`、`plugins[]`，每条目 `name`、`source`（`local` 路径 / `url` / `git-subdir`）、`policy`（`installation`、`authentication: ON_INSTALL | ON_USE`、`profiles[]`）、`category`、`interface`）；官方市场仓库（与 Qian 同组织下的独立仓库，S1 私有，S1 收录全部平台插件、参考应用、至少一个 skill-only 示例组件）；多市场并存（本机 `.qian/marketplaces.json` 登记官方 / 租户私有 / 社区市场，解析按登记顺序，同名组件按市场优先级并写入 `lock.json`）；社区发现：GitHub topic `qian-plugin`（只发现、不自动信任，安装需显式 `--trust`）；目录网站由 marketplace.json 生成（静态站：分类、安装命令、README 渲染、校验状态徽章）；市场条目校验器（条目指向的组件必须过 7.2.2 校验器与 7.2.1 验签要求）。❌ 不做：商业市场（授权、计费、审核、ISV 入驻，S3 §09）；租户私有市场 UI（S3）；市场作为运行时依赖；自动信任社区市场；工具描述哈希钉死与复审（S3）；市场服务 `qian.marketplace`（S3）。

**技术落点**。marketplace.json 的 JSON Schema 在 specs/component-model.md；`@version` 解析为 git tag（声明式）与 NuGet / npm 版本（代码部分），`lock.json` 记录 commit 与 digest；官方仓库布局 `plugins/<domain>/<id>/`，每个组件文件夹符合 ADR-020 D20.6 目录约定；CI：每个条目的组件过校验器 + `qian add` 到 `minimal` profile 的 smoke；目录网站生成器为 CLI 子命令（子命令名随 CLI 定稿）；`policy.authentication: ON_USE` 条目在 S1 只登记并在 `qian add` 时提示「首次使用时填入凭据」，S3 连接器组件消费（凭据录入 UI 归 `qian.identity` 连接器设置卡的 `ui` 表面，R36）。

**插件化定位**。市场不是插件也不是服务；`qian add` / `qian list` 是 S1 的唯一消费者；S3 `qian.marketplace` 服务读同一份文件，商业字段（价格、套餐、席位、审核状态）以条目 `name@version` 为外键存平台库，不写进 marketplace.json（§09 9.1，R36）；租户私有市场在 S3 默认平台托管（租户上传组件文件夹，平台生成其私有 marketplace.json），租户自带 git 仓库为 S3 可选。

**Agent 契约**。marketplace.json 机器可读；Agent（S4）可检索并**提议**安装，安装动作 `policyHint: confirm`；条目 `interface.defaultPrompt[]` 是组件装入后给模型的默认提示；社区条目的「未验证」标记进入工具元数据，Agent 不得自动安装未验证组件。

**验收标准**
- [ ] CI：官方市场仓库每个条目指向的组件通过五种表面校验器与清单校验零错误；`qian add` 每个条目到 `minimal` profile 的 smoke 全部通过。
- [ ] E2E：登记第二个（租户私有）市场后 `qian list --remote` 合并两处结果并标注来源；同名组件按市场优先级解析并在 `lock.json` 记录来源。
- [ ] E2E：目录网站由 marketplace.json 一条命令生成，条目数、分类与仓库一致，每个条目显示校验状态徽章。
- [ ] IT：经 GitHub topic 发现的社区组件标记「未验证」，`qian add` 该组件在无 `--trust` 时拒绝并说明原因。

### 7.3 qian.tenancy 租户目录与生命周期（P0 · S1）

**设计理念**。租户是平台可信计算基的第一道边界：租户上下文只来自已校验令牌，解析不到即拒绝（fail-closed），非 HTTP 入口（消费者、作业、Agent）显式传播并逐跳校验。租户控制面与业务面分离，业务面只消费「租户上下文契约」（租户号、隔离策略与连接、生效插件清单、配额、状态）。参考平台的租户解析缓存未命中静默落默认库、到期不踢已登录用户、「永久删除」实为软删不清库，是反例。

**范围与边界**。✅ 做：租户主档（租户号小写字母数字、创建后不可改，兼作登录租户标识）；隔离策略装配（默认共享库 + `tenant_id` + PG RLS，创建时一次性决定，ADR-004）；显式状态机与事件；开通编排（幂等步骤表）；单管理员 + 用户配额（不计平台超管、计租户管理员，补充裁决 Q12）；租户配置服务（租户级在线配置，不进 Apollo，ADR-007）；租户事件与配额查询契约；运营后台最小 CRUD；切换租户的令牌重签模型（与 7.4 共担）。❌ 不做：自助申请/审核/加入/退出 UI（S3）；多管理员、邀请链接（S3）；完整状态机尾部（只读、注销与数据销毁，S3）；品牌与自定义登录域（S3，Q4）；套餐下发（S3）；DatabasePerTenant 高隔离档（S3）；SchemaPerTenant 真库验证（P2，仅集成测试）；域名/子域名解析租户（S3）；租户层级多级隔离（不做，用组织树与 `project_id` 在 7.5 解决）。

**技术落点**。组件 `qian.tenancy`：Definition `Qian.Tenancy.Abstractions`（服务键 `ctx.Tenancy`）+ Provider `Qian.Tenancy`，默认进 `platform` profile 的 base 组合包，可独立安装（`qian add tenancy`；独立模式下宿主可改提供自己的 `ITenantContext` 或选 `SingleTenant`）；schema `tenancy`；依赖 `Kun.MultiTenancy`（修复后）、`Kun.EntityFrameworkCore` 三策略 Provisioner、`Kun.EventBus`。

- 实体：`Tenant`（id、code、name、isolation `row|schema|database`、status、trial/valid 起止、quotas jsonb、contact jsonb、brand jsonb、remark）、`TenantAdmin`、`TenantSetting`（plugin_id、key、value jsonb、schema 版本、修改人/时间）、`tenant_provision_steps`（tenant × step × 状态 × 幂等键）。Membership 归 7.4，`TenantEntitlement` 归 7.14。模型迁移自 iClaw 的 Tenant/TenantMember（去 Agent 字段，补充裁决 Q40 允许）。
- 状态机（S1 实现的迁移加粗）：**待激活 → 试用 | 正常 → 宽限**（到期自动）→ 只读 → **冻结**（运营方 Suspend，任一状态可进；Resume 回原状态）→ 注销（保留期满销毁）。每次迁移发 `tenant.created/activated/suspended/resumed/expired.v1`；会话中心订阅后吊销该租户会话；到期扫描由 7.13 调度器执行。
- 上下文：`ITenantContext` 唯一实现（AsyncLocal + 中间件），claim `tenant_id`；生产模式禁用请求头兜底与 `default` 回退；`ITenantContextPropagator` 把 `tenant_id` 写入事件信封、作业参数、gRPC 元数据并在对端 `BeginScope`。
- 开通编排由 `qian.lifecycle` 插件（原 `qian.kernel` 生命周期控制器，7.2.1）承担（R13），`qian.tenancy` 的 `TenantProvisioningOrchestrator` 只是其租户级 `Provision` 入口的装配：创建主档 → `ITenantProvisioner.ProvisionAsync`（Shared+RLS：确认策略与 GUC 就绪）→ 对每个平台插件与该租户 scope 已挂载的组件执行 `tenantProvision` 钩子 → 创建租户管理员 Membership → 写初始授权 → 发通知。失败由生命周期控制器按步骤表（`tenant_provision_steps` / `lifecycle_runs`）重试，不靠数据库事务；不用 Saga（7.1 第 3 项 P1 可选）。
- 租户配置服务 `ITenantSettings.Get<T>(pluginId, key)`：值按插件 `config.schema`（`scopes: [tenant]`）校验，变更审计，缓存键带租户前缀；平台级默认值来自 Apollo，租户覆盖在此。租户覆盖即租户 scope 的 patch 层（ADR-020 D20.7）：存数据库，但与文件 patch 同一格式、同一校验器，`qian dump-config --scope tenant:<id>` 可打印合成结果。登录策略、密码策略、文件配额等都是它的客户。
- API：`GET/POST /api/tenancy/tenants`、`PUT /tenants/{id}`、`POST /tenants/{id}:activate|suspend|resume`（幂等 + 审批钩子）、`GET/PUT /tenants/{id}/admins`、`GET/PUT /tenants/{id}/settings/{plugin}`、`GET /tenants/{id}/quota`。运营后台页面（yue）：列表/筛选、新建/编辑抽屉、授权（调 7.14）、管理员维护、重置密码。
- 跨租户渗透测试样例进 TCK（7.2.3）；CI 检查「在线 DbContext 必须继承多租户基类」。

过渡态（2026-10）：Kun 只有上下文/解析/开户器，无租户目录；两套 `ITenantContext` 互斥、claim 名 `tid` vs `tenant_id`、header 兜底 fail-open，均在 7.1 第 1 项修复后装配；SchemaPerTenant / DatabasePerTenant 只有单元测试，选用前需 Qian 自补集成测试。

**插件化定位**。Inject `ctx.Registry`、`ctx.Lifecycle`（`qian.registry` / `qian.lifecycle` 插件，开通即编排各组件钩子；独立模式下两者缺席时退化为单租户装配）、`ctx.Events`、`ctx.Audit`、`ctx.Scheduler`、`ctx.Notify`；向外提供 `ITenantContext`、`ITenantSettings`、`ITenantDirectory.Get(code)`、事件 `tenant.*.v1`、租户 schema 与状态机元数据端点 `GET /api/tenancy/meta/state-machine`。

**Agent 契约**。租户对象 schema、状态与合法迁移、配额定义机器可读（Agent 先查合法动作再调用）；`suspend / purge` 标 `destructive` + `humanOnly`；`inputSchema` 中永不出现 `tenantId`（租户来自令牌，运营接口的目标租户是资源路径而非输入字段）。S4 可订阅 `tenant.*` 事件做编排。

**验收标准**
- [ ] E2E：运营后台创建租户 A、B（共享库 + RLS），各自管理员登录后只看到本租户数据；以 A 的令牌直接请求 B 的记录 id 返回 404。
- [ ] IT：开通中途在第 3 步注入失败，重试后从第 3 步继续，前两步不重复执行（`tenant_provision_steps`）。
- [ ] IT：租户到期进入宽限后，该租户新登录被拒、已登录会话在 ≤ 60s 内失效（`tenant.expired.v1` → 会话吊销）。
- [ ] IT：用户配额为 5 时第 6 个成员创建返回结构化错误 `CARDINALITY_USER_LIMIT`，平台超管不计入计数。
- [ ] IT：租户级登录策略覆盖写入 `TenantSetting` 后 7.4 在下一次登录即生效，且变更出现在审计链。
- [ ] UT：生产模式下无令牌、仅带 `X-Tenant-Id` 的请求与无 `tenant_id` 的消息都被拒绝。

### 7.4 qian.identity 身份与 SSO（P0 · S1）

#### 7.4.1 qian.identity 平台唯一 issuer 与账号模型（P0 · S1）

**设计理念**。基于 OpenIddict 自建 `qian.identity` 作平台**唯一 issuer**（ADR-005）：自己管账号、租户成员、会话、令牌与客户端注册；上游联邦全部做成连接器插件；Authentik 退居运维侧。权限不进令牌（ADR-006），令牌只含身份与会话类 claim（规范集 `sub`、`tenant_id`、`membership_id`、`scope`、`identity_revision`、`sid`、`amr`、`acr`，预留 `profile`、`act`，见下文技术落点），不含权限集合（S2S 令牌的 `roles[]` 例外见 ADR-010 D10.4 / R24）。参考平台的 MD5 密码、前端固定密钥 AES、HS 对称密钥写配置文件、无服务端吊销、5 分钟静默自动绑定，一概不沿用。

**范围与边界**。✅ 做：OIDC/OAuth2 端点（discovery、authorize、token、userinfo、revocation、introspection、end_session、back-channel logout）；授权码 + PKCE（公开与机密客户端）、refresh 轮换、`client_credentials`；RFC 8707 `resource`；非对称签名 + JWKS 轮换；Person（全局自然人）+ Membership（租户成员）双层账号，补充裁决 Q2；统一登录流水线（密码 Argon2id、短信 OTP、TOTP 三种因子，Q18）；图形验证码按策略开关；多租户账号选择（5 分钟选择令牌）；第三方账号绑定/解绑（显式确认、同租户唯一）；会话中心（user → sessions 索引、强制下线、后通道登出双向、并发上限默认 5、单端按端类型）；登录策略与密码策略（租户级配置 Schema）；在线用户管理；登录审计；`identity_revision`；CSRF 与安全头；内置客户端注册表（平台、官方插件、外壳、CLI）；个人中心最小集（资料、改密、社交账号、安全）。❌ 不做：租户托管身份（S3 可选）；租户自助配 IdP、HRD、域名验证、Passkey、SCIM（S3）；每租户 issuer / 自定义登录域（S3，Q4）；SAML/CAS/LDAP（S3，Q3）；应用安装即建 client（S3）；Agent 主体与 `act` 令牌交换（S4，S1 预留 claim 位）；PAT/API Key 个人密钥（S2）；扫码登 PC（S3）；国密算法（无假设，待确认）。

**技术落点**。组件 `qian.identity`：Definition `Qian.Identity.Abstractions`（服务键 `ctx.Identity`）+ Provider `Qian.Identity.Server`（可嵌入任意 ASP.NET Core 宿主的 OpenIddict IdP，含 Person / Membership 与连接器 SPI）与 `Qian.Identity.Client`（RP 接入、Cookie 会话、S2S 校验）；Server 默认进 `identity` profile（独立安全边界、独立 PG schema `identity`、独立 Redis db），Client 进 `platform` / `gateway` / `standalone`；可独立安装（`qian add identity`，独立宿主可嵌入 Server 或只装 Client 对接任意 OIDC 发行方）；依赖 OpenIddict 7.x（Apache-2.0）、`Kun.Identity.AspNetCore`（Cookie 会话、`IAuthenticatedUser`，修 G3/G4/G5 后）、`Kun.Caching`、`Kun.Audit`。

- 实体：`Person`（标识集合手机/邮箱/用户名、Argon2id 凭据与历史哈希、MFA 因子、状态）、`Membership`（person × tenant、租户内账号、显示名、状态、是否租户管理员、`profile` 预留）、`ExternalIdentity`（唯一键 tenant × connector × subject）、`Connector`、`Client`（类型 机密/公开/机器，redirectUris、scopes、resource、归属）、`Session`（sid、person、membership、设备、端类型、IP、过期、identity_revision、refresh 族）、`LoginAudit`。
- 令牌 claim：`sub`（person）、`tenant_id`、`membership_id`、`scope`、`identity_revision`、`sid`、`amr`/`acr`、预留 `profile`、`act`；access token 15 分钟、refresh 轮换 + 复用检测；每环境一个规范 issuer。
- 登录流水线 `ISignInPipeline`：所有方式构造同一 `LoginInput` 走同一签发路径（统一审计、限流、单端策略）；错误次数阈值 → 锁定或延时（策略）；初始密码强制修改不可取消；命中多个 Membership → `selection_token`（5 分钟）→ 换正式令牌；切换租户 = 以选择令牌重签。
- 会话中心：PG 持久 + Redis 索引 `sessions:{person}`；失效触发：改密、停用、锁定、租户冻结/到期、强制下线、权限变更；吊销后经 7.13 实时通道推 `logout`；日志只记 sid 哈希。
- 失效传播：`identity_revision` 递增 → 网关 `ForwardedClaimTypes` 白名单转发 → 服务端比对缓存的权限快照（7.5）。
- API 之外的管理面：`GET /api/identity/sessions?person=`、`POST /sessions/{sid}:revoke`、`POST /persons/{id}:revoke-all`、在线用户列表（平台/租户两视角）。
- 安全预算：OIDC 一致性测试套件、渗透测试、依赖补丁节奏写进 S1 计划（K 基线风险 1）。

过渡态（2026-10）：Kun 只有依赖方（RP）一侧且链路未打通（不发 `code_verifier`、不校验 nonce、无 refresh/client_credentials、无登录端点、无按用户吊销、日志打印明文 sid）；团队现用 Authentik 2024.10 只做员工认证。Qian 用 OpenIddict 自建，Kun.Identity 保留 Cookie 会话与 `IAuthenticatedUser` 抽象并按 7.1 第 6 项修复。

**插件化定位**。依赖 7.3 租户目录（Membership 外键、租户状态）、7.8 配置、7.11 审计、7.12 通知（短信 OTP、锁定通知、密码到期提醒）、7.13 实时推送（可降级为下次请求失效）；向外提供 `IIdentityConnector` SPI（7.4.2）、`IAuthenticatorFactor` SPI、`ICurrentUser`/`IAuthenticatedUser`、事件 `identity.login/logout/password_changed/locked/session_revoked.v1`。

**Agent 契约**。OIDC discovery / RFC 8414 元数据；受保护资源元数据（RFC 9728）S3；客户端类型枚举预留 `agent`；`act` 委托链与 `profile` claim 位置 S1 预留不启用；`Check / ListAllowedActions / Explain` 以工具形式暴露在 7.5。危险操作（重置他人密码、批量强制下线）标 `humanOnly`。

**验收标准**
- [ ] IT：OpenIddict 一致性测试（授权码 + PKCE、refresh 轮换、client_credentials、revocation、back-channel logout）全部通过；refresh 令牌复用被检测并吊销整族。
- [ ] E2E：同一手机号在租户 A、B 各有 Membership，密码登录后出现租户选择，选 A 后令牌 `tenant_id = A`；在外壳切换到 B 时令牌重签且 `membership_id` 变化。
- [ ] IT：改密后该 Person 全部会话 ≤ 5s 内失效；管理员强制下线某会话后其 refresh 被拒。
- [ ] IT：并发上限 5 时第 6 次登录踢掉最早会话并发出 `session_revoked` 事件；单端模式下 PC 后登录踢 PC 先登录、不影响移动端。
- [ ] IT：密码错误达到阈值触发锁定，锁定通知经 7.12 发出；初始密码账号登录后被强制跳转改密且不可跳过。
- [ ] 代码审查 + IT：令牌中不含任何权限码；日志中无明文 sid、无密码。
- [ ] E2E：TOTP 绑定与校验、短信 OTP 登录（经 7.12 短信 Provider）在 staging 走通。

#### 7.4.2 qian.identity.connector.* 上游连接器 SPI 与首批连接器（P0 · S1）

**设计理念**。连接器只负责「跳转/挑战 → 拿回外部身份断言 → 标准化 `ExternalAssertion`」，绑定、JIT 建号、合并一律归 `qian.identity` 本体（Definition 的 seam 之内）；连接器天然是为该 seam 提供 Provider 的组件，每家一个。补充裁决：S1 必做通用 OIDC + 企业微信（扫码与工作台免登），钉钉、飞书 S2 必做，ISV 第三方应用模式 S3。

**范围与边界**。✅ 做：`IIdentityConnector` SPI 与 `connector.manifest`（协议、支持终端 PC/移动/工作台、配置 JSON Schema、密钥字段只填 secretRef、回调模板）；`qian.identity.connector.oidc`（平台级一个企业 IdP/Authentik 作上游）；`qian.identity.connector.wecom`（网页扫码 + 工作台 UA 识别免登，code 换 userId，多租户先反查 openid 绑定的租户）；绑定流程（未绑定 → 引导登录并显式确认）。❌ 不做：钉钉、飞书（S2 P0）；微信网站扫码（S2 P1，R4）；微信公众号/小程序（S3 可选）；SAML/CAS/LDAP（S3，先代理 IdP 过渡）；每租户多连接 + HRD（S3）；目录同步（S3，事件契约由 7.4.3 预留）。

**技术落点**。连接器为独立组件 `qian.identity.connector.oidc` / `.wecom`：`connector` 在 v2 不是 `runtime.form`，而是「为 `ctx.Identity` seam 提供 Provider 的组件」——清单以 `provides[]` 声明所实现的 `IIdentityConnector` SPI、以 `policy.egress` 声明出网域名（R31）；Provider 包 `Qian.Identity.Connector.Oidc` / `.WeCom` 只依赖 `Qian.Identity.Abstractions`，不依赖 `Qian.Identity.Server` 实现；默认进 `identity` profile 的组合包；平台级启停走 profile patch，租户级启停 = 在租户 scope 挂载 / 不挂载其配置条目（`scopes: [tenant]`，ADR-020 D20.5），被过滤的连接器在登录页与回调路由两处与不存在无法区分。接口：`IIdentityConnector { ConnectorDescriptor Descriptor; Task<ChallengeResult> BeginChallengeAsync(ctx); Task<ExternalAssertion> HandleCallbackAsync(ctx); Task<ExternalAssertion?> ExchangeImplicitAsync(code, ctx) }`，`ExternalAssertion { issuer, subject, unionId?, profile, amr[] }`。回调路由 `/connect/callback/{connectorId}` 由 `qian.identity` 统一校验 state/nonce。企微 SDK 凭据来自 7.8 secretRef；不照搬参考平台的 JWT 自动续签头与 query token。

**插件化定位**。依赖 7.4.1 的绑定与签发、7.3 租户级连接器配置、7.11 审计；向外是 `qian.identity.connector/v1` 扩展点的首批实现。

**Agent 契约**。连接器清单（协议、终端、配置 Schema）机器可读；连接器不暴露工具。

**验收标准**
- [ ] E2E：以 Authentik（运维侧）为上游，经通用 OIDC 连接器登录 Qian；首次登录进入「确认绑定」页，确认后 `ExternalIdentity` 建立，同租户内重复绑定被拒。
- [ ] E2E：企业微信扫码登录与企微工作台内免登在 staging 走通；同一企微账号在两个租户分别绑定不同 Membership。
- [ ] TCK：用一个假连接器实现 SPI，连接器清单校验、state 篡改拒绝、回调幂等三项通过。
- [ ] IT：在租户 A scope 挂载企微连接器、B 不挂载：A 的登录页出现企微入口，B 不出现且直接请求其回调路由返回与「连接器不存在」相同的错误；挂载 / 卸载经 scope patch 完成，不重启 `identity` profile。

#### 7.4.3 组织目录最小集（P0 · S1）

**设计理念**。组织树、岗位、用户档案是 RBAC 主体、审批人解析与数据权限变量的基础主体；ADR-012 要求「组织模型含岗位/责任岗位/组织类型与上级查询契约」在 S1 落地。档案与凭据分离：HR 字段不进平台插件。S1 把目录作为 `qian.identity` 的子模块交付，不独立成插件（ADR-003 已裁决，R1；见章末待确认第 2 条）。

**范围与边界**。✅ 做：单棵组织树（类型为租户可配字典，层级上限默认 9，路径冗余，主管字段，子树有人则拒删）；岗位树（默认岗位、责任岗位标记、责任人）；用户档案 `UserProfile`（1:1 Membership，姓名、拼音、联系方式、扩展 jsonb）；成员多组织多岗位归属 + 主归属；用户状态机 `启用 ⇄ 禁用 → 锁定`，「删除」= 禁用 + 标记；用户组（纯集合，P1）；派生查询契约（直属主管、上级链、部门负责人、`@下属 / @子孙`）；目录变更事件（CloudEvents）；批量操作三态结果。❌ 不做：岗位/角色约束互斥、基数、先决（S3）；Excel 导入向导（S2，框架契约在 7.2.1）；工作交接（S3，扩展点 `IUserAssetHandover` S2 预留）；目录同步企微/钉钉/飞书/SCIM/LDAP（S3）；通讯录页面（S3）。

**技术落点**。表 `organizations`（含 `path`、`org_type`、`manager_membership_id`、`responsible_position_id`）、`org_types`、`positions`、`groups`、`member_relations`（membership × objectType × objectId × isPrimary）、`user_profiles`；API `GET /api/directory/orgs/tree`、`/orgs/{id}/members`、`/members/{id}/manager`、`/members/{id}/superiors`、`/positions/{id}/holders`、`/me/subordinates?depth=`；事件 `directory.org.*`、`directory.position.*`、`directory.member.*.v1`；SCIM 2.0 核心 schema 作为对外契约（S3 实现 Server）。责任人解析契约在 S1 定义、S2 由流程消费。

**插件化定位**。依赖 7.3、7.4.1、7.10；向外提供组织类选择器 API（yue `OrganizeSelect / UserSelect / RoleSelect` 的数据源）与 RBAC 主体来源。

**Agent 契约**。目录查询语义化（找某部门负责人、某岗位所有人、我的上级链），每个操作带 description 与示例；`disable member` 标 `write` + `confirm`；物理删除标 `destructive` + `humanOnly`。

**验收标准**
- [ ] IT：删除含成员的组织返回结构化错误并拒绝；移动组织后 `path` 冗余字段同步更新。
- [ ] IT：`/members/{id}/superiors` 按岗位链返回上级责任人序列；主归属变化后结果随之变化。
- [ ] IT：批量禁用 10 人其中 2 人为租户管理员，返回 `PARTIAL_SUCCESS` + 逐条 items[]。
- [ ] E2E：参考应用用 `OrganizeSelect` 选部门、`UserSelect` 选人，数据来自本租户目录。

### 7.5 qian.authz 鉴权引擎（P0 · S1）

**设计理念**。Kun 侧只新增 `Kun.Authorization` 端口，Qian 自建权限引擎（ADR-006）：权限点由插件清单声明、租户域 RBAC、决策可解释并入审计、权限不进令牌靠 `identity_revision` 失效；UI、API、MCP 三入口同一个判定服务。参考平台接口层权限校验被注释恒 `true`、鉴权散在控制器且可被客户端 `menuId / flowIds` 绕过，是最大的反面教训。判定接口（PDP）是 Definition `Qian.Authz.Abstractions` 暴露的服务键 `ctx.Authz`（再导出 `Kun.Authorization` 端口），强制执行点（PEP）是 base 组合包注册的 `waterfall` 监听器（ADR-020 对 ADR-003 的修订：鉴权、租户、审计三个强制执行点保留为 waterfall 监听器），策略模型、存储、管理 API 在 Provider `Qian.Authz`（P 基线裁决 A-4）。

**范围与边界**。✅ 做：权限点注册表（清单 `provides.permissions` → `PermissionDefinition`，含资源类型、动作、风险级别、是否需 step-up、终端）；资源类型由插件注册；租户域 RBAC（内置角色：平台超管、租户 Owner / Admin / 开发者 / 使用者 + 自定义角色；主体可为用户、组织、岗位、用户组）；插件默认角色模板（策略包，安装即可用）；决策端口 `Check / BatchCheck / ListAllowedActions / ListPermissions / Explain`；API 层强制校验（端点 → 权限点写进 OpenAPI 扩展 `x-qian-permission`）；Mediator 行为 PEP；超管旁路显式声明并审计；决策审计入哈希链；`identity_revision` 失效与按主体缓存；结构化错误码目录；最小角色授权页（P1，菜单/按钮级）。❌ 不做：数据权限编译器（条件树 → SQL/EF 谓词，S2；S1 只定 `IDataPermissionFilter` 端口与 Filter AST 契约，见 7.6）；字段权限（S2，`IFieldPolicy` 端口占位）；资源实例 ACL（S2 可选）；组织角色/逐级递减/身份切换/约束（S3 可选，令牌 `profile` 位 S1 预留）；OpenFGA / OPA 适配（S3 可选）；完整授权向导（S2）。

**技术落点**。组件 `qian.authz`：Definition `Qian.Authz.Abstractions`（服务键 `ctx.Authz`）+ Provider `Qian.Authz`，默认进 `platform` profile 的 base 组合包，可独立安装（`qian add authz`；独立模式下权限点经 `[Permission]` 特性登记，角色与授权用库自带表与 API，ADR-019 D19.4）；schema `authz`；依赖 `Kun.Authorization`（7.1 第 11 项新增端口）、`Kun.Caching`、`Kun.Audit`、7.4.3 目录。

- 表：`permission_definitions`（code、resource_type、action、risk、step_up、plugin_id、terminals）、`resource_types`、`roles`（tenant_id 可空 = 平台角色、code、builtin、source_pack）、`role_assignments`（role × subjectType × subjectId × 有效期）、`grants`（subject × permission/resourceRef × 来源 × 授予人）、`policy_packs`（插件默认角色模板）、`decision_logs`（可配采样，拒绝必记）。
- 权限点命名规范（R9）：`<plugin-id>:<resource>:<action>`，例如 `qian.workflow:flow:launch`、`qian.crm:customer:read`；`plugin-id` 为清单 `id`，`resource` 为该插件注册的资源类型编码，`action` 为动作。清单 `provides.permissions[].resource` 不带插件前缀，由 `qian.authz` 登记时拼接成全名并写入 `permission_definitions.code`；PEP 注解、`defaultRoles[].grant`、`x-qian-permission` 与 scope 目录一律使用全名。
- 求值：有效角色 = 直接授予 ∪ 经组织/岗位/组获得；权限点 = 角色授权并集；`Check(principal, action, resource)` 缺省拒绝；结果缓存键 `(tenant, membership, identity_revision)`，Redis，TTL 10 分钟，revision 变即失效；授权变更 → 发 `authz.grant.changed.v1` → 7.4 递增受影响主体的 `identity_revision` → 7.13 推送刷新。
- PEP：ASP.NET Core `[RequirePermission("qian.sample.crm:customer:read")]`；Mediator `AuthorizationBehavior` 读 `[Capability(RequiredPermission=...)]`；gRPC 拦截器同一入口；S2 的动态数据引擎在唯一入口做「资源 + 动作 + 行级」三段式。
- `Explain(principal, action, resource)` 返回 `{ decision, matchedRoles[], matchedGrants[], missing[], reason }`；错误码目录（`AUTHZ_DENIED`、`CONSTRAINT_MUTEX`、`CARDINALITY_USER_LIMIT`、`PARTIAL_SUCCESS` 等）与多语言文案导出为 JSON。
- 数据权限契约（S1 定、S2 实现）：`DataScopeRule { resource, filter: FilterAST, allData: bool }`；系统变量枚举 `currentUser / currentUserAndSubordinates / currentUserAndAllSubordinates / currentOrg / currentOrgAndChildren / currentOrgAndDescendants / currentPosition / currentPositionAndChildren / currentPositionAndDescendants / currentManagedOrgs / formId / keyword / flowId / taskId / launchUserId`，每个带自然语言描述；合并语义固定「同角色 AND / 跨角色 OR / 任一全部数据则放开」（补充裁决 Q19）。
- API：`POST /api/authz/check`、`/batch-check`、`GET /api/authz/me/permissions`、`/me/allowed-actions?resource=`、`POST /api/authz/explain`、角色与授权 CRUD。

过渡态（2026-10）：Kun 全无鉴权抽象（无 `IAuthorizationHandler`、策略或 `IPermissionChecker`，Mediator 无授权行为，module-map 画的 Auth 行为不存在）；仅 `roles` claim 透传与 `ForwardedClaimTypes` 白名单可用。本插件是 S1 最大单项工作量之一，与 7.4 互为前置，需并行开工并以契约先行。

**插件化定位**。Inject `ctx.Registry`（权限点来源：清单 `provides.permissions`；独立模式下来源是 `[Permission]` 特性）、`ctx.Tenancy`、7.4.3 目录主体、`ctx.Audit`、`ctx.Events`；向外提供 `IPermissionChecker` 实现、`qian.authz.resource-type/v1` 扩展点、`qian.authz.engine/v1`（S3 OpenFGA/OPA 适配位）、事件 `authz.grant.changed.v1`、`authz.role.changed.v1`。

**Agent 契约**。`Check / ListAllowedActions / Explain` 是 S4 首批工具（S1 只落能力元数据）；权限点带风险级别与 step-up；Agent 有效权限 = 委托人权限 ∩ 授予 scope（S4 启用，S1 预留 `act` 处理位）；授权变更操作标 `write` + `confirm`，授予平台超管标 `humanOnly`。

**验收标准**
- [ ] IT：未声明权限点的端点在 `Production` 模式返回 403（默认拒绝）；声明后无权限主体 403、有权限主体 200，且每次拒绝有 `decision_logs` 记录。
- [ ] IT：把角色授予组织 X，X 下成员在 ≤ 3 秒内获得权限；移除后 `identity_revision` 递增、缓存失效，旧令牌请求被服务端拒绝。
- [ ] IT：`Explain` 对被拒请求返回缺少的权限点与命中角色；对放行请求返回命中链路。
- [ ] IT：平台超管访问租户资源被放行且审计记录 `bypass = superadmin`；超管不出现在租户成员列表。
- [ ] TCK：参考应用清单声明资源 `customer` 的动作 `[read, write, export]`（登记为 `qian.sample.crm:customer:<action>`）与默认角色模板，安装后租户 Owner 自动拥有全部、使用者只有 read。
- [ ] UT：Filter AST JSON Schema 与系统变量枚举可被 `GET /api/authz/meta` 读取并通过 2020-12 校验。

### 7.6 qian.data 数据存储默认安全装配（P0 · S1）

**设计理念**。PostgreSQL 16 是唯一一等数据库，默认共享库 + 行级 `tenant_id` + RLS 双保险，平台自身表按 schema-per-service（ADR-004）。Kun 的安全机制都是 opt-in（RLS 拦截器「注册 ≠ 生效」、忘记模型缓存键即跨租户泄漏），Qian 改成**强制装配 + 构建期检查**。动态实体不走 EF 模型（ADR-011，S2）；本插件只服务平台自身表与 `module` 插件的表。

**范围与边界**。✅ 做：`AddQianData<TContext>()` 默认安全装配（RLS 连接版 + 事务版拦截器、`KunMultiTenantModelCacheKeyFactory`、租户与软删过滤、审计字段自动填充、`xmin` 并发令牌、snake_case、Guid v7 主键、Outbox 同 DbContext）；禁用 `IgnoreQueryFilters` 的分析器；多副本迁移协调（PG advisory lock）；分布式 ID；序号服务（前缀 + 日期 + 流水，租户/应用作用域）；基准数据类型与 PG 方言类型映射（数据而非代码）；数据连接档案（凭证只存 secretRef、连通测试、按租户）；Filter AST JSON Schema 与运算符 × 字段类型矩阵（契约）；数据规则策略契约（主键策略、乐观锁、逻辑删、业务主键）；CI 检查「在线 DbContext 必须继承 `QianDbContext`」。❌ 不做：MySQL / 国产库作业务库（本期不做，Q5）；动态表与 SQL 生成（S2 `qian.dynamic-data`）；Schema Migrator（S2）；数据建模 UI（S2）；数据接口（SQL/API）设计器（S2，S1 只定契约在能力目录）；MySQL 方言（P2 以下，不做）；跨库数据同步（不做）；租户自带业务库连接（待 S3 评估）。

**技术落点**。组件 `qian.data`：Definition `Qian.Data.Abstractions`（服务键 `ctx.Data`）+ Provider `Qian.Data`，随 `Qian.Plugin.Sdk` 进入每个 .NET profile（`platform` / `identity` / `gateway` / `standalone`），可独立安装（`qian add data`）；方言为独立组件 `qian.data.dialect.postgres`（Provider `Qian.Data.Dialect.Pg`，方言数据包）；依赖 `Kun.EntityFrameworkCore`、`Kun.Dapper`（读侧可选）、Npgsql。

- `QianDbContext : KunDbContext`：构造期校验拦截器与缓存键工厂已注册，否则抛 `QianDataMisconfiguredException` 拒绝启动；`UseKunSchema("plugin_<id>")` 强制；系统列约定 `id (uuid v7) / tenant_id / created_at / created_by / updated_at / updated_by / deleted_at / deleted_by`，并发令牌 `xmin`；RLS 策略模板 `FORCE ROW LEVEL SECURITY` + `WITH CHECK (tenant_id = current_setting('app.tenant_id')::uuid)`，由迁移生成器为每个 `IMultiTenant` 表自动输出。
- 迁移协调：`MigrateAsync` 前取 `pg_advisory_lock(hash(schema))`，K8s 迁移 Job 先行（7.17），多副本启动时只校验不迁移。
- 分布式 ID：默认 Guid v7；需要数值 id 处用雪花，WorkerId 经 Redis 租约 `qian:snowflake:worker:{n}`（TTL 续租）分配，避免参考平台未配 WorkId 的问题。
- 序号服务 `ISequenceService.NextAsync(ruleCode, scopeKey)`：规则 DSL `prefix + yyyyMMdd + seq(6)`，PG `UPDATE ... RETURNING` 原子取号，规则修改后重新开始；单据序号归属表单（S2）只是它的客户。
- 基准类型 `BaseDataType`（int / bigint / decimal / float / datetime / varchar / text / bool / json / uuid，系统类不可删）、`DbDialect`（postgres）、`DialectTypeMapping`（长度/精度/区间校验）作为种子数据与 `GET /api/data/meta/types` 端点；`StandardField` 缺省字段由基类代码固化（S2 建模器 UI 消费）。
- `DataConnection`（方言、主机、端口、库、schema、账号、`secretRef`、编码）+ `POST /connections/{id}:test`；详情接口不回传密钥。
- Filter AST：`{ logic: and|or, groups: [{ logic, conditions: [{ field, op, value | sysVar }] }] }`，运算符 `eq ne like notLike isNull notNull in notIn ge gt le lt between`，矩阵 `{ fieldType: [op] }` 发布为 JSON Schema；数据过滤、高级查询、数据权限、启用规则、流程条件五处共用，S2 的 SQL 生成只接受参数化 + 标识符白名单。
- 数据事件：实体变更经 Outbox 发 `qian.record.changed.v1` 信封（S2 动态表）与各聚合自定义事件（S1）；服务端 before/after Hook 契约 `IRecordHook`（S1 定、S2 用）。

过渡态（2026-10）：Kun 只有 Shared + RLS 有真库集成测试；事务版 RLS 在事务外读返回空、连接版只对 Npgsql 生效；`CreatedBy` 为非空 `Guid.Empty`、无并发令牌、无命名约定、`IOutboxMessageStore` 无实现、多副本 `MigrateAsync` 无协调——按 7.1 第 1、2、9 项修复后装配。`Kun.Infrastructure` 的 `IRepository/IUnitOfWork` 死接口不引用，直接用 `DbContext` + `Kun.EntityFrameworkCore.IUnitOfWork`。

**插件化定位**。依赖 7.3 租户上下文、7.8 secretRef、7.10 Outbox；向外提供 `QianDbContext` 基类、`ISequenceService`、`IDataConnectionRegistry`、方言 SPI `qian.data.dialect/v1`、Filter AST 契约与 `IRecordHook` 扩展点。

**Agent 契约**。基准类型、方言映射、Filter AST、运算符矩阵、系统变量全部是可查询元数据（S4 Agent 生成查询时作强制过滤合入）；数据连接的凭据永不出现在契约；`connections.test` 标 `external`。

**验收标准**
- [ ] UT：缺少 RLS 拦截器或模型缓存键的 DbContext 在启动时被 `AddQianData` 拒绝；代码中 `IgnoreQueryFilters()` 触发分析器错误。
- [ ] IT：以数据库超级用户直连绕过 EF，在设置 `app.tenant_id = A` 后 `SELECT` 不到租户 B 的行，向 B 插入被 `WITH CHECK` 拒绝（RLS 兜底）。
- [ ] IT：两个副本同时启动并执行迁移，只有一个执行 DDL、另一个等待后校验通过。
- [ ] IT：100 并发取号同一序号规则无重复无跳号；规则修改后从 1 重新开始。
- [ ] IT：雪花 WorkerId 在 3 个副本间互不相同，副本重启后租约回收复用。
- [ ] UT：Filter AST 示例通过 JSON Schema 校验；`between` 用于布尔字段被矩阵拒绝。
- [ ] E2E：参考应用的客户表在租户 A、B 各插入 1000 行，A 的列表查询只返回 A 的数据且 `EXPLAIN` 显示走租户索引。

### 7.7 qian.files 文件存储（P0 · S1）

**设计理念**。文件上传是 S1 退出标准点名的能力。基于 Kun 新线 `Kun.Storage.Abstractions` 的 `IFileStorage`，由 Qian 补齐存储键不感知租户、无配额、无元数据表三个缺口；活跃 Web MIME 强制 `attachment` 的策略**不可关闭**。参考平台匿名上传下载、URL 带登录 token 预览，是反例。

**范围与边界**。✅ 做：`IQianFileService` 封装（租户前缀、配额、上传策略对象、文件元数据表）；预签名上传/下载（短 TTL）；`ContentDispositionPolicy = attachment` 不可关闭；provider 子插件 `qian.files.provider.oss`（现网对象存储）、`.s3`（MinIO/S3 兼容）、`.local`（仅开发与单节点）；分片上传与 MD5 秒传（P1，yue Upload 组件依赖）；文件事件；来源插件与扫描状态字段（扫描本身 S2）。❌ 不做：缩略图、用途目录、图片处理（S2 可选）；文件预览服务与票据（S2，预览提供者接口可替换，补充裁决 Q34）；文件中心网盘 UI（S3）；水印（S3）；旧线 `Kun.Storage`（废弃）。

**技术落点**。组件 `qian.files`：Definition `Qian.Files.Abstractions`（服务键 `ctx.Files`）+ Provider `Qian.Files.Oss` / `Qian.Files.S3` / `Qian.Files.Local`（装载其一；Provider 从本地盘换成 OSS，所有消费者不变——ADR-020 D20.4 的样例 seam），默认进 `platform` profile 的 base 组合包，可独立安装（`qian add files`）；schema `files`；依赖 `Kun.Storage.Abstractions/Oss/S3/Local`、7.3 租户配额、7.8 secretRef、7.11 审计。

- 表 `file_objects`：`id、tenant_id、plugin_id、purpose、name、size、content_type、sha256、storage_key、provider、status (uploading | available | quarantined | deleted)、scan_status、created_*、deleted_*`；`file_uploads`（分片会话，P1）。
- 存储键 `tenants/{tenant_id}/{plugin_id}/{yyyy}/{MM}/{uuid}{ext}`，由服务端生成，客户端不可指定；配额（字节数、文件数）读 7.3 租户配置，超限返回 `QUOTA_EXCEEDED`。
- 上传策略对象 `UploadPolicy { allowedTypes[], maxSize, maxCount, purpose }` 由插件声明（清单 `contributes: qian.files.upload-policy/v1`）；MIME 由服务端嗅探，不信任客户端声明。
- API：`POST /api/files/uploads:presign`（返回 PUT URL，TTL ≤ 15 分钟）、`POST /api/files/{id}:complete`（校验 sha256 与大小）、`GET /api/files/{id}/download-url`（TTL ≤ 5 分钟，经 7.5 鉴权）、`DELETE /api/files/{id}`（软删 + 保留期后物理删）；分片：`POST /uploads:init`、`PUT /uploads/{id}/parts/{n}`、`POST /uploads/{id}:complete`、`GET /uploads:lookup?sha256=`（秒传）。
- 事件 `files.object.created.v1`、`files.object.deleted.v1`；审计记录下载。
- Provider 选择 = profile patch 替换 `qian.files` 条目的 `provider` 配置（平台级）或租户 scope patch（S3 开放），切换不影响已有键。

过渡态（2026-10）：Kun 新线 7 操作 + 预签名 + 上传约束已可用，`OpenReadAsync` 已补；旧线与新线同名 `IFileStorage` 并存且命名空间互反，Qian 用 Banned API 分析器禁用旧线。

**插件化定位**。依赖租户、配置、鉴权、审计、事件；向外提供 `IQianFileService`、`qian.files.provider/v1` 扩展点、`qian.files.upload-policy/v1` 扩展点；yue `Upload` 组件消费其分片契约。

**Agent 契约**。上传与下载 URL 签发是能力 `files.presign-upload`（`write`）与 `files.download-url`（`read`），文件内容本身不经 Agent；`files.delete` 标 `destructive` + `confirm`。

**验收标准**
- [ ] IT：租户 A 的下载 URL 对 B 的文件 id 返回 404；任何文件的 `storage_key` 都以 `tenants/{tenant_id}/` 开头。
- [ ] IT：上传 `text/html` 文件后下载响应头为 `Content-Disposition: attachment`，且无配置项可改变。
- [ ] IT：配额 10 MB 时第 11 MB 上传返回 `QUOTA_EXCEEDED`；软删后配额释放。
- [ ] IT：预签名 URL 过期后 PUT 失败；`complete` 时 sha256 不符返回错误并标记 `quarantined`。
- [ ] E2E：yue `Upload` 以分片 + MD5 上传 200 MB 文件至 OSS provider，秒传命中返回已有 id（P1）。
- [ ] E2E：空白 ASP.NET Core 项目 `qian add files`（独立模式，显式 `SingleTenant`）后按 README 完成上传与下载 URL 签发；同一组包在 `platform` profile 下 TCK 两种模式都过（ADR-019 D19.8）。

### 7.8 qian.config 配置中心（P0 · S1）

**设计理念**。Apollo 是硬性要求，按 ADR-007：Apollo 作 L1 共享组件（需新增 MySQL），自研 AOT 友好的 .NET provider，只存平台/服务级非敏感配置与密钥引用（secretRef）；租户级配置不进 Apollo（无租户概念、namespace 有上限、灰度只按实例），由 7.3 租户配置服务承载；Nacos 收窄为 AI 资产注册。插件配置 Schema 化，设置页按 Schema 渲染，敏感值只能填 secretRef——参考平台 100+ 键扁平堆放、密钥与参数同表，是反例。

**范围与边界**。✅ 做：`Qian.Configuration.Apollo` provider（公开 HTTP 协议：`/configs`、`/notifications/v2` 长轮询、AccessKey HMAC-SHA1 签名、本地缓存文件、集群内直连 ConfigService）；配置源顺序 appsettings → Apollo（`qian.common.*` → `qian.plugin.<id>` → `application`）→ K8s Secret 文件（KeyPerFile）→ 环境变量 → 命令行；热更新「先校验、失败保留旧值并告警」；`ISecretResolver`（`secretRef://k8s/<ns>/<name>#<key>`；Infisical 后端 P2）；插件 `config.schema` → Apollo namespace 骨架与校验器、options JSON Schema 导出；特性开关 `IFeatureFlags`（OpenFeature 式，`targetingKey = tenantId`，与授权分离）；Apollo 发布事件进审计；疑似密钥键名/值扫描拦截。❌ 不做：官方 Apollo .NET 客户端（维护停滞、反射式序列化不适配 AOT/裁剪、无缓存时以空配置继续；只作 PoC 对照）；租户级配置进 Apollo；控制台渲染插件配置页（S2）；配置灰度/审核/回滚编排（S3 可选）；Nacos 作配置中心（不做）。

**技术落点**。组件 `qian.config`：Definition `Qian.Config.Abstractions`（服务键 `ctx.Config`）+ Provider `Qian.Config.Apollo`（独立模式可只用标准 `IConfiguration`，Apollo 可选），随 `Qian.Plugin.Sdk` 进入每个 .NET profile，可独立安装（`qian add config`）；`dump-config` 打印的每条目 `config` 即按本组件登记的 `config.schema` 校验（7.2.1）；依赖 `Kun.Configuration`（`AddKunOptions<T>` 校验器，7.1 第 10 项增强）。

- provider：`AddApollo(options)` 以 `Apollo__AppId/Env/Cluster/ConfigServer__0/Label/CacheDir/AccessKey(secretRef)` 引导（Kustomize 只注入这几项）；长轮询 hold 60s 对应 HttpClient 超时 > 60s；启动时无网络且无本地缓存 → 关键配置缺失则**启动失败**，不以空配置继续；失败降级矩阵 L0–L4 写进运维手册。
- 映射：AppId = 一个微服务/后端插件宿主；Environment 只建 UAT + PRO；Cluster 默认 `default`；私有 namespace `application` 与 `qian.plugin.<id>`；公共 namespace `qian.common.*`（如 `qian.common.trust`、`qian.common.flags`）。
- 热更新：`ValidatedOptionsMonitor<T>` 包装 `IOptionsMonitor<T>`，变更先跑校验器，失败保留上一份并计数 `qian_config_reload_failed_total`、发 `config.reload.failed.v1`。
- Schema：`config.schema`（JSON Schema 2020-12，`sensitive: true` 字段只能是 secretRef）→ CI 生成 namespace 骨架并比对线上键；`GET /api/config/schemas/{plugin}` 对外可读（值不可读除非有权限且非敏感）。
- 特性开关：namespace `qian.common.flags`，键 `flag.<name>` 值为规则 JSON（默认、租户白名单/百分比）；`IFeatureFlags.IsEnabled(name, tenantId)`；授权（7.14）与开关分开存（ADR-014）。`IFeatureFlags` 只管灰度（按租户百分比 / 白名单渐进放量）；「租户是否启用某组件」不再用开关表达，而是租户 scope 是否挂载该组件的配置条目（ADR-020 D20.5、7.2.1）。
- 分工表（写进开发手册）：部署级引导项 → Kustomize；平台/服务级非敏感 → Apollo；密钥 → K8s Secret（Infisical 待运行时注入接通）；租户级在线设置 → 7.3 `ITenantSettings`；前端运行时配置 → `window.__APP_CONF__` ConfigMap。

过渡态（2026-10）：Kun `Kun.Configuration` 无任何配置源；现网无 Apollo、无 MySQL；Nacos 3.0.1 standalone 只作 AI Registry PoC 且边界 ADR 曾把动态配置权威划给 Nacos——需先修订边界 ADR（ADR-007 已裁决）。Apollo 版本 ≥ 2.5.2（3.0.0 刚发布，生产等首个补丁）；部署见 7.17。

**插件化定位**。Inject `ctx.Registry`（清单 `config.schema`；独立模式缺席时只用 `IConfiguration`）、`ctx.Audit`；向外提供 `IConfigurationProvider`、`ISecretResolver`、`IFeatureFlags`、事件 `config.reload.failed.v1`；7.3 租户配置服务与之形成「平台默认 / 租户覆盖」两层。

**Agent 契约**。配置 Schema 与非敏感当前值可读，写入走审批（S3）；Agent 不能经手明文密钥（`secretRef` 只是名字）；`flags.set` 标 `write` + `confirm`。

**验收标准**
- [ ] IT：在 Apollo 修改 `qian.plugin.sample.pageSize` 后，宿主 ≤ 60s 内 `IOptionsMonitor` 值更新且不重启。
- [ ] IT：发布一个不满足 `[Range]` 的值，宿主保留旧值、指标 +1、告警事件发出；修正后恢复。
- [ ] IT：Apollo 不可达且本地缓存存在时宿主用缓存启动；缓存也不存在时关键配置缺失导致启动失败并打印明确原因。
- [ ] UT：`config.schema` 中标 `sensitive` 的键若在 Apollo 填了非 `secretRef://` 值，CI 门禁失败。
- [ ] IT：`IFeatureFlags` 对白名单租户返回 true、其他租户 false；与 7.14 授权结果、租户 scope 挂载状态互不影响。
- [ ] E2E：Apollo Portal 经 Authentik OIDC 登录，不公网；配置发布出现在 7.11 审计。

### 7.9 qian.gateway 业务 API 网关（P0 · S1）

**设计理念**。三层网关（ADR-010）：Traefik 北向公网入口不做鉴权 → `qian-gateway`（Kun.Gateway 以库方式引用并加固）是业务 API 边界，把外部用户令牌换成短时 S2S 令牌下行 → Higress 只管模型与 MCP 流量、对租户无感。Kun 网关的默认装配不可上线（不强制认证、限流未挂路由、信任任意 `X-Forwarded-For`、换私钥须重启），Qian 把安全默认值写成强制项。

**范围与边界**。✅ 做：强制认证（回退策略 = 已认证，匿名白名单显式枚举）；路由级限流（租户/用户/IP 分区；多 Pod 共享计数 Redis `PartitionedRateLimiter` P1）；可信代理网段；签名私钥热轮换（双钥重叠）；只信任 `qian-identity` 一个 issuer；Cookie 会话换 S2S（`IS2sPrincipalResolver`：sid → Redis 会话 → 铸 S2S）；per-service audience（按 cluster）；S2S claim 对齐 iClaw 契约、`exp − iat ≤ 300s`（网关用 60s）；`ForwardedClaimTypes` 白名单含 `tenant_id / membership_id / identity_revision / scope / session_id`；`tenant × 组件` 可用性检查（= 7.14 权益「买没买」∧ 租户 scope 已挂载「开没开」，后者读 `qian.registry` 的 scope 视图，7.2.1）；路由与集群片段由插件清单生成（S1 构建期静态）；OpenAPI 聚合端点与能力目录端点代理；`/healthz` `/readyz`；OTel；gRPC-Web。❌ 不做：动态 `IProxyConfigProvider`（租户 × 已安装应用，S3）；按租户选 IdP 多 issuer（S3）；网关级 OPA/ABAC（S3 可选）；把 SSO/S2S 交给 Higress（不做，ADR-015）；接口体 AES 加密中间件（不做，本质是混淆）。

**技术落点**。组件 `qian.gateway`：Definition `Qian.Gateway.Abstractions` + Provider `Qian.Gateway`，默认进 `gateway` profile（独立 Pod，可 AOT 发布——边缘 profile 可 AOT、`platform` 不承诺 AOT，ADR-020 D20.10），可独立安装（`qian add gateway`）；依赖 `Kun.Gateway`（YARP、`S2sTokenIssuer`、JWKS、三种限流策略、`IS2sPrincipalResolver`、gRPC-Web）、`Kun.Security`、`Kun.Caching`、`Kun.Observability`。

- 认证：外部 Bearer（`qian-identity` JWKS）或 Cookie `qian.sid`；两者统一解析为主体后由 `S2sTokenIssuer` 签 60s S2S（RS256，`kid` 轮换），`aud` = 目标 cluster 的服务标识（`qian-platform`、`qian-identity`、`<service-plugin-id>`）。下游各跳重新验签，禁止令牌透传。
- 路由：`qian pack` 从 `plugin.json`（`runtime.form: service` 的协议声明与 `module` 的 API 前缀）生成 YARP `routes/clusters` 片段，构建期合并；每条路由带 `authorizationPolicy`、`rateLimiterPolicy`、`requiredScopes`、`pluginId`。`PluginEnabledTransform` 读 7.14 权益缓存与租户 scope 视图，未授权、未挂载或被 restrict 一律返回 404（与不存在的路由无法区分，不泄露组件是否存在于其他租户，ADR-020 D20.5 / R32）；可挂载原因只在管理面 `/api/registry/*` 对持有 `qian.registry:scope:read` 的调用者以 403 `plugin_not_mounted` 返回。
- 限流：`kun-default`（匿名 IP）、`tenant`、`user` 三策略真正绑定；Redis 共享计数（P1）；429 带 `Retry-After`。
- 可信代理：`ForwardedHeaders.KnownNetworks` 只含 Traefik Pod 网段；Traefik `forwardedHeaders.trustedIPs` 必须配置，否则 OIDC issuer 变 http（现网教训）。
- 机器身份：`client_credentials` 由 7.4 签发，网关对服务主体同样换签 S2S（`sub = client_id`，`principal_kind = service`）；`/s2s/issue` 风格端点不单独提供。
- 聚合：`GET /api/openapi.json`（需认证，汇总各宿主 `/openapi.json`）、`GET /api/capabilities`（代理到 7.2.1 `qian.capabilities`；v1 别名 `/api/kernel/capabilities` S1 内保留并带 `Deprecation` 头，R33）。
- 健康：`/healthz` 进程存活；`/readyz` 含 JWKS 可取、Redis 可达、上游 cluster 至少一个健康。

过渡态（2026-10）：Kun.Gateway 可执行亦可作库，但默认不强制认证、`kun-default` 未挂路由、IP 分区信任 XFF、私钥只读一次、无动态 provider、宿主未接 OTel；三套 S2S 签发器并存——按 7.1 第 4、7 项修复后装配。借鉴 iClaw `UpstreamS2STokenFactory` 的 per-service audience 逻辑（只借鉴思路）。

**插件化定位**。Inject 7.4 JWKS、`ctx.Entitlement`（7.14 授权缓存）、`ctx.Registry` scope 视图与 `ctx.Capabilities`（7.2.1）、`ctx.Config`、7.11.2 观测；向外提供路由片段生成器（供 `qian pack`）、`IS2sPrincipalResolver` 扩展位、S3 的动态路由 provider 扩展点 `qian.gateway.route-provider/v1`。

**Agent 契约**。S4 的 Agent 不直连业务 URL，一律经 Higress MCP 路由到 `qian-gateway`；S1 保证令牌受众绑定、禁透传、最小 scope + `403 insufficient_scope` 逐步提权三件事就位。网关自身不暴露工具。

**验收标准**
- [ ] E2E：未带凭据访问 `/api/tenancy/tenants` 返回 401；带外壳 Cookie 访问同一路由，下游收到 60s S2S 且 `aud = qian-platform`、`tenant_id` 正确。
- [ ] IT：S2S 令牌通过 iClaw `s2s-jwt-claims` Schema 校验；`exp − iat ≤ 300s`。
- [ ] IT：对限流路由以同一租户 200 rps 压测，超阈值返回 429；两个网关副本共享计数（P1）。
- [ ] IT：轮换私钥期间（双钥重叠 10 分钟）在途请求零失败；重叠期后旧 `kid` 被拒绝。
- [ ] E2E：租户 B 的 scope 未挂载参考应用时访问其路由 404，响应与访问不存在的路由完全相同（R32）；对 B 写一条租户 scope patch 挂载后 ≤ 5s 放行，网关不重启。
- [ ] E2E：`/readyz` 在 Redis 不可达时返回 503，K8s 停止向其导流。

### 7.10 qian.events 事件总线（P0 · S1）

**设计理念**。RabbitMQ 上 K3s，Outbox 修复后装配，至少一次语义 + Redis 幂等（ADR-008）；事件信封对齐 iClaw 契约（`version / event_id / subject / occurred_at / tenant_id / payload`），事件目录（稳定 ID + JSON Schema）升格为能力目录的一部分。参考平台 RabbitMQ 单队列、非持久化、先 ack 后处理、订阅端吞异常，是反例。

**范围与边界**。✅ 做：Outbox 同事务写入与可靠发布（`FOR UPDATE SKIP LOCKED` / Redis 锁默认开、publisher confirm）；消费端至少一次（处理成功后 ack）、指数退避、DLX 死信；Redis 幂等表（`processed:{consumer}:{event_id}`，TTL 7 天）+ PG `processed_messages` 兜底；租户回灌（信封 `tenant_id` → `BeginScope`）；CloudEvents 风格信封；事件目录 `GET /api/capabilities/events` 与 JSON Schema 导出；竞争消费（每服务一队列）与广播（每实例一队列）两种订阅；主题 ACL 由清单 `provides.events.publish/subscribe` 生成（RabbitMQ 权限，P1）；`IOutboxWriter` 面向 `NpgsqlTransaction` 的实现（S2 动态表同事务，契约 S1 定）。❌ 不做：Kafka（不选）；跨产品事件总线（与 iClaw 各用各的 vhost，信封同形）；事件回放/事件溯源存储（不做）；Saga 编排（P2，见 7.1）。

**技术落点**。组件 `qian.events`：Definition `Qian.Events.Abstractions`（服务键 `ctx.Events`）+ Provider `Qian.Events`（RabbitMQ），随 `Qian.Plugin.Sdk` 进入每个 .NET profile，可独立安装（`qian add events`，独立模式 Outbox 用宿主自己的 DbContext）。分界：`ctx.Events` 是跨进程的集成事件总线；进程内的插件扩展事件（`emit` / `waterfall` / `parallel` / `serial` / `bail`）由 `Qian.Runtime` 自带（7.2.1），拦截与策略用运行时事件，跨服务通知用集成事件，两者不混用。依赖 `Kun.EventBus`、`Kun.EventBus.RabbitMQ`、`Kun.EntityFrameworkCore` Outbox 表、`Kun.Caching`、工具 `Kun.IntegrationEvents.SchemaGen`。

- 信封 `QianEventEnvelope { version: "v1", event_id (uuid v7), subject: "<domain>.<entity>.<verb>", type: "<id>.v<N>", occurred_at, tenant_id, producer, correlation_id?, causation_id?, actor { kind, id, on_behalf_of? }, trace_parent, payload }`；`[IntegrationEventId("crm.customer.created.v1")]` 稳定 ID；payload JSON Schema 由源生成 `JsonTypeInfo` 导出。
- 发布：`IIntegrationEventBus.Publish` 只允许在命令 handler 同 scope（分析器）；Outbox 发布器轮询 `outbox_messages`，批 100，`FOR UPDATE SKIP LOCKED`，confirm 后标记；失败重试 `RetryBaseDelay` 指数退避。
- 消费：`Subscribe<TEvent, THandler>()`；手动 ack；重试 5 次后进 `qian.dlx`；幂等装饰器先查 Redis 再执行，成功后写入；`tenant_id` 回灌与 baggage 注入在订阅器统一完成。
- 拓扑：exchange `qian.events`（topic），routing key = subject；每服务队列 `q.<service>.<subscription>`（quorum queue）；广播用 `q.<service>.<instance>` 自动删除队列。
- 目录：`GET /api/capabilities/events` 返回每事件 ID、版本、Schema、发布插件、订阅者（按权限过滤）；AsyncAPI 描述随 `qian pack` 导出。

过渡态（2026-10）：现网 K3s 上没有 RabbitMQ（Outbox 空转）；Kun 消费端 at-most-once、消费侧不回灌租户、`IOutboxMessageStore` 无实现、`RetryBaseDelay` 未实现、无 publisher confirm——按 7.1 第 2 项修复；领域事件→集成事件路径丢消息先用最小集成测试实证。RabbitMQ 部署见 7.17。

**插件化定位**。依赖 7.3 租户、7.6 Outbox 表、7.11 审计（审计事件经同一总线）、7.12 观测；向外提供 `IIntegrationEventBus`、`IOutboxWriter`、事件目录与 Schema、主题 ACL 生成器。所有平台插件的 `*.v1` 集成事件都走它。

**Agent 契约**。事件契约化（稳定 ID + Schema）为 S4 Agent 触发器预留；事件目录按权限过滤；信封 `actor.kind` 支持 `agent`。订阅管理（S4）标 `write` + `confirm`。

**验收标准**
- [ ] IT：参考应用创建客户后 `outbox_messages` +1，RabbitMQ 收到 1 条，消费端 handler 内租户上下文为该租户。
- [ ] IT：消费端前两次抛异常、第三次成功，`processed_messages` 恰一行；第 6 次仍失败进 DLX 并发告警。
- [ ] IT：杀掉发布器副本后另一副本接管，无重复发布（confirm 计数 = Outbox 行数）。
- [ ] IT：同一事件重投两次，幂等装饰器拦截第二次，handler 只执行一次。
- [ ] UT：信封通过 iClaw `events-schema.json` 校验；每个 `[IntegrationEventId]` 都能导出 JSON Schema。
- [ ] TCK：清单未声明 `publish` 的事件被运行时拒绝发布（P1，主题 ACL）。

### 7.11 qian.audit 与 qian.observability 审计与可观测（P0 · S1）

#### 7.11.1 qian.audit 审计（P0 · S1）

**设计理念**。只留一套（ADR-009）：命令/能力级 `AuditEntry` 哈希链 + PostgreSQL 持久化链状态；实体级 `AuditLog` 走同事务拦截器（借 iClaw 写法，补充裁决 Q40 允许搬该拦截器）并经 Outbox 发事件；弃用事后不同事务的拦截器路径。审计是一等公民且防篡改：主体链（actor、`on_behalf_of`）、能力 ID、契约版本、输入哈希、来源通道，为 S4 回答「哪个 Agent 以谁的名义做了什么」。参考平台靠开发者加特性才记录、请求日志落库可一键清空，不沿用。

**范围与边界**。✅ 做：命令级哈希链（按租户一条链 + 平台链）与 PG `IAuditChainState`；实体级同事务 `AuditLog`（字段级差异）；`actor_type ∈ user | service | agent`、`on_behalf_of`、`capability_id`、`contract_version`、`input_hash`、`channel ∈ ui | api | mcp | a2a | job | event`；登录审计、操作审计（由能力契约 `audit: true` 自动记录，命令默认开、查询默认关）、决策审计（7.5）、生命周期审计（7.2）、配置发布审计（7.8）；`IAuditReader`（平台/租户两视角）；保留策略；链校验作业（P1）。❌ 不做：请求/异常日志落业务库与一键清空（进可观测栈）；审计看板（S3）；跨产品共享审计存储（与 iClaw 只共享事件形状与 `trace_id`）。

**技术落点**。组件 `qian.audit`：Definition `Qian.Audit.Abstractions`（服务键 `ctx.Audit`）+ Provider `Qian.Audit`，随 `Qian.Plugin.Sdk` 进入每个 .NET profile，可独立安装（`qian add audit`）；schema `audit`；依赖 `Kun.Audit`（`AuditEntry { PrevHash, ContentHash }`、`IAuditSink`、`IAuditChainState` 原语）、`Kun.Auditing` 同事务 Outbox 路径、`Kun.EntityFrameworkCore`。

- 表 `audit_entries`（tenant_id、seq、prev_hash、content_hash UNIQUE、actor_type、actor_id、on_behalf_of、capability_id、contract_version、input_hash、channel、resource、result、trace_id、occurred_at、payload jsonb）、`audit_chain_state`（tenant_id、tip_hash、tip_seq，可串行化更新）、`audit_logs`（entity、key、op、diff jsonb、actor、trace_id、tx_id）。
- 哈希：`content_hash = SHA256(prev_hash || canonical_json(entry))`；追加在 `SERIALIZABLE` 事务读 tip 并更新；冲突重试；链校验作业重放全链比对。
- 实体级：`QianAuditSaveChangesInterceptor` 在 `SavingChangesAsync` 阶段写 `audit_logs` 并投递 `qian.audit.record.v1` 到 Outbox，与业务同事务提交。
- 行为：Mediator `AuditBehavior` 读 `[Capability(Audit = true)]`，命令默认记录，查询可 opt-in；记录输入哈希而非全量入参（敏感字段按 Schema `sensitive` 脱敏）。在运行时层面它是 base 组合包注册的 `waterfall` 监听器（ADR-020 对 ADR-003 的修订：审计强制执行点保留为 waterfall 监听器），卸载组件即逆序撤销监听。
- 读侧：`GET /api/audit/entries?tenant=&actor=&capability=&from=&to=`、`GET /api/audit/logs?entity=&key=`；租户视角只见本租户，平台视角可跨租户但本身被审计。

过渡态（2026-10）：Kun 四条审计路径并行，哈希链只有 InMemory 实现、`IAuditReader` 无实现、`TenantId/UserId` 在 `Kun.Auditing` 写死 null、无 `agent` actor——按 7.1 第 8 项收敛。

**插件化定位**。依赖 7.6 事务、7.10 Outbox、7.3 租户、7.4 主体；向外提供 `IAuditWriter`、`IAuditReader`、审计事件 Schema，所有平台插件的审计经它写入。

**Agent 契约**。审计事件 Schema 公开；`channel = mcp` 与 `actor_type = agent` 字段 S1 就位；审计查询 `audit.query` 是 S4 工具（`read`）；不提供删除审计的能力。

**验收标准**
- [ ] IT：参考应用一次「创建客户」命令产生 1 条 `audit_entries`（`capability_id = crm.customer.create`）与 1 条 `audit_logs`（字段级 diff），二者 `trace_id` 相同；事务回滚时两者都不存在。
- [ ] IT：篡改任一历史 `audit_entries.payload` 后链校验作业报告断链位置。
- [ ] IT：两个副本并发写同一租户链 1000 条，`seq` 连续、`prev_hash` 严格串联。
- [ ] IT：以 `actor_type = agent, on_behalf_of = <user>` 记录一条审计并可按 `on_behalf_of` 查询。
- [ ] 代码审查：仓库内不存在对 `Kun.Audit.EntityFrameworkCore.AuditSaveChangesInterceptor` 的引用。

#### 7.11.2 qian.observability 可观测与计量（P0 · S1）

**设计理念**。零配置获得 trace/metrics/logs，且追踪与计量贯通租户、用户、插件、主体类型；计量表从第一天带租户/项目双键（iClaw 的教训：旧成本表无 `tenant_id` 导致无法按租户出账）。

**范围与边界**。✅ 做：`AddQianObservability()`（`Kun.Observability` 一行装配 + 网关宿主接入）；baggage `tenant.id / user.id / membership.id / plugin.id / principal.kind` 在租户中间件、消息订阅器、作业执行器三处写入，`agent.id / run.id` 预留；计量事件 `qian.meter.v1`（tenant_id、project_id?、plugin_id、principal_kind、capability_id、unit、quantity）经 Outbox 落 `meter_events`（append-only）；健康检查实现（PG / Redis / RabbitMQ / Apollo）；标准仪表盘与 SLO 指标（P1）；OTel 语义约定文档。❌ 不做：自研服务器监控（多副本下单机指标无意义，用 LGTM）；Alertmanager 部署（P2，见 7.17）；计费（S3，`qian.billing` 消费 `meter_events`）；以 OTel GenAI 属性名作持久化 Schema（仍为 Development 级）。

**技术落点**。组件 `qian.observability`：Definition `Qian.Observability.Abstractions` + Provider `Qian.Observability`，随 `Qian.Plugin.Sdk` 进入每个 .NET profile，可独立安装（`qian add observability`）；依赖 `Kun.Observability`（预注册 4 个 ActivitySource、baggage 键定义）、OTel Collector → LGTM（Prometheus/Grafana/Loki/Tempo）。`traceparent` 贯通 HTTP、gRPC、RabbitMQ 头与作业参数；日志结构化（`tenant_id`、`trace_id` 字段）；指标前缀 `qian_`；`/readyz` 聚合 `IHealthCheck` 实现 `PgHealthCheck / RedisHealthCheck / RabbitMqHealthCheck / ApolloHealthCheck`。

过渡态（2026-10）：Kun baggage 键已定义但 `src` 内无任何 `SetBaggage` 写入点；网关宿主未调 `AddKunObservability`；`AddKunHealthChecks` 只是别名、无任何 `IHealthCheck` 实现——均由本插件补齐。

**插件化定位**。依赖 7.3、7.10；向外提供 baggage 约定、计量事件 Schema、`IMeter.Record(...)`，供 7.16 模型调用计量与 S3 计费复用。

**Agent 契约**。计量事件带 `principal_kind = agent` 位；trace 贯通是 S4 工具调用可追溯的前提；不暴露工具。

**验收标准**
- [ ] E2E：外壳一次请求 → 网关 → platform → RabbitMQ 消费 → 作业回调，在 Tempo 中为一条 trace，每个 span 带 `tenant.id`。
- [ ] IT：参考应用调用一次 `files.presign-upload` 产生一条 `meter_events`（`unit = request`），带 `tenant_id` 与 `plugin_id`。
- [ ] E2E：拔掉 RabbitMQ 后 `/readyz` 503、`/healthz` 200；恢复后 `/readyz` 200。
- [ ] 代码审查：Grafana 仪表盘 JSON 入 git，含请求量/错误率/延迟/Outbox 积压/消费重试五类面板（P1）。

### 7.12 qian.notify 通知内核与首批渠道（P0 · S1）

**设计理念**。通知是「内核 + 渠道适配器插件」的典型样例：内核管消息类型注册、模板、发送配置、接收配置、投递、监控与重试；每个渠道一个插件实现 typed port（借 iClaw `INotificationChannel` 范式，补充裁决 Q40 允许）。按补充裁决，短信渠道 S1 必做（登录验证码与找回密码依赖它）。参考平台 8 个渠道写死在一个 switch、「异步」分支被注释、重试用线程休眠、失败对全员重发、一键清空删光站内信，不沿用。

**范围与边界**。✅ 做：消息类型注册（插件清单 `contributes: qian.notify.message-type/v1`，含编码、参数 Schema、模板变量、落地深链）；模板（每模板一渠道，`{@字段}` 占位）与发送配置（一个业务通知点绑定多渠道模板、同步/异步、测试发送两步）；接收配置管理员默认与锁定（用户订阅矩阵 S2）；真正异步投递（队列 + 「发送中」态）、每收件人一条记录、失败按收件人定向重试（默认 3 次，1–5）；渠道 Provider 接口 `Send / TestConnect / SyncTemplates / ResolveAddress`；渠道 `inbox`（站内信，已读/回执/深链）、`smtp`（邮件，唯一默认账号）、`sms`（短信 Provider：阿里云/腾讯云，国家码不写死，模板同步 P1）；`notify.send` 能力带幂等键；系统预置模板不可删；「系统公告」消息类型 S1 可发。❌ 不做：用户订阅矩阵（S2 可选）；群机器人 Webhook（S2）；企微/钉钉/飞书/公众号/App 推送渠道（S3 可选）；公告管理（草稿/定时/回执，S3 可选）；敏感词、短链（S3 可选）；审批短链时效（S2 流程域）；渠道配置 UI 与模板同步 UI（S2，S1 用 API + 种子）。

**技术落点**。组件 `qian.notify`：Definition `Qian.Notify.Abstractions`（服务键 `ctx.Notify`）+ Provider `Qian.Notify`，默认进 `platform` profile 的 base 组合包，可独立安装（`qian add notify`）；schema `notify`；渠道为独立组件 `qian.notify.channel.inbox / .smtp / .sms`（Provider `Qian.Notify.Channel.Inbox` / `.Email` / `.Sms`，只依赖 `Qian.Notify.Abstractions` 的 `INotificationChannel`；平台级启停走 profile patch，租户级启停 = 在租户 scope 挂载 / 不挂载其配置条目）；依赖 7.3 租户、7.4.3 目录（收件人寻址）、7.8 secretRef（渠道凭据）、7.10 队列、7.13 调度（延时重试）与实时推送（站内信推送）、7.11 审计。

- 表：`message_types`（plugin、code、params_schema、deep_link_template）、`notify_templates`、`send_configs`、`send_config_templates`、`receive_configs`（admin 默认/锁定）、`channel_accounts`（渠道、配置 jsonb、`secretRef`、is_default）、`notify_monitors`（一次发送：状态 `sending | succeeded | failed | partial`、重试载荷）、`notify_records`（每收件人：状态、尝试次数、最后错误）、`inbox_messages`（收件人实例、已读、回执、跳转参数）。
- 接口：`INotificationChannel { ChannelDescriptor; Task<DeliveryResult> SendAsync(msg); Task TestConnectAsync(account); Task<IReadOnlyList<VendorTemplate>> SyncTemplatesAsync(account); Task<Address?> ResolveAddressAsync(membershipId) }`；内核 `INotifier.SendAsync(configCode, recipients, params, idempotencyKey)`。
- API：`GET /api/notify/send-configs` → 每个配置的参数 JSON Schema；`POST /send-configs/{code}:validate`；`POST /api/notify/send`；`GET /api/notify/monitors`、`POST /monitors/{id}:retry`（只重发失败收件人）；`GET /api/notify/inbox`、`POST /inbox/{id}:read`。
- 判定链：场景未开启不生成记录 → 收件人被接收配置过滤的渠道跳过 → 全部过滤则不生成监控；模板必须启用；被引用模板不可删；详情接口不回传密钥。
- 短信 Provider：凭据 `secretRef`，签名与模板码配置，`+86` 作为默认国家码可配；S1 服务 7.4 的短信 OTP 与找回密码、7.3 的租户到期提醒、7.4 的锁定通知与密码到期提醒。

过渡态（2026-10）：Kun 无通知模块；iClaw 的 `INotificationChannel` typed port 与「供应商 SDK 隔离在实现 csproj + 配置选实现」范式已落地，直接迁移为渠道插件范式。

**插件化定位**。Inject `ctx.Registry`（消息类型注册）、`ctx.Tenancy`、目录、`ctx.Config`、`ctx.Events`、`ctx.Scheduler`、`ctx.Realtime`、`ctx.Audit`；向外提供 `INotifier`、`qian.notify.channel/v1` 扩展点、`qian.notify.message-type/v1` 贡献点、事件 `notify.sent / notify.failed / notify.read.v1`。

**Agent 契约**。通知以四操作暴露（S4）：列发送配置（`read`）、校验参数（`read`）、发送（`external` + `confirm`）、查监控（`read`）；参数模型为 JSON Schema；凭据永不出现在契约；对外发送类工具默认需确认。

**验收标准**
- [ ] IT：调用 `notify.send` 给 3 个收件人（其中 1 人关闭了邮件渠道），生成 1 条监控、2 条邮件记录 + 3 条站内信记录；监控先为 `sending` 再终态。
- [ ] IT：SMTP 账号故障时 2 条邮件记录失败，监控 `partial`；修复后 `retry` 只重发这 2 人，不重复发站内信。
- [ ] E2E：7.4 的短信 OTP 经 `sms` Provider 发出并在 staging 收到短信（供应商测试账号）；国家码可配为非 `+86`。
- [ ] IT：同一 `idempotencyKey` 的两次 `send` 只产生一次投递。
- [ ] IT：删除被发送配置引用的模板返回结构化错误；渠道账号详情接口不含明文密钥。
- [ ] E2E：站内信产生后外壳 ≤ 3s 收到未读角标更新（经 7.13.2；降级为 30s 轮询）。
- [ ] IT：租户 A scope 未挂载 `sms` 渠道组件时，A 的发送配置里不出现短信渠道，强行指定返回与「渠道不存在」相同的错误；经 scope patch 挂载后无需重启即可用。

### 7.13 qian.scheduler 与 qian.realtime 调度与实时推送（P0 / P1 · S1）

#### 7.13.1 qian.scheduler 集群安全调度器（P0 · S1）

**设计理念**。ADR-012 把「可靠延时调度」列为 S1 流程前置能力；S1 自身也依赖它（租户到期扫描、密码周期提醒、通知重试、审计链校验）。K3s 多副本下调度器必须集群安全；回调契约替代参考平台「HTTP 自调用 localhost、持久化本机地址、创建者缺失回退 admin 身份」。

**范围与边界**。✅ 做：PG 持久化的作业定义与触发器（cron 6/7 段、固定间隔、一次性延时）；多租户命名空间；集群安全领取（`FOR UPDATE SKIP LOCKED` 领取到期触发器 + 租约）；misfire 策略；并发策略与超时；按业务键可取消（`Cancel(businessKey)`）；任务类型 `local`（SDK `[QianJob("code")]` 声明的进程内处理器）与 `callback`（HTTP 回调契约：按 K8s Service 名寻址、受限服务身份 S2S、签名、幂等键 = run id、超时、重试、结果回写）；结构化执行日志（状态、耗时、错误分类、业务结果）；API 与最小管理面。❌ 不做：脚本型任务（任意代码执行，不做）；「调用数据接口」任务类型（S2）；任务管理 UI（S2，S1 用 API）；内置启动作业清在线用户（不做，多实例下有副作用）；集中式 `ScheduleTask/{taskCode}` 分发器（各插件注册自己的回调端点）。

**技术落点**。组件 `qian.scheduler`：Definition `Qian.Scheduler.Abstractions`（服务键 `ctx.Scheduler`）+ Provider `Qian.Scheduler`，默认进 `platform` profile 的 base 组合包，可独立安装（`qian add scheduler`）；schema `scheduler`；依赖 7.6、7.10、7.4（服务身份 `client_credentials`）、7.11。表 `job_definitions`（code、tenant_id、type、target（能力 URI 或回调地址）、params jsonb、cron/interval/run_at、timeout、retry_policy、concurrency、business_key、created_by）、`job_triggers`（状态 `ready | paused | archived`、next_run_at、last_run_at、run_count）、`job_runs`（started/finished、http_status、business_result、error_class、duration）。领取循环：每副本每秒 `SELECT ... WHERE next_run_at <= now() FOR UPDATE SKIP LOCKED LIMIT n`，领取后写租约、执行、回写、计算下次。回调契约：`POST {service}/qian-jobs/{code}` 头 `X-Qian-Run-Id`、`X-Qian-Signature`（HMAC，密钥 secretRef）、S2S `aud = service`；对端 `POST /api/scheduler/runs/{id}/result`。SDK：`IScheduler.ScheduleOnce(businessKey, runAt, target, params)`、`ScheduleCron(...)`、`Cancel(businessKey)`。

过渡态（2026-10）：Kun 无调度模块；参考平台调度器无集群协调、RunResult 恒 0、业务失败不入日志——只借鉴其「一切定时变回调」的解耦方向。

**插件化定位**。依赖租户、数据、事件、身份（服务主体）、审计；向外提供 `IScheduler`、`qian.scheduler.job-type/v1` 扩展点（S2 加 `data-api`）、事件 `scheduler.run.completed / failed.v1`；7.3、7.4、7.12、7.11 的定时需求全部经它。

**Agent 契约**。作业定义 Schema（名称、cron、目标能力 URI、参数、超时、重试、并发）机器可读；`jobs.create` 标 `write` + `confirm`，`run-now` 标 `write`；目标必须是能力目录中的 URI，Agent 不能填任意 URL。

**验收标准**
- [ ] IT：3 个副本运行、100 个每分钟触发器，每分钟恰 100 次执行无重复（`job_runs` 按 trigger × 分钟唯一）。
- [ ] IT：杀掉正在执行的副本，租约到期后触发器被另一副本接管并按 misfire 策略处理。
- [ ] IT：`ScheduleOnce(businessKey)` 后 `Cancel(businessKey)`，到点不执行；重复 `ScheduleOnce` 同一业务键只保留一条。
- [ ] IT：回调签名错误被对端拒绝；回调超时后按策略重试并最终写 `error_class = timeout`；对端回写业务失败后 `job_runs.business_result = failed`。
- [ ] E2E：7.3 租户到期扫描作业在 staging 准点触发并发出 `tenant.expired.v1`。

#### 7.13.2 qian.realtime 实时推送（P1 · S1）

**设计理念**。进程内 WebSocket 连接池、URL 带 token、启动时清空在线用户，都是单体思路；多副本必须带 Redis 背板，握手期鉴权用一次性票据。基线 D 列 S1 必做；ADR-003 已把它列入 S1 平台插件并定为 P1（R1）：站内信与权限刷新在无实时通道时降级为轮询，不阻塞退出标准。

**范围与边界**。✅ 做：WebSocket/SSE 会话（运行在 `platform` profile，经 `gateway` 透传）；握手一次性票据（`POST /api/realtime/tickets`，30s）；Redis Pub/Sub 背板扇出（用户 / 租户 / 全部）；在线状态；事件类型目录与 JSON Schema（`messagePush`、`unReadMsg`、`online`、`logout`、`identityRevisionChanged`）；服务接口 `IRealtimePush.PushAsync(target, eventType, payload)`；与 7.4 会话吊销联动（`session_revoked` → `logout`）。❌ 不做：IM 私聊（不做）；App 推送（S3）；WebSocket 携带长期 token（禁止）；跨租户广播。

**技术落点**。组件 `qian.realtime`：Definition `Qian.Realtime.Abstractions`（服务键 `ctx.Realtime`）+ Provider `Qian.Realtime`，默认进 `platform` profile 的 base 组合包，可独立安装（`qian add realtime`）；依赖 Redis、7.4 票据校验、7.9 网关 WebSocket 透传、7.10 事件。连接表只在 Redis（`rt:conn:{tenant}:{membership}` → 实例集合），实例重启自动清理；SSE 事件 JSON Schema 作为 yue `@yue/sdk` 的类型来源。

**插件化定位**。依赖身份、网关、事件；向外提供 `IRealtimePush` 与事件类型注册点 `qian.realtime.event-type/v1`，7.12 站内信、7.5 权限刷新、S2 流程待办刷新、S4 Agent 进度都是客户。

**Agent 契约**。推送通道暴露为服务接口（S4 Agent 可作为推送发起方，`write`）；事件 Schema 公开。

**验收标准**
- [ ] IT：用户在两个浏览器连接到不同副本，向该用户 `Push` 一次，两个连接各收到一次。
- [ ] IT：票据过期或复用握手失败 401；URL 中不接受 `access_token` 参数。
- [ ] E2E：管理员强制下线后该用户前端 ≤ 3s 收到 `logout` 并跳转登录页。

### 7.14 qian.entitlement 授权 v0（P0 · S1）

**设计理念**。「授权是否买了」与「特性开关是否灰度开」分开存（ADR-014）：前者在租户/订阅库，后者在 Apollo（7.8）。S1 的 v0 只需「租户 × 插件 × 版本范围」手工授予表 + 运行时权益查询 API，但权益 API 的形状是 S3 市场几乎改不了的三件事之一，S1 就要定对。功能授权是白名单（替代参考平台「未勾选菜单」黑名单）。ADR-020 进一步把「开没开」从状态机移出：租户是否启用一个组件 = 租户 scope 是否挂载它的配置条目（scope patch，7.2.1），`qian.entitlement` 的状态机只回答「买没买、能不能」。

**范围与边界**。✅ 做：`tenant_entitlements` 表（tenant、plugin、version_range、plan_id 可空、source `manual | plan | order`、valid_from/to、status、granted_by）；运营 API 手工授予/撤销；权益查询 API（`GET /api/entitlements/me`、`IEntitlementService.IsEntitled / GetEntitlement(tenant, plugin)`）；Redis 缓存与 `entitlement.changed.v1` 失效；与 `qian.lifecycle` 的租户级状态机联动（授予 → `Entitled`；`Enabled ⇄ Disabled` 不再由状态机持有，改由租户 scope patch 表达，7.2.1）；三处拦截（网关、SDK、外壳）中「买没买」的权威来源（「开没开」的来源是 `qian.registry` 的租户 scope 视图）。❌ 不做：Plan / Seat / Meter / Trial 完整模型（S3，字段预留可空）；套餐批量下发、订单打通（S3）；席位计数与用量额度执行（S3）；市场目录（S3）。

**技术落点**。组件 `qian.entitlement`：Definition `Qian.Entitlement.Abstractions`（服务键 `ctx.Entitlement`）+ Provider `Qian.Entitlement`，默认进 `platform` profile 的 base 组合包，可独立安装（`qian add entitlement`）；schema `entitlement`；依赖 7.3、7.2.1、7.10、7.11。API：`PUT /api/tenants/{id}/entitlements/{plugin}`（body：version_range、valid_to、source）、`DELETE ...`、`GET /api/tenants/{id}/entitlements`、`GET /api/entitlements/me?plugin=`（返回 `{ entitled, versionRange, validTo, features[]（S3）, seats（S3）, meters（S3）}`）。缓存键 `ent:{tenant}:{plugin}`，TTL 5 分钟 + 事件失效。平台插件（`policy.tier: system`、`policy.required: true`，不可从 `platform` profile 卸载；取代 v1 `alwaysEnabled`，R34）视为恒授权。

**插件化定位**。Inject `ctx.Tenancy`、`ctx.Lifecycle`（`qian.lifecycle` 状态机）、`ctx.Events`；向外提供 `IEntitlementService`、事件 `entitlement.changed.v1`；7.9 网关 `PluginEnabledTransform`、SDK `PluginEnabledMiddleware`、7.15 外壳远程模块加载三处消费。

**Agent 契约**。权益查询 `entitlements.me` 是 `read` 工具；授予/撤销标 `write` + `confirm`（S3 接订单后再放开自动化）。

**验收标准**
- [ ] E2E：对租户 B 撤销参考应用授权后 ≤ 5s：网关 404、SDK 404、外壳菜单消失（与不存在无法区分，R32）；重新授予后恢复。
- [ ] IT：`IsEntitled` 对平台插件恒 true；对过期授权（`valid_to` 已过）返回 false 并发事件。
- [ ] UT：权益响应 Schema 含 S3 预留字段（可空），契约快照测试锁定形状。
- [ ] IT：授权、租户 scope 挂载与 7.8 灰度开关三者正交：已授权 + scope 未挂载 = 404；授权撤销 + scope 已挂载 = 404（R32）；两者都满足 + 灰度开关关 = 该灰度特性不可见但组件可用。

### 7.15 qian.shell 与 yue S1 最小集 前端外壳与组件库（P0 · S1）

#### 7.15.1 qian.shell 前端外壳（P0 · S1）

**设计理念**。外壳 + 带版本号的插槽注册表 + Module Federation 2 运行时加载（ADR-013，补充裁决「MF2 S1 必做」）：插件在清单 `contributes` 静态声明贡献，运行时只加载「本租户已启用 × 当前页面用得到」的远程；共享依赖 singleton；统一一个壳按「平台控制台 / 应用」上下文切换（补充裁决 Q37），S1 单一侧栏布局（Q38）。MF 零隔离只给可信插件，不可信 UI 走 Worker 远程渲染或 iframe（S3）。令牌不进 URL/iframe，前端权限裁剪只是体验层。

**范围与边界**。✅ 做：MF2 宿主（`registerRemotes / loadRemote`，`mf-manifest.json` 类型提示与预加载）；共享 singleton `react / react-dom / @yue/* / @qian/plugin-sdk`，`engines.yue` 校验；插槽注册表（`qian.shell.nav/v1`、`qian.shell.settings/v1`、`qian.shell.user-menu/v1`、`qian.shell.command/v1`、`qian.shell.preview/v1`、`qian.shell.client-action/v1`，每个带输入数据 Schema 与输出类型）；桥接 API（作用域校验、来自插件 UI 的消息视为不可信输入）；认证流程（OIDC code + PKCE、`AuthProvider` 契约、刷新队列、多租户选择、过期弹窗）；后端菜单 → 路由（`menuTypeRegistry`：页面、外链两类）；租户切换器（令牌重签）+ 平台代管常驻横幅；主题/语言切换、用户下拉；兜底页 403/404/500/离线、ErrorBoundary；运行时配置注入 `window.__APP_CONF__`（K8s ConfigMap）；`Preferences` 对象与注入；站内信顶栏弹窗（轻）；偏好 KV 与 URN 资源引用的消费。❌ 不做：多标签完整版（S2，基础版 P2）；全局搜索命令面板（S2 可选，插槽 S1 定义）；通知抽屉完整版（S3）；7 种布局与偏好抽屉（S3 可选）；不可信 UI 沙箱（S3）；应用切换多面新窗口（不采用）；锁屏（不做，仅 UI 遮罩非安全措施）；内置 IM（不做）；MCP Apps / WebMCP（S4）。

**技术落点**。组件 `qian.shell`（`runtime.form: ui`）：外壳本身是 `@qian/runtime` 上的一棵 TypeScript 插件树（ADR-020 D20.2，与 .NET 侧语义相同），打包为 `platform` 的 `shell` 组合包（静态前端制品：Vite 构建产物 + 运行时配置 ConfigMap，随 `platform` 镜像发布、经 `gateway` 提供；不是 profile，原 `qian-shell` 单元不再单独出镜像，R31）；React 19 + TypeScript + Vite + Tailwind v4 + `@module-federation/enhanced`；react-router 7、Zustand + TanStack Query、i18next。桥接 API `window.__qian.bridge`：`getContext()`（tenant、membership、app、locale、theme，只读）、`navigate(urn | route)`、`notify(level, msg)`、`invoke(capabilityId, input)`（经 `PlatformClient`，带幂等键）、`registerClientAction(descriptor)`；所有调用校验调用方远程模块 ID 与声明的插槽作用域。加载规则：读 7.14 权益 ∧ 租户 scope 已挂载 + 路由需要的插槽 → `loadRemote`；未挂载组件的远程不请求。

过渡态（2026-10）：无 Kun 对应；iClaw 的壳只用了 Base UI Dialog 一个原语、无组件库；参考平台前端「两套并存」是反面教材。

**插件化定位**。依赖 7.4（登录）、7.9（Cookie 会话）、7.14（权益）、7.13.2（推送，可降级）、7.12（站内信）、7.8（运行时配置）；向外提供插槽注册表（`@qian/runtime` 的 scope 化注册：租户 scope 的贡献遮蔽同名全局贡献）、桥接 API、`@qian/plugin-sdk` 前端部分；S2 业务插槽（字段类型、区块、详情页签）在同一注册表扩版本。

**Agent 契约**。客户端动作注册表（名称、参数 JSON Schema、权限、人话描述）S1 定义、S4 Agent 消费；插槽与贡献静态可索引；壳不暴露服务端工具。

**验收标准**
- [ ] E2E：参考应用的远程模块在租户 A scope 已挂载时按需加载并出现在导航；在租户 B scope 未挂载时 `mf-manifest.json` 不被请求。
- [ ] E2E：`engines.yue` 不满足的远程模块被拒绝加载并在控制台与审计留痕；共享依赖只存在一份 React 实例。
- [ ] E2E：登录 → 多租户选择 → 切换租户 → 令牌重签 → 菜单与权限随之刷新；令牌过期时刷新队列合并并发请求，失败弹过期对话框。
- [ ] UT：桥接 API 拒绝来自未注册远程或超出声明作用域的调用；`getContext()` 返回对象不可写。
- [ ] 代码审查：`localStorage` 中不存在 access/refresh token 明文；URL 与 iframe src 中不出现令牌。
- [ ] E2E：改 ConfigMap 中 `__APP_CONF__.apiBase` 后刷新即生效，不重编译。

#### 7.15.2 yue S1 最小集（P0 · S1）

**设计理念**。路线 A：React 19 + Vite + Tailwind v4 + Base UI + shadcn 源码自有化（ADR-013，Q12 接受风险、首迭代设验证门，不通过整体切 Radix、不混用）。六层分层用 lint 强制；**只保留一套表单/表格/弹层引擎**贯穿 S1 后台 CRUD 与 S2 低代码运行时；令牌三层、明暗两态、对比度单测进 CI。参考平台弹窗/表单/表格双轨、控件三处呈现各自维护，是反例。

**范围与边界**。✅ 做（全部 S1 必做，除注明）：`@yue/tokens`（primitive → semantic → component 三层、HSL/oklch 可加 alpha、状态色成对 + `Tone`、shadcn 语义变量映射、`html.dark` + `color-scheme`、首帧防闪烁、命名唯一真相源、WCAG AA 对比度单测、`data-theme / data-brand` 运行时注入点；DTCG 导出 P2）；`@yue/core` 约 30 个基础组件（Button 族、Input 族、Checkbox/Radio/Switch/Toggle/Segmented、Select/Combobox、Field、Dialog/AlertDialog/Sheet/Drawer/Popover/Tooltip/HoverCard/DropdownMenu/ContextMenu、Tabs/Accordion/Card/Separator/ScrollArea/Resizable、Badge/Tag/Avatar/Skeleton/Spinner/Progress/Alert/Toast/Notification/Empty/Result、Breadcrumb/Pagination/BackTop/FullScreen、Icon 统一入口、Descriptions 提前；每个暴露 `data-slot` 与 `data-testid`）；`@yue/shell`（AppShell 单一侧栏布局、Page/PageHeader/ColPage/LeftTree、`menuTypeRegistry`、Preferences、租户切换器、兜底页）；`@yue/data`（DataTable 基础版：TanStack Table v9，列定义 schema、服务端分页/排序/筛选、行选择、列显隐、固定列、三态、工具栏；Tree/TreeSelect/Cascader 虚拟化；Date/Time 及 Range；Upload 分片 + MD5 + 进度 + ImagePreview）；`@yue/form`（Field + react-hook-form + Ajv；SchemaForm 基础版 + API 式 `FormApi`；`dependencies` 基础联动；编辑/只读态引擎级统一；校验与脱敏规则目录；`useModal/useDrawer/usePopup` 弹层 API；Message/Confirm 统一出口）；`@yue/biz` 基础版（ApiSelect、DictSelect、OrganizeSelect、UserSelect、RoleSelect、菜单/按钮授权树、IconPicker，全部经注入的 `PlatformClient`）；认证页族（账号密码、SSO 跳转与回调、多租户二次选择、登录过期弹窗；验证码登录、忘记密码 P1；Captcha P2）；`@yue/sdk`（注册表 API、`PlatformClient`、请求拦截器链：鉴权刷新队列、租户头、大数处理；i18n；mock 中间件；`.d.ts` 契约）；元数据契约草案（控件 manifest、FormSchema/ListSchema 含 `device`、`auth`；SSE 事件 JSON Schema；权限码目录格式）；工程化基线（见下）。❌ 不做：Slider/Rate/ColorPicker/Steps/Timeline（S2）；DataTable 进阶（树表、列权限、行内编辑、复杂表头，S2）；ImportWizard/ExportDialog/SuperQuery/ConditionBuilder（S2）；CodeEditor/Cron/富文本/Markdown（S2，富文本选型待补）；控件注册表交付控件集与表单/列表渲染器（S2）；任何设计器（S2）；`@yue/charts`（S2 可选）；`@yue/mobile`（S2 可选）；`@yue/agent`（S4，SSE 事件契约 S1 定）；antd 混用（不做；若 S2 压力极大仅限控制台域并约定迁移期限）；`eval` / `new Function` 执行租户脚本（不做）。

**技术落点**。pnpm workspace + `catalog:` + Turborepo；分层边界 eslint-plugin-boundaries / dependency-cruiser 进 CI；tsdown 或 Rslib 输出 ESM + d.ts，CSS 单独导出，重型物料懒加载；Storybook 10（CSF Factories、a11y 插件、令牌可视化页）；Vitest 5 + Testing Library + `@vitest/browser`；契约测试从 Kun/Qian OpenAPI 生成类型（orval / openapi-typescript）；axe + Playwright E2E `critical = 0`；Lighthouse CI 首屏预算；CI 门禁矩阵（lint、typecheck、unit、a11y、visual（P2 → S2 必做）、Lighthouse、边界、publint、循环依赖）；Changesets + SemVer（0.x 每 minor 可破坏，1.0 后只在大版本破坏且废弃保留一个 minor 并附 codemod）；私有 npm 为运行时渠道（shadcn 私有 registry S3 可选）；i18n 全部走 key、内置 zh-CN / zh-TW / en-US、远程语言包；`data-testid` 命名 `<app>-<page>-<element>`；样式 Tailwind + cva + CSS 变量，禁全局 BEM 大文件与 `!important` 覆盖；安全基线：前端不持有加密密钥、不持久化 token 明文、嵌第三方页面用一次性票据或 postMessage 握手。Base UI 验证门：S1 首迭代验证 Menu/Select/Combobox/Tabs/Tooltip/Tree 的键盘与可访问性，记录结论后锁定。

**插件化定位**。yue 是 Qian 前端的组件库而非插件（`@yue/*` 以 npm 独立可用于任意 React 应用，ADR-019 D19.1；`@qian/runtime` 是前端插件树的运行时，yue 不依赖它）；`@yue/sdk` 的注册表 API（导航/设置/命令/预览器/控件/客户端动作）是 7.15.1 插槽的实现；组件 props JSON Schema 是设计器、远程渲染白名单、S4 生成式 UI 共用的契约（S1 定格式，S2 全量）。

**Agent 契约**。每个组件/控件导出机器可读 manifest（props JSON Schema、slots、events、a11y 角色、令牌依赖、示例）；注册表可导出 JSON；SSE 事件以 JSON Schema 为唯一真相源生成 TS；校验器前后端共用（S2）。

**验收标准**
- [ ] CI：令牌明暗两套全部语义色对比度 ≥ 4.5:1 单测通过；同名异义令牌（如 `--accent` 双义）被 lint 拒绝。
- [ ] CI：`@yue/core` 组件在 Storybook a11y 插件与 axe E2E 下 critical = 0；每个组件有 `data-slot` 与 `data-testid`。
- [ ] CI：`@yue/biz` 引用 `@yue/designer` 之类的向上依赖被 boundaries 规则拒绝。
- [ ] E2E：用 SchemaForm + DataTable 实现参考应用的客户 CRUD（新增/编辑弹窗、只读详情、服务端分页排序筛选、列显隐），不出现第二套表单或表格实现。
- [ ] E2E：租户管理页用 OrganizeSelect / UserSelect / RoleSelect / 授权树完成角色授予；Upload 分片上传经 7.7 成功。
- [ ] 文档：Base UI 验证门结论（通过/切 Radix）记录在 yue 仓库 ADR；Lighthouse 首屏 script 体积在预算内。
- [ ] UT：控件 manifest 与 FormSchema/ListSchema 草案通过各自 JSON Schema 校验，`device`、`auth` 字段存在。

### 7.16 qian.model-gateway 模型服务网关底座（P1 · S1）

**设计理念**。本期唯一的 AI 范围（ADR-015）：不另起一套，并入团队现有 Higress / MaaS 基础设施；GPUStack 作为 OpenAI 兼容上游注册进 Higress + 一个 Provider Adapter；租户/配额/计量在平台侧，Higress 对租户无感；不自写模型代理层；不在 S1 把 SSO/S2S 交给 Higress。接入范围先 LLM + Embedding（Q17）。

**范围与边界**。✅ 做：`model-gateway` 组合包的 adapter 服务（GPUStack 模型发现 `/v1/models`、健康探测、生命周期：上下线）；把 GPUStack 注册为 Higress 上游（`ai-proxy` 供应商与逻辑模型名映射入 git，经部署仓库 PR 而非直改线上）；填写 `model-capabilities` 契约（模态、工具调用、上下文长度、数据驻留、隐私、时延、成本档）与逻辑模型目录（`chat-default`、`embedding-default` 等用途槽位）；为 Qian 租户生成 Higress consumer（key-auth）并由服务端生成 `x-qian-consumer` 头作限流桶键；从响应回流 usage 落 7.11.2 计量事件；密钥 secretRef；模型目录查询 API。❌ 不做：自研模型代理（不做）；租户可见的模型中心与按用途路由 UI（S2/S3）；ASR/TTS/Rerank 接入（待拍板）；SSO/S2S 经 Higress（不做）；Usage Ledger 与 iClaw 合并（S3 再议，Q15）；MCP 流量治理（S4）；价格表与账单脚本（MaaS 侧，Qian 只消费）。

**技术落点**。组件 `qian.model-gateway`（`runtime.form: service`）：Definition `Qian.ModelGateway.Abstractions`（服务键 `ctx.ModelGateway`）+ Provider `Qian.ModelGateway`，打包为 `model-gateway` 组合包（.NET，S2S 服务主体），默认挂在 `platform` profile，可选拆为独立 profile `model-gateway`（镜像 `qian-model-gateway-adapter`，S1·P1，R31）；依赖 Higress 2.2.x（`maas-*` 路由、`ai-proxy / ai-quota / ai-token-ratelimit / ai-statistics` 插件、网关专用 Redis HA、MaaS Loki 90 天访问日志）、GPUStack、7.8 secretRef、7.11.2 计量、7.3 租户。契约（直接用 iClaw 的）：`model-capabilities`、`model-governance`；控制面产物：Higress McpBridge/ai-proxy 配置片段、consumer 定义、逻辑模型映射表。API：`GET /api/models`（目录，含类型、能力标签、上下文、配额、可用租户）、`GET /api/models/{id}/health`、`POST /api/models:sync`（从 GPUStack 刷新）。租户侧：Qian 应用经 OpenAI 兼容面 `/v1/chat/completions`、`/v1/embeddings` 调用，带租户 consumer key；坏配置 fail-open 到产品默认路由。

过渡态（2026-10）：现网 Higress 2.2.3 两副本 + PDB 已合入，但 `ai-proxy` 供应商与逻辑模型名、配额/统计插件绑定、价格表与账单脚本尚未入 git；推理层是 Xinference（0 副本）+ Ollama，无 GPUStack manifest；首节点两块 3090 已借出一块（GPU 归属待定）。本插件可独立于其他 S1 项延期，不影响退出标准。

**插件化定位**。依赖租户、配置、计量、身份（服务主体）；向外提供 `IModelCatalog`、`IModelProviderAdapter` 扩展点 `qian.model-gateway.provider/v1`（S2 加其他上游）、事件 `model.catalog.changed.v1`。

**Agent 契约**。模型目录含类型、能力标签、上下文长度、配额、可用租户，供 S4 Agent 按能力选模型；`models:sync` 标 `external` + `confirm`；目录查询 `read`。

**验收标准**
- [ ] E2E：GPUStack 新增一个模型后 `models:sync` 使其出现在目录并在 Higress 上可经逻辑名调用；下线后健康状态变更且路由摘除。
- [ ] E2E：租户 A 的应用经 OpenAI 兼容面完成一次 chat 与一次 embedding 调用，`meter_events` 出现 `unit = token` 记录且带 `tenant_id`；超过 `ai-token-ratelimit` 阈值返回 429。
- [ ] 代码审查：Higress 配置变更全部经部署仓库 PR 合入，adapter 不持有线上直改权限；模型供应商密钥只以 secretRef 出现。
- [ ] UT：目录条目通过 `model-capabilities` JSON Schema 校验。

### 7.17 部署物料与 K3s 交付（P0 · S1）

**设计理念**。沿用 iclaw-deploy 的 L0/L1/L2 三层、Argo CD app-of-apps、Kustomize、不可变发布事件、按 digest 写回、迁移 Job 先行、供应链证据（ADR-017）；Qian 新建独立命名空间与专属 PG/Redis；不以 per-tenant namespace 为默认；补齐现网治理缺口（default-deny NetworkPolicy、ResourceQuota/LimitRange 现为零）。Kun 没有任何 Dockerfile、Helm、manifest、健康端点实现，K3s 交付物全部由 Qian 从清单生成。

**范围与边界**。✅ 做：Dockerfile 按 profile 出镜像（R31：`platform` → `qian-platform`（JIT，含 `shell` 与 `model-gateway` 组合包）、`identity` → `qian-identity`（JIT）；`gateway` → `qian-gateway` 可 AOT distroless；可选独立 profile `model-gateway` → `qian-model-gateway-adapter`（JIT，P1）；`standalone` / `minimal` 不出镜像；`shell` 组合包的静态前端制品随 `platform` 镜像发布、经 `gateway` 提供，不再单独出 nginx 镜像）；Helm/Kustomize 片段由 `qian pack` 从 `plugin.json` 与 profile 生成（`runtime.form: service` 的 resources / health、`policy` 与 `permissions.egress` → NetworkPolicy、`config` → Apollo 引导项、迁移 Job），profile 文件与其 `qian.patch.yml` 随镜像发布，环境级 `--patch` overlay 由 ConfigMap 挂载；`/healthz` `/readyz` 约定；`qian-<env>` 命名空间 + 专属 PostgreSQL 16 + Redis 7；default-deny NetworkPolicy + 按 egress 白名单放行；ResourceQuota / LimitRange / PDB；RabbitMQ 上 K3s（quorum queue，prod 3 副本、staging 1 副本，PVC）；Apollo（≥ 2.5.2，configservice + adminservice 每环境一组 2 副本 + podAntiAffinity + PDB，portal 2 副本 OIDC + JDBC session）+ MySQL（Q8：staging 集群内 operator，prod 托管；不与 iClaw 共用）；Apollo Portal/Headlamp/Argo 走 Authentik blueprint，per-env 独立 OIDC client；Harbor 新增第二类制品「插件包 OCI artifact」，清单只允许 digest；Argo Application：`qian-platform`（L2）、`qian-integrations`（租户接线，`*-integrations` 模式）、L1 新增 `apollo`、`rabbitmq`；`prune: false` 起步；K3s `--secrets-encryption`；Traefik `forwardedHeaders.trustedIPs`；生产独立节点池（Q16）；Alertmanager（P2）。❌ 不做：per-tenant namespace 默认拓扑；高隔离租户 ApplicationSet 与私有化 overlay（S3）；cert-manager 进集群（公网 nginx 终结 TLS，维持）；Dapr sidecar；Infisical 运行时注入（P2，接通 Operator/ESO 前以 K8s Secret 为权威）。

**技术落点**。
- 镜像：多阶段构建，非 root，只读根文件系统，`SBOM`（Syft）+ 漏洞扫描（Grype/Trivy）+ Cosign 签名；镜像与插件包都推 Harbor。
- 发布链：不可变发布事件（40 位 SHA、CalVer、`dry_run`）→ 验签 → 固定检出 → 构建推送 → **迁移 Job 先行**（`qian-platform` 启动参数 `--migrate-only`，取 advisory lock）→ `kustomize edit set image` 按 digest 写回 → Argo 收敛 → smoke（`/readyz` + 参考应用登录）。`Qian.Runtime` 与 profile 随 CalVer 发布；组件独立灰度（Apollo Label 按实例、租户灰度靠租户 scope patch + 7.8 特性开关 + 7.14 权益）。
- 生成的 Kustomize 片段：Deployment（资源、探针、`terminationGracePeriodSeconds ≥ 优雅停机 + 10s`、`automountServiceAccountToken: false`）、Service、NetworkPolicy（默认拒绝入出，放行同 ns 内网关 → 服务、服务 → PG/Redis/RabbitMQ/Apollo、`permissions.egress` 列出的外部域经出口代理）、ResourceQuota/LimitRange（命名空间级模板）、PDB、ConfigMap（`__APP_CONF__`、Apollo 引导项）、Secret 模板（只键名）。
- 配置：Kustomize 只注入引导项（`Apollo__AppId/Env/Cluster/ConfigServer__0/Label/CacheDir`、Secret 文件）；Apollo 环境只建 UAT + PRO 与现网 staging/prod 对齐；基线定期导出进 git。
- 数据层：每环境专属 PG 16（`qian` 库，schema-per-service，RLS 开启）、Redis 7（专属实例，不复用被 L1 跨 ns 共享的 Redis）、RabbitMQ vhost `qian`。
- 可观测：OTel Collector → 现网 LGTM；Alertmanager 部署与 5 条基础告警（P2）。
- 环境：同集群按 ns 分 staging / prod；生产建议独立节点池（按物理宿主计故障域；现网三节点只跨两台宿主、几乎全钉首节点、唯一存储类 local-path）。

过渡态（2026-10）：现网无 Apollo、无 MySQL、无 Alertmanager、K3s 上无 RabbitMQ、Infisical 注入未接通、业务 ns NetworkPolicy 为零、无 ResourceQuota/LimitRange/HPA、多处 `:latest`；首节点同时是唯一 GPU 节点与 CI runner。Qian 多租户上线前必须先补上述治理项。

**插件化定位**。部署物料不是组件，但 `qian pack` 的生成器与 `qian.lifecycle` 的 `IDeploymentExecutor`（7.2.1）是它的入口；每个 `runtime.form: service` 组件自带部署段（镜像 digest、资源、Secret 键名、健康端点）。

**Agent 契约**。发布事件、Argo 状态、健康端点机器可读；部署操作全部 `destructive` + `humanOnly`（S4 不开放给 Agent）。

**验收标准**
- [ ] E2E：从一次发布事件到 staging 三个 profile 镜像（`platform` / `gateway` / `identity`；`shell` 与 `model-gateway` 组合包随 `platform`）全部 `Ready`、外壳经 `gateway` 可访问、参考应用可登录，中间无人工步骤；镜像按 digest 引用、带 Cosign 签名与 SBOM。
- [ ] 检查：每个 profile 镜像内 `qian --dump-config` 的输出与 git 中对应 profile + 组合包 + patch 的合成结果一致（配置漂移门禁）。
- [ ] E2E：在 `qian-staging` 内启动一个未声明 egress 的 Pod，访问外网与其他 ns 被 NetworkPolicy 拒绝；`qian-platform` 能访问 PG/Redis/RabbitMQ/Apollo。
- [ ] E2E：kill `qian-platform` 一个副本，`/readyz` 摘流、无 5xx 泄漏到网关；滚动升级时迁移 Job 先完成再切镜像。
- [ ] E2E：RabbitMQ 一个节点宕机，quorum queue 继续收发；Apollo configservice 一个副本宕机，宿主仍能拉配置。
- [ ] 检查：`qian-staging` 命名空间存在 ResourceQuota 与 LimitRange；无 `:latest` 镜像；Apollo Portal 不可从公网访问且经 Authentik 登录。
- [ ] 检查：Harbor 中存在参考应用的插件包 OCI artifact，`qian publish` 以 digest 登记进注册表。

### 7.18 S1 必须做对的 Agent 就绪设计（P0 · S1）

**设计理念**。S4 是 AI 原生 Agent 平台，但「S1 到 S3 期间必须提前做对、否则 S4 返工」的设计从 S1 起生效（ADR-018；P 基线 §8.3）。Qian 不写任何 Agent loop、沙箱、记忆、技能引擎（ADR-016，复用 iClaw），S1 只负责把能力做成可发现、可解释、可审计、可计量的契约。按 ADR-020 D20.9，skills / agents / commands / hooks / MCP 五种 Agent 原生表面从 S1 起就是组件的一等公民（7.2.2）；本节的 12 项是「能力契约」侧的就绪设计，与表面侧互补。

**范围与边界**。✅ 做：下表 11 项 S1 必做；第 12 项（Operation/Task）S1 只预留状态枚举。❌ 不做：每租户 MCP 门面与由能力目录派生工具（S4）；A2A（S4）；Skills over MCP 分发（S4；但 `skills/` 作为组件表面 S1 即支持，7.2.2）；`extensions`（S4）；工具描述哈希钉死与复审（S3）；授权服务器对齐 MCP 的 RFC 9728 元数据（S3）；低代码连接器与第三方出网点的出网代理与 SSRF 基线（S2）——平台自有出网点（OIDC 发现、企业微信、SMTP、短信、Apollo、对象存储）的 SSRF 基线为 S1 必做（R10），落在 §13.8 与 7.17 的 egress 白名单 NetworkPolicy；字段敏感度标签（S2）。

| # | 设计 | S1 落点（本章对应） | 防止的返工 |
|---|---|---|---|
| 1 | 所有写操作走带 Schema 的命令（含 `sideEffect / idempotencyKey / dryRun`），UI 也不例外 | 7.2.3 分析器 + TCK「写命令必须有 Schema 与幂等键」；`MapCommand` 为唯一写入口 | 日后补「后门写入」的权限与审计 |
| 2 | 结构化、可被模型自纠正的错误：ProblemDetails 扩展（错误码、字段 JSON Pointer、允许值；区分协议错误与业务错误） | `Kun.AspNetCore` ProblemDetails + Qian `ErrorDetail { code, message, trace_id, pointer?, allowed? }`；7.5 错误码目录；错误码与文案 JSON 导出 | Agent 盲调撞错无法修正 |
| 3 | 统一主体模型与委托链（`user / service / agent`，`act`），预留 RFC 8693 令牌交换与 `client_credentials` | 7.4.1 `Client.type` 枚举含 `agent`、令牌预留 `act`/`profile`、`client_credentials` 可用；7.11.1 `actor_type` | 每个服务后补「这是不是 Agent 调的」 |
| 4 | 可解释、可预检的权限判定 API | 7.5 `Check / BatchCheck / ListAllowedActions / Explain` | Agent 盲调撞 403 |
| 5 | 一份契约多处生成；插件自带意图式工具描述 | 7.2.1 能力目录（`qian.capabilities`）+ 7.2.3 源生成器强制 `Description`；能力条目的 Agent 字段合并进 `plugin.json` 的 `tools` 表面并落库 | OpenAPI → MCP 转换质量 |
| 6 | 租户上下文由平台注入，`inputSchema` 禁止 tenant 字段 | 7.3 fail-closed；7.2.1 清单校验与契约 lint | 跨租户越权 |
| 7 | 审计一等公民且防篡改：主体链、能力 ID、契约版本、输入哈希、来源通道 | 7.11.1 | 无法回答「哪个 Agent 以谁的名义做了什么」 |
| 8 | 追踪与计量贯通；协议端点无状态 | 7.11.2 baggage 与 `meter_events`；所有 API 无会话亲和 | K3s 水平扩展与无状态 MCP |
| 9 | 事件契约化（稳定 ID + JSON Schema） | 7.10 事件目录 | Agent 触发器 |
| 10 | 能力目录 ≠ 公开 OpenAPI：经鉴权、按权限过滤、网关聚合 | 7.2.1 + 7.9 | — |
| 11 | 插件包签名、内容哈希、兼容范围，安装即审批「反向权限」 | 7.2.1 验签；清单 `permissions.platform/data/egress/secrets/ui`（S1 落库，租户管理员批准流程 S2 UI、S1 API） | 供应链 |
| 12 | 长任务与人工步骤资源化（Operation/Task） | S1 可选实现；在 SDK 契约包预留 Operation 资源契约与状态枚举 `working / input_required / completed / failed / cancelled`，导入导出等长操作按该契约返回 Operation 资源（7.2.1，R8） | S2 流程待办与 S4 Agent 中断成两套模型 |

**技术落点**。能力条目（v1 `provides.capabilities[]`；v2 落在 `plugin.json` 的 `tools` 表面，字段映射见 specs/component-model.md）的 Agent 字段（`sideEffect: read|write|external|destructive`、`policyHint: allow|confirm|deny`、`idempotent`、`async`、`requiredScopes`、`requiredSecrets`（名字）、`dataClassification`、`costTier`、`contractVersion`、`schemaSha256`、`agent.expose / requiresConfirmation`）S1 全部落库不派生；副作用枚举采 iClaw 四值（P 基线裁决 A-9）；契约格式 OpenAPI 3.1 + `operationId`、JSON Schema 2020-12 + `additionalProperties: false`，`x-status: draft` 期允许破坏性修订、定稿后 additive-only；MCP 以 2026-07-28 为目标版本并与 iClaw 制定对齐计划。与 iClaw 的对齐项（S2S claim、事件 envelope、工具元数据、运行授权快照、契约格式、注册表四层形状）写进 `contracts/` 目录并以冻结文件为准。

**验收标准**
- [ ] TCK：参考应用每个写能力都有 JSON Schema、`idempotencyKey`、`sideEffect` 与意图式 `description`；缺一不过。
- [ ] UT：任一业务错误响应符合 `ErrorDetail` Schema，含稳定 `code` 与（字段错误时）JSON Pointer。
- [ ] IT：以 `principal_kind = service` 的 `client_credentials` 令牌调用能力，审计记录 `actor_type = service`；Schema 中 `actor_type` 可取 `agent`。
- [ ] IT：`GET /api/capabilities` 对同一插件返回的条目含全部 Agent 字段，且 `schemaSha256` 与 `pack` 时计算值一致。
- [ ] 文档：`contracts/v0/` 下存在 S2S claim、事件 envelope、ErrorDetail、能力条目四份 JSON Schema，并有与 iClaw 对齐的差异说明。

### 7.19 S1 明确不做清单与理由

| 不做项 | 理由 / 归属 |
|---|---|
| 为第三方代码与可 AOT 的边缘 profile 做运行时热插 DLL；参考平台式外部程序集目录加载 | 不做（R27）：不可信代码无法安全载入受信进程，AOT 边缘不支持；官方代码插件在 `platform` profile 的热装卸（collectible `AssemblyLoadContext`）为 S2 可选，默认进程重启（ADR-020 D20.7 / D20.11、§7 第 5 条）；S1 只对声明式表面（skills、commands、hooks、MCP、元数据、patch）承诺热重载 |
| 第二套清单格式：手写 `qian-plugin.yaml` 与派生 `*.generated.yaml` 并存，或为声明式组件另起格式 | ADR-020 D20.6 / D20.11：只有一份 `.qian-plugin/plugin.json`，代码派生字段由源生成器合并进同一份；v1 `qian-plugin.yaml` 仅作字段迁移对照（`kind` → `runtime.form`） |
| 按物理形态（`kind` 七值）做顶层分类；把「预装全部 module + 数据库开关」作为租户启用的唯一路径 | ADR-020 D20.6 / D20.11：顶层分类改为表面，`kind` 退为 `runtime.form`；租户启用 = scope patch（S1 预装仍是 `platform` 的默认组合包） |
| 进程共享的服务端 JS 引擎；自由 JS 按钮 | 多租户安全与 Agent 不可发现；S2 用声明式规则 + 服务端 Hook + 可序列化表达式 DSL，重脚本走 Wasm `function`（S2 可选，ABI S2 中期拍板） |
| Dapr sidecar 作为默认依赖 | 三节点 K3s 代价高，与 Kun Outbox/S2S/YARP 大面积重叠 |
| 第三方 `service`（任意代码容器）上架 | Q9：不允许；S3 只开放 package / function / connector / 受限 ui |
| 元数据注册表、动态数据引擎、Schema Migrator、表单/列表/流程引擎与设计器 | S2（ADR-011、ADR-012）；S1 只落可发布元数据框架、导入导出框架、Filter AST、控件 manifest 等契约 |
| 数据权限编译器、字段权限、授权向导完整版 | S2；S1 定 `IDataPermissionFilter / IFieldPolicy` 端口与 Filter AST、系统变量契约 |
| 钉钉、飞书、微信网站扫码连接器 | 钉钉、飞书 S2 P0，微信网站扫码 S2 P1（R4）；S1 只做通用 OIDC + 企业微信 |
| SAML / CAS / LDAP、租户自助配 IdP、HRD、Passkey、SCIM、每租户 issuer、自定义登录域 | S3（Q3、Q4） |
| 租户自助申请/审核/加入/退出、多管理员与邀请、完整状态机尾部与数据销毁、套餐 | S3 |
| DatabasePerTenant 高隔离档、独立 ns 租户 ApplicationSet、私有化 overlay | S3；S1 只保留 SchemaPerTenant 真库验证为 P2 |
| MySQL / 国产数据库作业务库；租户自带业务库连接 | Q5 不支持（本期）；后者待 S3 评估 |
| per-tenant namespace 作为默认拓扑 | ADR-017 |
| Operation/Task 资源 | S2 P0；S1 可选但必须预留契约（7.2.1、7.18 第 12 项），导入导出等长操作返回 Operation 资源（R8） |
| 应用市场、门户、报表、打印、大屏、文件预览服务 | S3 / S2（ADR-014；补充裁决 Q29–Q34）；大屏不复用参考平台源码 |
| 每租户 MCP 门面、工具派生、A2A、Skills over MCP、`extensions`、Agent 运行时、沙箱、记忆 | S4；运行时复用 iClaw（ADR-016）。skills / agents / commands / hooks / MCP 作为组件表面 S1 即支持（7.2.2），S4 增加的是门面派生、`extensions` 与 Agent 生成组件 |
| 工具描述哈希钉死与复审、授权服务器对齐 MCP（RFC 9728 等） | S3 |
| 低代码连接器与第三方出网点的出网代理与 SSRF 基线、字段敏感度标签 | S2；平台自有出网点的 SSRF 基线 S1 必做（§13.8、7.17，R10） |
| 代码生成（参考平台形态） | 不作主轨；S2 后期可选薄壳（Q11） |
| Nacos 作 Qian 配置中心；官方 Apollo .NET 客户端作生产依赖 | ADR-007：Nacos 收窄为 AI 资产注册；官方客户端只作 PoC |
| 接口体 AES 加密中间件 | 本质是混淆，应下沉网关 TLS |
| 内置 IM、自研服务器监控、请求/异常日志落库 + 一键清空、锁屏 | 非主线或多副本下无意义；日志进可观测栈 |
| 7 种布局模式与偏好抽屉、多标签完整版、全局搜索、通知抽屉完整版 | S3 可选 / S2（Q38） |
| antd 混用 | 不混用；若 S2 压力极大仅限控制台域并约定迁移期限（Q12） |
| 用户订阅矩阵、群机器人、企微/钉钉/飞书/公众号/App 推送渠道、公告管理、敏感词、短链 | S2 / S3 |
| 「调用数据接口」任务类型与任务管理 UI；脚本型任务 | S2；脚本型任务永不做 |
| 基于 Saga 的开通编排 | 不做：开通编排由 `qian.lifecycle` 插件（原 `qian.kernel` 生命周期控制器）以幂等步骤表承担（R13）；Saga 租户回灌 P0，其余修补 P1 可选 |
| Elsa 3 PoC、Flowable 适配器 | S1 末可选 PoC 对照，不进 S1 交付 |
| 引入 NocoBase / Dify / Grafana / n8n 以及参考平台代码 | 许可证限制或许可不清，只借鉴设计 |
| 国密（SM2/SM3）签名 | 无假设；待确认后另立项 |

### 待确认（与决策或其他章节可能不一致之处）

1. **平台插件数量**：已裁决（R1；再经 ADR-020 D20.7 改写）——平台插件数量不再是决策：原 18 个即 `platform` profile base 组合包里的平台插件，数量由组合包定义；`qian.notify`、`qian.scheduler` 为 P0，`qian.realtime` 为 P1 的优先级不变；本章 7.0.4 总览与之一致。
2. **组织目录归属**：已裁决（R1）——组织目录最小集作为 `qian.identity` 的子模块交付（7.4.3），不独立成 `qian.directory` 插件；§08 8.0.4 的 S2 增量沿用此归属。
3. **短信 OTP 登录的阶段**：S 基线把短信 OTP 列 S1 可选 / S3；ADR-005 推荐内容写「密码/短信/TOTP」，补充裁决「短信渠道 S1 必做（登录验证码与找回密码依赖它）」。本章按 ADR-005 把短信 OTP 作为 S1 P0 认证因子。
4. **机器身份签发**：K 基线把 `client_credentials` / 机器身份列 S1 可选（S4 前必做）；S 基线 C12 列 S1 必做。本章按 S 基线与 7.13 调度回调、7.16 adapter 的实际需要列为 P0（由 OpenIddict 提供，成本低）。
5. **实时推送**：已裁决（R1）——`qian.realtime` 列入 S1 平台插件（ADR-020），P1；无实时通道时站内信/权限刷新降级为轮询。
6. **章节编号假设**：本章把 S2 低代码构建章引用为 `§08`；S3/S4 及其他章节编号未知，正文未引用。请在章节总目录定稿后统一校正。
7. **Kun 修复的两处判断依赖实证**：7.1 第 2 项「领域事件→集成事件路径丢消息」为代码路径推断（无测试），第 10 项「热重载会重跑校验但不保留旧值」为依 .NET 10 运行时源码推断；两者都要求 S1 第一周用最小集成测试实证后再定上游 PR 还是 Qian 侧规定。
8. **GPU 归属与 Apollo 版本**：7.16 的 GPUStack 需 GPU 归属拍板（Q17 留给产品负责人）；7.17 的 Apollo 首发按 Q8 取 2.5.x，若 3.0.x 首个补丁在 S1 内发布可改选，需在 ADR-007 转正时注明。
9. **ADR-020 §7 六条待确认事项对本章的影响**：(1) TypeScript 侧（yue 外壳、`qian` CLI）直接采用 Cordis 还是自研同语义的 `@qian/runtime`——影响 7.2.1 与 7.15.1，本章按自研写；(2) 清单文件名 `.qian-plugin/plugin.json` 还是沿用 `qian-plugin.yaml`——7.2.1–7.2.3 按前者写；(3) `qian` CLI 实现语言 Node + `dotnet qian` 薄包装还是 .NET global tool——7.2.3 按前者写；(4) 官方市场仓库名称与可见性——7.2.4 按「同组织独立仓库、S1 私有」写；(5) 官方代码插件在 `platform` profile 的热装卸是否进 S2 及其与 Kun AOT 约束的兼容验证归属（R27 已定 S1 默认进程重启、第三方与 AOT 边缘永不热插）——7.19 第 1 行；(6) 许可证与公开 feed（沿 ADR-019 §7）——影响 7.2.4 社区市场与 `qian add` 的包源。
10. **`standalone` / `minimal` profile 的精确内容与 adapter / shell 的落位**：已裁决（R31）——`standalone` = 消费者自己的宿主装最小组合包；`minimal` = 测试与 SDK 用最小树；原 `qian-shell` 不是 profile，yue 外壳为 `platform` 的 `shell` 组合包（静态前端制品，经 `gateway` 提供）；原 `qian-model-gateway-adapter` 为 `model-gateway` 组合包，默认挂 `platform`，可选独立 profile `model-gateway`（S1·P1）；profile 可被 patch 合并或拆分，7.0.4 表中为 S1 默认。本章 7.0.4、7.15.1、7.16、7.17 已按此写；`connector` 不是 `runtime.form` 而是为某 seam 提供 Provider 的组件（`provides[]` + `policy.egress`），7.4.2 已按此写。
11. **未挂载组件的响应**：已裁决（R32）——面向业务与模型的表面（网关路由、`ctx.Tools`、能力目录、菜单）一律 404，与不存在无法区分（ADR-020 D20.5）；只有管理面 `/api/registry/*` 在调用者持有 `qian.registry:scope:read` 时返回 403 `plugin_not_mounted` 并附原因（未授权 / 未挂载 / 被 restrict）。7.2.1、7.9、7.14 已按此改。
12. **CLI 动词与端点前缀**：已裁决（R33）——CLI 统一为 `qian pack | sign | publish | dev` 与 `qian add | remove | list | doctor | dump-config | init`（去掉 v1 的 `plugin` 名词）；管理面端点统一 `/api/registry/*`、`/api/lifecycle/*`、`/api/capabilities/*`，v1 `/api/kernel/*` 在 S1 内保留为别名并带 `Deprecation` 响应头，S2 移除。本章 7.2.1、7.2.3、7.9、7.10、7.17、7.18 与 §08 引用已按此改。
13. **三个平台插件的包名、服务键与旧字段**：已裁决（R34）——`Qian.Registry`（`ctx.Registry`）、`Qian.Lifecycle`（`ctx.Lifecycle`）、`Qian.Capabilities`（`ctx.Capabilities`），只进 `platform` profile，不是供消费者独立安装的库（ADR-019 D19.3 的清单另行补）；v1 `tenant_plugin_installs.Enabled / Disabled` 删除，挂载状态只在 `scope_patches`；`alwaysEnabled` 改为 `policy.required: true`（不可从 `platform` profile 卸载）。本章 7.0.4、7.2.1、7.14 已按此改。

