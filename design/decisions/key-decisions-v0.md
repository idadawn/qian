> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Qian 关键决策清单 v0（已全部拍板，作为历史记录保留）

> 本页由构建脚本镜像自仓库 [`docs/decisions/key-decisions-v0.md`](https://github.com/idadawn/qian/blob/main/docs/decisions/key-decisions-v0.md)，请修改源文件，不要改本页。

> 状态：草案，2026-10-06。用途：在写 PRD 之前把会牵动整体的决策摆到一处，每条给出推荐、备选、依据和需要产品负责人确认的前提。拍板后转为 ADR（`docs/adr/`），PRD 只引用不重复。
> 依据来源：7 份领域基线（`research/baseline-digest.md` 为摘要；全文在仓库外的调研目录）。凡引用「参考平台」指被借鉴的商业低代码产品，只借鉴功能与教训，不引入代码。
> 阶段口径：S1 技术底座期 → S2 低代码构建期 → S3 面向客户期 → S4 AI 原生 Agent 平台。各阶段退出标准见 D18。
> 2026-10-06 追加：D1–D18 已全部认可并转为 ADR-001…018；D19「库优先」由产品负责人同日的方向修正新增（ADR-019）；其待确认项（许可证、公开 feed、组件安装入口等）已于 2026-10-07 由产品负责人授权全部拍板，ADR-019 转为 Accepted（结论见 ADR-019 §7）。
> 2026-10-06 第二条方向修正：D20「组件模型 v2」新增（ADR-020）——一切皆插件、没有特权内核；组件 = 文件夹 + 清单 + 声明式表面 + 可选代码；`qian add` 一键安装与目录市场。D2、D3 的部分条目被 D20 取代，D14、D19 被修订，修订清单见 ADR-020 §6。其 §7 八条待确认项已于 2026-10-07 由产品负责人授权全部拍板，ADR-020 转为 Accepted。
> 2026-10-07 追加：D21「产品族结构」应产品负责人「Kun 与 Qian 如何定位、是否建大项目关联子项目、Kun 要体现在文档中」之问新增（ADR-021，Accepted）；修订 ADR-001 / ADR-013，不改 D1 的消费方式。

## 一页总览

| #   | 决策                      | 推荐                                                                                                                                                                                                                                                       | 需要你拍板的点                                                                                                                                                                                                                                                                                |
| --- | ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| D1  | Qian 与 Kun 的关系          | Kun 是底座 SDK，Qian 内核自成一层；NuGet + CalVer 锁版本消费；Kun 的 13 项关键缺口在 S1 先修                                                                                                                                                                                       | 修复走上游 PR 还是 Qian 维护补丁包；Kun 的维护人与响应节奏                                                                                                                                                                                                                                                   |
| D2  | 插件模型                    | kind × role × distribution 三轴；平台自带能力也是插件；第三方代码永不进共享进程                                                                                                                                                                                                    | S3 是否允许第三方任意代码容器上架                                                                                                                                                                                                                                                                     |
| D3  | 内核边界与 S1 服务拆分           | 内核只留契约、强制执行点、注册表；S1 共 5 个可部署服务 + 18 个系统插件                                                                                                                                                                                                                | 接受「S1 预装全部官方插件 + 按租户开关」                                                                                                                                                                                                                                                                |
| D4  | 数据库与租户隔离                | PostgreSQL 唯一一等库；默认共享库 + RLS；schema-per-service                                                                                                                                                                                                          | 是否接受不支持 MySQL/国产库                                                                                                                                                                                                                                                                      |
| D5  | 身份与 SSO                 | 基于 OpenIddict 自建 `qian.identity` 作平台唯一 issuer；外部 IdP 一律做成连接器插件；Authentik 退居运维侧                                                                                                                                                                           | 是否有私有化交付、是否首期必须 SAML/CAS/LDAP、是否需要租户自定义登录域                                                                                                                                                                                                                                             |
| D6  | 鉴权模型                    | Kun 侧只加判定端口，Qian 自建权限引擎：权限点由插件清单声明 + 租户域 RBAC + 数据权限编译为查询谓词 + 字段权限；权限不进令牌                                                                                                                                                                                | 等保/商密要求；MFA 是否 S1 必做                                                                                                                                                                                                                                                                   |
| D7  | 配置中心                    | Apollo 作 L1 共享组件（需新增 MySQL），自研 AOT 友好的 .NET provider；只存非敏感配置与密钥引用；租户级配置不进 Apollo；Nacos 收窄为 AI 资产注册                                                                                                                                                       | MySQL 用托管还是集群内；Apollo 首发 2.5.x 还是 3.0.x；是否与 iClaw 共用                                                                                                                                                                                                                                   |
| D8  | 事件与消息                   | RabbitMQ 上 K3s；修 Outbox 丢消息与至少一次语义；事件信封对齐 iClaw 契约                                                                                                                                                                                                       | —                                                                                                                                                                                                                                                                                      |
| D9  | 审计                      | 只留一套：命令级哈希链落 PostgreSQL + 实体级同事务写入；actor 含 agent                                                                                                                                                                                                         | —                                                                                                                                                                                                                                                                                      |
| D10 | 网关分层                    | Traefik（北向入口）→ `qian.gateway`（Kun.Gateway 加固，业务 API 边界）；Higress 只管模型与 MCP 流量                                                                                                                                                                             | —                                                                                                                                                                                                                                                                                      |
| D11 | 元数据与动态数据层               | 元数据驱动运行时为主；动态表走「物理主表 + JSON 列」混合存储，不走 EF 模型；统一 Filter AST；正式的 Schema Migrator；代码生成退化为可选薄壳                                                                                                                                                                | 是否保留代码生成；低代码业务库是否允许租户自带                                                                                                                                                                                                                                                                |
| D12 | 流程引擎                    | 自研 .NET 轻量审批内核；简单/标准流程合并为一套 JSON 定义；S2 只做节点树视图设计器                                                                                                                                                                                                        | S2 最小集边界（加签/会签延后计算/子流程/超时哪些升为必做）                                                                                                                                                                                                                                                       |
| D13 | 前端与 yue                 | React 19 + Vite + Tailwind v4 + Base UI + shadcn 源码自有化；六层分层；一套表单/表格/弹层引擎贯穿后台与低代码运行时；Module Federation 2 外壳                                                                                                                                               | 接受自建数据密集组件的交付风险；S2 是否出 H5 渲染器                                                                                                                                                                                                                                                          |
| D14 | 应用市场形态                  | 应用 = 插件包（bundle）；template / managed 两种分发；S1 就定下包格式、升级与冲突处理、权益 API                                                                                                                                                                                        | 默认分发模式；共享/专属部署是否作为套餐差异                                                                                                                                                                                                                                                                 |
| D15 | 模型服务网关                  | 并入团队现有 Higress/MaaS 基础设施；GPUStack 作为 OpenAI 兼容上游 + Provider Adapter；不自写模型代理；不把 SSO 交给 Higress                                                                                                                                                            | GPU 归属；接入范围是否含 ASR/TTS/Rerank                                                                                                                                                                                                                                                          |
| D16 | 与 iClaw 的边界             | Qian = 能力提供方 + 业务主数据权威；iClaw = Agent 运行时 + AI 资产控制面；共用 Kun、Higress；Qian 不写任何 Agent loop                                                                                                                                                                  | 租户/身份权威归谁；是否共用 Authentik、S2S 签发、Usage Ledger                                                                                                                                                                                                                                           |
| D17 | 部署                      | 沿用现有 K3s/ArgoCD/Kustomize/Harbor 约定；Qian 独立 ns + 专属 PG/Redis；不以 per-tenant namespace 为默认                                                                                                                                                                 | 生产与 iClaw 共集群还是独立集群/节点池                                                                                                                                                                                                                                                                |
| D18 | 阶段切分与 S1 范围             | S1 18 个系统插件 + SDK + 部署物料；每阶段有可验收的退出标准                                                                                                                                                                                                                    | 阶段顺序是否认可；S1 是否含模型网关底座接线                                                                                                                                                                                                                                                                |
| D19 | 库优先与 Qian 包族            | 每个系统组件首先是可独立安装使用的 NuGet 包，其次才经清单成为插件（插件 = 库 + 清单）；`Abstractions` / 实现两层；独立 / 平台两种模式同一份代码；组件库不依赖 `Qian.Kernel` 运行时                                                                                                                                        | 已拍板（2026-10-07，ADR-019 §7）：许可证 Apache-2.0；S1 GitHub Packages（.NET 包公开可见、安装需令牌，R93）、S2 末或首个外部宿主试用时公开到 nuget.org / npm；安装入口由 ADR-020 定义（`qian add` + 目录市场）；外部应用清单不单独立项；S1 不提供元包                                                                                                          |
| D20 | 组件模型 v2                 | 一切皆插件、没有特权内核（极小的 `Qian.Runtime` / `@qian/runtime`）；组件 = 文件夹 + 清单 + 声明式表面 + 可选代码，`kind` 退为 `runtime.form`；能力按 seam 三角色拆分；作用域两层扁平，租户启用 = scope patch；profile / 组合包 / patch 分层取代固定可部署单元与 18 个系统插件；`qian add` 一键安装 + `marketplace.json` 目录市场；Agent 原生表面 S1 起 | 已拍板（2026-10-07，ADR-020 §7）：TS 侧 `@qian/runtime` 为公共 API、S1 内部 vendor Cordis 实现；清单 `.qian-plugin/plugin.json`；CLI 为 Node / TS 实现、`npx qian@latest`、`dotnet qian` 薄包装 P2；官方市场仓库 `qian-plugins`（S1 私有）；热装卸不进 S2 核心、RT 领域 5 天 spike；Apache-2.0 + GitHub Packages；遥测默认不采集；模板名以 spec §9.1 为准 |
| D21 | 产品族结构（Kun / Qian / yue） | 三层口径；不建大单仓；一个组织 + 一个文档站 + 一张版本矩阵 + 一个看板关联各仓库；`KunVersion` 单点钉版；Kun 文档按锁定版本镜像进文档站                                                                                                                                                                         | 已拍板：所有者 `idadawn`，Kun 迁入（S1-DEC-009 关闭，S1-PM-003 执行）；仍待确认 Kun 仓库随文档站公开的时点                                                                                                                                                                                                              |

***

## D1 Qian 与 Kun 的关系

**推荐**

1. 定位：Kun 是「微服务开发框架」，不是平台内核。Qian 内核（插件注册表、生命周期、能力目录、租户目录、身份、权限、配置、审计装配）自成一层，Kun 模块是它的实现依赖。
2. 消费方式：NuGet + CalVer 锁版本，禁止把 Kun 源码复制进 Qian 仓；要求 Kun 提供预发布 feed、公共 API 兼容承诺、弃用窗口（≥2 个小版本）和完整变更日志。
3. 不推翻 Kun 的核心设计（四层依赖、Result 错误模型、显式注册的 Mediator、Outbox 表结构与稳定事件 ID、schema-per-service、RLS 双保险、PublicAPI 门禁），但改三件事：不把「AOT 兼容」写成平台承诺（业务服务全 JIT，AOT 只留网关/gRPC 边缘）；Kun 的安全默认值在 Qian 里改成强制装配而非 opt-in；多组并存的双胞胎 API 选定唯一一套（见 D10）。
4. S1 必修的 Kun 缺口（代码已确认，不修则多租户在后台链路漏）：
   - 多租户后台链路：消息消费端、Saga 执行器、Outbox 发布器不恢复租户上下文；两套 `ITenantContext` 互斥且 claim 名不一致；请求头兜底与 `default` 回退会 fail-open。
   - 事件语义：领域事件→集成事件路径会静默丢消息；消费端实际是 at-most-once；Outbox/Saga 多副本无锁。
   - 网关默认不强制认证、限流未挂路由、私钥不能热轮换；gRPC 流式调用绕过鉴权。
   - 身份客户端 OIDC 链路未打通（PKCE verifier 不发、nonce 不校验、无 refresh、无会话吊销）。
   - 三套 S2S 签发器、两族存储接口、多条审计路径并存；框架内写死了下游产品的默认值。

**备选**：monorepo / ProjectReference 引用 Kun（iClaw 现状）。放弃原因：Kun 破坏性变更直接打断 Qian 构建，镜像构建依赖私有仓，第三方插件开发者无法依赖源码。

**需要拍板**：§4 的修复走 Kun 上游 PR（需要明确维护人与响应节奏；Kun 事实上单人维护，近两月提交明显放缓）还是 Qian 维护 `Kun.*.Patches` 补丁包并设回合入期限。

## D2 插件模型

> 2026-10-06：本条的形态轴（`kind` 七值）、租户级生命周期与「预装 + 开关」已被 D20 取代，被 D20 取代 / 修订的部分见 ADR-020 §6；其余（信任等级、清单从代码派生、digest 引用、能力条目 Agent 字段、Wasm 阶段）保留并映射到新字段。

**推荐**

- 三个正交轴：`kind`（顶层七值：module 进程内编译期并入 / service 独立 Pod / package 元数据 / function Wasm / connector / ui / bundle 复合包，bundle 的 `components` 下各组件的 kind 取前六值）× `role`（provider / capability / extension / app / theme / agent）× `distribution`（template 拷贝即拥有 / managed 托管可升级）。一个「应用」= `kind: bundle`。
- 平台自带的技术组件（租户、身份、鉴权、存储、文件、配置、网关、模型网关）全部按同一清单以 `tier: system` 交付，同一生命周期、同一 TCK。
- 信任等级决定形态：第三方代码永不进共享进程；`private/community` 只允许 package / function / connector / 受限 ui。
- 清单（`qian-plugin.yaml`）从代码派生、静态可索引：宿主不执行插件代码即可生成路由、权限点、菜单、Apollo namespace、OpenAPI 聚合与 MCP 工具描述。
- 两层生命周期：平台级（制品 → 修订 → 激活，保留旧修订可回切）与租户级（授权 → 开通 → 启用 → 升级 → 卸载 → 清除）分开建模；租户级钩子逐租户、幂等、可重试。
- S1 接受「预装全部官方 module + 按租户开关」；运行时装载只对 package / service 成立。

**明确不做**：运行时热插 DLL；进程共享的服务端 JS 引擎；Dapr sidecar 作为默认依赖；引入 NocoBase / Dify / Grafana / n8n 代码（许可证限制，只借鉴设计）。

**需要拍板**：S3 是否允许第三方 `service`（任意代码容器）上架。这决定审核与运维投入量级；可参照「禁第三方代码但允许纯数据模块」的做法先只开放 package / function / connector。

## D3 内核边界与 S1 服务拆分

> 2026-10-06：本条的内核定位与「5 个可部署单元 + 18 个系统插件」已被 D20 取代（内核 → 极小的 `Qian.Runtime`；可部署单元 → profile；系统插件数量不再是决策），被 D20 取代 / 修订的部分见 ADR-020 §6；强制执行点（鉴权、租户、审计）保留为 base 组合包里的 waterfall 监听器。

**推荐**

- 内核最小集：租户上下文与解析、主体身份与 S2S、权限判定点（PDP 接口 + PEP）、插件注册表与生命周期控制器、扩展点/契约仓库、能力目录（命令/查询/事件/实体四类）、事件总线、审计、配置绑定契约、可观测与计量。其余一律是插件。
- S1 可部署单元克制在 5 个（三节点 K3s 的现实）：`qian-gateway`（Kun.Gateway 加固）、`qian-identity`（OpenIddict，独立安全边界）、`qian-platform`（内核 + tenancy / authz / config / events / audit / files 等 module 型系统插件的宿主）、`qian-model-gateway-adapter`（GPUStack ↔ Higress 控制面适配，可选）、`qian-shell`（前端外壳静态站）。
- S1 系统插件 18 个：`qian.kernel`、`qian.plugin-sdk`、`qian.tenancy`、`qian.identity`（+ `connector.*`）、`qian.authz`、`qian.data`、`qian.files`、`qian.config`、`qian.gateway`、`qian.events`、`qian.audit`、`qian.observability`、`qian.shell`、`qian.entitlement`（授权 v0）、`qian.model-gateway`（P1，可选）、`qian.notify`（P0）、`qian.scheduler`（P0）、`qian.realtime`（P1）。组织目录最小集作为 `qian.identity` 的子模块交付，不独立成插件。

**需要拍板**：接受 S1 的「预装 + 开关」模式（见 D2）。

## D4 数据库与租户隔离

**推荐**

- PostgreSQL 16 为唯一一等数据库。Kun 的 RLS、schema 供给、PgBouncer 方案全部是 PG 专属，MySQL 只有版本钉住没有任何引用与测试。
- 默认隔离策略：共享库 + 行级 `tenant_id` 过滤 + PostgreSQL RLS 双保险（唯一有真库集成测试的策略）；S1 可选验证 schema-per-tenant；S3 提供独立库高隔离档作为付费选项。
- 平台自身表按 schema-per-service；低代码动态表见 D11。

**需要拍板**：是否接受本期不支持 MySQL / 国产数据库；是否允许业务库由租户自带连接。

## D5 身份与 SSO

**推荐**（基线称「混合 C2 路线」）

- 自建 `qian.identity`：基于 OpenIddict（Apache-2.0，.NET 10，原生 OIDC/OAuth2）作平台唯一 issuer。内容：自然人（Person）+ 租户成员（Membership）双层账号、密码/短信/TOTP、会话中心（强制下线、后通道登出、refresh 轮换、`identity_revision`）、客户端注册表（应用安装即建 client）。
- 上游联邦全部做成连接器插件：S1 只做通用 OIDC + 企业微信（扫码与工作台免登）；S2 钉钉、飞书（P0）与微信网站扫码（P1）；S3 加 SAML/CAS/LDAP、租户自助配 IdP、域名验证与 HRD、Passkey、SCIM、ISV 第三方应用模式。
- Authentik 升级后只服务运维侧（Apollo Portal、Headlamp、Argo），也可作为一个上游连接器；客户 CIAM 不用它（企业版多租户是 alpha 且按租户收费）。
- 同时修 Kun.Identity 的 G2–G8 缺口。

**备选**：Keycloak 26.x Organizations 做协议层（协议最全，但 Java、每 Pod 约 1.25 GB、国内登录要写 SPI、租户主数据双真相）；Logto OSS（国内连接器全，但 Node 栈、无 LDAP、部分付费）。不推荐 Casdoor（2026-05 一次 9 个 CVE）、Zitadel（AGPL）、Duende（商业许可）。

**需要拍板**：是否有私有化/离线交付（决定 AGPL/商业许可可否）；首期是否必须 SAML/CAS/LDAP（政企、高校客户）；租户自定义登录域名是否刚需（决定单 issuer 还是每租户 issuer）；是否有等保/商密（SM2/SM3）要求。

## D6 鉴权模型

**推荐**

- Kun 侧只新增 `Kun.Authorization` 端口（`IPermissionChecker` Check / BatchCheck / ListAllowedActions、`IDataPermissionFilter`、`IFieldPolicy` + ASP.NET Core / Mediator / gRPC 集成），不放实现。
- Qian 的 `qian.authz`：权限点与默认角色模板由插件清单声明；租户域 RBAC（内置 + 自定义角色，主体可为用户/组织/岗位/组）；数据权限 = 条件树 + 系统变量，编译成 SQL/EF 谓词在本地执行，RLS 永远兜底；字段权限读投影/写白名单；每次决策可解释并入审计。
- 权限不进令牌：令牌只含身份与会话类 claim（规范集 `sub`、`tenant_id`、`membership_id`、`scope`、`identity_revision`、`sid`、`amr`、`acr`，预留 `profile`、`act`），不含权限集合（S2S 令牌的 `roles[]` 例外见 D10 / R24）；权限变更即时失效靠 `identity_revision` 递增 + 推送刷新，网关只转发 `identity_revision`。
- 鉴权唯一入口：UI、API、MCP 三入口同一个判定服务；低代码数据引擎在唯一入口做「资源 + 动作 + 行级」三段式校验。参考平台鉴权分散在控制器且可被客户端参数绕过，是明确反例。
- S3 可选：OpenFGA 插件处理资源实例共享与市场授权；但行级过滤不交给远程引擎。

**需要拍板**：等保 2.0 / 商密要求；MFA 是否 S1 必做。

## D7 配置中心 Apollo

**推荐**

- Apollo 作为 L1 共享平台组件按现有「vendor chart + 离线渲染入 git」范式部署；版本 ≥2.5.2（3.0.0 刚于 2026-10-03 发布，生产等首个补丁）；需要新增 MySQL（Apollo 唯一官方生产库，现网全 PG、无 MySQL）；Portal 接 Authentik OIDC、不公网。
- .NET 客户端：官方客户端维护停滞、反射式序列化不适配 AOT/裁剪、无缓存时启动阻塞后以空配置继续——因此自研轻量 provider（按公开 HTTP 协议：`/configs`、`/notifications/v2`、AccessKey 签名），官方 SDK 只作 PoC。
- `Kun.Configuration` 增强：运行期变更先校验、失败保留上一份有效值并告警；options JSON Schema 导出。
- 边界：Apollo 只存平台/服务级非敏感配置与密钥引用（secretRef）；密钥走 K8s Secret / Infisical；租户级配置不进 Apollo（Apollo 无租户概念、namespace 有上限、灰度只按实例），由 `qian.tenancy` 的租户配置服务承载。
- 映射：AppId = 一个微服务/后端插件；Environment 只建 UAT + PRO；私有 namespace `application` 与 `qian.plugin.<id>`，公共 namespace `qian.common.*`。
- 修订现有边界 ADR：Nacos 收窄为 AI 资产 / MCP 注册，不再承担配置职责。

**需要拍板**：MySQL 托管 RDS 还是集群内 operator；首发版本；是否与 iClaw 共用一套 Apollo。

## D8 事件与消息

**推荐**：RabbitMQ 上 K3s（现网未部署，Outbox 空转）；修复 Outbox 丢消息、消费端改至少一次、Redis 幂等、锁默认开、退避；事件信封对齐 iClaw 的 `version / event_id / subject / occurred_at / tenant_id / payload`；事件目录（稳定 ID + JSON Schema 导出）升格为能力目录的一部分。

## D9 审计

**推荐**：固定一套——命令/能力级 `AuditEntry` 哈希链 + PostgreSQL 持久化链状态；实体级 `AuditLog` 走同事务拦截器（借 iClaw 写法）；弃用事后拦截器路径；`actor_type` 增 `agent`，记录 `on_behalf_of`、`capability_id`、`contract_version`；实现 `IAuditReader`。

## D10 网关分层

**推荐**：三层——Traefik（北向公网入口，不做鉴权）→ `qian.gateway`（Kun.Gateway 以库方式引用并加固：强制认证、路由级限流、可信代理、密钥热轮换、Cookie 会话换 S2S、按 cluster 的 audience；S3 加租户 × 已安装应用的动态路由）；Higress 只管模型与 MCP 流量，对租户无感。两套网关职责从 S1 画清，不互相承担。

## D11 元数据与动态数据层

**推荐**

- 以元数据驱动运行时为主轨。元数据（实体/字段/视图/表单/流程定义）用普通 EF 聚合存储，版本化、可校验、可 diff、可回滚（草稿/发布双版本 + 不可变快照）。
- 动态数据平面不走 EF 模型：`qian.dynamic-data` 按实体编码读写 JSON 行、统一 Filter AST → SQL 生成（参数化 + 标识符白名单）、强制注入租户与软删谓词；住在 Kun 允许的「非 AOT 孤岛」里，复用租户上下文、RLS、Outbox。
- 存储模型：混合——每个实体一张物理主表（系统列约定：`tenant_id`、软删、审计列、乐观锁），多值/子表字段用原生 JSON 列或关联表。
- 正式的 Schema Migrator：元数据 diff → 迁移计划 → 审核 → 逐租户分批执行 → 记录；参考平台「改表 = 删表重建、只允许空表」是反例。
- 少量泛型命令（CreateRecord / UpdateRecord / DeleteRecord / QueryRecords）注册一次、内部按元数据分派；通用信封事件 `qian.record.changed.v1`。
- 代码生成退化为可选「调用运行时 SDK 的薄壳」，不早于 S2 后期；参考平台双轨（运行时 + 模板代码）导致行为漂移。

**需要拍板**：是否保留代码生成；业务库是否允许租户自带连接。

## D12 流程引擎

**推荐**

- 自研 .NET 轻量审批内核（令牌推进：排他/包容/并行网关，默认分支 = else，单库单事务），复用 Saga 的状态机命名与 Outbox；Elsa 只做 S1 末 PoC，Flowable 只作可选 BPMN 适配器。理由：「中国式审批」的工作量在审批域（会签/或签/加签/退回/转审/委托/撤回/抄送）而不在引擎，参考平台审批域代码超过 2.6 万行。
- 简单流程与标准流程合并为一套 JSON 定义、两种视图；S2 只交付瀑布/节点树视图设计器，自由画布延后。
- 流程前置能力放在 S1：组织模型含岗位/责任岗位/组织类型与上级查询契约、事件总线、可靠延时调度、表达式引擎规范（JSON AST）、消息模板注册、元数据版本化契约。
- 参考平台的全局 admin 兜底、会签停滞、转审改写记录、拼 SQL 判条件、匿名回调等 14 处做法列为必须修正。

**需要拍板**：S2 最小集边界——加签/减签、候选人、会签延后计算、子流程、限时/超时/催办目前列为可选，哪些升为必做；审批设计器形态（纵向节点树对 Agent 生成最友好 vs React Flow 自由画布）；是否需要 BPMN 导出。

## D13 前端与 yue

**推荐**

- 技术栈沿用 iClaw 已验证的：React 19 + TypeScript + Vite + Tailwind v4 + Base UI + shadcn 源码自有化（路线 A）；Zustand、react-router、i18next、Vitest + Playwright + axe、Lighthouse 预算、Changesets 发包。
- yue 六层：`@yue/tokens`（三层令牌、明暗两态、DTCG 导出）→ `@yue/core`（约 30 个基础组件）→ 后台通用层（DataTable 基于 TanStack、SchemaForm 基于 react-hook-form + Ajv、AppShell、Upload、Tree/Cascader/日期）→ 业务选择器层（组织/用户/角色/字典/接口选择）→ 低代码运行时层（表单/列表渲染器、控件注册表）→ 设计器层（表单/列表/审批流）。依赖方向用 lint 强制。
- 只保留一套表单/表格/弹层引擎贯穿 S1 后台 CRUD 与 S2 低代码运行时；参考平台的弹窗/表单/表格双轨、控件三处呈现各自维护是反例。
- 外壳：Module Federation 2 运行时加载 + 带版本号的插槽注册表；共享依赖 singleton；不可信 UI 走 Worker 远程渲染或 iframe（S3）。
- 控件注册表与 yue 组件 props 的 JSON Schema 机器可读，设计器、远程渲染白名单、S4 Agent 生成 UI 共用一份契约。
- Base UI 原语覆盖未经验证（iClaw 只用了 Dialog），首迭代设验证门；不过则切 Radix。

**需要拍板**：接受自建数据密集组件的 S2 交付风险，还是允许平台控制台域临时用 antd 并约定迁移期限；S2 是否交付 H5 表单渲染器；后台壳是「控制台 / 应用前台 / 应用后台」三面还是统一一个壳按应用上下文切换。

## D14 应用市场形态

> 2026-10-06：本条的包格式被 D20 修订——应用包格式 = 组件文件夹 + marketplace 条目；OCI 制品只用于代码部分；S3 商业市场叠在同一 `marketplace.json` 格式之上。被 D20 取代 / 修订的部分见 ADR-020 §6；两种分发、元数据分层、升级与冲突处理、权益 API 的结论不变。

**推荐**

- 应用 = bundle（package 元数据 + ui + 可选 function/service + 种子数据 + 迁移脚本 + 依赖清单 + id 重映射表）。参考平台导出包不含菜单/权限/字典、导入不重映射 id，是反例。
- 两种分发都支持：`template` 拷贝即拥有（升级只能同源导入合并/覆盖）与 `managed` 托管锁定可升级（租户在上层叠加定制；升级做三方比较、冲突项列清单）。
- 演进：S1 末「一切皆包」跑通（包格式 v0、制品库、安装/升级引擎、授权 v0 手工授予）→ S2 低代码应用可打包分发、平台自营应用吃自己的狗粮 → S3 初私有目录型市场（目录/详情/安装向导/试用/升级中心/审核台）→ S3 中开放 ISV 与在线交易（入驻、席位、分成、评分）→ S4 市场兼任私有 MCP registry。
- S1 就要做对且之后几乎改不了的三件事：包格式与可定制性契约、升级与冲突处理、授权与权益 API（授权是否买了 vs 特性开关是否灰度开，分开存）。

**需要拍板**：默认分发模式；共享/专属部署是否作为套餐差异售卖；市场购买的 managed 应用是否禁止导出。

## D15 模型服务网关（本期唯一 AI 范围）

**推荐**

- 不另起一套：并入团队正在推进的 Higress / MaaS 基础设施（Higress 2.2.3 两副本、`maas-*` 路由、key-auth 双形态、网关专用 Redis HA、MaaS Loki 90 天访问日志、AI 大盘）。
- 按《GPUStack + Higress 模型服务整合方案》第 1–2 阶段补齐：ai-proxy 供应商与逻辑模型名映射入 git、ai-quota / ai-token-ratelimit / ai-statistics 绑定、价格表与账单脚本；推理层从 Xinference 切到 GPUStack。
- Qian 侧只做：GPUStack 作为 OpenAI 兼容上游注册进 Higress + Provider Adapter（发现模型、健康、生命周期）+ 填写 `model-capabilities` 契约；租户/配额/计量在平台侧，Higress 对租户无感；不自写模型代理层；不在 S1 把 SSO/S2S 交给 Higress。

**需要拍板**：GPU 归属（首节点两块 3090 已借出一块）；接入范围仅 LLM/Embedding 还是含 ASR/TTS/Rerank；Provider Adapter 由谁维护。

## D16 与 iClaw 的边界

**推荐**：Qian = 能力提供方 + 业务主数据权威；iClaw = Agent 运行时 + AI 资产控制面；Higress = 共用数据面；Kun = 共用身份/审计/Outbox 基座。Qian 不写任何 Agent loop、沙箱、记忆、技能引擎（S4 经 MCP/HTTP 对接 iClaw，沙箱对接 CubeSandbox）。对齐项：S2S JWT claim 形状、事件信封、工具元数据（`sideEffect / policyHint / requiredScopes / schemaSha256`）、契约格式（OpenAPI 3.1 + JSON Schema 2020-12）。MCP 以 2026-07-28 为目标版本并与 iClaw 制定对齐计划（其客户端仍钉在旧版）。

**需要拍板**：租户主数据权威归 Qian 内核还是 iClaw Edge（iClaw 已有两个 Tenant 实体之痛）；是否共用同一 Authentik、同一 S2S 签发、同一 Usage Ledger / 租户 API Key；Qian 应用市场与 iClaw 插件市场是否合成一个注册表。

## D17 部署

**推荐**：沿用 iclaw-deploy 的 L0/L1/L2 三层、Argo CD app-of-apps、Kustomize、不可变发布事件、按 digest 写回、迁移 Job 先行、供应链证据；Qian 新建 `qian-<env>` 命名空间与专属 PG/Redis；每个服务 `/healthz` `/readyz`；业务命名空间补 default-deny NetworkPolicy 与 ResourceQuota（现网为零）；补 Alertmanager。多租户拓扑三档：默认共享（数据按 RLS 隔离）、高隔离租户独立 ns + 独立 PG/Redis（ApplicationSet 生成）、私有化客户整套 overlay 复制；不以 per-tenant namespace 为默认。新增第二类制品「插件包（OCI artifact）」推 Harbor。

**需要拍板**：生产与 iClaw 共用同一 K3s（同 Argo/Authentik/Higress）还是独立集群/节点池（现网故障域是整台首节点）。

## D18 阶段切分与 S1 范围

| 阶段                | 退出标准                                                                                          | 核心交付                                                                                                                                                                |
| ----------------- | --------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| S1 技术底座           | 开发人员用代码在 Qian 上写出一个多租户应用：登录/SSO、鉴权、数据存储、文件上传、配置、网关、事件、审计全部由平台提供；一个官方 module 插件能独立出修订、逐租户迁移、回切 | 18 个系统插件、SDK（源生成器 + CLI + TCK）、部署物料、Kun 缺口修复、能力目录与清单 v1、授权 v0、yue 令牌层 + 基础组件 + 后台壳                                                                                  |
| S2 低代码构建          | 跑通「建应用 → 建表单与列表 → 走审批 → 授权给租户用户使用」；同一应用包在两个租户分别安装、各自配置、独立升级互不影响                               | 元数据注册表、动态数据引擎、Schema Migrator、表单/列表/审批流设计器与运行时、流程内核、控件注册表、数据权限与字段权限、Operation/Task；S2 可选项（不计入核心交付）：Wasm `function` 运行时（P2，ABI 在 S2 中期拍板；服务端公式与规则先用 JSON AST 表达式解释器） |
| S3 面向客户           | 租户能从应用市场选购并开通应用，自助配置 IdP 与品牌                                                                  | 私有目录型市场 → ISV 开放、权益与计费、门户、报表、租户自助、高隔离档、不可信 UI 沙箱                                                                                                                    |
| S4 AI 原生 Agent 平台 | Agent 能发现、调用、编排乃至生成平台能力与应用                                                                    | 每租户 MCP 门面、Agent 主体与委托链、元数据生成闭环、与 iClaw 运行时对接                                                                                                                       |

S1 到 S3 期间必须提前做对、否则 S4 返工的设计（S1 起生效）：所有写操作走带 Schema 的命令（含副作用等级、幂等键、dryRun）；结构化可自纠正的错误；统一主体模型（user / service / agent）与委托链字段；可解释可预检的权限判定；一份契约多处生成；租户上下文只来自令牌、`inputSchema` 禁止租户字段；审计含主体链与来源通道；追踪与计量贯通；事件契约化；插件包签名与反向权限审批。

**需要拍板**：四段顺序是否认可（尤其「低代码构建」单独成 S2 并先于「面向客户」）；S1 是否包含模型网关底座接线（可选项）。

## D19 库优先（Library-first）与 Qian 包族

> 2026-10-06 方向修正新增：产品负责人希望「别人安装一个 NuGet 包就可以直接使用，尤其是通用的一些组件」，而不是只能作为客户端调用 Qian 平台 API。详见 ADR-019（Accepted，2026-10-07）。同日产品负责人进一步要求组件模型参考「声明式组件 + 一键安装 + 目录市场 + 一切皆插件」的设计，组件的安装入口与声明格式由后续 ADR 定义；本条只定库的形态与依赖规则。（已由 D20 / ADR-020 定义；本条 D19.2 / D19.4 的装配表述经 ADR-020 §6 修订为「`Apply` 内部细节」，组件库唯一必需的 Qian 依赖改为 `Qian.Runtime`。）

**推荐**

- 每个 Qian 系统组件首先是可在任意 ASP.NET Core（.NET 10）应用中独立安装使用的 NuGet 包，其次才通过清单成为 Qian 插件；「插件 = 库 + 清单」；Qian 平台 = `Qian.Kernel` + 这些库的装配。前端 yue 的 `@yue/*` npm 包同理（D13）。
- 包分层：`Qian.<Component>.Abstractions`（接口 / 契约 / DTO，仅依赖 `Kun.*.Abstractions`）→ `Qian.<Component>`（实现 + EF 迁移随包；`AddQian<Component>()` 只是 .NET 宿主的底层装配方式，不是面向开发者的安装入口）；源生成器与钩子基类在 `Qian.Plugin.Sdk`；组件库不得依赖 `Qian.Kernel` 运行时。S1 包清单见 ADR-019 D19.3，与 D3 的 18 个系统插件一一对应。
- 两种运行模式、同一份代码：**独立模式**（宿主未装配内核：宿主提供 `ITenantContext` 或用内置 `SingleTenant`；权限点经 `[Permission]` 特性注册、角色与授权用库自带表与 API；任意 OIDC 发行方或嵌入 `Qian.Identity.Server`；标准 `IConfiguration`，Apollo 可选；Outbox 用宿主 DbContext）与**平台模式**（内核接管注册表、两层生命周期、租户级配置、能力目录、权益开关）。切换不改业务代码。
- 隔离规则：组件库只依赖 `Kun.*` 与 `Qian.*.Abstractions`，彼此只经 Abstractions 引用；各库只拥有自己的 schema；多租户安全默认（fail-closed、RLS）在独立模式同样生效，需显式 `SingleTenant` 才能关闭。
- 与 Kun 的分界：无产品观点的通用基础设施上游进 Kun；带多租户 / 业务语义的组件（租户主档、IdP、RBAC 引擎、带配额与策略的文件服务、消息、调度）是 Qian 包（D1 的补充）。
- 版本与分发：SemVer、同一版本号锁步发布；S1 发到 GitHub Packages（.NET 包公开可见、安装需令牌，R93）；`Microsoft.CodeAnalysis.PublicApiAnalyzers` 守门；TCK 增加独立模式套件。
- S1 验收四条：空白 ASP.NET Core 项目安装 `Qian.Files` + `Qian.Authz` + `Qian.Identity.Client`、不部署平台、照 README 30 分钟内跑通上传与权限检查（样例 `samples/standalone-minimal` 入仓）；同一组包在 `qian-platform` 平台模式行为一致（TCK 双模式）；`Qian.<Component>` 依赖图不含 `Qian.Kernel`（CI 断言）；嵌入 `Qian.Identity.Server` 的独立宿主过 OpenIddict 一致性测试。

**备选**：只提供平台 HTTP API、外部系统作客户端（原口径；放弃：复用门槛 = 部署整套平台）；组件全部上游进 Kun（放弃：Kun 不做 IdP / 权限 / 租户目录，且单人维护）；单个 `Qian.All` 大包（放弃作为主形态：隐式带入内核、无法按需装配）。

**明确不做**：不提供「拆单个库替代 Qian 平台」的支持承诺（独立模式是能力，不是第二个产品）；不为独立模式做低代码设计器与运行时；非 .NET 消费者沿用 OIDC + HTTP API。

**已拍板（2026-10-07，ADR-019 §7）**：

- 安装入口与组件模型：由 ADR-020 定义（`qian add`、`.qian-plugin/plugin.json`、`.qian/marketplace.json`）；D19.2 / D19.4 的装配表述以 ADR-020 为准。
- 许可证：Qian 包族、`qian` CLI、yue 一律 Apache-2.0；S3 商业组件另定（理由与可回退条件见 ADR-020 §7 第 6 条）。
- 分发渠道：S1 GitHub Packages 私有；S2 末或首个外部宿主试用时公开到 nuget.org / npm（前提：PublicApiAnalyzers 门禁与 0.x → 1.0 SemVer 策略就位）。
- 「外部应用清单」：不单独立项——非 .NET 外部应用以「只含声明的组件文件夹」接入（无 `src/`），S1 不专门交付，S3 随 ISV 入驻给出模板。
- 元包：S1 不提供 `Qian.All` / `Qian.Platform`，profile 的组合包承担「一组包一起装」的职责。
- `IPermissionChecker` 端口归属：`Kun.Authorization` 保持端口定义（ADR-006 D6.1），`Qian.Authz.Abstractions` 只依赖并再导出，不重复定义。
- 包名与插件 ID 的映射：写在 `.qian-plugin/plugin.json` 的 `runtime.nuget[]` / `runtime.npm[]`（包名 + 版本 + digest），不在 Schema 里加 `assembly` 约束。
- ADR-019 状态：2026-10-07 转为 Accepted，细则由 ADR-020 与 specs/component-model.md 吸收。

***

## D20 组件模型 v2（一切皆插件、声明式组件、一键安装、目录市场）

> 2026-10-06 第二条方向修正新增：产品负责人要求组件化设计对标当代 Agent 生态的三类做法（文件即组件、一条命令安装；插件 = 文件夹 + 清单 + 声明式表面；一切皆插件的运行时），「不要再做传统设计」。详见 ADR-020（Accepted，2026-10-07）。Kun 仍是底座（D1 不变），多租户 / SSO / 鉴权 / K3s / Apollo 的硬要求不变。

**推荐**（ADR-020 D20.1–D20.11 浓缩）

1. 一切皆插件，没有特权内核：运行时只剩极小的 `Qian.Runtime`（上下文 `ctx`、服务注册、类型化事件、可逆副作用、加载器）；租户、身份、鉴权、文件、配置、网关、事件、审计、低代码运行时、Agent 门面全部是插件，向共享上下文贡献服务（`ctx.Tenancy`、`ctx.Identity`、`ctx.Authz`、`ctx.Files`…）；原 `qian.kernel` 的注册表、生命周期控制器、能力目录、契约仓库改为普通插件（`qian.registry`、`qian.lifecycle`、`qian.capabilities`），每一项都能从配置替换。
2. 插件最小接口 = `Inject` + `Apply`：声明依赖的服务键，依赖就绪才启动，装载顺序由依赖表达；所有注册经 `ctx.Effect()` 返回释放函数，卸载 / 重载 / teardown 时逆序撤销。.NET 落地 `IQianPlugin`（keyed services + `Kun.Mediator`），TypeScript 落地 `@qian/runtime`（yue 外壳也是一棵插件树），语义相同。
3. 事件即扩展点，五种分发模式（`emit` / `waterfall` / `parallel` / `serial` / `bail`）；定义处标注模式，CI 校验声明与调用点一致并生成「生产方与消费方」映射；拦截与策略优先用事件，直接能力调用优先用服务方法。
4. 能力按 seam 三角色拆分：Service Definition（拥有 `ctx.<key>`，放 `Qian.<X>.Abstractions`）+ Provider（`Qian.<X>.<Provider>`）+ Consumer（含面向模型的工具）；扩展插件只依赖 Definition，换 Provider 不动 Consumer。D19 的 Abstractions / 实现分层据此落位。
5. 作用域两层扁平：任一注册要么全局、要么归属恰好一个 scope（S1：租户、应用、Agent 会话）；带作用域的注册遮蔽同名全局项；`restrict` 过滤全局集合、多个取交集，被过滤项不出现在目录与提示词里且拒绝执行；scope 不继承。**租户启用插件 = 在租户 scope 挂载其配置条目；租户定制 = 带作用域的遮蔽注册**（取代 D2 的租户级生命周期与「预装 + 开关」）。
6. 组件 = 文件夹 + 清单 + 声明式表面 + 可选代码：`.qian-plugin/plugin.json` 清单（`id`、`version`、`interface`、各表面指针、`runtime`、`inject` / `provides`、`policy`、`scopes`）+ `skills/`、`agents/`、`commands/`、`hooks/`、`.mcp.json`、`tools/`、`ui/`、`forms/ lists/ flows/ pages/`（S2）、`schemas/`、`config/qian.patch.yml`、`src/`（可选）。原 `kind` 七值退为 `runtime.form` 的实现细节，顶层分类改为「表面（surface）」；只有一份清单，不再手写 YAML 与派生文件并存；无代码组件是常态。
7. profile / 组合包 / patch 分层：运行中的 Qian 是一棵插件树，按 profile（S1 随发行版交付 `platform` / `gateway` / `identity` / `standalone` / `minimal`）→ 组合包 → profile patch → home patch → `--patch` overlay 叠加；`qian --profile platform --dump-config` 打印整棵树，任何条目可被 patch 替换；租户层 = 租户 scope 的 patch（存数据库、与文件 patch 同格式同校验器）。**可部署单元 = profile；系统插件 = `platform` profile 的 base 组合包内容，数量不再是决策**（取代 D3 的「5 + 18」）。
8. 一键安装与目录市场：`qian add <component>[@version]`（复制声明式表面进 `.qian/`、加 NuGet / npm 依赖、写入当前 profile 的 patch），配套 `remove / list / doctor / dump-config / init --template <stack>`；市场 = git 仓库里的 `.qian/marketplace.json`，条目只写 `name`、`source`（local / url / git-subdir）、`policy`、`category`、`interface`；官方 / 租户私有 / 社区多市场并存；目录网站由 marketplace.json 生成。D14 据此修订：应用包格式 = 组件文件夹 + marketplace 条目，OCI 仅代码部分，S3 商业市场叠加。
9. Agent 原生表面是一等公民、S1 起：skill、agent、command、hook、MCP 服务器、tool、提示词片段 S1 即受支持；模型可见的一切注册在 `ctx.Tools` 等注册表并受 scope 过滤；`AGENTS.md` 进每个仓库与包；配置目录 / 模块图 / 事件图 / seam 图由代码生成并设 CI 新鲜度门禁；S4 `extensions` 建立在同一装载器上，S1 只开放给人类 CLI。
10. 与 Kun、NuGet 的关系：D19 保留为代码部分的打包规则，`AddQian<X>()` 只是 `Apply` 内部细节；`Qian.Runtime` 是组件库唯一必需的 Qian 依赖，组件库不依赖 `qian.registry` / `qian.lifecycle` 等插件；Kun 的 `AddKunXxx()` 包装为插件，`Kun.*` 不改；`gateway` 等边缘 profile 可 AOT，`platform` profile 不承诺 AOT。热重载：声明式表面 S1 支持运行中重载，代码插件热装卸 S2 可选、默认进程重启。

**备选**（ADR-020 §4，均放弃）：保留 D2 / D3 只补一个 CLI（内核仍是特权层、租户开关仍在数据库、Agent 表面仍在 S4）；.NET 侧直接采用现成的 TypeScript 运行时（经 Node 侧车或 JS 引擎，与 D2「不引入进程共享的服务端 JS 引擎」冲突）；用 .NET 现有模块框架（传统「模块 + 特权内核」，无 scope、可逆副作用与 patch 分层）；声明式表面全部交给 iClaw 托管（组件无法脱离 iClaw 独立使用，违反 D19）。

**明确不做**：不再按物理形态做顶层分类；不把「预装全部 module + 数据库开关」作为租户启用的唯一路径（S1 预装仍是 `platform` profile 的默认组合包，租户开关改为 scope patch）；不维护第二套清单格式；不把现成 TypeScript 运行时的源码搬进 .NET（.NET 侧自研同语义运行时）；不在 S1 做代码插件热装卸；不为第三方代码打开共享进程（D2 信任等级规则保留，映射到 `policy.tier` 与 `policy.sandbox`）。

**已拍板（2026-10-07，ADR-020 §7 八条；产品负责人授权 ADR 作者拍板，ADR-020 转为 Accepted）**：

1. TypeScript 侧：`@qian/runtime` 为公共 API 包，S1 内部以固定版本 vendor 的 Cordis（MIT）作为实现，薄适配层对齐 ADR-020 与 specs/component-model.md 的语义，TCK 以我们的语义为准；.NET 侧自研不变。
2. 清单文件：`.qian-plugin/plugin.json`（JSON）；profile 与 patch 继续用 YAML；`qian doctor --migrate-manifest` 负责从 v1 YAML 迁移。
3. `qian` CLI：单一 Node / TypeScript 实现、发布到 npm，`npx qian@latest` 为一键入口，Node ≥ 22 为前置；`dotnet qian` 薄包装（定位并调用 Node 版 CLI、缺 Node 时提示安装）为 S1 可选 P2。
4. 官方市场仓库：与 Qian 同组织下的独立仓库 `qian-plugins`，根目录 `.qian/marketplace.json`；S1 私有，S3 随许可证评估公开；社区发现用 GitHub topic `qian-plugin`。
5. 代码插件热装卸不进 S2 核心交付；S2 内由 RT 领域做时间盒 5 天的 spike 验证 collectible `AssemblyLoadContext` 与 Kun 约束，结论写回 ADR-020；生产默认滚动重启，声明式表面热重载 S1 不变。
6. 许可证：Qian 包族、`qian` CLI、yue 一律 Apache-2.0，S3 商业市场与计费组件的许可证到 S3 再定；公开 feed：S1 GitHub Packages（.NET 包公开可见、安装需令牌，TS 包私有，R93），S2 末或首个外部宿主试用时公开到 nuget.org 与 npm（前提：PublicApiAnalyzers 门禁与 0.x → 1.0 的 SemVer 策略就位）。
7. CLI 遥测：默认不采集；S2 提供 opt-in `qian telemetry on`，只采集命令名、CLI 版本、操作系统与匿名安装 id，不含路径、租户与代码；私有化部署可在 profile 中禁用。
8. `qian init --template` 模板名统一为 specs/component-model.md §9.1 的集合（R42），后续新增模板在 spec 登记。

**补充裁决（2026-10-06，R27–R31，由 PRD 回填时上报并裁决）**

- R27：PRD §12 N1 收窄为「不为第三方代码与可 AOT 的边缘 profile 做运行时热插 DLL」；官方代码插件在 `platform` profile 的热装卸保持 S2 可选、默认进程重启（与 D20.7 / D20.11 一致）。§7 第 5 条的「兼容验证归属」已于 2026-10-07 拍板：S2 内由 RT 领域做 5 天时间盒 spike，结论写回 ADR-020。
- R28：`connector` 不是 `runtime.form`，而是「为某个 seam 提供 Provider 的组件」——清单上 `provides[]` 写出服务键 + `policy.egress` 声明出网目标，`runtime.form` 按实现取 `module` 或 `service`；`bundle` = 组合包（有 `config/qian.patch.yml`，可携带多个组件），应用 = 组合包 + marketplace 条目；字段细节以 specs/component-model.md 为准。
- R29：PRD §02 新原则采用 2.7a / 2.7b 编号，不重编整章。
- R30：规范术语「平台插件」（`policy.tier: system`，`platform` profile base 组合包成员）；「系统插件」保留为别名，UI 文案用「平台插件」。
- R31：原可部署单元 ↔ v2 落位定稿表（`qian-platform` → profile `platform`；`qian-gateway` → profile `gateway`，可 AOT；`qian-identity` → profile `identity`；`qian-shell` 不是 profile，为 `platform` 的 `shell` 组合包；`qian-model-gateway-adapter` → `model-gateway` 组合包，默认挂 `platform`、可选独立 profile；S2 `qian-lowcode` / `qian-workflow` → profile `lowcode` / `workflow`；S2 `qian-file-preview` → 挂在 `lowcode` 的 `file-preview` 组合包；`standalone` = 消费者宿主装最小组合包；`minimal` = 测试与 SDK 用最小树），表落在 prd/README 命名约定与 ROADMAP S1 核心交付；profile 可以被 patch 合并或拆分，表中为 S1 默认。

## D21 产品族结构：Kun / Qian / yue 的定位、仓库关系与统一文档

> 2026-10-07 新增：产品负责人提问「Kun 和 Qian 如何定位，是不是创建一个大的项目关联起来各个子项目；Kun 也需要体现在文档中」。详见 ADR-021（Accepted）。D1 的消费方式（NuGet + CalVer 锁版本、不复制源码）不变。

**推荐**

1. 三层口径：Kun = 框架层（面向任何 .NET 微服务的底座 SDK，无产品观点的通用基础设施）、Qian = 平台层（基于 Kun 的多租户 PaaS：极小运行时 + 平台插件 + profile + CLI + 市场）、yue = 前端层（组件库与外壳）。对外说「Qian 平台基于 Kun 框架构建，前端使用 yue 组件库」；不说「Kun 是 Qian 的子项目」（Kun 有独立下游与发布节奏），也不说「Qian 是 Kun 的扩展」。
2. 不建大单仓、不建 submodule 元仓库（ADR-001 §4 已否决）。「大项目」= 产品族，用四样东西关联：一个 GitHub 所有者（同一所有者等于一段包源配置、`qian-plugins` 不再占位；2026-10-07 拍板为 `idadawn`，Kun 从 CavaiLAB 迁入，因为 iClaw 源码与其余仓库都在 `idadawn`）、一个文档站（Qian 文档覆盖整个产品族，Kun 为第 3 部分第一节）、一张自动生成的版本矩阵（Qian、Kun CalVer、yue、.NET / Node / K3s）、一个看板（跨仓库工作在 `pm/` 的 KUN 领域跟踪，kun 仓库 PR 回链）。
3. Kun 版本单点：`Directory.Packages.props` 的 `KunVersion` 属性，全部 `Kun.*` 引用它；文档同步与版本矩阵读同一属性。
4. Kun 文档进站：真相源在 kun 仓库 `docs/`，Qian 站按锁定版本镜像白名单内容到 `/sdk/kun/`、DocFX 生成 API；每页横幅注明来源与「以代码为准」（D1.8）；Qian 只手写一页「与 Qian 的关系」。首版 docs-site.md 的「Kun 外链不重写」作废。
5. 全站 frontmatter 加 `project`（qian / kun / yue）；各仓库 README 开头同一段产品族地图。

**备选**：新建专用组织（公开形象更干净，但 Kun 的 feed、CI 与其它下游都要改源；S3 公开前再评估）；Kun 自建文档站（单人维护无带宽、读者重叠；否决）；把 Kun 文档复制改写进 Qian（双真相源；否决）。

**已拍板**（2026-10-07）：仓库归属 = `idadawn`，Kun 迁入（S1-DEC-009 关闭，执行见 S1-PM-003）。**仍待确认**：Kun 仓库随文档站公开的时点（需 Kun 维护人确认）。

### contracts/v0 默认裁决 R45–R53（S1-DEC-010，2026-10-08）

S1-AGT-001 写契约时的 8 条默认值由 Claude 在产品负责人授权下裁决：R45 错误码大写下划线、OAuth `error` 值仍小写；R46 业务错误只用 409 / 422，404 一律 `NOT_FOUND`，配额类为 422 业务错误；R47 `AUTHZ_DENIED` 平台内置；R48 R32 的 403 原因即错误码（`PLUGIN_NOT_ENTITLED` / `PLUGIN_NOT_MOUNTED` / `PLUGIN_RESTRICTED` / `PLUGIN_DEPENDENCY_PENDING`，`message` 带层级、`pointer` 指向 patch 条目，ErrorDetail 不加 `details`）；R49 `tenant_id` 可为 null，发往 iClaw 的不转发 null；R50 字段级码 + `pointer`，ADR-011 D11.5 在 S2 映射；R51 `schemaSha256` 用 RFC 8785 JCS；R52 `humanOnly` = `agent.expose: false` + 服务端 `ACTION_HUMAN_ONLY`；R53 `draft` 保留到 S1 第 1 波消费方全部 review，由 S1-AGT-002 统一去掉；R80 补注：届时将 `x-status: draft` 改写为 `x-status: frozen`。原文见 contracts/v0/README.md 第 8 节。

### 文档站 Kun 小节默认裁决 R54–R62（S1-DEC-011，2026-10-08）

S1-DOCS-005（PR #4）合并时留下的 9 个未决问题，由 Claude 在产品负责人授权（「按经验和教训处理掉」）下裁决：

- R54：API 参考不进侧栏，命名空间页与类型页也不渲染侧栏（`sidebar: false`），两个概览页保留。原因：Rspress 把整棵侧栏渲染进每一页，四百多个 API 页进侧栏会让产物从 57 MB 涨到 170 MB；去掉 API 页上的侧栏后，产物从约 57 MB 降到约 48 MB。
- R55：接受当前体积（每份约 48 MB，gzip 后约 10 MB），产物检查加 100 MB 总预算。2026-10-15 S1-QA-010 按本条流程调为 120 MB（#95 合并时实测 96.2 MB / 756 页，原因见 [文档站规范 §7](/design/prd/specs/docs-site.md#7-门禁)）。
- R56：API 参考统一挂在 `/sdk/api/{kun,dotnet,frontend,http}/` 下，不另设顶层 `/api/`；S1-DOCS-002 的验收按此改。
- R57：SDK 侧栏的概览页在最前，「Kun 框架」是第一节。
- R58：Kun ADR 白名单大小写都收，`adr-p2d-multitenancy`（已 Accepted）进站。
- R59：上游文档问题机械处理、只告警，源头修复提到 kun 仓库（idadawn/kun#42）；DocFX 的 11 条 `InvalidCref` 来自 EF Core 的继承文档，不处理。
- R60：文档站构建需要 .NET 与 kun 读权限；不在 fork 上构建，Dependabot 接入时配同名 Secret。
- R61：禁用词中已作废项目名按整词匹配，避免误伤英文单词；中文语境照样拦截。
- R62：文档站 DocFX 复用 S1-RT-001 的 `packageSourceMapping`（`Kun.*` 映射到 nuget.org）；若 `KunVersion` 改钉只在 GitHub Packages 上的预发布版，docs-site 工作流也要配该源的读凭据。

原文见 specs/docs-site.md §2、§2b、§6、§7。

### 验收复核裁决 R63–R64（S1-DEC-013，2026-10-10）

- R63：消费端幂等默认用 Kun 的 Redis `idemp:` 键，PostgreSQL `processed_messages` 兜底表作为 S1-EVT-001 的可选项。
- R64：`Qian.Identity.Abstractions` 暂时直接依赖 `Kun.EventBus`，记为延期项；拆包与依赖门禁白名单由 S1-KUN-010 跟进。

### 实施期决策任务（S1-DEC-012、S1-DEC-014–018）

| 任务         | 结论                                                                                   | 权威落点                            | 批准留痕                                      |
| ---------- | ------------------------------------------------------------------------------------ | ------------------------------- | ----------------------------------------- |
| S1-DEC-012 | minimal profile 默认装配清单按推荐口径                                                          | ADR-020 §7.1                    | 任务文件记载 2026-10-09 在聊天中确认                  |
| S1-DEC-014 | 运行时物理包 `@idadawn/qian-runtime` 发布到 GitHub Packages，消费方用 npm alias 保留 `@qian/runtime` | ADR-021 D21.6                   | cody 自述；产品负责人 2026-10-13 在聊天中确认（R70 留痕补齐） |
| S1-DEC-015 | Identity 分阶段：SDK 阶段用真实协议夹具，真实 `qian-identity` 的 PKCE 联调留在 S1-IDP-002                 | ADR-005 §9                      | cody 自述；产品负责人 2026-10-13 在聊天中确认（R70 留痕补齐） |
| S1-DEC-016 | DEP-001 只验基础设施，真实平台四依赖联调留在 S1-DEP-005                                                | ADR-017 §9                      | cody 自述；产品负责人 2026-10-13 在聊天中确认（R70 留痕补齐） |
| S1-DEC-017 | RLS 正向验收用无 BYPASSRLS 的应用角色，会话变量统一为 `app.current_tenant_id`                           | ADR-004                         | cody 自述；产品负责人 2026-10-13 在聊天中确认（R70 留痕补齐） |
| S1-DEC-018 | DATA-001 本体先交付，代码字段源生成与真实安装门留在 S1-RT-014 / S1-RT-011                                 | S1-DATA-001、S1-RT-011、S1-RT-014 | cody 自述；产品负责人 2026-10-13 在聊天中确认（R70 留痕补齐） |

### 验收复核裁决 R65–R74（S1-DEC-019，2026-10-13）

由 Claude 在产品负责人授权（「把问题都修一下，然后合并」）下裁决，未经逐条确认；全文见 `pm/tasks/S1-DEC-019.md`。

- R65：Kun 同日修复版 `-hotfix.N` 推送 nuget.org 但按预发布排序；`Kun.*` 以 `[$(KunVersion)]` 精确钉定，钉在修复版上时于下一个无后缀正式版发布后升级回来（本次 2026.10.13）。 「发布同日修复版之后，下一个工作日补发无后缀正式版」已于 2026-10-13 由产品负责人拍板写进 Kun 发版规则（kun#58）。
- R66：受保护请求除编译期 QIAN0005 外，由 Kun 2026.10.13 的 Mediator 构建期兜底拒绝漏装权限行为。
- R67：CFG-002 的 CI 门禁对仓库全部 profile 生效配置执行明文密钥扫描；公开引用以组件规格 §11.3 为准，`secretRef://k8s/...` 只作底层定位。
- R68：QA-002 的 T23 用 TCK 平台组合包与 CRM 样例 Provider 组；D19.8 点名三包的平台模式对照归 S1-QA-003 / S1-QA-005。
- R69：DEC-018 所说「QA-002 总目标」不是 S1-QA-002 的验收；S1-RT-011 依赖 S1-RT-014。
- R70：DEC-014–018 的批准只有 cody 自述，补确认前按默认裁决继续生效；产品负责人 2026-10-13 已在聊天中确认，留痕见各任务文件「复核备注」。
- R71：任务日志必须署实际代理名，先建档再交付。
- R72：文档站换用 `@idadawn/yue-tokens`（S1-DOCS-004），yue 规格令牌三项勾选、暗色选择器与实现一致。
- R73：DEP-001 的集群事实经只读核验部分确认，未覆盖项写明；Harbor OCI 制品与 egress 动态放行移交 S1-RT-013 / S1-DEP-004。
- R74：修复状态表、PRD 7.1 第 4 项、RT-003 / RT-016 措辞、fixture 会话变量、Apollo 失败原因断言与本机测试前置对齐。

***

## 需要你拍板的问题汇总

按影响面排序，每条给我的默认倾向；不回复即按默认写入 PRD。

| #   | 问题                                                 | 我的默认                                                                       |
| --- | -------------------------------------------------- | -------------------------------------------------------------------------- |
| Q1  | 四段阶段划分是否认可                                         | 认可                                                                         |
| Q2  | 身份路线：自建 OpenIddict 唯一 issuer（Authentik 退居运维侧）      | 是                                                                          |
| Q3  | 是否有私有化/离线交付、政企客户是否首期必须 SAML/CAS/LDAP               | 假定有私有化需求、SAML/CAS/LDAP 放 S3                                                |
| Q4  | 租户自定义登录域名是否刚需                                      | S3 再做；S1 单 issuer                                                          |
| Q5  | 是否接受 PostgreSQL 为唯一一等库、不支持 MySQL/国产库               | 接受                                                                         |
| Q6  | 默认隔离策略共享库 + RLS，独立库作 S3 付费选项                       | 是                                                                          |
| Q7  | Kun 缺口修复走上游 PR 还是 Qian 补丁包；Kun 维护人与响应节奏            | 上游 PR 优先，设 1 个迭代回合入期限，否则补丁包                                                |
| Q8  | Apollo：MySQL 托管还是集群内；首发版本；是否与 iClaw 共用             | 集群内 operator 先跑 staging，生产托管；2.5.x；不共用                                     |
| Q9  | S3 是否允许第三方任意代码容器上架                                 | 不允许，只开放 package / function / connector / 受限 ui                             |
| Q10 | 元数据应用包默认分发模式                                       | 默认 managed，template 作为可选                                                   |
| Q11 | 是否保留代码生成                                           | 不作为主轨；S2 后期可选薄壳                                                            |
| Q12 | 前端组件底座：接受路线 A 自建数据组件风险                             | 接受，首迭代设验证门                                                                 |
| Q13 | S2 是否交付 H5 表单渲染器                                   | S2 只做 PC 响应式，预留端覆盖                                                         |
| Q14 | 流程 S2 最小集：加签/会签延后计算/子流程/超时哪些升为必做                   | 子流程与超时催办升为必做，加签减签与候选人保持可选                                                  |
| Q15 | 与 iClaw：租户权威归属、是否共用 Authentik / S2S / Usage Ledger | 租户权威归 Qian；共用 Authentik（运维侧）与 Higress；S2S 各签各的但 claim 同形；Usage Ledger 待 S3 |
| Q16 | 生产集群是否与 iClaw 共用                                   | 共用集群但独立节点池，达到一定租户数再拆                                                       |
| Q17 | 模型网关：GPU 归属、接入范围                                   | 先 LLM + Embedding；GPU 归属你定                                                 |
| Q18 | MFA 是否 S1 必做；等保/商密要求                               | TOTP S1 做；商密不做假设，需你确认                                                      |

> 2026-10-06：Q1–Q18 的默认全部获认可。
> D19、D20 的待确认事项已于 2026-10-07 由产品负责人授权全部拍板，结论见各自的「已拍板」小节（ADR-019 §7、ADR-020 §7），不编入 Q 序号。

***

## 补充裁决（来自基线汇编 §3.4 / §3.5，2026-10-06）

基线汇编 `research/baseline-digest.md` 汇总了 41 个待拍板问题，其中约一半已被 D1–D18 覆盖；其余按下表裁决，与各基线的倾向一致处不再说明理由。

| 汇编编号 | 问题                        | 裁决                                                                                                     |
| ---- | ------------------------- | ------------------------------------------------------------------------------------------------------ |
| Q2   | 账号归属                      | 全局自然人（Person）+ 租户成员（Membership）为主；「租户托管身份」作为 S3 可选，并存时以邮箱/手机验证后的显式合并为准                                 |
| Q10  | 身份切换、组织角色、互斥/基数约束等高级权限模式  | S3 可选；S1 在令牌里预留 `profile` claim，权限模型按 L0–L6 分层留扩展位                                                     |
| Q12  | 用户配额口径                    | 平台超管不计入租户配额，租户管理员计入；平台超管对租户不可见（审计可见）                                                                   |
| Q13  | 租户自助申请审核、「企业 × 应用」关系、邀请格式 | 参考平台未交付该部分，由 Qian 自行设计，写入 PRD 租户生命周期章                                                                  |
| Q17  | 视图（数据集驱动的纯列表）             | S2 可选，归入列表引擎的「只读数据源」形态，不单独立项                                                                           |
| Q18  | 脚本能力边界                    | S2 只做声明式规则 + 服务端 before/after Hook + 可序列化表达式 DSL；前端 QuickJS 沙箱为 S2 可选兜底；自由 JS 按钮不做                     |
| Q19  | 数据权限多方案合并语义               | 沿用源码确认的「同角色 AND / 跨角色 OR / 任一全部数据则放开」，不做可配置                                                            |
| Q20  | 功能编码与多应用挂菜单               | 采用「租户.应用.功能」命名空间化稳定编码 + id 重映射表；一个功能只归属一个应用，其他应用通过「跨应用引用」挂菜单（引用而非归属）                                   |
| Q22  | 审批设计器形态                   | S2 用纵向节点树（DOM 自绘，对 Agent 生成最友好）；自由画布（React Flow）S3 可选；不用 bpmn-js（许可证要求水印可见）；BPMN 只作导出器且不早于 S3          |
| Q24  | 拒绝后继续流转                   | S3；S2 在动作枚举中预留                                                                                         |
| Q25  | 审批人兜底                     | 兜底链 = 节点指定异常处理人 → 租户管理员角色 → 「无法提交」结构化错误；不再兜底到全局管理员                                                     |
| Q26  | Agent 代办身份                | 委托模型区分本人 / Agent / 系统三类 actor；Agent 有效权限 = 委托人权限 ∩ 授予 scope；签名类动作标 `humanOnly`；转审、协办、撤销 S4 前对 Agent 禁止 |
| Q27  | 流程命名与市场                   | 流程层用「启用/停用」替代「上架/下架」避免与市场撞名；流程模板、决策表节点包作为 S3 市场商品                                                      |
| Q28  | 自动化与决策阶段                  | 自动化（任务流程）S2 可选、S3 必做；决策引擎 S3 可选                                                                        |
| Q29  | 门户与报表阶段                   | 按阶段路线归 S3；S2 提供「配置路径型」门户（固定模板 + 配置）与报表子集（列表 + 分组汇总 + 导出 XLSX）作为可选，保证 S2 应用有可用首页                        |
| Q30  | 报表深度                      | 先子集（列表、分组汇总、导出），交叉表/父子格/分栏 S3 再评估是否自研展开引擎                                                              |
| Q31  | 大屏                        | 延后到 S3 可选，且不复用参考平台大屏源码（许可证高风险）；只消费内核数据接口                                                               |
| Q32  | 数据接口 SQL 文本模式             | 开放，但作为高级权限：只读数据库账号、超时与行数上限、参数化、审计；发布需租户管理员批准                                                           |
| Q33  | 服务端 PDF                   | S2 浏览器打印；服务端 HTML→PDF S3 可选                                                                            |
| Q34  | 文件预览技术栈                   | 预览提供者接口可替换；首选 .NET 生态方案，Java 预览进程只作可选 sidecar                                                          |
| Q35  | 开放接口凭据                    | 废弃用户级 AppSecret，统一为网关的应用级客户端凭据（client_credentials），取消「公有」免鉴权                                           |
| Q37  | 后台壳多面策略                   | 统一一个壳，按「平台控制台 / 应用」上下文切换，与插件外壳模型一致；不采用三面新窗口                                                            |
| Q38  | 布局模式与偏好                   | S1 单一侧栏布局；多布局与偏好抽屉 S3 可选                                                                               |
| Q39  | 富文本 / Markdown 编辑器        | 五份调研均未覆盖，S2 前补一次选型调研                                                                                   |
| Q40  | iClaw 代码复用授权              | iClaw 与 Qian 同属一个所有者，允许搬代码；但只搬审计拦截器、租户主数据模型、OIDC 授权码实现、通知通道范式四处，其余只借鉴思路                                |
| Q41  | yue 分发渠道                  | 私有 npm 为主；shadcn 私有 registry + MCP Server S3 可选、S4 必做                                                  |

**基线之间的矛盾裁决（汇编 §3.5）**

| 议题                      | 裁决                                                                                                                   |
| ----------------------- | -------------------------------------------------------------------------------------------------------------------- |
| IdP 形态                  | 以 K/S 为准：OpenIddict 自建唯一 issuer；P 基线的「RP 侧 module + 托管 IdP」表述作废                                                      |
| 企微/钉钉/飞书连接器             | S1 必做：通用 OIDC + 企业微信（扫码与工作台免登）；钉钉、飞书 S2 必做；ISV 第三方应用模式 S3                                                            |
| refresh 令牌与单点登出         | S1 必做（K/S）                                                                                                           |
| 审计路径                    | 以语义定：实体级审计必须与业务同事务写入 `AuditLog` 并经 Outbox 发事件（借 iClaw 拦截器写法）；命令级哈希链补 PostgreSQL 持久化；事后不同事务的拦截器弃用。具体落在哪个 Kun 包由 ADR 定 |
| Wasm `function` 运行时     | S2 可选：公式与规则先用 JSON AST 表达式解释器；Wasm 在 ISV 函数插件（S3）前落地，ABI 在 S2 中期拍板                                                   |
| 打印                      | S2 必做（模板、版本、数据绑定、浏览器打印）；打印设计器 S2 后期                                                                                  |
| 工作台                     | S3 必做（S2 只有审批四列表 + 应用入口）                                                                                             |
| 短信渠道                    | S1 必做（登录验证码与找回密码依赖它）                                                                                                 |
| 公告                      | S3 可选                                                                                                                |
| DTCG 令牌                 | 令牌三层结构 S1 必做；DTCG 格式导出 S1 可选                                                                                         |
| Module Federation 2 运行时 | S1 必做（K/P），U 基线的分层表补上 `@yue/shell` 对 MF2 的依赖                                                                         |

### 验收复核裁决 R75–R77（S1-RT-006 复核，2026-10-14）

由 Claude 在产品负责人要求「给我一个解决方案」后作默认裁决，未经逐条确认；整改任务见 `pm/tasks/S1-RT-019.md`，有异议时新建决策任务。2026-10-14 产品负责人认可 S1-RT-006 与 S1-RT-019 的交付，本组裁决按交付的方式落地（含 R75 实现说明）。 2026-10-15 claude 独立复核 S1-RT-019：八项验收成立、done 维持，非阻断缺陷记入该任务日志，本组裁决不变。

- R75：保留通用执行入口 `POST /api/capabilities/{plugin}/{name}/invoke` 并写入规格 §9.5 与 PRD 7.2.1：只在 `platform` profile 存在；每次调用重做与目录同款的判定；请求体先按条目 `inputSchema` 校验，失败返回 ErrorDetail `INPUT_INVALID`（原文误写 `VALIDATION_FAILED`，contracts/v0 §3.2 的输入校验码是 `INPUT_INVALID`，S1-RT-019 按契约更正） 并带 JSON Pointer；支持 `Idempotency-Key`，与外壳桥接 `invoke` 的幂等键及 R63 一致；Agent 主体需 `agent.expose` 且 S1 不开放 Actor 委托；聚合 OpenAPI 列出每个能力的具体路径，ApiExplorer 不列通用模板并在 README 说明。
  - 实现说明（S1-RT-019）：幂等语义与 Kun 幂等消费相同（成功后才写回执、失败不留标记），按短事务登记「执行中」→ 不占连接执行 → 写完成或撤销三段实现，同键请求仍在执行时返回 409，只读能力（`sideEffect: read`）忽略幂等键；回执存 `plugin_qian_capabilities.invoke_receipts`（租户 RLS），不用 R63 的 Redis 键——R63 针对事件消费只需「已处理」标记，HTTP 重放要原样返回首次响应；Kun 未改动，不开 KUN 任务。
  - 实现说明（S1-RT-020）：回执按条目 `dataClassification` 与组件配置 `receiptMaxBytes`（默认 64 KiB、上限 1 MiB）决定是否保存响应体；`pii` / `finance` / `secret` 与超限的响应只记「已执行」，同键重放返回 409 `CAPABILITY_REPLAY_UNAVAILABLE`、不再执行，回执表另以检查约束限定响应体不超过 1 MiB。条目与输入校验器在能力登记时生成和预编译，不合格的契约登记即被拒绝。口径由 S1-RT-020 按产品负责人认可的方案确定，写入组件规格 §9.5 与 PRD 7.2.1。
- R76：S1-RT-006 验收第 3 项按「机制完成」勾选；平台挂载路径（`Mount` 与 Registry scope Reconciler）强制套用 `OpenApiContractPlugin` / `GrpcContractPlugin` 归 S1-RT-005，S1-RT-004 的 Reconciler 复用同一包装。
- R77：目录四类 kind 的归属：事件类目录归 S1-EVT-003；实体类目录暂无任务，由产品负责人分配（建议 S1-RT-007 或 DATA 系列）；交付前 `?kind=event|entity` 返回空集合并在 README 注明。

### S1-DATA-003 收口裁决 R78–R79（2026-10-15）

产品负责人要求「把我需要知道的，都搞定吧」后，由 Claude 作默认裁决，未经逐条确认；有异议时新建决策任务。

- R78：方言组件 id 定为 `qian.data.dialect.pg`，与组件模型 §6.1 的包名映射（`Qian.Data.Dialect.Pg` → `qian.data.dialect.pg`）一致；PRD 7.6 原写的 `qian.data.dialect.postgres` 已在 idadawn/qian#79 改正。组件形态（`plugins/qian.data.dialect.pg`，清单 `provides` 派生为 `Data.Dialects:pg`）登记为 S1-DATA-006，依赖 S1-RT-014 的具名注册派生。
- R79：数据连接连通测试的进程内出网检查提前到 S1（S1-DATA-005）。缺省拒绝回环、链路本地（含云元数据）、100.100.100.200、未指定、组播与广播地址，运维可追加拒绝网段或限定放行网段。PRD 7.18 / 7.19「低代码连接器与第三方出网点的出口代理与 SSRF 基线归 S2」不变，生产环境仍以 7.17 的 NetworkPolicy 出网白名单或出口代理限定可达网段。
  - 补记（2026-10-16，S1-DATA-007）：产品负责人已确认本条（原话「这四项一起解决掉吧」），并要求两处缺省值现在就改，不再留给部署任务：宿主环境为 Development 时缺省放行回环，便于连本机数据库；K3s 缺省的 Pod 网段 10.42.0.0/16 与 Service 网段 10.43.0.0/16 并入缺省拒绝列表。集群改用了自定义网段时，由运维移除这两项并追加实际网段；节点网段因部署而异，仍由部署配置追加。

### 验收复核裁决 R80（S1-QA-010 复核，2026-10-15）

由 Claude 在产品负责人要求「验收 S1-QA-010」后作默认裁决，未经逐条确认；整改退回 `pm/tasks/S1-QA-010.md`，有异议时新建决策任务。

- R80：契约目录的冻结状态统一显式写 `x-status: frozen`，不以「删除 `draft`」表示：contracts/v0 在 S1-AGT-002 去 draft 时改写为 `frozen`（v0 README §2、§7 与 R53 补注、`ContractSchemaTests` 届时改断言 `frozen`）；contracts/data/v1 已按此标记，README 写明批准依据为本条，并更正 `$id` 陈述（data-types / data-connection 两份的旧标识符前缀与目录不符，趁零消费方统一为 `contracts/data/v1/` 并在 PR 说明；S1-QA-010 已按此对齐）；data/v1 在零代码消费方时冻结是复核人在 S1-QA-010 验收里提出的要求（C# 契约 `Qian.Data.Abstractions` 已发布为消费边界），作为 R53「消费方全部进入 review 后才冻结」前提的例外记录；data/v1 冻结信封 `$ref` v0 草稿信封列为已知例外，v0 event-envelope 冻结时复核 data/v1 信封。（本条原文的旧前缀字面量由 #104 去除、语义不变，复核人 2026-10-15 认可）

### 阻塞任务排序裁决 R81–R84（S1-DEC-021，2026-10-15）

产品负责人 2026-10-15 问「现在阻塞的任务可以给一个人来做吗，能排一个先后顺序吗」，Claude 按看板依赖链与解锁数量给出顺序，产品负责人回复「认可，写进看板」。四条裁决把伸进下游任务范围的验收移交到归属任务，其余顺序与等待说明见 `pm/tasks/S1-DEC-021.md`。

- R81：S1-TEN-001 收口。原 E2E「运营后台创建租户 A、B，各自管理员登录后只看到本租户数据」拆为运营后台页面（归 S1-TEN-005）与管理员登录（归 S1-IDP-003），TEN-001 只保留「A 令牌请求 B 记录 404」的 HTTP 租户边界；传播器 IT 的消费端归 S1-EVT-002、作业执行器归 S1-SCH-002，TEN-001 保留 gRPC 对端；「inputSchema 不出现 tenantId」由 S1-RT-020 的登记期契约检查实现，TEN-001 引用其测试勾选。TEN-001 的依赖已全部完成，状态由 blocked 改回 todo。
- R82：S1-AZ-003「把角色授予组织 X，X 下成员 ≤ 3 秒获得权限」依赖 S1-IDP-009 组织目录，移交已依赖 IDP-009 的 S1-AZ-005；AZ-003 以直接主体授权验证求值引擎与 ≤ 3 秒失效传播。S1-AZ-002 的 `me/permissions` 验收与 AZ-003 一并交付。
- R83：S1-RT-009 的 hooks 桥接验收用测试宿主发布的 `tenant.created.v1` 事件验证触发与审计 `channel = hook`；真实开户流程触发归 S1-TEN-003。（R91 更正：触发名为进程内事件 `tenant/provisioned`，本条原文的 `tenant.created.v1` 是集成事件 ID，hooks 矩阵不收）
- R84：S1-RT-011 的 E2E「qian add identity authz files」在 identity / files 交付前改为用市场条目已存在的三个组件验证；完整组合归 S1-QA-005。
- 顺序与归属：一人（cody）串行 S1-TEN-001 → S1-RT-004 → S1-AUD-001 → S1-RT-014 → S1-RT-005 → S1-AZ-002 + S1-AZ-003 → S1-RT-011；S1-RT-009 剩余一条按 R83 可在 RT-005 后顺手做；S1-DEP-003 等 S1-IDP-002 / S1-IDP-005 / S1-GW-001 / S1-YUE-001 交付后再动。S1-RT-004 与 S1-AUD-001 的依赖已全部完成，状态由 blocked 改回 todo。

### 验收复核裁决 R85（S1-TEN-001 复核，2026-10-15）

由 Claude 在产品负责人要求「验收 S1-TEN-001」后作默认裁决，未经逐条确认；执行人在该任务日志的偏离 ② 提出归属待定，有异议时新建决策任务。

- R85：tenancy HTTP 运营接口 `GET/POST /api/tenancy/tenants`、`PUT /api/tenancy/tenants/{id}`（PRD 7.3 API 行的前两组）不在 S1-TEN-001 交付范围，另立 S1-TEN-006 交付，depends `S1-TEN-001`、`S1-AZ-003`：调用方是没有租户的平台主体，要走 AZ-003 的超管旁路显式声明并审计，`TenantRequestBoundary` 已写明平台运营入口不经租户边界回退。不并入 S1-TEN-002（它承接同一资源上的状态迁移端点与审批钩子，依赖 EVT-001 / SCH-001 / IDP-004，列表、新建、编辑不应被这些拖住），也不并入 S1-TEN-005（页面任务是消费方不是提供方）；S1-TEN-005 depends 加 S1-TEN-006。S1-GW-001 的 E2E 探针路由 `/api/tenancy/tenants` 以 TEN-006 交付为准，TEN-006 未交付时以测试宿主的任意受保护路由代替，GW-001 不加依赖。TEN-001 验收第 4 条后半句「运营接口的目标租户是资源路径」在 TEN-001 按内部 API 与 DTO 现状成立（写入口 `internal`、id 服务端生成、`CreateTenant` / `UpdateTenant` 请求体拒绝 `tenantId` / `tenant_id` / `id`），HTTP 层复验列入 S1-TEN-006 验收；TEN-001 题名中的「tenancy API」自此指 `ITenantDirectory` 内部 API 与 HTTP 租户边界。

### 验收复核裁决 R86–R87（S1-RT-004 复核，2026-10-15）

由 Claude 在产品负责人要求「验收 S1-RT-004」后作默认裁决，未经逐条确认；执行人在 S1-RT-004 与 S1-RT-011 的任务日志里分别提出建议裁决，有异议时新建决策任务。

- R86：注册表管理面路径以 PRD 7.2.1 为准——scope 视图 `GET /api/registry/scopes/{scopeKey}/entries[/{entryId}]`，v1 别名 `/api/kernel/scopes/...`；组件规格 §9.5 表与 §12 验收里的 `GET /api/registry/entries?scope=` / `/api/registry/entries` 改为该路径，S1 不另设 dump-config 等价的全树 HTTP 端点，全树视图由 CLI `qian dump-config` 承担（S1-RT-012 的 `--scope tenant:<id>` UT）。写入端点 `PUT /api/registry/tenants/{tenant}/patches/{scope}/{entry}`（幂等、带 `revision`，§7.6 / §9.5）在 S1-RT-004、S1-RT-005、S1-RT-011 三个任务里都没有承接：另立 S1-RT-029 交付，depends S1-RT-004（视图与通知）、S1-RT-005（`MountAsync` / `UnmountAsync` 与 `registry.scope.*.v1`）、S1-RT-011（CLI）、S1-AZ-003（平台主体授权）；S1-RT-011 验收第 5 条「`qian add --scope tenant:<id>`」随之移交 S1-RT-029（执行人建议 ② 采纳「拆出新任务」而非「并入 RT-005 后续」），RT-005 范围不变。
- R87：管理面 403 的口径按 R45 / R48 读：错误码即原因（S1 产生 `PLUGIN_NOT_MOUNTED` 与 `PLUGIN_DEPENDENCY_PENDING`，`PLUGIN_RESTRICTED` / `PLUGIN_NOT_ENTITLED` 待 restriction 来源与 S1-ENT-001），`message` 说明被哪层 patch 禁用，ErrorDetail 不加 `reason` 字段；S1-RT-004 验收第 3 条的旧写法 `plugin_not_mounted`、`reason = not_mounted` 按此解读。`pointer` 约定写进规格 §7.6 与 PRD 7.2.1：以逻辑文档 `registry/scope-patches.json` 为前缀，`#/n` 为该 scope patch 行按条目标识排序后的序号（增删行会漂移，是行位置不是稳定标识），目标租户没有该条目时指向文档根 `#`，被 profile 某层禁用时指向该层文件位置。PRD 7.2.1 第 11 条（R32 摘要）补齐第四个码 `PLUGIN_DEPENDENCY_PENDING`，与 7.2.1 正文、规格 §9.5、R48 一致。

### 验收复核裁决 R88–R90（S1-RT-005 复核，2026-10-15）

由 Claude 在产品负责人要求「验收 S1-RT-005」后作默认裁决，未经逐条确认；执行人在任务日志提出建议裁决 A–C，整改任务见 `pm/tasks/S1-RT-031.md`，有异议时新建决策任务。

- R88：S1-RT-005 验收第 4 条「Unmount 后 ≤ 5s 三处拦截恢复」以 SDK 请求入口（`PluginEnabledMiddleware`）的证据勾选；网关路由的「未挂载 404 / 挂载后 ≤ 5s 放行」已在 S1-GW-003 验收第 1 条，外壳的「未挂载不加载」在 S1-ENT-002 验收第 3 条，ENT-002 验收第 2 条改写为三处都验证。执行人建议 A 采纳。S1-ENT-002、S1-DEP-004、S1-DEP-005 的移交登记都以 RT-005 的交付为前提，depends 补 S1-RT-005。
- R89：生命周期钩子口径以组件规格为准：S1 不建 `service` 的 gRPC `qian.lifecycle.v1.Lifecycle` 钩子、平台级 install 钩子与 `migrations.platform / tenant` 编排（规格 §3.6 已把两层钩子取消，§5.6 把逐租户初始化放在 `tenant/provisioned` 监听器，迁移随包），`IPluginLifecycleHooks` 保留为 `module` 组件可选的进程内钩子；PRD 7.2.1 的钩子契约句按此改写。执行人建议 C（另立任务）不采纳。OCI 制品的 cosign 验签在 `Install` 侧随 S1-RT-013 产出 OCI 制品时补齐，RT-013 加一条验收（执行人建议 B 采纳为「记入 RT-013」）。
- R90：开通分两层，细化 R13：组件级 `Provision(tenant, plugin)` 与钩子幂等（`lifecycle_runs`）由 `qian.lifecycle` 承担；租户级步骤表（创建主档 → Kun `ITenantProvisioner` → 逐组件调用 `Provision(tenant, plugin)` → 管理员 Membership → 初始授权 → 通知，`tenant_provision_steps`）由 `qian.tenancy` 的 `TenantProvisioningOrchestrator`（S1-TEN-003）承担；PRD 7.3 与 7.2.1 按此改写，7.2.1 的表清单按包归属标注（`plugin_revisions` / `tenant_plugin_installs` / `lifecycle_runs` / `mount_failures` 在 `plugin_qian_lifecycle`）。同时固定四条语义交 S1-RT-031 落地：Purged / Uninstalled 的租户再次开通须重新授权；Deprecated 修订不能被 Activate / Rollback / Upgrade 到、Retired 修订不能被挂载与开通、`Retire` 在仍有租户停留时拒绝（经 S1-AZ-003 超管旁路跨租户查询）；升级 → 回切 → 再升级到同一版本时钩子要再次执行（幂等键加入 fromVersion 或执行序号）；经 Lifecycle 挂载或升级失败并恢复前值时发出 `registry.scope.mount-failed.v1`，注册表的 mounted 表示写入而非 Ready。

### 验收复核裁决 R91–R92（S1-RT-009 复核，2026-10-16）

由 Claude 在产品负责人要求「验收 S1-RT-009」后作默认裁决，未经逐条确认；执行人在任务日志提出建议裁决，剩余范围见 `pm/tasks/S1-RT-032.md`，有异议时新建决策任务。

- R91：hooks 的触发名只收 R39 冻结矩阵里的进程内事件 `<domain>/<event>`，租户创建对应 `tenant/provisioned`（生产方 `qian.tenancy`，serial）。S1-RT-009 验收第 3 条、R83 与 PRD 7.2.2 验收句里的 `tenant.created.v1` 更正为 `tenant/provisioned`：`tenant.created.v1` 是 ADR-008 D8.7 的跨进程集成事件，由 S1-TEN-002 的状态机经 Outbox 发布，不进 hooks 矩阵，也不扩展 hooks/v2 接受集成事件 ID。真实开户触发（R83 移交 S1-TEN-003）定为：开通编排在 `qian.tenancy` 上下文派发 `tenant/provisioned`（执行人做法 ①），S1-AZ-002 的内置角色监听与 hooks 桥接共用这一派发；不走 S1-EVT-002 把集成事件转成进程内事件（做法 ②），EVT-002 范围不变。执行人建议采纳。
- R92：S1 的 hook 桥接范围 = 矩阵里四个 serial 事件（`tenant/provisioned`、`tenant/purged`、`registry/entry-mounted`、`registry/entry-disabled`）× `type: plugin`；Agent 宿主事件按 R39 透传、不得拒装；`command` / `http` 类型在 S1 不实现执行器（规格 §5.4 的 `qian.hooks` 转换器、非 serial 分发模式与 `identity/*`、`files/*`、`tenant/purging` 的桥接归 S2，PRD 7.2.2 据此标注），静态校验器与运行时对 `command` / `http` 的口径统一为「S1 拒装并给出 `SURFACE_POLICY_DENIED` 级诊断」，R41 的受限 argv 规则保留在校验器里待 S2 执行器启用。`registry/entry-*` 的生产方接线、`.mcp.json` 经 `qian add` 装进 Agent 宿主配置（PRD 7.2.2 列入 S1）、TS 热重载与 doctor 的发布形态、产品宿主映射表面诊断端点，归 S1-RT-032。
- R93：`Qian.*` NuGet 包族 S1 发到 GitHub Packages（`https://nuget.pkg.github.com/idadawn/index.json`）；`idadawn/qian` 是公开仓库，包随仓库公开可见、GitHub 不允许改回私有，安装与还原仍需带 `read:packages` 的 GitHub 令牌（匿名 401）。执行人记录产品负责人 2026-10-15 选择「保留公开」，本裁决把它落进文档：ADR-019 D19.7 / §7、ADR-021 §1 / D21.2 / §4、ADR-020 §7 第 6 条及其在 key-decisions D19 行、PRD 07 的 DATA-001 分期段、PRD 12 已拍板清单第 13 条、规格 §13 第 6 条里的转述，「私有 feed / 私有包」改为「公开可见、安装需令牌」；S1-DEC-018 第二阶段、S1-DATA-001、S1-RT-011 验收 1 与 S1-RT-014 验收 1 里的「私有 NuGet 源」自此读作该正式源，任务原文不改。TS 运行时 `@idadawn/qian-runtime`（D21.6）、`@qian/surfaces` 与 `qian` CLI 仍按 npm `--access=restricted` 私有，不受本条影响；`dotnet-release` 推送后的 verify-feed 断言「可见性与仓库一致」即可，不另要求 private。公开到 nuget.org / npm 的时点仍按 ADR-019 §7。
- R94：PRD 7.2.3 的 SDK / 源生成器范围在 S1 按任务归属收敛：`[QianPlugin]` / `[Capability]` 与 `inject[]` / `provides[]` / `runtime.form` / `runtime.entry` / `tools/*.json` 派生、`qian-plugin check|sync` 漂移门、SDK 传递依赖门禁、`PluginEnabledMiddleware` 由 S1-RT-014 交付；`[Permission]` 在 `Qian.Authz.Abstractions`（S1-AZ-002，反射登记）、`[QianEvent]` 在 `Qian.Runtime`（S1-RT-010 文档分析）、`[QianJob]` 归 S1-SCH-001；`[ExtensionPoint]` / `[Contributes]` 与具名 `Provide` / `Register(name, …)` 的 provides 派生、权限点与事件声明合并进 `plugin.json`、`configSchema` 派生（规格 §3 带代码组件、§12 G1）、其余 32 个平台插件骨架清单的替换（S1-RT-006 / S1-RT-019 移交）、CI 对全部带代码组件跑 check 与工具诊断归 S1-RT-033；`IQianPlugin` 基类、客户端门面、生命周期钩子基类与 DI 注册代码生成推迟到 S2（S1 以 `IQianPlugin` 接口 + `ctx.Inject` 为准；服务级钩子已按 R89 取消）。ADR-019 D19.2「源生成器放在 `Qian.Plugin.Sdk`」读作 SDK 包族：源生成器按 PRD 7.2.3 以 `Qian.Plugin.Sdk.Generators` 独立开发依赖包发布，消费者同时引用两包（RT-014 偏离 ⑤ 认可）。S1-DEC-018 的「SDK 默认装配」在 RT-014 内读作：经派生入口装载的组件继承 `AddQianData` 的安全装配、组件无需 opt-in（偏离 ① 认可）；模板级默认装配是 DEC-018 第二阶段尚未关闭的门，登记为 S1-RT-012 验收。RT-014 验收 6 的「platform profile 组合包」以 R68 的 TCK 平台组合包宿主为准（偏离 ③ 与建议 A 认可），生产 `qian-platform` 宿主的同一装配补为 S1-DEP-005 验收。

### 租户批次排序裁决 R95–R97（S1-DEC-022，2026-10-16）

产品负责人 2026-10-16 要求把看板上租户领域的全部待办按顺序交 cody 一次做完并「确保租户可用」。Claude 按 S1-DEC-021 的做法把伸进别的领域的验收移交到归属任务、解除不必要的依赖，产品负责人 2026-10-16 回复「认可 R95–R97」并自行把执行提示词交给 cody；顺序、暂缓说明（S1-TEN-005 等 yue 第 2 波）、「租户可用」收口检查与执行提示词见 `pm/tasks/S1-DEC-022.md`，有异议时新建决策任务。

- R95：S1-TEN-002 收口。到期扫描交付为 `qian.tenancy` 的幂等能力（能力目录条目，供 7.13 调度器以 callback 类型调用，不标 `humanOnly`），TEN-002 以进程内 IT 验证「到期 → 宽限 + `tenant.expired.v1` + 业务入口即时拒绝」；调度器准点触发与 staging E2E 移交 S1-SCH-002（callback 类型的首个真实作业），「新登录被拒」移交 S1-IDP-003（登录流水线按租户状态拒绝 pending / grace / suspended），「已登录会话 ≤ 60s 失效」移交 S1-IDP-004（会话中心订阅 `tenant.expired.v1` / `tenant.suspended.v1`）。状态迁移端点 `:activate|suspend|resume` 与 S1-TEN-006 同一资源、同一平台主体授权与审计路径；审批钩子是 `Qian.Tenancy.Abstractions` 定义的进程内事件，不进 hooks.json 矩阵（R92），S1 无监听器时放行。TEN-002 depends 改为 S1-TEN-001、S1-TEN-006、S1-EVT-001、S1-AZ-003，不再依赖 S1-SCH-001 / S1-IDP-004。
- R96：S1-TEN-003 收口。验收第 3 条「租户管理员可登录」改为「管理员指针与 tenant.owner 初始授权已写入」，登录复验归 S1-IDP-003（R81 同口径）。步骤表里「创建租户管理员 Membership」「发通知」经 `qian.identity` / `qian.notify` 的端口调用，组件缺席时该步记 `skipped` 并写原因（不记成功、不算失败），整轮保持可重入，端口就位后重跑只补这些步；「写初始授权」经 `Qian.Authz.Abstractions` 的最小授予端口（为租户内主体绑定内置角色，幂等；TEN-003 顺手加入 Definition、`AuthzPlugin` 提供、登记 PublicAPI，并向 S1-AZ-005 登记，AZ-005 的授权 API 建在其上），`qian.tenancy` 不直写 authz 表、不引用 Provider 包；派发 `tenant/provisioned` 之后再写初始授权（S1-AZ-002 登记的时序）。S1-TEN-006 的 `POST /api/tenancy/tenants` 进入该编排（创建主档为第 1 步），重跑入口由 TEN-003 定并记入任务日志；TEN-003 depends 加 S1-AZ-002。
- R97：S1-TEN-004 收口。不另建 `TenantSetting` 表：租户覆盖就是 `qian.registry` 的 `scope_patches` 行（ADR-020 D20.7、组件规格 §7.6，scope `tenant:<id>`、entry 为该插件条目、`config` 整体替换），`ITenantSettings.Get<T>(pluginId, key)` 是其读模型（Apollo / 全局条目默认值回退、租户前缀缓存键、`registry/entry-mounted` 失效），`GET/PUT /tenants/{id}/settings/{plugin}` 是运营门面：平台主体经 S1-AZ-003 旁路放行并审计后，在目标租户上下文调用 `IRegistryService.MountAsync`（同一校验器、同一 `revision` 并发、同事务 `registry.scope.*.v1`），与 S1-RT-029 的通用写入端点共用服务端口，RT-029 范围不变；只有 plugin.json `scopes[]` 含 `tenant` 的组件接受租户覆盖。验收第 1 条拆为 TEN-004 的配额存储、`GET /tenants/{id}/quota` 与配额判定端口（平台超管不计、租户管理员计入，Q12）加 S1-IDP-003 的成员创建拦截 IT；第 2 条 TEN-004 只验证写入经校验、进审计链、另一宿主 ≤ 5 s 读到，「下一次登录即生效」落在 S1-IDP-006 既有验收第 3 条；第 4 条改为注册表 tenant scope 合成视图与 `ITenantSettings` 一致，CLI 打印归 S1-RT-012 验收第 4 条。depends 改为 S1-TEN-001、S1-TEN-006、S1-RT-004、S1-RT-005、S1-AUD-001、S1-AZ-003，不再依赖 S1-CFG-003（热更新校验、CI 骨架比对与 Apollo 发布审计不是租户覆盖的前置）。
- 顺序与归属：一人（cody）串行 S1-TEN-003 → S1-TEN-006 → S1-TEN-002 → S1-TEN-004，TEN-004 转 review 前按 S1-DEC-022「租户可用」六步在真实宿主收口；S1-TEN-005 等 S1-YUE-007 / 008 / 009 交付后再派。S1-AZ-002 / S1-AZ-003 / S1-AUD-001 已合入 main（PR #126 / #125 / #116）、状态 review，不阻塞开工（同 S1-DEC-021 第 7 步）。

### S1-RT-011 验收裁决 R98–R100（2026-10-16）

- R98：R84 的「市场条目已存在的三个组件」在 S1 读作：条目在 CLI 支持的签名市场里存在（可以是指向仓库真实组件文件夹的签名本地市场，E2E 现场生成亦可），且其代码包已发布到正式源（R93）；S1-RT-011 验收 2 以 data / events / config 三个组件成立。官方市场仓库 `qian-plugins` 建立后，由 S1-RT-017 的 CI smoke 用真实市场条目重跑同一 E2E 回验（RT-017 补一条验收）。
- R99：`qian remove` 的「逆序撤销 add 的全部改动」读作撤销组件带来的改动——表面目录、代码依赖、patch 条目与 lock 记录；`.qian/` 工作区（`config.yml`、空 `lock.json`、`journal/`、`.staging/`）与 `.gitignore` 的两条忽略规则是规格 §9.3 的工作区布局，不随最后一个组件移除而删除。CLI 用 `dotnet restore --use-lock-file` 生成的 `packages.lock.json` 不在此列：它让宿主此后进入锁文件模式，是 add 对宿主的持久改动，最后一个 CLI 管理的包移除时必须一并删除（或 add 改为不生成锁文件），归 S1-RT-035；卸载顺序以 lock 记录顺序为准，命令行参数顺序不影响。
- R100：PRD 7.2.3 / 7.2.4 与 S1-RT-017 的旧措辞按规格 §9 与实现改写：表面复制到 `.qian/plugins/<id>/`；patch 写进当前 profile 定义里 `patch` 键指向的文件（默认 `profiles/<p>/qian.patch.yml`，standalone 为 profile 目录下的 `./qian.patch.yml`）；NuGet / npm 版本按组件清单 `runtime.nuget[]` / `npm[]` 钉精确版本（市场条目没有版本字段）；未验证市场的安装开关是 `--allow-unverified`（R40），不是 `--trust`。S1-DEC-018 第二阶段（RT-014 + RT-011）两门均过、关闭；qian.events / qian.config 已同时通过发布与真实安装两门，其 `x-skeleton` 按同一口径随 S1-RT-033 第 3 条移除。

### 验收复核裁决 R101–R103（S1-AZ-002 / S1-AZ-003 复核，2026-10-16）

由 Claude 在产品负责人要求「验收复核 AUD-001 / AZ-002 / AZ-003 / RT-011 / RT-030」后作默认裁决，未经逐条确认；复核在执行方 cody 所在会话内进行（产品负责人选择在该会话做，非独立复核）。RT-011 已由另一会话复核（R98–R100），本组不重复。承接范围见 `pm/tasks/S1-AZ-007.md`、`pm/tasks/S1-RT-033.md`、`pm/tasks/S1-DEP-005.md`，有异议时新建决策任务。

- R101：服务键语法以组件规格术语表与 §3 `inject[]` 的写法及 R38 为准，不放宽 `plugin.schema.json` 的 `serviceKeys` 模式：单例 seam 为单段 `<Key>`，注册表 seam 的具名注册为 `<Key>.<Registry>:<name>`。`Qian.Authz.Abstractions` 的 `Authz.Permissions`、`Authz.SuperadminBypass` 改为单段键（例如 `AuthzPermissions`、`AuthzSuperadminBypass`）。`qian.authz.resource-type/v1` 只作扩展点契约名，资源类型端口另给单段键（例如 `AuthzResourceTypes`）。三者都没进 `PublicAPI.Shipped.txt`，改名没有兼容负担。改名由 S1-RT-033 在给 Authz 加 `[QianPlugin]` 并同步清单之前完成，在此之前其它组件不得把这三个键写进 `inject`。同时澄清 R94：S1-RT-033 验收 2 的 `[Permission]` / `[QianEvent]` 派生落点按 R37 是组件文件夹的 `metadata/permissions.json` / `metadata/events.json`，不进 `plugin.json`。PRD 7.2.3「合并进 `plugin.json` 的派生字段」与 7.5「权限点来源：清单 `provides.permissions`」按 R37 改写。
- R102：S1-AZ-002 验收 1「安装后租户 Owner 自动拥有」按组件规格（监听 `tenant/provisioned` 实例化 `roles.json`），对新开通租户成立。以下四条生产路径另立 S1-AZ-007（P1，depends S1-AZ-002、S1-RT-005）：

  - 平台模式运行期激活、退役组件的权限目录，增量登记与撤销；
  - 已有租户 `Provision(tenant, plugin)` 时实例化该组件的默认策略；Lifecycle 缺少的租户级开通通知由 S1-AZ-007 或 S1-RT-031 补齐；
  - 租户级升级（`lifecycle.tenant-plugin.upgraded.v1`）升级默认授权，不覆盖租户自定义；
  - 独立宿主组件升级后，已有租户的默认授权随之升级。

  开通时绑定 tenant.owner 的初始授权仍按 R96 归 S1-TEN-003。
- R103：PRD 7.5 的判定缓存键 `(tenant, membership, identity_revision)` 仍是目标。
  - **过渡口径**：identity_revision（S1-IDP-003 / S1-AZ-004）与组织、岗位、组归属（S1-AZ-005）接入前，S1 以 `(tenant, 主体种类, 主体, 租户授权修订)` 为键。租户内角色、绑定、授权或策略安装的变更与修订号同事务递增，每次判定读当前修订，失效不依赖 TTL（TTL 10 分钟只回收旧键）。S1-AZ-003 验收 3「直接主体 ≤ 3 秒」按此成立。
  - **后续接入**：S1-AZ-004 接入 identity_revision 与 `authz.grant.changed.v1` 时，把 identity_revision 并入键，并评估是否保留「每次判定读一次修订号」。
  - **平台超管行**：存于 `roles` / `role_assignments` 中 `tenant_id` 为空的行，经专用平台授权登录读写（`AuthzPlatformAccess`，与审计平台链的 `AuditPlatformAccess` 同构）。`platformRole` 未配置时旁路一律拒绝；生产接线写进 S1-DEP-005 验收。
  - **成员列表**：「平台超管不出现在租户成员列表」（ADR-006 D6.11）写进 S1-IDP-009 与 S1-AZ-005 的验收。

### 验收复核裁决 R104–R105（S1-DEC-023 / S1-TEN-003 / S1-TEN-006 复核，2026-10-16）

- R104：S1-DEC-023「开户期间 hook 的执行身份与 pending 租户授权边界」的留痕（产品负责人 2026-10-16 在执行会话批准最小权限方案，执行人记录；有异议另立决策任务）：`tenant/provisioned` 监听器里经 SDK hook 触发的能力，以进程内、不可序列化的一次性初始化凭据执行——只能由 `qian.tenancy` 的开通编排在目标租户 scope 的生产方上下文开启，绑定真实平台操作者（HTTP 路径绑定到已认证的超管主体；无请求来源的进程内调用只做格式校验，调用方须是可信控制面）、`qian.tenancy` 全局配置 `initializationCapabilities` 的精确清单（`<组件>/<能力>`，禁通配符，只接受 read / write 副作用）、请求类型与权限点；每个能力每次开通只执行一次、不嵌套；不更换宿主当前用户、不签发登录令牌、不建 Membership；审计 `channel = hook`、actor 为平台操作者，重复开通不新增审计；pending 租户对 HTTP 与普通命令入口仍一律拒绝。该凭据下三项命令策略与 Kun PermissionBehavior 仍运行，但判定由凭据给出、不查 RBAC。部署配置可用性（base 组合包 tenancy 条目同时带 `resolver` 时 Apply 被拒）、负例补齐、清单 `scopes` 与文档措辞归 S1-TEN-007。
- R105：R101 的服务键规则（单例 seam 单段 `<Key>`，注册表 seam `<Key>.<Registry>:<name>`）同样适用于 PR #147 / #152 / #154 / #155 新增的 `Authz.TenantRoleGrant`、`Tenancy.Provisioning`、`Tenancy.Operations`、`Tenancy.Lifecycle`、`Tenancy.Settings`、`Tenancy.SettingsOperations`、`Tenancy.Quota`、`Tenancy.UserCounter`、`Tenancy.MembershipProvisioner`、`Tenancy.ProvisionedNotifier`：都改为单段键（例如 `AuthzTenantRoleGrant`、`TenancyProvisioning`），未进 `PublicAPI.Shipped.txt`、无兼容负担；Authz 侧随 S1-RT-033 的改名一起做，Tenancy 侧由 S1-TEN-007 承担，在给 qian.tenancy 加 `[QianPlugin]` 并同步清单之前完成；在此之前其它组件不得把这些键写进 `inject`。S1-TEN-006 验收 4「契约」按以下口径成立：三端点进入组件 OpenAPI 契约、登记 `qian.tenancy.http/v1` 并经 S1-RT-006 的准入接线；契约里缺 4xx 响应与 `x-qian-permission`（PRD 7.5 列为 S1 范围，生成器归 S1-AZ-004 的 API 层 PEP）、准入用例为自比、`tenantId` 断言为空断言、`PlatformHttpContract` 排除无用例，归 S1-TEN-008。

### DEP-003 解阻裁决 R106（2026-10-16）

产品负责人 2026-10-16 要求「处理掉 S1-DEP-003 阻塞」，由 Claude 作默认裁决，未经逐条确认；产品负责人当天认可 R106，#159 随即合入。本条取代以下两处安排：

- 2026-10-14「DEP-003 保持 blocked、四任务范围不变」；

- S1-DEC-021（R81–R84 一节）「顺序与归属」里「S1-DEP-003 等 S1-IDP-002 / S1-IDP-005 / S1-GW-001 / S1-YUE-001 交付后再动」一句。

- R106：S1-DEP-003 拆为两段。
  - **S1-DEP-003 收窄为镜像构建与供应链流水线。** 交付内容：

    - 统一构建入口：多阶段，JIT 与 NativeAOT distroless 两种运行镜像模板，非 root、只读根。
    - 供应链门禁：Syft SBOM、漏洞扫描、Cosign 签名与 SBOM 证明、带发布者身份与 issuer 约束的验签。
    - dump-config 漂移门禁。

    用 CI 内构建、不发布的自检镜像做端到端验证。自检镜像不是任何 profile 的产品镜像，所以不违反 R31「standalone / minimal 不出镜像」，也不替代业务镜像。依赖改为 S1-RT-002、S1-DEP-001，状态由 blocked 改为 todo。
  - **三项真实镜像验收原文移到新任务 S1-DEP-008。** 它依赖 S1-DEP-003，以及 S1-IDP-002、S1-IDP-005、S1-GW-001、S1-YUE-001。只有真实镜像及其证据齐全才可勾选。
  - **发布链**：
    - S1-DEP-004 仍依赖 S1-DEP-003，片段只需 digest 引用约定。
    - S1-DEP-005 显式加依赖 S1-DEP-008，S1-DEC-016「发布门要求真实 qian-platform」的口径不变。
    - ADR-017 §9、deploy/baseline 与 deploy/images 的 README 同步改写。
  - **前置链**按各自 wave 推进，本条不另排期：
    - 身份 / 网关：S1-IDP-002 → S1-IDP-005 → S1-GW-001。
    - 外壳：S1-YUE-006、S1-YUE-007 与 S1-IDP-009 → S1-IDP-010 → S1-YUE-009，最后到 S1-YUE-001。其中 IDP-009 依赖 S1-IDP-003。（R109 补注：S1-YUE-009 拆分后只含 `@yue/sdk`，不再依赖 S1-YUE-007 与 S1-IDP-010，外壳一段为 S1-YUE-006 与 S1-YUE-009 → S1-YUE-001）

### 验收复核裁决 R107（S1-TEN-002 / S1-TEN-004 复核，2026-10-16）

由 Claude 在产品负责人要求「独立验收 S1-TEN-002 与 S1-TEN-004」后作默认裁决，未经逐条确认；复核记录在两个任务文件的进度日志，缺口归 `pm/tasks/S1-TEN-009.md`，有异议时新建决策任务。

- R107：到期扫描能力 `qian.tenancy/tenant.scan-expired` 对模型不可见，契约 `agent.expose` 改为 `false`。R95 写「不标 humanOnly」时把 humanOnly 读成了「只许人调用」；R52 的定义是 `agent.expose: false` = 不派生为模型可见工具、服务端拒绝 `principal_kind = agent`，服务主体不受影响——调度器（S1-SCH-002 的 callback）是服务主体，照样可调。实现里 `TenantExpiryCapability.InvokeAsync` 只接受 global 的 `qian.tenancy` / `qian.scheduler` 调用方，与 `expose: true` 自相矛盾（目录把它派生给 Agent，调用却必拒）。R95 验收 2 的括注据此更正为「不派生为模型工具、服务主体可调」；契约、`TenantStateMetadata`、`TenantStateMachineTests` 的 expose 断言与 README「非 humanOnly」措辞的修改归 S1-TEN-009。S1-TEN-002 / S1-TEN-004 的四条验收按原文成立，done 维持；扫描审计无收尾、`settings.write` 审计追加失败的语义、迁移端点的审计口径一并归 S1-TEN-009。

### yue 第 2 波与租户运营页面批次裁决 R108–R111（S1-DEC-024，2026-10-16）

产品负责人 2026-10-16 想要一个能点的前端页面来测试租户。页面任务 S1-TEN-005 的三个 yue 前置（S1-YUE-007 / 008 / 009）全是 todo：YUE-008 后面排着 S1-FILE-001（其前置 S1-AZ-004 未开工），YUE-009 后面排着 S1-IDP-010 → S1-IDP-009 → S1-IDP-003 → S1-IDP-002。Claude 按 S1-DEC-021 / 022 的做法作默认裁决，把伸进别的领域的验收移交到归属任务，解除互锁与不必要的依赖，产品负责人 2026-10-16 回复「认可 R108–R111」并自行把执行提示词交给 cody 与第二人；产品负责人随后要求把不在页面路径上的 S1-YUE-006 也改好口径、便于交第二人并行（R111）。顺序、「页面可用」收口检查与执行提示词见 `pm/tasks/S1-DEC-024.md`，有异议时新建决策任务。

- R108：yue 组件任务在靶场对契约夹具与 mock 验收，真实后端的 E2E 归提供方或消费方任务。
  - S1-YUE-007 验收 1「用 SchemaForm + DataTable 实现参考应用的客户 CRUD」改为在靶场（`apps/yue-playground`）用 SchemaForm 完成新增 / 编辑弹层、只读详情与提交，请求经 `@yue/sdk` 的 mock 中间件。参考应用 `qian.sample.crm` 没有前端，后端只有服务主体可调的列表、详情与新建，没有编辑与排序筛选；参考应用版本的 CRUD 已在 S1-QA-007 验收 2。真实后端上第一个 SchemaForm + DataTable 的 CRUD 由 S1-TEN-005 承担（R110）。
  - S1-YUE-008 去掉对 S1-FILE-001 的依赖。验收 1 的「参考应用客户列表」改为靶场列表对 mock 中间件完成服务端分页 / 排序 / 筛选与列显隐，参考应用版本同在 QA-007。验收 2「Upload 分片上传经 7.7 成功；秒传命中返回已有 id」与 S1-FILE-003 验收 1 同义，合并到 FILE-003：分片端点 `uploads:init`、`parts`、`complete`、`lookup` 由 FILE-003 交付，FILE-001 只有单次预签名。YUE-008 改为按 FILE-003 背景所列契约，对 mock 完成分片、哈希、进度、断点续传与秒传命中的浏览器测试；FILE-003 depends 加 S1-YUE-008。原口径下两边互锁：YUE-008 的 Upload E2E 需要 FILE-003 的分片端点，FILE-003 的 E2E 又需要 YUE-008 的 Upload。PRD 7.7 写「MD5 秒传」而契约以 `sha256` 查询，Upload 按契约做，口径统一归 FILE-003。验收 3 的「story」读作 Rspress 场景示例（ADR-022 已移除 Storybook）。
  - YUE-007 与 YUE-008 的 depends 加 S1-YUE-009（mock 中间件与 i18n）。
  - PRD 7.15.2 验收第 4 条（参考应用 CRUD）由 S1-QA-007 承担；第 5 条拆为角色授予（S1-AZ-005，R109）与 Upload（S1-FILE-003）。
- R109：S1-YUE-009 拆分，解除对 S1-IDP-010 的依赖。
  - YUE-009 只保留 `@yue/sdk`（注册表 API、`PlatformClient`、请求拦截器链、i18n、mock 中间件、`.d.ts` 契约与 SSE 事件类型），depends 改为 S1-YUE-005，排在 S1-YUE-007 之前：form、data、页面与 biz 都经它取数、取文案、用 mock 测试。
  - `@yue/biz` 基础版另立 S1-YUE-010，depends S1-YUE-007、S1-YUE-008、S1-YUE-009。OrganizeSelect 与授权树建在 `@yue/data` 的 Tree / TreeSelect 上，不另做一套树（yue 规格 D3）。
  - 原 YUE-009 验收 1「参考应用用 OrganizeSelect 选部门、UserSelect 选人，数据来自本租户目录」与 S1-IDP-010 验收 2 同义，合并到 IDP-010，IDP-010 depends 加 S1-YUE-010；YUE-010 对 mock 中间件按 PRD 7.4.3 的目录 API 验证选择器。原口径下两边互锁：YUE-009 依赖 IDP-010，IDP-010 的 E2E 又需要 YUE-009 的选择器。
  - S1-TEN-005 验收 2「租户管理页用 OrganizeSelect / UserSelect / RoleSelect / 授权树完成角色授予」与 S1-AZ-005 验收 2 同义，合并到 AZ-005。AZ-005 depends 的 S1-YUE-009 换为 S1-YUE-010，并加 S1-IDP-010（选择器的真实数据源）。
  - S1-YUE-001 仍依赖 S1-YUE-009：外壳只用 sdk。R106「前置链」的外壳一段据此为 S1-YUE-006 与 S1-YUE-009 → S1-YUE-001，S1-IDP-009 → S1-IDP-010 不再在外壳链上（R106 原文处加补注）。
- R110：S1-TEN-005 收口为「租户运营页面」，在真实后端上验收，不含登录。
  - **范围**：租户列表与筛选（DataTable 服务端分页）、新建 / 编辑抽屉（SchemaForm，字段与校验取自 `qian.tenancy` 的 OpenAPI 契约）、按 `GET /api/tenancy/meta/state-machine` 给出的合法动作做激活 / 冻结 / 恢复（S1-TEN-002 的端点，与 S1-TEN-006 同一平台主体路径，R95）、只读详情与配额（S1-TEN-004 的 `GET /tenants/{id}/quota`）。depends 加 S1-TEN-002、S1-TEN-004（均已 done）。状态迁移是本条对 PRD 7.3 页面清单的补充：新建租户处于 pending，不激活就用不了。
  - **后端补齐**：S1-TEN-006 交付的 `GET /api/tenancy/tenants` 只有 `skip` / `take`，响应没有总数；状态机元数据端点在契约里没有响应 Schema；租户号（小写字母数字、≤ 64）与名称（≤ 200）的约束只在 `PostgresTenantDirectory` 里校验，契约没有，违反时的 400 也不带 `pointer`。TEN-005 顺手补上：列表的可选筛选参数（状态、租户号或名称关键字）、稳定排序与总数；状态机元数据的响应 Schema；新建 / 编辑请求的这两项约束进契约，违反时的 400 带 `pointer`（R50）。契约经 S1-RT-006 准入，只增加可选参数、字段与约束说明，与服务端既有校验一致、向后兼容。这是同一组件的接口补齐，不违背 R85「页面任务是消费方」：端点仍由 qian.tenancy 提供，页面是第一个提出需要的消费方。
  - **移出**：管理员维护（PRD 7.3 API 行的 `GET/PUT /tenants/{id}/admins`，此前没有任务承接）、重置密码与「授权（调 7.14）」另立 S1-TEN-010，depends S1-TEN-005、S1-IDP-003、S1-ENT-001。R81 移交来的「各设管理员，管理员登录后只看到本租户数据」随之归 TEN-010，登录本身仍在 S1-IDP-003 的 R81 条目。角色授予归 S1-AZ-005（R109）。
  - **验收载体**：S1-TEN-006 的真实宿主（Kestrel + JwtBearer RS256 + 受限 PG 应用角色 + 专用平台授权登录写入的超管行）与测试签发的超管令牌。令牌不进 localStorage、URL 与 iframe（PRD 7.15.1 代码审查条）；登录页、令牌刷新与过期弹窗归 S1-YUE-002 / S1-IDP-003。S1-IDP-003 与 S1-TEN-010 交付前，新建表单的管理员按契约直接填 `membershipId` / `subjectId`；开通第 5 步因 `qian.identity` 缺席记 `skipped`，这是 R96 的既定行为，不是测试替身。
  - **形态**：页面是 `qian.tenancy` 的 `ui` 表面（组件规格 §4.7、yue 规格 §5.1），源码作为 pnpm 工作区包交付，带独立开发入口。MF2 远程打包、`ui/manifest.json`（`ui/v2` Schema 尚未入库）与外壳加载归 S1-YUE-001。
- R111：S1-YUE-006 改口径，可交第二人与本批并行。
  - `@yue/shell` 是库，`qian.shell` 是用它的部署单元（yue 规格 §5.2）。YUE-006 验收 1「认证页族完成账号密码登录、SSO 回调与多租户二次选择；登录过期弹窗可触发」改为：在靶场经注入的 `AuthProvider` 与 `@yue/sdk` 的 mock 中间件完成，租户切换器同理。接 qian-identity 的真实流程（OIDC code + PKCE、selection_token、令牌重签）与 S1-YUE-002 验收 1 同义，合并到 YUE-002：它已依赖 S1-IDP-003，并经 S1-YUE-001 依赖 YUE-006。以 Authentik 为上游、经通用 OIDC 连接器的 SSO 登录仍以 S1-IDP-007 验收 1 为准，不重复登记。原口径下 YUE-006 只依赖 YUE-005，验收却要 S1-IDP-002 / 003 的真实登录。
  - YUE-006 depends 加 S1-YUE-009 与 S1-YUE-007：菜单类型注册表建在 `@yue/sdk` 的注册表 API 上（规格 §3.1）；登录表单用 `@yue/form`，shell → form 是同层唯一例外，D3 只留一套表单引擎。验收 3 的「story」读作 Rspress 场景示例（ADR-022）。
  - 第二人在 YUE-009、YUE-007 合入后开工，与 cody 的 YUE-008 / TEN-005 同期，只改 `packages/yue/shell` 与靶场；需要改 sdk、form 或 data 时在任务日志写建议，交 cody 处理。YUE-006 若先合入，TEN-005 可以用 Page / PageHeader，否则用 core 拼骨架。外壳链据此为 YUE-009 → YUE-007 → YUE-006 → YUE-001。
- **顺序与归属**：一人（cody）串行 S1-YUE-009 → S1-YUE-007 → S1-YUE-008 → S1-TEN-005，TEN-005 转 review 前按 S1-DEC-024「页面可用」在本机真实宿主收口；S1-YUE-010 收尾，不阻塞页面可用。S1-YUE-006 按 R111 交第二人并行，不在 cody 的串行顺序里。

### 验收复核裁决 R112–R113（S1-TEN-007 / S1-TEN-008 复核，2026-10-17）

- R112：验收条目里要求的「用例」须有区分度——把被测分支删掉或反转，用例必须失败；自比（候选等于已发布契约再跑准入）、空断言（断言一段永远不会出现的文本）、恒真（被测分支不存在时结果相同）都不算完成。依据：S1-TEN-006 复核的 B1 / B2 与 S1-TEN-008 复核的 A1 三次撞到同一类问题。复核人对每个新增用例至少问一次「删掉实现里的这一行它会不会失败」。本条自 2026-10-17 起适用于全部验收复核，不回溯已 done 的任务。
- R113：平台运营 HTTP 端点（`MapQianTenancy` 及后续同类）的请求处理顺序定为：认证与平台主体检查（401 / 403，含租户主体拒绝）→ 请求绑定与业务输入校验（400，不进审计链、不写旁路审计）→ 组件上下文核对（404）→ 超管旁路授权（403，拒绝原因只进结构化日志）→ 执行；授权之后发生的业务校验失败按 R97 保留已写的授权审计。非超管平台主体因此可能先收到 400：接受，因为输入校验不触碰租户数据且 400 不留审计。DTO 上的 `[JsonRequired]` / `[DisallowNull]` / `[AllowedValues]` / `[Range]` 只服务契约形状（OpenAPI 的 required / 非空类型 / enum / minimum-maximum），运行时的非空与取值约束由端点 validate 保证，README 须写明这一分工。契约快照门禁目前只有 qian.tenancy 一份，其它组件原生 HTTP 契约的导出规则变化没有快照门禁：S1-TEN-011 记录，是否推广由产品负责人定。

### 首次发布前契约纠偏 R114（S1-DEC-025，2026-10-17）

产品负责人附五项条件确认 [租户 HTTP v1 基线纠偏](/design/decisions/tenancy-http-v1-schema-correction.md)：九项 breaking 必须逐条对应修正前服务端代码和测试，保留规则 ID、前后快照及差异；不改 ignore、fail-on 或准入探针。只因 qian.tenancy 尚未进入平台契约仓库，允许 TEN-008/TEN-011 收口线一次形成首个 v1 发布基线；TEN-005 消费该结果，不再重复修改。后续 v1 收窄必须走 v2 及 R33 的别名、Deprecation 和 successor-version Link。发布前必须带齐服务端约束并有真实边界验收；RT-013 publish 或 DEP-005 发布链一旦跑过，或发现已登记基线，纠偏窗口关闭。观察未知不视为未发布。

### 产品负责人指示与裁决 R116–R117（yue 组件视觉收口 S1-YUE-011，2026-10-17；原编号 R114–R115 与 S1-DEC-025 的 R114 撞号，#179 先进 main，2026-10-17 改号）

产品负责人 2026-10-17 判定 yue 现有组件样式不可用，并指示前端组件主要参考 shadcn 与参考平台（产出物一律写「参考平台」）。复核定位到三处基础设施问题：对比度单测把 4.5:1 套到 border / input 等非文字令牌，迫使边框用 neutral-600（亮）/ neutral-300（暗）；全库没有字体栈定义；组件类名用 `*-[var(--x)]` 任意值绕开 Tailwind 主题映射，用不了带透明度的 shadcn 配方。

- R116：令牌对比度门禁分三类：文字配对（foreground 对 background 等）≥ 4.5:1；控件边界（`--yue-input`、`--yue-ring` 对 background）≥ 3:1（WCAG 1.4.11 非文字对比）；`--yue-border`、`--yue-sidebar-border`、分隔线为装饰性，豁免。`registry.json` 每个语义色令牌标 `contrast: text | boundary | decorative`，`tokenContrasts()`、`check-tokens.mjs` 与 `tokens.test.ts` 按类取阈值；yue 规格 §3.5 / §8 同步。S1-YUE-004 已勾选的「全部语义色 ≥ 4.5:1」保留为历史事实，新口径由 S1-YUE-011 落地。
- R117：yue 视觉基准与排版口径：视觉语言的唯一基准是 shadcn 以 Base UI 风格生成的组件源码（`components.json` 的 `style: base-nova`、`baseColor: neutral`、`cssVariables: true`），51 个组件逐个比对 className 配方；参考平台只借后台交互习惯（表格工具栏、查询表单、弹层表单、分页位置、按钮顺序），不借视觉、不抄代码。primary 保留品牌蓝（亮色取与白字 ≥ 4.5:1 的档，暗色降一档），其余语义令牌的明度对齐 shadcn neutral 主题（例外：`--yue-input` 等控件边界按 R116 取满足 3:1 的最浅档——shadcn 亮色 0.922 对白底只有约 1.26:1，暗色白 15% alpha 经浏览器 sRGB 合成后对 L = 0.145 背景约 1.47:1，同样要选达标灰（原 3.8:1 估算未按 sRGB 合成，执行时更正；验收以实际合成色为准）；卡片 / 表格 / 分隔线仍用 0.922。产品负责人 2026-10-17 在执行人提出冲突后选定「保留 3:1，输入边界用最浅达标灰」），`--yue-radius` 0.625rem；字体用系统栈（含 PingFang SC / Microsoft YaHei / Noto Sans CJK SC），不打包 webfont，正文与控件文字 14px；`@idadawn/yue-tokens/theme.css` 以 Tailwind v4 `@theme inline` 注册语义令牌，组件只用语义工具类，任意值清零；弹层 Trigger 不再自带裸按钮基类，示例用 `render={<Button />}`。视觉回归基线从 yue 规格 §8 的「S1 可选」改为 S1-YUE-011 必做。ADR-013 D13.1 / D13.7 的技术栈与 Base UI 锁定不变，不引入 Radix / antd 或第二套引擎。

### 验收复核裁决 R118（S1-YUE-011 复核，2026-10-17）

产品负责人 2026-10-17 要求验收 S1-YUE-011。第二轮复核发现 Dialog / AlertDialog / Sheet 明暗六张基线未随右上角关闭图标的改动刷新而 CI 仍绿：`maxDiffPixelRatio: 0.001` 在 1280×900 全页截图上放过约 1150 像素，Playwright 裸 `--update-snapshots` 只重写比对失败的图。执行人已在 #187 修复，本条固化口径。

- R118：yue 视觉基线一致性与像素门禁：（1）基线必须与改动提交一致——样式 PR 中受影响的基线在同一 PR 刷新，复核以基线文件的 git log 对照组件改动提交；（2）像素门禁取绝对值 `maxDiffPixels: 0`（比例阈值保留但不再起作用），刷新只能显式 `--update-snapshots=all`，普通校验把基线目录只读挂载，`scripts/yue-visual.mjs` 与 `tests/gates/yue/visual-cli.test.ts` 拒绝裸参数、`-u`、`changed` / `missing` 与重复模式；（3）门禁盲区须有反例——隐藏弹层关闭图标后比对必须失败，刷新模式下跳过反例；（4）升级 Playwright 须同步视觉镜像版本并重新审阅全部基线。yue 规格 §4.1 第 4 条由「S1 可选、S2 阻断」改为自 S1-YUE-011 起阻断，§8 对应三项按 #185 / #186 / #187 勾选。验收复核结论：S1-YUE-011 十项成立，done 维持；HoverCard / Accordion 的 Trigger 仍以 button 配方为基类，列为观察项，不阻断。
