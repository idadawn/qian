# Qian 关键决策清单 v0（待拍板）

> 状态：草案，2026-10-06。用途：在写 PRD 之前把会牵动整体的决策摆到一处，每条给出推荐、备选、依据和需要产品负责人确认的前提。拍板后转为 ADR（`docs/adr/`），PRD 只引用不重复。
> 依据来源：7 份领域基线（`research/baseline-digest.md` 为摘要；全文在仓库外的调研目录）。凡引用「参考平台」指被借鉴的商业低代码产品，只借鉴功能与教训，不引入代码。
> 阶段口径：S1 技术底座期 → S2 低代码构建期 → S3 面向客户期 → S4 AI 原生 Agent 平台。各阶段退出标准见 D18。

## 一页总览

| # | 决策 | 推荐 | 需要你拍板的点 |
|---|---|---|---|
| D1 | Qian 与 Kun 的关系 | Kun 是底座 SDK，Qian 内核自成一层；NuGet + CalVer 锁版本消费；Kun 的 13 项关键缺口在 S1 先修 | 修复走上游 PR 还是 Qian 维护补丁包；Kun 的维护人与响应节奏 |
| D2 | 插件模型 | kind × role × distribution 三轴；平台自带能力也是插件；第三方代码永不进共享进程 | S3 是否允许第三方任意代码容器上架 |
| D3 | 内核边界与 S1 服务拆分 | 内核只留契约、强制执行点、注册表；S1 共 5 个可部署服务 + 15 个系统插件 | 接受「S1 预装全部官方插件 + 按租户开关」 |
| D4 | 数据库与租户隔离 | PostgreSQL 唯一一等库；默认共享库 + RLS；schema-per-service | 是否接受不支持 MySQL/国产库 |
| D5 | 身份与 SSO | 基于 OpenIddict 自建 `qian.identity` 作平台唯一 issuer；外部 IdP 一律做成连接器插件；Authentik 退居运维侧 | 是否有私有化交付、是否首期必须 SAML/CAS/LDAP、是否需要租户自定义登录域 |
| D6 | 鉴权模型 | Kun 侧只加判定端口，Qian 自建权限引擎：权限点由插件清单声明 + 租户域 RBAC + 数据权限编译为查询谓词 + 字段权限；权限不进令牌 | 等保/商密要求；MFA 是否 S1 必做 |
| D7 | 配置中心 | Apollo 作 L1 共享组件（需新增 MySQL），自研 AOT 友好的 .NET provider；只存非敏感配置与密钥引用；租户级配置不进 Apollo；Nacos 收窄为 AI 资产注册 | MySQL 用托管还是集群内；Apollo 首发 2.5.x 还是 3.0.x；是否与 iClaw 共用 |
| D8 | 事件与消息 | RabbitMQ 上 K3s；修 Outbox 丢消息与至少一次语义；事件信封对齐 iClaw 契约 | — |
| D9 | 审计 | 只留一套：命令级哈希链落 PostgreSQL + 实体级同事务写入；actor 含 agent | — |
| D10 | 网关分层 | Traefik（北向入口）→ `qian.gateway`（Kun.Gateway 加固，业务 API 边界）；Higress 只管模型与 MCP 流量 | — |
| D11 | 元数据与动态数据层 | 元数据驱动运行时为主；动态表走「物理主表 + JSON 列」混合存储，不走 EF 模型；统一 Filter AST；正式的 Schema Migrator；代码生成退化为可选薄壳 | 是否保留代码生成；低代码业务库是否允许租户自带 |
| D12 | 流程引擎 | 自研 .NET 轻量审批内核；简单/标准流程合并为一套 JSON 定义；S2 只做节点树视图设计器 | S2 最小集边界（加签/会签延后计算/子流程/超时哪些升为必做） |
| D13 | 前端与 yue | React 19 + Vite + Tailwind v4 + Base UI + shadcn 源码自有化；六层分层；一套表单/表格/弹层引擎贯穿后台与低代码运行时；Module Federation 2 外壳 | 接受自建数据密集组件的交付风险；S2 是否出 H5 渲染器 |
| D14 | 应用市场形态 | 应用 = 插件包（bundle）；template / managed 两种分发；S1 就定下包格式、升级与冲突处理、权益 API | 默认分发模式；共享/专属部署是否作为套餐差异 |
| D15 | 模型服务网关 | 并入团队现有 Higress/MaaS 基础设施；GPUStack 作为 OpenAI 兼容上游 + Provider Adapter；不自写模型代理；不把 SSO 交给 Higress | GPU 归属；接入范围是否含 ASR/TTS/Rerank |
| D16 | 与 iClaw 的边界 | Qian = 能力提供方 + 业务主数据权威；iClaw = Agent 运行时 + AI 资产控制面；共用 Kun、Higress；Qian 不写任何 Agent loop | 租户/身份权威归谁；是否共用 Authentik、S2S 签发、Usage Ledger |
| D17 | 部署 | 沿用现有 K3s/ArgoCD/Kustomize/Harbor 约定；Qian 独立 ns + 专属 PG/Redis；不以 per-tenant namespace 为默认 | 生产与 iClaw 共集群还是独立集群/节点池 |
| D18 | 阶段切分与 S1 范围 | S1 15 个系统插件 + SDK + 部署物料；每阶段有可验收的退出标准 | 阶段顺序是否认可；S1 是否含模型网关底座接线 |

