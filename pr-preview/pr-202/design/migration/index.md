> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Qian 迁移方案总览

> 本页由构建脚本镜像自仓库 [`docs/migration/README.md`](https://github.com/idadawn/qian/blob/main/docs/migration/README.md)，请修改源文件，不要改本页。

> 状态：v0 草案，2026-10-06。本目录回答一个问题：我们如何从「参考平台」（一套成熟的商业低代码产品，只借鉴、不照搬、不引入代码）的能力集合，分阶段走到 Qian 的插件化平台。这不是数据迁移，而是**能力与设计的迁移 / 重建方案**。
> 权威来源：决策清单（`docs/decisions/key-decisions-v0.md`，含补充裁决）是唯一权威；基线汇编与七份领域基线是素材。基线与决策冲突处一律按决策写，并在各映射文件的「未决问题」注明。
> 引用约定：决策写 `ADR-0xx`（001 Kun 关系、002 插件模型、003 内核边界、004 数据库与隔离、005 身份、006 鉴权、007 Apollo、008 事件、009 审计、010 网关、011 元数据与动态数据、012 流程、013 前端 yue、014 市场、015 模型网关、016 iClaw 边界、017 部署、018 阶段）；PRD 章节写 `PRD §07` 等（01 愿景 … 07 S1 功能规格、08 S2、09 S3、10 S4、11 词表、12 边界、13 安全、14 质量、15 路线、16 指标、17 借鉴声明）。
> 优先级口径（全目录统一）：**P0** = 该阶段退出标准所依赖的必做项；**P1** = 该阶段可选、下一阶段必做或高价值项；**P2** = 视客户需求再排。写法如「S2·P0」。
> 组件模型 v2 口径（2026-10-06，ADR-020）：本目录各表中的「插件 ID」继续有效，但其落位按 v2 理解——`qian.kernel` 的注册表 / 生命周期 / 能力目录改为普通插件（`qian.registry` / `qian.lifecycle` / `qian.capabilities`），运行时只剩 `Qian.Runtime`；「宿主 / 可部署单元」= profile（S1：platform、gateway、identity、standalone、minimal）；「系统插件」= `platform` profile 的 base 组合包成员，按 seam 三角色拆为 `Qian.<X>.Abstractions` / Provider / Consumer 包并可 `qian add` 独立安装；租户开关 = 租户 scope patch。本目录的逐表回填待 PRD §07–§10 修订完成后进行。

## 目录

| 文件                                    | 内容                                                                    | 来源基线 |
| ------------------------------------- | --------------------------------------------------------------------- | ---- |
| `README.md`（本文）                       | 目标与原则、总体映射图、插件 ID 总表、分阶段顺序与完成定义、必须修正的做法汇总、风险与依赖、与 ADR / PRD 的关系、待拍板问题 | 全部   |
| `01-tenant-identity-access.md`        | 租户、组织、身份与 SSO、权限、系统管理、控制台与个人中心、消息入口、移动端                               | S    |
| `02-app-form-list.md`                 | 应用、表单设计、列表设计、视图、功能发布、运行时数据引擎                                          | F    |
| `03-workflow.md`                      | 流程与审批：五类流程、节点、审批方式、处置操作、运行态、工作台                                       | W    |
| `04-data-integration-presentation.md` | 数据中心、集成中心、消息与调度、门户、报表、打印、大屏、文件预览、AI 中心清单                              | D    |
| `05-frontend-yue.md`                  | 参考平台前端组件与设计器全集 → yue 分层与阶段、工程化、移动端策略                                  | U    |

每个映射文件的主体是一张表：参考平台功能 / 组件 → 概括其做法 → Qian 处理方式（沿用概念 / 重新设计 / 不做）→ 目标插件 ID → 阶段与优先级 → 说明与理由；文件末尾统一有「明确不做清单」「必须修正的做法」「未决问题」三节。

***

## 1. 目标与原则

### 1.1 为什么不是移植

参考平台与 Qian 在五个根本点上不同，使得「移植」在技术上不可行、在产品上不可取：

1. **运行形态**：参考平台是 .NET 单体（另有独立 Java 引擎与预览进程），进程内事件总线、进程内 WebSocket 连接池、内存延时队列、本机地址自调用、无集群协调的调度器；Qian 是基于 Kun 的 .NET 10 微服务，部署在 K3s 多副本上（ADR-001 / ADR-017）。几乎所有「进程内状态」在 Qian 都不成立。
2. **组织能力的方式**：参考平台用菜单树 + 系统模块组织能力，第三方登录源、消息渠道、控件类型、列表布局、菜单类型、权限资源类型全部写死为枚举；Qian 以插件思维组织一切能力，平台自带的技术组件也按同一清单以 `tier: system` 交付（ADR-002 / ADR-003），上述枚举全部变成扩展点。
3. **多租户安全基线**：参考平台的租户解析 fail-open、接口层权限校验恒 `true`、会话不可吊销、凭据明文、任意 SQL / 任意 URL 代理；这些不是局部 bug，而是在单体 + 单租户假设下形成的实现习惯，Qian 作为多租户 PaaS 必须在 S1 把它们封死（ADR-004 / ADR-005 / ADR-006）。
4. **前端技术栈**：参考平台是 Vue 3 且「两套并存」（框架无关原语 + 自绘壳之上再叠业务 UI 库与第三方表格）；Qian 前端重建为 React 19 + Base UI + Tailwind v4 + shadcn 源码自有化的 yue 组件库（ADR-013），不移植任何 Vue 代码。
5. **许可证**：参考平台是商业产品，其大屏模块整体嵌入另一第三方商业版产品、流程设计器隐藏了 bpmn-js 水印；Qian 只借鉴功能与教训，不引入代码（PRD §17 借鉴声明）。

### 1.2 借鉴什么

| 借鉴对象        | 内容                                                                                                                             | 落点                           |
| ----------- | ------------------------------------------------------------------------------------------------------------------------------ | ---------------------------- |
| **概念模型**    | 统一授权表 + 多层主体、身份收窄、数据权限方案自动生成、租户控制面与业务面分离、消息「发送配置 → 模板 → 渠道」三层、功能「未发布 / 已发布 / 已修改」三态、流程版本「设计中 / 启用中 / 已归档」三态、主 / 副 / 子表、权限「双重门」 | 映射 01 §2、02 §2、03 §1.1、04 §2 |
| **功能清单与规则** | 参考平台九大功能域的功能树、业务规则、状态机、运算符矩阵、审批人解析规则、任务流程集合语义——作为 Qian 的需求参照与测试用例                                                              | 各映射文件 §1                     |
| **交互模式**    | 三栏设计器 + 右侧页签、向导式三步、校验浮层 + 定位、状态驱动的操作菜单、条件构造器五处复用、导入向导五步、参数绑定表格、「流程级默认 + 节点覆盖」四态选择器、授权向导 + 大树、主记录合并明细展开、「设计如下 / 效果如下」文档模式       | yue 组件与设计器                   |
| **教训**      | 约 40 处「必须修正的做法」（§6）——它们定义了 Qian 的反向约束                                                                                          | 各映射文件 §4 / §5                |

### 1.3 不借鉴什么

- 任何源码、模板、专有包格式（`.bs` / `.vdd` / `.bm` / `.vp` / `.rp` / `.bp` 等）、大屏模块、旧版报表设计器、Vue 前端、uni-app 移动端。
- 「运行时 + 代码生成」双轨（Q11：代码生成不作主轨，S2 后期可选薄壳）。
- 任意 JavaScript 逃生舱（控件事件、表单生命周期、流程自定义按钮、大屏代码组件）——以声明式规则 + 可序列化表达式 DSL + 服务端 Hook 替代，前端 QuickJS 沙箱仅作 S2 可选兜底（Q18）。
- 单体思路的运维面：落库日志一键清空、单机服务器监控、清空全部缓存、内置 IM。

### 1.4 迁移原则

1. **先契约后界面**：凡 S2 才有界面的能力，其契约（Schema、扩展点、事件、权限资源类型）在 S1 定下并由代码开发者先用；各映射文件中「S1·P0（契约）/ S2·P0（界面）」即此意。
2. **横切能力只实现一次**：可发布元数据框架、导入导出框架、引用登记、分享链接、预览票据、编码规则、控件注册表契约、Filter AST、系统变量目录——内核或 `qian.metadata` 一处实现，门户 / 报表 / 打印 / 表单 / 流程共用。
3. **一份契约多处生成**：插件清单派生路由、权限点、菜单、Apollo namespace、OpenAPI、MCP 工具描述；控件注册表同时是设计器左栏与 `GET /registry/widgets`；校验器前后端共用，Agent 生成物走同一校验。
4. **分级克制**：每阶段只做让该阶段验收场景跑通所必需的能力；不复刻参考平台全量。
5. **S4 防返工从 S1 生效**：所有写操作走带 Schema 的命令（副作用等级、幂等键、dryRun）；结构化错误；统一主体模型（user / service / agent）与委托链；可解释可预检的权限判定；租户上下文只来自令牌、`inputSchema` 禁止租户字段；审计含主体链与来源通道；事件契约化；插件包签名与反向权限审批（ADR-018）。

***

## 2. 总体映射图：九大功能域 → Qian 插件家族 → 阶段

```
参考平台九大功能域                      Qian 插件家族                                 主阶段
────────────────────────────────  ─────────────────────────────────────────────  ──────────
① 系统管理 / 租户 / 组织 / 身份 /  →  qian.tenancy · qian.identity(+connector.*)      S1
   权限 / 消息入口 / 个人中心          · qian.directory · qian.authz · qian.config
                                      · qian.audit · qian.navigation · qian.notify
                                      · qian.realtime · qian.masterdata · qian.i18n
② 应用中心 / 在线开发              →  qian.kernel(能力目录·bundle) · qian.metadata      S1 契约
   (表单 / 列表 / 视图 / 发布)         · qian.dynamic-data · qian.schema-migrator        → S2
                                      · qian.forms(+widgets) · qian.lists · qian.expression
③ 流程与审批                       →  qian.workflow(+designer/workbench/admin/        S1 前置
   (简单/标准/任务/自由/决策)          assignee/notify/sla/subflow…) · qian.automation   → S2
                                      · qian.decision · qian.comments
④ 数据中心 / 集成中心              →  qian.data(+dialect.pg) · qian.data-api            S1 契约
                                      · qian.connector.* · qian.open-api                → S2/S3
                                      · qian.directory.sync.*
⑤ 消息中心 / 任务调度              →  qian.notify(+channel.*) · qian.scheduler(+job.*)  S1
                                      · qian.realtime · qian.announce
⑥ 门户 / 报表 / 打印 / 大屏 /       →  qian.print(S2) · qian.portal(+widgets.*) · qian.reports  S2 打印
   文件预览                           · qian.screen(可选) · qian.geo · qian.file-preview · qian.archive  → S3
⑦ 前端框架与组件库                 →  yue(@yue/tokens·core·shell·data·form·charts·     S1 最小集
   (两套并存·8 个设计器)              code-editor·biz·runtime·widgets·designer·sdk)   → S2 设计器
                                      · qian.shell(MF2 外壳)
⑧ 移动端(uni-app 五端)             →  S1 契约层 · @yue/mobile(S3 可选) · 深链协议       S1 契约 → S3
⑨ AI 中心                          →  qian.model-gateway(唯一本期范围) · 其余 S4         S1 底座 → S3/S4
                                      (qian.agent-facade · qian.sandbox · 复用 iClaw)
横切(内核一次实现)                 →  qian.kernel · qian.plugin-sdk · qian.entitlement   S1
                                      · qian.events · qian.gateway · qian.files
                                      · qian.observability · 可发布元数据 / 导入导出 /
                                      引用登记 / 分享链接 / 预览票据 / Filter AST
市场与商业化                       →  qian.marketplace · qian.billing                  S1 包格式 → S3
```

阶段 × 功能域的要点：

| 功能域                   | S1 技术底座                                                                                                                                              | S2 低代码构建                                                       | S3 面向客户                                                                            | S4 Agent                       |
| --------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------- | ---------------------------------------------------------------------------------- | ------------------------------ |
| ① 租户 / 身份 / 权限 / 系统管理 | 租户控制面、唯一 issuer、Person + Membership、会话中心、连接器 SPI + 通用 OIDC + 企业微信、TOTP、RBAC、权限点清单声明、决策端口、配置 Schema + secretRef、菜单清单注册、URN、偏好 KV、通知内核 + 站内信 / 邮件 / 短信 | 数据 / 字段权限编译器、责任人解析、授权界面、导入向导、钉钉 / 飞书、应用菜单多端                    | 完整租户状态机、套餐、多管理员与邀请、自助申请、租户 IdP + HRD、SAML / CAS / LDAP、目录同步 + SCIM、OpenFGA、面向客户工作台 | Agent 主体、`act` 链、Explain 工具    |
| ② 应用 / 表单 / 列表        | 应用 = bundle、菜单与资源模型、可发布资产模型、Field Type Catalog、控件注册表契约、Filter AST、数据规则策略、服务端 Hook、导入导出引擎、能力清单替代系统表单登记                                                | 元数据注册表、动态数据引擎、Schema Migrator、表单 / 列表设计器与控件集、发布向导、权限资源同步、表达式引擎 | bundle 市场安装、外链能力令牌、个人视图、卡片布局                                                       | AI 建表、OCR、Agent 工具契约落地         |
| ③ 流程                  | 只交付前置：组织链、事件、调度、表达式规范、消息模板、元数据版本化契约                                                                                                                  | 审批内核最小集 + 子流程 + 时效催办、纵向节点树设计器、工作台四列表、监控最小集                     | 自由画布视图、SLA 自动处置、委托 / 签收 / 撤销、决策、自由流、归档、批量审批                                        | AI 建流程草稿、Agent 作解析器 / 审批者      |
| ④⑤ 数据 / 集成 / 消息 / 调度  | 类型系统、PG 方言、数据连接、数据接口契约、凭证引用、通知、调度、实时推送                                                                                                               | 数据建模、接口设计器、监控与任务管理 UI、群机器人                                     | 开放 API、身份源同步、公告、第三方消息渠道                                                            | 接口即工具、注册表 → MCP 映射             |
| ⑥ 呈现                  | 控件注册表契约、i18n、预览票据                                                                                                                                    | 打印全套（设计器后期）、文件预览、配置路径型门户与报表子集（可选）                              | 门户设计型、报表、分享链接、大屏（可选）                                                               | 模板 validate / render / explain |
| ⑦⑧ 前端 / 移动            | 令牌、`@yue/core`、AppShell + MF2、DataTable、SchemaForm、Upload、业务选择器、认证页族、SDK、契约草案、工程化基线、移动端契约                                                            | 控件集、三态渲染器、三大设计器、校验器共用                                          | 门户 / 打印 / 报表设计器、UI 沙箱、通知抽屉、H5（可选）                                                  | `@yue/agent`、shadcn MCP Server |
| ⑨ AI                  | 模型网关底座接线（可选）                                                                                                                                         | —                                                              | 模型目录租户可见                                                                           | 每租户 MCP 门面、Agent 生成 package    |

***

## 3. 插件 ID 总表

命名以决策清单 D3 列出的 S1 系统插件为准；S2 / S3 插件沿用基线命名；基线之间命名不一致时以 P 基线附录 B 和决策清单为准，并在表末给出别名对照。子插件用 `父.子` 形式。

### 3.1 S1 系统插件（决策 D3，`tier: system`，18 个；一致性裁决 R1 在原 15 个之上纳入 `qian.notify`、`qian.scheduler`、`qian.realtime`）

| 插件 ID                | 职责                                                                                                                                    | 形态              | 子插件 / 说明                                                                                                                                                                                         |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------- | --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `qian.kernel`        | 插件清单解析与校验、注册表、两层生命周期控制器、扩展点 / 契约仓库、能力目录（命令 / 查询 / 事件 / 实体）、修订回切、验签、GitOps 执行器；横切：URN 目录、用户偏好、引用登记、导入导出框架、分享链接、预览票据、Filter AST 契约、编码服务 | 内核              | —                                                                                                                                                                                                |
| `qian.plugin-sdk`    | `Qian.Plugin.Sdk`（源生成器）、`@qian/plugin-sdk`、`qian` CLI、TCK、Aspire 本地编排                                                                 | 非运行时            | —                                                                                                                                                                                                |
| `qian.tenancy`       | 租户主档、隔离策略与开通编排、状态机、配额、租户事件、租户上下文 fail-closed、租户级配置服务、运营后台                                                                             | module          | —                                                                                                                                                                                                |
| `qian.identity`      | OpenIddict 唯一 issuer、Person + Membership、凭据、会话中心、客户端注册表、连接器 SPI、绑定关系、外部身份映射                                                           | service（独立安全边界） | `qian.identity.connector.oidc`（S1）、`.wecom`（S1）、`.dingtalk` / `.feishu`（S2 P0）、`.wechat`（微信网站扫码，S2 P1，R4）、`.saml` / `.cas` / `.ldap`（S3）；`qian.identity.factor.totp`（S1）、`.sms` / `.passkey`（S3） |
| `qian.authz`         | 权限点注册表、租户域 RBAC、决策端口、数据权限编译器、字段权限、策略包、限时授权 grant、决策审计                                                                                 | module          | `qian.authz.openfga`（S3 可选）                                                                                                                                                                      |
| `qian.data`          | 平台表 EF 写侧 + Outbox + RLS 装配、基准类型系统、方言 SPI、数据连接、审计列与序号约定                                                                               | module          | `qian.data.dialect.pg`（S1）；MySQL 方言本期不做                                                                                                                                                          |
| `qian.files`         | `IFileStorage` 新线、租户前缀、配额、上传策略对象、元数据表                                                                                                 | module          | `qian.files.oss` / `.s3` / `.local`                                                                                                                                                              |
| `qian.config`        | Apollo provider、热更新校验、配置 Schema → namespace、secretRef 解析（`ISecretResolver`）                                                           | module          | 凭证保管入口（K8s Secret / Infisical）                                                                                                                                                                   |
| `qian.gateway`       | Kun.Gateway 加固：强制认证、路由级限流、可信代理、密钥热轮换、Cookie 换 S2S、匿名路由、回调入口；S3 动态路由                                                                   | module（装进网关宿主）  | —                                                                                                                                                                                                |
| `qian.events`        | Outbox 至少一次、租户回灌、Redis 幂等、事件目录 + AsyncAPI、信封对齐 iClaw                                                                                  | module          | —                                                                                                                                                                                                |
| `qian.audit`         | 命令级哈希链（PG 持久化）+ 实体级同事务 `AuditLog`、`IAuditReader`、`actor_type` 含 agent                                                                 | module          | —                                                                                                                                                                                                |
| `qian.observability` | OTel 装配、租户 / 用户 / 插件 baggage、计量事件                                                                                                     | module          | —                                                                                                                                                                                                |
| `qian.shell`         | yue 外壳：MF2 运行时、带版本插槽注册表、桥接 API、统一一个壳按上下文切换                                                                                            | ui              | —                                                                                                                                                                                                |
| `qian.entitlement`   | 授权 v0：租户 × 插件 / 应用 × 版本白名单，与开关分离                                                                                                      | module          | S3 接 `qian.marketplace`                                                                                                                                                                          |
| `qian.model-gateway` | GPUStack → Higress 上游注册、Provider Adapter、`model-capabilities`、租户配额与计量                                                                 | service（可选）     | S1 可选底座 / S3 必做；范围先 LLM + Embedding                                                                                                                                                              |
| `qian.notify`        | 消息类型注册、模板、发送配置、接收配置、投递、重试、记录、落地深链；渠道适配器契约                                                                                             | module          | S1·P0（R1 纳入）；`qian.notify.channel.inbox` / `.email` / `.sms`（S1）、`.webhook`（S2）、`.wecom` / `.dingtalk` / `.feishu` / `.wechat-mp` / `.push`（S3）                                                  |
| `qian.scheduler`     | 集群安全调度器、多租户命名空间、回调契约、结构化执行日志                                                                                                          | module          | S1·P0（R1 纳入；D12 流程前置）；`qian.scheduler.job.local`（S1）、`.data-api`（S2）                                                                                                                             |
| `qian.realtime`      | 网关层 WebSocket / SSE 会话 + Redis / MQ 背板扇出、在线状态、`Push()` 服务接口、事件类型目录                                                                    | module          | S1·P1（R1 纳入；基线 D 为 S1 必做、P 基线为 S2，裁决取 P1）                                                                                                                                                        |

### 3.2 S1 必须就位、但不单列为系统插件的技术组件

以下能力在基线 S / W / D 中均为 S1 必做（它们是登录、审批、数据接口的前置）。一致性裁决 R1 已把 `qian.notify`、`qian.scheduler`、`qian.realtime` 纳入 §3.1 的系统插件清单；其余条目按裁决归入既有插件家族或先以契约形式交付，不扩充系统插件数量（见 §8 问题 1）。

| 插件 ID                           | 职责                                                                          | 阶段                                                        | 来源                                                                                         |
| ------------------------------- | --------------------------------------------------------------------------- | --------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| `qian.directory`                | 组织树、组织类型字典、岗位、责任岗位 / 责任人 / 上级链 / 第 N 级责任人查询、用户档案、分组、通讯录、目录变更事件、交接扩展点        | S1·P0（S1 最小集作为 `qian.identity` 的目录子模块交付，R1；PRD §07 7.4.3） | S 基线 Directory；D12 流程前置；本目录沿用 `qian.directory` 作为目录能力家族 ID，S3 `qian.directory.sync.*` 以其为父 |
| `qian.data-api`                 | 数据接口定义 / 参数 / 返回字段 / 分页 / 执行契约与 SDK、字段语义与值翻译（S1）；设计器、API / 静态执行器、写接口、日志（S2） | S1·P0 契约 / S2·P0 设计器                                      | D 基线 data-api；W 基线 ※interface-center                                                       |
| `qian.navigation`               | 菜单由清单贡献、菜单类型扩展点、端维度、URN 目录消费                                                | S1·P0                                                     | S / F 基线                                                                                   |
| `qian.masterdata`               | 数据字典、行政区划、标签、编号规则 / 序号服务                                                    | S1·P0（字典、序号 API）/ S2                                      | S / F / D 基线                                                                               |
| `qian.i18n`                     | 插件语言包资源机制（S1）、在线翻译管理（S2 可选）                                                 | S1·P0                                                     | S / D / U 基线                                                                               |
| `qian.expression`               | 表达式 / 条件引擎：JSON AST、运算符矩阵、函数注册表、系统变量目录、试算端点（S1 规范 / S2 实现）                  | S1·P0 契约 / S2·P0                                          | F / W 基线；K 基线归入 `qian.forms`（别名）                                                           |
| `qian.saga` / `qian.operations` | 平台内部长事务；Operation / Task 长操作资源                                              | S1·P1 / S2·P0                                             | P / K 基线                                                                                   |

### 3.3 S2 低代码构建

| 插件 ID                                          | 职责                                                                                                                                                                                | 阶段                              |
| ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------- |
| `qian.metadata`                                | 元数据注册表（JSON Schema、草稿 / 发布 / 版本快照 / 回滚 / diff）、低代码应用实体、依赖图、`package` 安装 = 写租户元数据、元数据分层（包层 / 租户层）、字段敏感度；可发布元数据框架在 S1 作为契约                                                          | S1·P0 契约 / S2·P0                |
| `qian.dynamic-data`                            | Records API、泛型命令、Filter AST → SQL（参数化 + 标识符白名单）、Field Type Catalog 与 Provider、批量 Resolver、服务端 Hook、信封事件、数据日志                                                                      | S2·P0（Field Type Catalog S1·P0） |
| `qian.schema-migrator`                         | 表名规范校验（S1）、元数据 diff → 迁移计划 → 审核 → 逐租户执行 → 记录（S2）                                                                                                                                  | S1·P0 / S2·P0                   |
| `qian.forms`                                   | 表单引擎（四段 Schema、校验器、渲染器三态、声明式规则、引用图、生成器）、表单设计器、控件注册表契约（S1）                                                                                                                         | S2·P0                           |
| `qian.forms.widgets`                           | 控件包：输入 / 选择 / 日期 / 上传 / 展示 / 码 / 组织 / 子表 / 数据源 / 区域 / 关联 / 公式 / 系统 / 布局                                                                                                           | S2·P0 / P1                      |
| `qian.lists`                                   | 列表引擎（布局提供者、标准操作集、权限开关 → 可授权项、运行时查询、视图只读数据源）、列表设计器、动作编排、数据管理页                                                                                                                      | S2·P0                           |
| `qian.workflow`                                | 审批内核：定义 / 版本 / `:validate`、Engine（令牌、三网关、父子实例、动态追加原语）、Runtime、内置动作与完成策略、领域事件                                                                                                      | S2·P0                           |
| `qian.workflow.*`                              | `.designer`（纵向节点树 S2 / 自由画布 S3）、`.workbench`、`.admin`、`.assignee`、`.notify`、`.sla`、`.subflow`（S2 必做）；`.data-mapping`、`.actions-advanced`、`.integration`（S2 可选）；`.freeflow`（S3 可选） | 分级                              |
| `qian.print`                                   | 打印模板与版本、模板 Schema、数据绑定与查询设置、挂载扩展点、浏览器打印；设计器 S2 后期；服务端 PDF S3 可选                                                                                                                   | S2·P0                           |
| `qian.file-preview`                            | 预览票据、提供者接口、格式清单、Helm 子 chart                                                                                                                                                      | S1·P1 / S2·P0                   |
| `qian.comments`                                | 通用评论对象（流程为宿主）                                                                                                                                                                     | S2·P1                           |
| `qian.automation`                              | 任务流程：触发器 / 动作插件、`cardinality` / `destructive` 显式、运行记录与重试、入站 WebHook 签名                                                                                                            | S2·P1 / S3·P0                   |
| `qian.codegen`                                 | 调用运行时 SDK 的薄壳脚手架                                                                                                                                                                  | S2·P2（后期可选）                     |
| `qian.wasm-runtime` / `qian.connector-runtime` | `function` Wasm 运行时（S2 可选，ABI S2 中期拍板；服务端公式与规则先走 `qian.expression` 的 JSON AST 解释器，R2）；通用连接器运行时                                                                                    | S2·P2 / S3                      |
| `qian.open-api`                                | 开放分组发布到网关、应用级客户端凭据、调用日志、每组 OpenAPI 3.x + scope                                                                                                                                    | S2·P1 / S3·P0                   |

### 3.4 S3 面向客户

| 插件 ID                                                                                              | 职责                                                   | 阶段                                   |
| -------------------------------------------------------------------------------------------------- | ---------------------------------------------------- | ------------------------------------ |
| `qian.marketplace`                                                                                 | 私有目录市场、安装向导、升级中心（三方比较与冲突清单）、审核台、ISV 入驻、套餐下发、工具描述哈希钉死 | S3·P0                                |
| `qian.billing`                                                                                     | Plan / Seat / Meter / Trial、计量计费（S1 定表约定）            | S3·P0                                |
| `qian.portal` / `qian.portal.widgets.*`                                                            | 门户实体、设计器、运行时、分配与切换、筛选联动；配置路径型 S2 可选；工作台（S2 开发者最小壳可选） | S2·P1 / S3·P0                        |
| `qian.reports`                                                                                     | 报表模板与版本、单元格绑定模型、服务端渲染与 XLSX 导出；子集 S2 可选              | S2·P1 / S3·P0（别名 P 基线 `qian.report`） |
| `qian.directory.sync.*`                                                                            | 企微 / 钉钉 / 飞书通讯录、SCIM Server、LDAP（事件契约 S1）            | S3·P0                                |
| `qian.announce` / `qian.signature` / `qian.geo` / `qian.archive` / `qian.decision` / `qian.screen` | 公告（S3 可选）、签章与签名、地理边界、流程归档、决策服务、大屏（S3 可选，不复用参考平台源码）   | S3·P1                                |
| `qian.authz.openfga`                                                                               | 资源实例共享与市场授权的 ReBAC 适配                                | S3·P1                                |

### 3.5 S4 Agent 原生

| 插件 ID               | 职责                                                                      |
| ------------------- | ----------------------------------------------------------------------- |
| `qian.agent-facade` | 注册表派生的每租户 MCP 逻辑门面（Higress 托管）、A2A Agent Card、Arazzo、Agent 生成 `package` |
| `qian.sandbox`      | CubeSandbox（E2B 兼容）对接                                                   |
| —                   | Agent 运行时、记忆、Skills、工具门禁复用 iClaw，经 MCP / HTTP 对接（ADR-016）               |

### 3.6 别名对照（基线命名 → 本方案 ID）

| 基线命名                                                                                                                                                                              | 本方案 ID                                                                                                                                                                                                       |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| P `qian.sdk`                                                                                                                                                                      | `qian.plugin-sdk`                                                                                                                                                                                            |
| P `qian.data-engine`                                                                                                                                                              | `qian.dynamic-data`（决策 D11 用名）                                                                                                                                                                               |
| P `qian.report`                                                                                                                                                                   | `qian.reports`                                                                                                                                                                                               |
| P `qian.designer`                                                                                                                                                                 | 并入 `qian.forms` / `qian.lists` / `qian.workflow.designer`                                                                                                                                                    |
| F `app-registry`                                                                                                                                                                  | `qian.kernel`（S1 应用 = bundle）/ `qian.metadata`（S2 低代码应用实体）                                                                                                                                                   |
| F `capability-registry` / `widget-registry` / `data-store` / `query-dsl`                                                                                                          | `qian.kernel`（能力目录、Filter AST）/ `qian.forms`（控件注册表契约）/ `qian.data`                                                                                                                                           |
| F `form-engine` / `form-designer` / `widgets-*` / `list-engine` / `list-designer` / `actions` / `publish-wizard` / `dataset` / `expression-engine` / `app-bundle` / `public-link` | `qian.forms` / `qian.forms.widgets` / `qian.lists` / `qian.metadata`（发布向导）/ `qian.data-api` / `qian.expression` / `qian.kernel` + `qian.marketplace`（bundle）/ `qian.dynamic-data` 扩展                         |
| S `Tenant Control Plane` / `Qian Identity` / `Directory` / `Access` / `Navigation` / `MasterData` / `消息内核` / `渠道适配器`                                                              | `qian.tenancy` / `qian.identity` / `qian.directory` / `qian.authz` / `qian.navigation` / `qian.masterdata` / `qian.notify` / `qian.notify.channel.*`                                                         |
| W `workflow-core` / `※expression` / `※messaging` / `※interface-center` / `※comments` / `※metadata-versioning` / `※identity-org`                                                   | `qian.workflow` / `qian.expression` / `qian.notify` / `qian.data-api` / `qian.comments` / `qian.metadata` / `qian.directory`                                                                                 |
| D `data-core` / `db-dialect-*` / `data-connect` / `data-model` / `connector-vault` / `connector-*` / `identity-sync-*` / `ai-gateway` / `share-link`                              | `qian.data` / `qian.data.dialect.*` / `qian.data` / `qian.metadata` + `qian.schema-migrator` / `qian.config`（secretRef）/ `qian.connector.*` / `qian.directory.sync.*` / `qian.model-gateway` / `qian.kernel` |
| K `qian.forms`（表达式解释器）                                                                                                                                                            | `qian.expression`                                                                                                                                                                                            |

***

## 4. 分阶段迁移顺序与完成定义

### 4.1 阶段退出标准（决策 D18）与本方案的「完成定义」

| 阶段                 | 退出标准（D18）                                                                                       | 本方案补充的完成定义（迁移视角的检查项）                                                                                                                                                                                                                                                                                                                                                                                                              |
| ------------------ | ----------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **S1 技术底座**        | 开发人员用代码在 Qian 上写出一个多租户应用：登录 / SSO、鉴权、数据存储、文件上传、配置、网关、事件、审计全部由平台提供；一个官方 module 插件能独立出修订、逐租户迁移、回切 | ① §6 中标「S1」的必须修正项全部有集成测试实证（跨租户越权探测、at-least-once、fail-closed）；② 参考平台「系统表单登记」被插件清单替代：一个代码构建的页面 / 接口经清单登记后自动获得菜单、权限项、流程可引用；③ 以下契约冻结 v1：插件清单、Filter AST、FormSchema / ListSchema 草案、控件注册表 manifest、CapabilityManifest、消息类型、事件信封、URN、深链协议、bundle 包格式 v0、租户 / 会话 / 审计事件 Schema；④ Kun 关键缺口修复合入或补丁包就位（Q7）；⑤ yue S1 最小集合 + MF2 外壳 + Base UI 验证门结论；⑥ Apollo + MySQL、RabbitMQ、Qian 专属 PG / Redis、default-deny NetworkPolicy、ResourceQuota 上线 |
| **S2 低代码构建**       | 跑通「建应用 → 建表单与列表 → 走审批 → 授权给租户用户使用」；同一应用包在两个租户分别安装、各自配置、独立升级互不影响                                 | ① 元数据驱动运行时主轨成立：Records API 参数化、唯一入口三段式鉴权、Schema Migrator 逐租户执行并可回滚；② 一套表单 / 表格 / 弹层引擎贯穿后台 CRUD 与低代码运行时（无第二套引擎）；③ 审批内核最小集 + 子流程 + 时效催办（Q14）跑通，`node.stalled` 有第三结局；④ 打印全套（浏览器打印）可用；⑤ 代码生成不存在或只是薄壳；⑥ 流程 / 表单 / 列表设计器产出物带版本 Schema 且校验器前后端共用；⑦ 参考平台 S2 范围内的「必须修正」全部不复现（拼 SQL、eval、树表跳过数据权限、会签停滞、转审改写记录）                                                                                                                              |
| **S3 面向客户**        | 租户能从应用市场选购并开通应用，自助配置 IdP 与品牌                                                                    | ① bundle 的 `managed` 升级做三方比较并列冲突清单；② 授权（买没买）与开关（开没开）分开存且市场订单打通套餐；③ 租户 IdP 连接 + HRD + 服务端强制「仅企业 IdP」；④ 完整租户状态机含数据保留期与销毁；⑤ 门户设计型、报表、开放 API、分享链接走同一套可发布元数据与鉴权；⑥ 不可信 UI 走沙箱；⑦ 参考平台的用户级 AppSecret、「公有」免鉴权、匿名分享不存在                                                                                                                                                                                                                      |
| **S4 AI 原生 Agent** | Agent 能发现、调用、编排乃至生成平台能力与应用                                                                      | ① 能力目录按权限过滤后派生 MCP 工具；② Agent 主体 + 委托链 + `humanOnly`；③ 生成只落草稿并经同一校验器；④ 审计含主体链与来源通道                                                                                                                                                                                                                                                                                                                                                |

### 4.2 S1 内部的迁移顺序（四波）

| 波次            | 内容                                                                                                                                                                                                                                                                                                                                                                                    | 依赖关系                 |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------- |
| 第 1 波：可信计算基   | Kun 缺口修复（租户回灌含 Saga P0、至少一次、Outbox 锁（其余 Saga 修补 P1 可选，R13）、网关加固、gRPC 流式、OIDC G2–G8、签发器与审计收敛）；`qian.kernel` 清单 v1 + 注册表 + 状态机；`qian.tenancy` fail-closed + 主档 + 开通编排；`qian.data` 默认安全装配 + PG 唯一；`qian.identity` 唯一 issuer + Person / Membership + 会话中心 + 通用 OIDC；`qian.authz` 端口 + RBAC + 权限点清单 + API 强制校验；`qian.gateway`；`qian.events`；`qian.audit`；`qian.config`（Apollo + secretRef） | 一切的前提；身份与鉴权互为前置须并行   |
| 第 2 波：开发者可用   | `qian.plugin-sdk`（源生成器 / CLI / TCK）；能力清单替代系统表单登记；`qian.files`；`qian.identity` 目录子模块（组织树 / 岗位 / 责任岗位链契约，R1）；`qian.navigation` + URN + 偏好；`qian.masterdata` / `qian.i18n`；`qian.notify` + inbox / email / sms；`qian.scheduler`；`qian.realtime`（P1）；`qian.observability`；yue 令牌 + `@yue/core` + AppShell + MF2 + DataTable 基础 + SchemaForm + Upload + 业务选择器 + 认证页族                       | 依赖第 1 波的租户 / 身份 / 鉴权 |
| 第 3 波：S2 契约冻结 | Filter AST 与系统变量目录；Field Type Catalog；控件注册表 manifest；FormSchema / ListSchema 草案；可发布元数据框架；导入导出框架；引用登记；`qian.data-api` 契约 + SDK；`qian.expression` 规范；消息模板注册；流程鉴权资源类型；bundle 格式 v0；`qian.entitlement` 授权 v0；deeplink 协议                                                                                                                                                                  | 让 S1 开发者先用、S2 不返工    |
| 第 4 波：收尾与可选   | 企业微信连接器 + TOTP（补充裁决升为 S1 必做）；SchemaPerTenant 真库验证；`qian.model-gateway` 底座接线（可选）；Elsa PoC 对照；`qian.saga`；DTCG 导出；视觉回归基线                                                                                                                                                                                                                                                                | 不阻塞 S1 退出标准          |

### 4.3 S2 → S4 的顺序约束

- S2 先元数据注册表与动态数据引擎，再表单 / 列表设计器，再审批内核（审批依赖表单字段元数据与责任人解析）；打印与文件预览可与设计器并行；`package` 元数据插件在 S2 中期成立以便「同一应用包两个租户独立升级」的验收。
- S3 先 bundle 安装 / 升级引擎与授权 → 私有目录市场 → 租户自助（IdP / 品牌 / 状态机）→ 门户 / 报表 / 开放 API → ISV 开放与计费。
- S4 以 S1–S3 的能力目录、命令 Schema、委托链、审计为素材，不新增 Agent 运行时（ADR-016）。

***

## 5. 对参考平台「必须修正的做法」汇总

各映射文件 §4 / §5 的全集约 60 条，此处汇总影响最大的 24 条；「阶段」指 Qian 的替代做法必须到位的阶段。

| #  | 参考平台做法                                                                 | 风险                 | Qian 替代做法                                                                | 阶段         | 出处           |
| -- | ---------------------------------------------------------------------- | ------------------ | ------------------------------------------------------------------------ | ---------- | ------------ |
| 1  | 接口层权限校验被注释、恒 `return true`；移动端控制器无校验；依赖客户端 `menuId` / `flowIds`        | 任何已登录用户可调任何接口      | 端点声明权限点，网关 + 服务端双层强制，缺省拒绝；数据引擎唯一入口「资源 + 动作 + 行级」三段式（ADR-006）             | S1         | 01 / 02      |
| 2  | 租户解析 fail-open：缓存未命中落默认库、请求头可指定租户、后台路径靠自觉                              | 跨租户串读              | 租户只来自已校验令牌，fail-closed；非 HTTP 入口显式传播；禁用 header 兜底与 `default` 回退（ADR-004） | S1         | 01           |
| 3  | 约 47 处拼 SQL、租户值字面量、乐观锁版本值直接入 SQL；流程条件拼 SQL 到租户库执行                      | 注入、跨租户             | 参数化 + 标识符白名单 + 查询构建器；RLS 兜底；条件改 JSON AST 内存求值                            | S1 规范 / S2 | 02 / 03      |
| 4  | 进程内状态：WebSocket 连接池、公告延时队列、线程休眠重试、内存延时队列、无集群协调调度器、非原子缓存锁、本机地址自调用       | 多副本下丢失或重复          | Redis / MQ 背板、平台调度、Outbox、乐观并发或分布式锁、内部服务契约调用                             | S1         | 01 / 03 / 04 |
| 5  | 凭据三处来源且明文入库、详情接口回传密钥、AES Key 一钥多用且仓库带默认值、数据库口令可回传还原                    | 泄密                 | 单一凭证来源 secretRef → K8s Secret / Infisical；配置只存引用；详情不回传（ADR-007）          | S1         | 01 / 04      |
| 6  | 改表 = 删表重建、只允许空表；只补系统列、无业务列迁移                                           | 生产库不可演进            | Schema Migrator：元数据 diff → 迁移计划 → 审核 → 逐租户执行（ADR-011）                    | S2         | 02           |
| 7  | 前端两套并存：弹窗 / 表单 / 表格 / Tree 两到三套；控件三处呈现各自维护；两份端配置                       | 维护成本、行为漂移          | yue 一套引擎贯穿后台 CRUD 与低代码；注册表三态呈现；一份 schema + `device` 覆盖（ADR-013）          | S1 起       | 05           |
| 8  | 运行时 + 代码生成双轨（95 个模板 Roslyn 编译，生成物不发事件、不校验按钮）                           | 第二真相源与能力漂移         | 元数据驱动运行时主轨；代码生成退化为 S2 后期可选薄壳（Q11）                                        | S2         | 02           |
| 9  | 元数据无 Schema：70 种控件共用一个 POCO；节点属性无 schema、属性面板 18.8k 行手写；77 个 JSON 列无约束 | 不可校验、不可生成、AI 只能出骨架 | 版本化 JSON Schema + 校验器前后端共用；节点类型发布 Schema + `uiMeta`；jsonb + 注册 Schema 版本 | S1 契约 / S2 | 02 / 03 / 04 |
| 10 | 事件：事务提交前发布、订阅端吞异常、只有事后通知；RabbitMQ 单队列非持久化先 ack 后处理                     | 丢消息、假成功            | Outbox 至少一次 + before / after Hook + 持久化 + 死信 + 两种订阅（ADR-008）             | S1         | 02 / 04      |
| 11 | 无服务端会话吊销；踢人只发 WebSocket；旧 token 到期前仍可用；SSO 登出单向                        | 被踢用户仍有效            | 会话中心 + refresh 轮换 + `identity_revision` + 后通道登出双向                        | S1         | 01           |
| 12 | 兜底一律落到全局 admin；admin 代码特判跳过全部授权                                        | 跨租户、责任不清           | 租户级兜底链（节点指定 → 租户管理员 → 无法提交）；超管旁路显式并审计（Q25）                               | S1 / S2    | 01 / 03      |
| 13 | 会签两阈值都未达到即停滞等管理员                                                       | 实例卡死               | `node.stalled` 事件 + 租户策略转人工或自动判定                                         | S2         | 03           |
| 14 | 转审 / 转办改写流转记录原处理人                                                      | 审计失真               | 只追加不改写，保留委派链                                                             | S2         | 03           |
| 15 | `eval` 执行控件 / 表单 / 按钮脚本且可拿到访问令牌；V8 单例跑任意 JS 公式；小程序端脚本失效                | 任意代码执行、令牌泄露        | 声明式规则 → 表达式 DSL → 服务端 Hook；前端 QuickJS 沙箱仅兜底；自由 JS 不做（Q18）                | S2         | 02 / 03 / 05 |
| 16 | 发布无事务边界，跨主库与业务库十几张表分开提交                                                | 半发布态               | 编译 → 迁移 → 切流 → 挂载的可重放幂等步骤 + 发布日志                                         | S2         | 02           |
| 17 | 外部节点回调、WebHook、公众号回调匿名或仅随机串弱校验；控制面回调只校验请求头存在                           | 伪造回调               | 经网关暴露，租户路由 + 签名或令牌 + 幂等键 + 超时；服务间 mTLS / 服务账号                            | S1 契约 / S2 | 01 / 03 / 04 |
| 18 | 任意 SQL 直执行（大屏）、任意 URL 代理携带用户 token、匿名上传与下载                             | SSRF、泄露            | SQL 文本为高级权限 + 只读账号 + 限额；HTTP 执行器白名单；下载鉴权（Q32）                            | S2 / S3    | 04           |
| 19 | 令牌拼进 iframe URL；access store 持久化 token 与锁屏密码；前端 AES 常量密钥               | 令牌泄露               | 一次性票据或 postMessage 握手；不持久化敏感字段；不在前端持有密钥                                  | S1         | 01 / 05      |
| 20 | 租户功能授权存黑名单、新增菜单默认放开；导出包不含菜单 / 权限 / 字典、不重映射 id                          | 插件化不可控、迁移靠人工       | 白名单 + 授权与开关分离；bundle 含依赖清单与 id 重映射表（ADR-014）                             | S1         | 01 / 02      |
| 21 | 用户级 AppSecret、自定义 AES 开放接口、「公有」免鉴权                                     | 伪安全                | 网关应用级 client_credentials + scope + 限流（Q35）                               | S2 / S3    | 01 / 04      |
| 22 | 消息「发送中」从未写入、每次失败各写一条、重试全员重发、一键清空删站内信、统计全表加载                            | 不可观测、骚扰            | 终态记录 + 尝试次数；发送中态；定向重试；清空只清监控                                             | S1         | 04           |
| 23 | 权限集合物化写入只增不减；基数超限取前 N 个「部分成功」                                          | 撤销不生效、静默截断         | 引用式策略包；直接拒绝 + 结构化错误                                                      | S1 / S3    | 01           |
| 24 | 任务流程集合语义隐式；删除数据节点不设条件即全表删除；定时任务以创建者（缺失则 admin）身份回调本机绝对地址               | 灾难性误操作、越权          | `cardinality` 与 `destructive` 显式 + 试运行；服务名寻址 + 受限服务身份                    | S2         | 03 / 04      |

***

## 6. 风险与依赖

### 6.1 风险

| #  | 风险                                                                                                         | 影响               | 缓解                                                                  |
| -- | ---------------------------------------------------------------------------------------------------------- | ---------------- | ------------------------------------------------------------------- |
| 1  | **身份核心与鉴权引擎同时自建**（「很大」量级且互为前置），补充裁决又把企业微信连接器与 TOTP 升为 S1 必做                                                | S1 延期；安全责任全在自己   | 显式列安全预算（OIDC 一致性测试、渗透）；端口在内核、实现为 system 插件；第 1 波并行推进；连接器 / 因子放第 4 波 |
| 2  | **Kun 关键路径缺口与维护带宽**：单人维护、节奏放缓；丢消息、at-most-once、丢租户、无锁在多副本多租户下必然触发                                          | 后台链路漏租户          | Q7：上游 PR 优先，一个迭代回合入期限，否则 `Kun.*.Patches` 补丁包；集成测试实证                 |
| 3  | **AOT 与动态性**：Kun 严格 AOT 不能热插，低代码靠运行时元数据                                                                    | 架构不成立            | 非 AOT 孤岛 + 进程外插件 + AOT 只留边缘（ADR-001）；S1 接受「预装 + 开关」                 |
| 4  | **逐租户迁移与三方合并**：数千租户 DDL、半升级态、租户定制与新版本冲突                                                                    | S2 验收「两租户独立升级」失败 | 批次 / 并发 / 人工介入；冲突预检；不承诺自动回滚                                         |
| 5  | **自建数据密集组件与 S2 窗口冲突**（路线 A）                                                                                | 设计器延期            | S1 最小集合先行；Base UI 验证门；备选 antd 限控制台域（见 §8 问题 7）                      |
| 6  | **审批逆向语义交叉**（退回 / 撤回 / 撤销 / 拒绝续流 × 子流程 / 加签 / 计时器）且 Q14 增补了子流程与时效                                          | S2 流程域工作量再加约一个迭代 | 回归集 + 属性测试；流转记录只追加；压缩其他 S2 可选项                                      |
| 7  | **现网单点与治理缺失**：三节点只跨两台宿主、几乎全钉首节点、无 ResourceQuota / NetworkPolicy / Alertmanager；需新增 Apollo + MySQL、RabbitMQ | 多租户上线前不可接受       | ADR-017：default-deny、ResourceQuota、Alertmanager；生产独立节点池（Q16）        |
| 8  | **许可证红线**：参考平台大屏嵌入商业产品、bpmn-js 水印、Univer Pro、AG Grid Enterprise、kkFileView 商业 CAD 依赖                       | 法律风险             | 只借鉴设计；大屏不复用；React Flow / DOM 自绘；开源核心 + 排除商业包                        |
| 9  | **iClaw 对齐与设计态资产**：想复用的 MCP Catalog / AI Registry / 市场尚未实现；S2S claim、事件信封、工具元数据需对齐                         | S4 返工            | Q15：租户权威归 Qian；claim 同形；按同一契约先行                                     |
| 10 | **调研输入缺口**：消息中心、任务调度、集成助手手册分卷未输入；富文本编辑器选型未覆盖；系统变量清单只在截图                                                    | PRD 细节缺失         | PRD 撰写时补读；Q39 补选型                                                   |
| 11 | **范围蔓延**：参考平台 251 个前端条目、68 个控件、27 个审批动作、23 个门户控件容易被当作「必须对齐」                                                | 分级失效             | 每阶段只做退出标准必需；各映射文件「明确不做清单」即边界                                        |

### 6.2 外部依赖

| 依赖                                            | 用途                                         | 状态 / 约束                                                         |
| --------------------------------------------- | ------------------------------------------ | --------------------------------------------------------------- |
| Kun（NuGet + CalVer）                           | 底座 SDK                                     | 13 项关键缺口 S1 先修；禁止复制源码进仓                                         |
| PostgreSQL 16                                 | 唯一一等库                                      | 现网每环境专属 PG + pgvector；RLS 为默认隔离                                 |
| Apollo（≥2.5.2）+ MySQL                         | 配置中心                                       | 现网无 Apollo、无 MySQL；集群内 operator 先跑 staging，生产托管（Q8）；不与 iClaw 共用 |
| RabbitMQ                                      | 事件总线                                       | 现网 K3s 上未部署，Outbox 空转；S1 上 K3s                                  |
| Redis                                         | 缓存、会话、幂等、实时推送背板、限流                         | Qian 专属实例                                                       |
| Traefik → `qian.gateway` → Higress            | 三层网关                                       | Higress 只管模型与 MCP 流量（ADR-010）                                   |
| GPUStack + Higress MaaS                       | 模型服务网关                                     | GPU 归属待定；范围先 LLM + Embedding                                    |
| Authentik                                     | 运维侧员工 IdP（Apollo Portal / Headlamp / Argo） | 需升级；客户 CIAM 不用它                                                 |
| Harbor                                        | 镜像与插件包（OCI artifact，只允许 digest 引用）         | 现网已有                                                            |
| Infisical / K8s Secret                        | 密钥运行时权威                                    | Infisical 注入未接通（见 §8 问题 5）                                      |
| Argo CD / Kustomize                           | GitOps                                     | 沿用 L0 / L1 / L2 三层                                              |
| iClaw（代码复用四处：审计拦截器、租户主数据模型、OIDC 授权码实现、通知通道范式） | 复用                                         | Q40：其余只借鉴思路                                                     |

***

## 7. 与 ADR / PRD 的关系

本目录位于「调研基线 → 决策清单 → ADR → PRD」链条的中间：它把基线里逐项的「参考平台怎么做 / Qian 怎么做 / 哪个阶段 / 哪个插件」按决策清单对齐，供 ADR 引用为「借鉴与反例依据」、供 PRD 引用为「功能来源与分级依据」。

| 映射文件                 | 主要支撑的 ADR                           | 主要支撑的 PRD 章节                                                                             |
| -------------------- | ----------------------------------- | ---------------------------------------------------------------------------------------- |
| 01 租户 / 身份 / 权限      | ADR-004、005、006、007、009、010         | §07（租户、身份、鉴权、配置、审计、导航、消息、个人中心）、§13 安全、§09（租户自助、IdP、套餐）                                   |
| 02 应用 / 表单 / 列表      | ADR-002、006、008、011、013、014         | §07（应用 = bundle、能力清单、Filter AST、数据规则）、§08（设计器、动态数据引擎、Schema Migrator、发布）                 |
| 03 流程                | ADR-012、008、006、011                 | §07（流程前置）、§08（审批内核最小集、设计器、工作台）、§09（SLA、委托、决策、自由流）                                        |
| 04 数据 / 集成 / 消息 / 呈现 | ADR-004、007、008、010、011、015、016、017 | §07（数据连接、数据接口契约、通知、调度、实时、凭证）、§08（建模、接口设计器、打印、预览）、§09（门户、报表、开放 API、分享）、§10（模型网关、MCP 映射预留） |
| 05 前端 / yue          | ADR-013、002、016                     | §07（外壳、基础组件、认证页）、§08（控件集、设计器）、§14 质量（工程化门禁）、§09（UI 沙箱、H5）                                |
| README               | ADR-001、003、018、014、017             | §01 愿景、§12 边界、§15 路线、§17 借鉴声明、§11 词表（术语对照见 03 §6 与各文件）                                   |

写作约定：ADR 引用本目录时写「见迁移映射 0x §y」；PRD 功能条目在「来源」列写映射文件编号与行标题，不再重复参考平台的做法描述。各映射文件的「未决问题」在拍板后应转为 ADR 或决策清单补充裁决，并从本目录删除。

***

## 8. 决策清单未覆盖、迁移时必须拍板的问题

以下 8 条是决策清单 D1–D18 与补充裁决均未覆盖、但本方案不拍板就无法落到插件与阶段的问题（第 1 条已在一致性复核裁决 R1 中消解，保留记录）；各映射文件「未决问题」中其余条目是更细粒度的派生项。

| # | 问题                                                                                                                                                                                                                                                                   | 本方案的暂定写法                                                                  | 影响                                                                       |
| - | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| 1 | **S1 系统插件清单的口径**（已裁决 R1）：D3 扩充为 18 个，纳入 `qian.notify`（P0）、`qian.scheduler`（P0）、`qian.realtime`（P1）；组织目录最小集作为 `qian.identity` 子模块（PRD §07 7.4.3）；`qian.navigation`、`qian.masterdata` / `qian.i18n`、`qian.data-api`（契约）、`qian.expression`（规范）不单列为系统插件，按 §3.2 归入既有家族或契约 | 已按裁决写入 §3.1 / §3.2                                                        | 已消解；S1 工作量口径以 PRD §07 为准                                                 |
| 2 | **模型服务网关底座接线是否纳入 S1**：D18 列为可选、Q17 只定了范围与 GPU 归属                                                                                                                                                                                                                     | S1·P1（可选底座）/ S3·P0                                                        | 影响 S1 是否需要 `qian-model-gateway-adapter` 可部署单元与 Higress 配置入 git           |
| 3 | **低代码业务库是否允许租户自带连接；多值字段默认存储策略**：D4 / D11 均列为待拍板但 Q 表无默认；ADR-011 允许 JSON 列或关联表但未定默认                                                                                                                                                                                   | S1–S2 仅平台库，租户自带连接 S3 评估；多值默认 JSON 列、可查询多值用关联表（由 Field Type Catalog 逐类型声明） | Schema Migrator 跨库执行、数据权限拼接、可索引性                                         |
| 4 | **外链填单 / 公开查询的阶段**：基线 F 为 S3 可选（需能力令牌重做），P 基线为 S2 可选                                                                                                                                                                                                                 | S3·P1                                                                     | 若 S2 需匿名采集表单演示，须提前交付能力令牌与网关匿名路由                                          |
| 5 | **密钥运行时权威与短信供应商**：D7 写「K8s Secret / Infisical」二选未定，现网 Infisical 注入未接通；短信 S1 必做但供应商未选                                                                                                                                                                                 | `ISecretResolver` 抽象两种后端；短信 Provider 先接一家云厂商                              | `qian.config` 实现、连接器凭据字段、S1 登录验证码可用性                                     |
| 6 | **跨域能力的阶段与交付归属**：节点打印（W 列 S3、D 列 S2 必做）、automation（触发器与动作跨数据 / 消息 / 流程三域，Q28 只定阶段）                                                                                                                                                                                   | 节点打印挂载契约 S2·P1、条件打印 S3；automation S2·P1 / S3·P0，归属待定                      | 团队分工与 S2 可选项取舍                                                           |
| 7 | **控制台域临时 antd v6 备选是否允许、Base UI 验证门判定标准**：Q12 只说「接受路线 A 风险、首迭代设验证门」                                                                                                                                                                                                  | 不允许混用；验证门不过整体切 Radix；antd 备选未启用                                           | yue S1 人力与 S2 设计器窗口                                                      |
| 8 | **S1 / S2 增补项的范围取舍**：补充裁决把企业微信连接器、TOTP 升为 S1 必做，把子流程、超时催办升为 S2 必做；原有范围未相应收缩                                                                                                                                                                                          | 增补项放入 S1 第 4 波、S2 流程域再加一个迭代；未删减其他项                                        | 若不接受延期，需从 S1 可选（SchemaPerTenant 验证、Elsa PoC）或 S2 可选（数据传递、办理节点、选择分支）中明确删减 |