---

## D1 Qian 与 Kun 的关系

**推荐**
1. 定位：Kun 是「微服务开发框架」，不是平台内核。Qian 内核（插件注册表、生命周期、能力目录、租户目录、身份、权限、配置、审计装配）自成一层，Kun 模块是它的实现依赖。
2. 消费方式：NuGet + CalVer 锁版本，禁止把 Kun 源码复制进 Qian 仓；要求 Kun 提供预发布 feed、公共 API 兼容承诺、弃用窗口（≥2 个小版本）和完整变更日志。
3. 不推翻 Kun 的核心设计（四层依赖、Result 错误模型、显式注册的 Mediator、Outbox 表结构与稳定事件 ID、schema-per-service、RLS 双保险、PublicAPI 门禁），但改三件事：不把「AOT 兼容」写成平台承诺（业务服务全 JIT，AOT 只留网关/gRPC 边缘）；Kun 的安全默认值在 Qian 里改成强制装配而非 opt-in；多组并存的双胞胎 API 选定唯一一套（见 D8/D9）。
4. S1 必修的 Kun 缺口（代码已确认，不修则多租户在后台链路漏）：
   - 多租户后台链路：消息消费端、Saga 执行器、Outbox 发布器不恢复租户上下文；两套 `ITenantContext` 互斥且 claim 名不一致；请求头兜底与 `default` 回退会 fail-open。
   - 事件语义：领域事件→集成事件路径会静默丢消息；消费端实际是 at-most-once；Outbox/Saga 多副本无锁。
   - 网关默认不强制认证、限流未挂路由、私钥不能热轮换；gRPC 流式调用绕过鉴权。
   - 身份客户端 OIDC 链路未打通（PKCE verifier 不发、nonce 不校验、无 refresh、无会话吊销）。
   - 三套 S2S 签发器、两族存储接口、多条审计路径并存；框架内写死了下游产品的默认值。

**备选**：monorepo / ProjectReference 引用 Kun（iClaw 现状）。放弃原因：Kun 破坏性变更直接打断 Qian 构建，镜像构建依赖私有仓，第三方插件开发者无法依赖源码。

**需要拍板**：§4 的修复走 Kun 上游 PR（需要明确维护人与响应节奏；Kun 事实上单人维护，近两月提交明显放缓）还是 Qian 维护 `Kun.*.Patches` 补丁包并设回合入期限。

## D2 插件模型

**推荐**
- 三个正交轴：`kind`（module 进程内编译期并入 / service 独立 Pod / package 元数据 / function Wasm / connector / ui）× `role`（provider / capability / extension / app / theme / agent）× `distribution`（template 拷贝即拥有 / managed 托管可升级）。一个「应用」= bundle。
- 平台自带的技术组件（租户、身份、鉴权、存储、文件、配置、网关、模型网关）全部按同一清单以 `tier: system` 交付，同一生命周期、同一 TCK。
- 信任等级决定形态：第三方代码永不进共享进程；`private/community` 只允许 package / function / connector / 受限 ui。
- 清单（`qian-plugin.yaml`）从代码派生、静态可索引：宿主不执行插件代码即可生成路由、权限点、菜单、Apollo namespace、OpenAPI 聚合与 MCP 工具描述。
- 两层生命周期：平台级（制品 → 修订 → 激活，保留旧修订可回切）与租户级（授权 → 开通 → 启用 → 升级 → 卸载 → 清除）分开建模；租户级钩子逐租户、幂等、可重试。
- S1 接受「预装全部官方 module + 按租户开关」；运行时装载只对 package / service 成立。

**明确不做**：运行时热插 DLL；进程共享的服务端 JS 引擎；Dapr sidecar 作为默认依赖；引入 NocoBase / Dify / Grafana / n8n 代码（许可证限制，只借鉴设计）。

**需要拍板**：S3 是否允许第三方 `service`（任意代码容器）上架。这决定审核与运维投入量级；可参照「禁第三方代码但允许纯数据模块」的做法先只开放 package / function / connector。

## D3 内核边界与 S1 服务拆分

**推荐**
- 内核最小集：租户上下文与解析、主体身份与 S2S、权限判定点（PDP 接口 + PEP）、插件注册表与生命周期控制器、扩展点/契约仓库、能力目录（命令/查询/事件/实体四类）、事件总线、审计、配置绑定契约、可观测与计量。其余一律是插件。
- S1 可部署单元克制在 5 个（三节点 K3s 的现实）：`qian-gateway`（Kun.Gateway 加固）、`qian-identity`（OpenIddict，独立安全边界）、`qian-platform`（内核 + tenancy / authz / config / events / audit / files 等 module 型系统插件的宿主）、`qian-model-gateway-adapter`（GPUStack ↔ Higress 控制面适配，可选）、`qian-shell`（前端外壳静态站）。
- S1 系统插件 15 个：`qian.kernel`、`qian.plugin-sdk`、`qian.tenancy`、`qian.identity`（+ `connector.*`）、`qian.authz`、`qian.data`、`qian.files`、`qian.config`、`qian.gateway`、`qian.events`、`qian.audit`、`qian.observability`、`qian.shell`、`qian.entitlement`（授权 v0）、`qian.model-gateway`（可选）。

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
- 上游联邦全部做成连接器插件：S1 通用 OIDC、企业微信/钉钉/飞书扫码与工作台免登、微信网站扫码；S3 加 SAML/CAS/LDAP、租户自助配 IdP、域名验证与 HRD、Passkey、SCIM。
- Authentik 升级后只服务运维侧（Apollo Portal、Headlamp、Argo），也可作为一个上游连接器；客户 CIAM 不用它（企业版多租户是 alpha 且按租户收费）。
- 同时修 Kun.Identity 的 G2–G8 缺口。

**备选**：Keycloak 26.x Organizations 做协议层（协议最全，但 Java、每 Pod 约 1.25 GB、国内登录要写 SPI、租户主数据双真相）；Logto OSS（国内连接器全，但 Node 栈、无 LDAP、部分付费）。不推荐 Casdoor（2026-05 一次 9 个 CVE）、Zitadel（AGPL）、Duende（商业许可）。

**需要拍板**：是否有私有化/离线交付（决定 AGPL/商业许可可否）；首期是否必须 SAML/CAS/LDAP（政企、高校客户）；租户自定义登录域名是否刚需（决定单 issuer 还是每租户 issuer）；是否有等保/商密（SM2/SM3）要求。

## D6 鉴权模型

**推荐**
- Kun 侧只新增 `Kun.Authorization` 端口（`IPermissionChecker` Check / BatchCheck / ListAllowedActions、`IDataPermissionFilter`、`IFieldPolicy` + ASP.NET Core / Mediator / gRPC 集成），不放实现。
- Qian 的 `qian.authz`：权限点与默认角色模板由插件清单声明；租户域 RBAC（内置 + 自定义角色，主体可为用户/组织/岗位/组）；数据权限 = 条件树 + 系统变量，编译成 SQL/EF 谓词在本地执行，RLS 永远兜底；字段权限读投影/写白名单；每次决策可解释并入审计。
- 权限不进令牌：令牌只含身份、`tenant_id`、`membership_id`、`scope`、`identity_revision`；权限变更即时失效靠 `identity_revision` 递增 + 推送刷新。
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

| 阶段 | 退出标准 | 核心交付 |
|---|---|---|
| S1 技术底座 | 开发人员用代码在 Qian 上写出一个多租户应用：登录/SSO、鉴权、数据存储、文件上传、配置、网关、事件、审计全部由平台提供；一个官方 module 插件能独立出修订、逐租户迁移、回切 | 15 个系统插件、SDK（源生成器 + CLI + TCK）、部署物料、Kun 缺口修复、能力目录与清单 v1、授权 v0、yue 令牌层 + 基础组件 + 后台壳 |
| S2 低代码构建 | 跑通「建应用 → 建表单与列表 → 走审批 → 授权给租户用户使用」；同一应用包在两个租户分别安装、各自配置、独立升级互不影响 | 元数据注册表、动态数据引擎、Schema Migrator、表单/列表/审批流设计器与运行时、流程内核、控件注册表、数据权限与字段权限、Operation/Task、Wasm 函数运行时 |
| S3 面向客户 | 租户能从应用市场选购并开通应用，自助配置 IdP 与品牌 | 私有目录型市场 → ISV 开放、权益与计费、门户、报表、租户自助、高隔离档、不可信 UI 沙箱 |
| S4 AI 原生 Agent 平台 | Agent 能发现、调用、编排乃至生成平台能力与应用 | 每租户 MCP 门面、Agent 主体与委托链、元数据生成闭环、与 iClaw 运行时对接 |

S1 到 S3 期间必须提前做对、否则 S4 返工的设计（S1 起生效）：所有写操作走带 Schema 的命令（含副作用等级、幂等键、dryRun）；结构化可自纠正的错误；统一主体模型（user / service / agent）与委托链字段；可解释可预检的权限判定；一份契约多处生成；租户上下文只来自令牌、`inputSchema` 禁止租户字段；审计含主体链与来源通道；追踪与计量贯通；事件契约化；插件包签名与反向权限审批。

**需要拍板**：四段顺序是否认可（尤其「低代码构建」单独成 S2 并先于「面向客户」）；S1 是否包含模型网关底座接线（可选项）。

---

## 需要你拍板的问题汇总

按影响面排序，每条给我的默认倾向；不回复即按默认写入 PRD。

| # | 问题 | 我的默认 |
|---|---|---|
| Q1 | 四段阶段划分是否认可 | 认可 |
| Q2 | 身份路线：自建 OpenIddict 唯一 issuer（Authentik 退居运维侧） | 是 |
| Q3 | 是否有私有化/离线交付、政企客户是否首期必须 SAML/CAS/LDAP | 假定有私有化需求、SAML/CAS/LDAP 放 S3 |
| Q4 | 租户自定义登录域名是否刚需 | S3 再做；S1 单 issuer |
| Q5 | 是否接受 PostgreSQL 为唯一一等库、不支持 MySQL/国产库 | 接受 |
| Q6 | 默认隔离策略共享库 + RLS，独立库作 S3 付费选项 | 是 |
| Q7 | Kun 缺口修复走上游 PR 还是 Qian 补丁包；Kun 维护人与响应节奏 | 上游 PR 优先，设 1 个迭代回合入期限，否则补丁包 |
| Q8 | Apollo：MySQL 托管还是集群内；首发版本；是否与 iClaw 共用 | 集群内 operator 先跑 staging，生产托管；2.5.x；不共用 |
| Q9 | S3 是否允许第三方任意代码容器上架 | 不允许，只开放 package / function / connector / 受限 ui |
| Q10 | 元数据应用包默认分发模式 | 默认 managed，template 作为可选 |
| Q11 | 是否保留代码生成 | 不作为主轨；S2 后期可选薄壳 |
| Q12 | 前端组件底座：接受路线 A 自建数据组件风险 | 接受，首迭代设验证门 |
| Q13 | S2 是否交付 H5 表单渲染器 | S2 只做 PC 响应式，预留端覆盖 |
| Q14 | 流程 S2 最小集：加签/会签延后计算/子流程/超时哪些升为必做 | 子流程与超时催办升为必做，加签减签与候选人保持可选 |
| Q15 | 与 iClaw：租户权威归属、是否共用 Authentik / S2S / Usage Ledger | 租户权威归 Qian；共用 Authentik（运维侧）与 Higress；S2S 各签各的但 claim 同形；Usage Ledger 待 S3 |
| Q16 | 生产集群是否与 iClaw 共用 | 共用集群但独立节点池，达到一定租户数再拆 |
| Q17 | 模型网关：GPU 归属、接入范围 | 先 LLM + Embedding；GPU 归属你定 |
| Q18 | MFA 是否 S1 必做；等保/商密要求 | TOTP S1 做；商密不做假设，需你确认 |

> 2026-10-06：Q1–Q18 的默认全部获认可。

---

## 补充裁决（来自基线汇编 §3.4 / §3.5，2026-10-06）

基线汇编 `research/baseline-digest.md` 汇总了 41 个待拍板问题，其中约一半已被 D1–D18 覆盖；其余按下表裁决，与各基线的倾向一致处不再说明理由。

| 汇编编号 | 问题 | 裁决 |
|---|---|---|
| Q2 | 账号归属 | 全局自然人（Person）+ 租户成员（Membership）为主；「租户托管身份」作为 S3 可选，并存时以邮箱/手机验证后的显式合并为准 |
| Q10 | 身份切换、组织角色、互斥/基数约束等高级权限模式 | S3 可选；S1 在令牌里预留 `profile` claim，权限模型按 L0–L6 分层留扩展位 |
| Q12 | 用户配额口径 | 平台超管不计入租户配额，租户管理员计入；平台超管对租户不可见（审计可见） |
| Q13 | 租户自助申请审核、「企业 × 应用」关系、邀请格式 | 参考平台未交付该部分，由 Qian 自行设计，写入 PRD 租户生命周期章 |
| Q17 | 视图（数据集驱动的纯列表） | S2 可选，归入列表引擎的「只读数据源」形态，不单独立项 |
| Q18 | 脚本能力边界 | S2 只做声明式规则 + 服务端 before/after Hook + 可序列化表达式 DSL；前端 QuickJS 沙箱为 S2 可选兜底；自由 JS 按钮不做 |
| Q19 | 数据权限多方案合并语义 | 沿用源码确认的「同角色 AND / 跨角色 OR / 任一全部数据则放开」，不做可配置 |
| Q20 | 功能编码与多应用挂菜单 | 采用「租户.应用.功能」命名空间化稳定编码 + id 重映射表；一个功能只归属一个应用，其他应用通过「跨应用引用」挂菜单（引用而非归属） |
| Q22 | 审批设计器形态 | S2 用纵向节点树（DOM 自绘，对 Agent 生成最友好）；自由画布（React Flow）S3 可选；不用 bpmn-js（许可证要求水印可见）；BPMN 只作导出器且不早于 S3 |
| Q24 | 拒绝后继续流转 | S3；S2 在动作枚举中预留 |
| Q25 | 审批人兜底 | 兜底链 = 节点指定异常处理人 → 租户管理员角色 → 「无法提交」结构化错误；不再兜底到全局管理员 |
| Q26 | Agent 代办身份 | 委托模型区分本人 / Agent / 系统三类 actor；Agent 有效权限 = 委托人权限 ∩ 授予 scope；签名类动作标 `humanOnly`；转审、协办、撤销 S4 前对 Agent 禁止 |
| Q27 | 流程命名与市场 | 流程层用「启用/停用」替代「上架/下架」避免与市场撞名；流程模板、决策表节点包作为 S3 市场商品 |
| Q28 | 自动化与决策阶段 | 自动化（任务流程）S2 可选、S3 必做；决策引擎 S3 可选 |
| Q29 | 门户与报表阶段 | 按阶段路线归 S3；S2 提供「配置路径型」门户（固定模板 + 配置）与报表子集（列表 + 分组汇总 + 导出 XLSX）作为可选，保证 S2 应用有可用首页 |
| Q30 | 报表深度 | 先子集（列表、分组汇总、导出），交叉表/父子格/分栏 S3 再评估是否自研展开引擎 |
| Q31 | 大屏 | 延后到 S3 可选，且不复用参考平台大屏源码（许可证高风险）；只消费内核数据接口 |
| Q32 | 数据接口 SQL 文本模式 | 开放，但作为高级权限：只读数据库账号、超时与行数上限、参数化、审计 |
| Q33 | 服务端 PDF | S2 浏览器打印；服务端 HTML→PDF S3 可选 |
| Q34 | 文件预览技术栈 | 预览提供者接口可替换；首选 .NET 生态方案，Java 预览进程只作可选 sidecar |
| Q35 | 开放接口凭据 | 废弃用户级 AppSecret，统一为网关的应用级客户端凭据（client_credentials），取消「公有」免鉴权 |
| Q37 | 后台壳多面策略 | 统一一个壳，按「平台控制台 / 应用」上下文切换，与插件外壳模型一致；不采用三面新窗口 |
| Q38 | 布局模式与偏好 | S1 单一侧栏布局；多布局与偏好抽屉 S3 可选 |
| Q39 | 富文本 / Markdown 编辑器 | 五份调研均未覆盖，S2 前补一次选型调研 |
| Q40 | iClaw 代码复用授权 | iClaw 与 Qian 同属一个所有者，允许搬代码；但只搬审计拦截器、租户主数据模型、OIDC 授权码实现、通知通道范式四处，其余只借鉴思路 |
| Q41 | yue 分发渠道 | 私有 npm 为主；shadcn 私有 registry + MCP Server S3 可选、S4 必做 |

**基线之间的矛盾裁决（汇编 §3.5）**

| 议题 | 裁决 |
|---|---|
| IdP 形态 | 以 K/S 为准：OpenIddict 自建唯一 issuer；P 基线的「RP 侧 module + 托管 IdP」表述作废 |
| 企微/钉钉/飞书连接器 | S1 必做：通用 OIDC + 企业微信（扫码与工作台免登）；钉钉、飞书 S2 必做；ISV 第三方应用模式 S3 |
| refresh 令牌与单点登出 | S1 必做（K/S） |
| 审计路径 | 以语义定：实体级审计必须与业务同事务写入 `AuditLog` 并经 Outbox 发事件（借 iClaw 拦截器写法）；命令级哈希链补 PostgreSQL 持久化；事后不同事务的拦截器弃用。具体落在哪个 Kun 包由 ADR 定 |
| Wasm `function` 运行时 | S2 可选：公式与规则先用 JSON AST 表达式解释器；Wasm 在 ISV 函数插件（S3）前落地，ABI 在 S2 中期拍板 |
| 打印 | S2 必做（模板、版本、数据绑定、浏览器打印）；打印设计器 S2 后期 |
| 工作台 | S3 必做（S2 只有审批四列表 + 应用入口） |
| 短信渠道 | S1 必做（登录验证码与找回密码依赖它） |
| 公告 | S3 可选 |
| DTCG 令牌 | 令牌三层结构 S1 必做；DTCG 格式导出 S1 可选 |
| Module Federation 2 运行时 | S1 必做（K/P），U 基线的分层表补上 `@yue/shell` 对 MF2 的依赖 |
