> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Qian 阶段路线图

> 本页由构建脚本镜像自仓库 [`docs/ROADMAP.md`](https://github.com/idadawn/qian/blob/main/docs/ROADMAP.md)，请修改源文件，不要改本页。

> 状态：草案 v1 · 更新日期：2026-10-07 · 本文件是**阶段、里程碑与退出标准的唯一真相源**；PRD §15 只保留总览表与指针，决策理由见 `docs/adr/`（ADR-018 为阶段切分决策；ADR-020 组件模型 v2 已于 2026-10-07 Accepted、§7 待确认项全部拍板，S1 交付与退出标准已按其 §6 更新），可测的退出准则细化见 PRD §16。
>
> 命名约定：产品 = Qian · 底座 = Kun · 组件库 = yue · 插件前缀 = `qian.` · 阶段 = S1–S4 · 优先级 = P0 / P1 / P2。
>
> 状态图例：✅ 已完成 · 🚧 进行中 · ⬜ 未开始。里程碑只用 M1 / M2 … 占位，不编日期；日期在阶段启动评审时填入。

## 0. 总览

| 阶段 | 名称             | 目标（一句话）                   | 状态 |
| -- | -------------- | ------------------------- | -- |
| S1 | 技术底座           | 开发者用代码在 Qian 上写出一个多租户应用   | ⬜  |
| S2 | 低代码构建          | 搭建者不写代码建出带审批的应用并授权给租户用户   | ⬜  |
| S3 | 面向客户           | 租户从应用市场自助选购、开通、配置         | ⬜  |
| S4 | AI 原生 Agent 平台 | Agent 发现、调用、编排乃至生成平台能力与应用 | ⬜  |

阶段之间的关系：后一阶段只在前一阶段的契约上叠加，不推翻；退出标准未达成不进入下一阶段的功能开发（允许并行设计与 PoC）。

***

## S1 技术底座 ⬜

### 目标

开发人员用代码在 Qian 上写出一个多租户应用，登录 / SSO、鉴权、数据存储、文件上传、配置、网关、事件、审计全部由平台提供；平台自身能力以 `platform` profile 的 base 组合包交付（原 18 个系统插件按 seam 三角色拆包，ADR-020），每个插件能独立出修订、逐租户迁移、回切；开发者在空白项目里 `qian add identity authz files` 即可直接使用这些能力。

### 退出标准（ADR-018）

- 开发人员用代码在 Qian 上写出一个多租户应用：登录 / SSO、鉴权、数据存储、文件上传、配置、网关、事件、审计全部由平台提供。
- 一个官方 module 插件能独立出修订、逐租户迁移、回切。
- 在空白项目里 `qian add identity authz files` 后 30 分钟内跑通登录、权限检查与上传（ADR-020 §6）。
- 可测项 E1-1 … E1-14 见 PRD §16.1（含：跨租户渗透测试 0 漏洞；集成测试覆盖多副本消费 0 丢失；`platform` profile base 组合包内全部插件 TCK 100%——原「18 个系统插件」口径，ADR-020）。

### 核心交付

| 类别                                                                       | 交付物                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | 优先级                           |
| ------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------- |
| 运行时（原「内核」，ADR-020）                                                       | `Qian.Runtime`（.NET）/ `@qian/runtime`（TS）：上下文 `ctx`、服务注册、类型化事件（`emit / waterfall / parallel / serial / bail`）、可逆副作用 `ctx.Effect()`、加载器；profile / 组合包 / patch 装载器与 `dump-config`；运行时语义 TCK。原内核的注册表、生命周期控制器、能力目录、契约仓库改为普通插件（`qian.registry` / `qian.lifecycle` / `qian.capabilities`）；租户上下文（fail-closed）、主体身份与 S2S、PDP/PEP、审计、计量等强制执行点作为 base 组合包里的 waterfall 监听器                                                                                                                                                                                             | P0                            |
| `platform` profile base 组合包 = 平台插件（原 18 个系统插件按 seam 拆包，ADR-020；规范术语 R30） | `qian.registry` / `qian.lifecycle` / `qian.capabilities`（原 `qian.kernel`；`qian.plugin-sdk` 的大部分并入 `Qian.Runtime` 与 `qian` CLI）、`qian.tenancy`、`qian.identity`（+ `connector.*`：通用 OIDC、企业微信，作为 `ctx.Identity` 的 Provider；组织目录最小集为其子模块）、`qian.authz`、`qian.data`、`qian.files`、`qian.config`、`qian.gateway`、`qian.events`、`qian.audit`、`qian.observability`、`qian.shell`、`qian.entitlement`（授权 v0）、`qian.model-gateway`（可选）、`qian.notify`、`qian.scheduler`、`qian.realtime`；每项拆为 Service Definition（`Qian.<X>.Abstractions`）+ Provider + Consumer，数量由组合包定义而不是决策 | P0（model-gateway、realtime P1） |
| profile ×5（原可部署单元 ×5，ADR-020）                                            | `platform`、`gateway`、`identity`、`standalone`、`minimal`；可部署单元 = profile，新增只是新增一个 profile 文件；原可部署单元 ↔ v2 落位见本表之后的映射表（R31）；`gateway` 等边缘 profile 可 AOT，`platform` 不承诺 AOT                                                                                                                                                                                                                                                                                                                                                                                      | P0                            |
| `qian` CLI / SDK / TCK                                                   | `qian add <component>[@version]` / `remove` / `list` / `doctor` / `dump-config` / `init --template <stack>`（等价 `npx qian@latest --skill x --agent y --plugin z --yes`；原 validate / pack / publish / provision / tck 子命令并入；Node / TS 实现、Node ≥ 22 前置，`dotnet qian` 薄包装 S1 可选 P2，ADR-020 §7 第 3 条）、源生成器（代码部分字段派生并合并进 `plugin.json`）、TCK 用例集（含运行时语义：可逆副作用、scope 过滤、五种分发）、契约生成（OpenAPI 3.1、TS 客户端、MCP 工具描述、权限点）                                                                                                                                                 | P0                            |
| 声明式表面 ×5（ADR-020）                                                        | skills / agents / commands / hooks / MCP 五种表面的解析器与校验器，另有 `tools/`、`ui/`；运行中重载；`AGENTS.md` 进每个仓库与每个包；包 README 含「模型体验」「已知限制与延期项」节                                                                                                                                                                                                                                                                                                                                                                                                                             | P0                            |
| 官方市场（ADR-020）                                                            | 官方市场仓库 `qian-plugins`（S1 私有，S3 随许可证评估公开，ADR-020 §7 第 4 条）与 `.qian/marketplace.json`、目录网站生成、`qian doctor` 健康检查；租户私有市场与社区市场发现 S3                                                                                                                                                                                                                                                                                                                                                                                                                              | P0                            |
| 租户 scope patch（ADR-020）                                                  | 租户启用插件 = 在租户 scope 挂载配置条目（存数据库、与文件 patch 同格式同校验器）；带作用域的遮蔽注册与 `restrict`；S1 scope 种类：租户 / 应用 / Agent 会话                                                                                                                                                                                                                                                                                                                                                                                                                                                      | P0                            |
| 文档生成与 CI 门禁（ADR-020）                                                     | 配置目录、模块图、事件图、seam 图、事件生产方 / 消费方映射由代码生成；新鲜度门禁；事件分发模式声明与调用点一致性校验                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              | P0                            |
| Kun 缺口修复                                                                 | 后台链路租户回灌（含 Saga 执行器）、至少一次消费、Outbox 多副本锁（Saga 锁等其余修补 P1 可选）、网关强制认证与限流、gRPC 流式鉴权、OIDC 客户端链路、签发器与审计路径收敛、安全默认值强制装配                                                                                                                                                                                                                                                                                                                                                                                                                                              | P0                            |
| 身份                                                                       | OpenIddict 唯一 issuer、Person + Membership、密码 / 短信 / TOTP、会话中心（强制下线、后通道登出、refresh 轮换、`identity_revision`）、client_credentials、多租户账号选择                                                                                                                                                                                                                                                                                                                                                                                                                          | P0                            |
| 鉴权                                                                       | `Kun.Authorization` 端口、权限点清单声明、租户域 RBAC、默认角色模板、`check / batchCheck / listAllowedActions / explain`、决策审计；数据权限契约与系统变量（编译器在 S2）                                                                                                                                                                                                                                                                                                                                                                                                                                | P0                            |
| 数据与文件                                                                    | PostgreSQL 唯一一等库、共享库 + RLS 默认装配、schema-per-service、`qian.files` 新线（分片、MD5、预览票据契约）、基准类型与 PG 方言、Filter AST 定义                                                                                                                                                                                                                                                                                                                                                                                                                                                 | P0                            |
| 配置                                                                       | Apollo 部署（≥ 2.5.2，需 MySQL）、自研 .NET provider、secretRef、配置 Schema 化、租户级配置服务、变更先校验失败保留                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | P0                            |
| 事件与审计                                                                    | RabbitMQ K8s 化、Outbox 至少一次、事件信封对齐、事件目录；审计一套（命令级哈希链 + 实体级同事务）、`actor_type` 含 `agent`                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | P0                            |
| 网关                                                                       | Traefik → `qian.gateway` → 服务；Higress 只管模型与 MCP；职责从 S1 画清                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | P0                            |
| 组织与导航                                                                    | 组织树、岗位、责任岗位链查询契约、用户档案、菜单与资源模型、URN、偏好 KV、字典、i18n                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             | P0                            |
| 消息与调度                                                                    | `qian.notify`（通知内核 + 站内信 / 邮件 / 短信渠道）、`qian.scheduler`（集群安全调度器 + 回调契约）、`qian.realtime`（实时推送）、凭证保管；三者在 `platform` profile 的 base 组合包内（原 ADR-003 的 18 个系统插件，R1）                                                                                                                                                                                                                                                                                                                                                                                               | P0（realtime P1）               |
| 前端 yue                                                                   | `@yue/tokens`（三层、明暗）、`@yue/core`、`@yue/shell`（AppShell、MF2、插槽注册表、单一侧栏）、`@yue/data` DataTable 基础版、`@yue/form` SchemaForm、`@yue/biz` 选择器、认证页族、`@yue/sdk`、元数据契约草案                                                                                                                                                                                                                                                                                                                                                                                              | P0                            |
| 包与市场前置                                                                   | 包格式 v0 = 组件文件夹 + marketplace 条目（ADR-014 经 ADR-020 修订，原 bundle v0）、组合包格式、代码部分制品库（OCI → Harbor，仅代码部分）、安装 / 升级 / 卸载引擎（`qian add / remove`）、修订回切、强制验签、授权 v0（手工授予）与租户 scope patch 分离                                                                                                                                                                                                                                                                                                                                                                             | P0                            |
| 部署                                                                       | `qian-<env>` 命名空间、专属 PG / Redis、Argo CD app-of-apps、Kustomize、`/healthz /readyz`、default-deny NetworkPolicy、ResourceQuota、按 digest 发布事件、Alertmanager                                                                                                                                                                                                                                                                                                                                                                                                        | P0（Alertmanager P1）           |
| Agent 防返工                                                                | 带 Schema 的写命令（sideEffect / idempotencyKey / dryRun）、结构化错误、统一主体模型与委托链字段、`inputSchema` 禁租户字段 lint、审计含主体链与来源通道、trace + 计量标签贯通、事件契约化                                                                                                                                                                                                                                                                                                                                                                                                                            | P0                            |
| 可选                                                                       | Operation/Task 服务实现（S1 末；其资源契约由 `qian.capabilities`（原 `qian.kernel`）在 S1 预留，导入导出等长操作返回 Operation 资源，R8）、模型网关底座接线（GPUStack 注册进 Higress + Provider Adapter）、SchemaPerTenant 真库验证、DTCG 导出、Elsa PoC 对照、流程内核 API 提前                                                                                                                                                                                                                                                                                                                                              | P1 / P2                       |

原可部署单元 ↔ v2 落位（R31，S1 默认）：

| 原可部署单元                       | v2 落位                                                                     |
| ---------------------------- | ------------------------------------------------------------------------- |
| `qian-platform`              | profile `platform`                                                        |
| `qian-gateway`               | profile `gateway`（可 AOT）                                                  |
| `qian-identity`              | profile `identity`                                                        |
| `qian-shell`                 | 不是 profile；yue 外壳为 `platform` profile 的 `shell` 组合包（静态前端制品，经 gateway 提供）  |
| `qian-model-gateway-adapter` | `model-gateway` 组合包，默认挂在 `platform`，可选独立 profile `model-gateway`（S1 · P1） |
| S2 `qian-lowcode`            | profile `lowcode`                                                         |
| S2 `qian-workflow`           | profile `workflow`                                                        |
| S2 `qian-file-preview`       | `file-preview` 组合包，挂在 `lowcode` profile                                   |
| `standalone`                 | 消费者自己的宿主，装 `standalone` profile 的最小组合包                                    |
| `minimal`                    | 测试与 SDK 用的最小树                                                             |

> profile 可以被 patch 合并或拆分，表中为 S1 默认（R31）。

### 里程碑占位

| 里程碑   | 内容                                                                                                                                                                                                        | 状态 |
| ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- |
| S1-M1 | ADR-001 … ADR-020 定稿（ADR-019 / ADR-020 已于 2026-10-07 转 Accepted，§7 待确认项全部拍板）；`plugin.json` 清单 Schema 与校验器、五种表面的解析器；`Qian.Runtime` 语义 TCK 首版；Kun 缺口修复 PR 提交；K3s 基线（Apollo、MySQL、RabbitMQ、NetworkPolicy）就绪  | ⬜  |
| S1-M2 | `Qian.Runtime` + profile / 组合包 / patch 装载器 + `qian.tenancy / identity / authz / data / config / events / audit` 可运行；`qian add identity authz files` 可在空白项目装入并跑通；`identity` profile 通过 OIDC 基本一致性测试；TCK 首版 | ⬜  |
| S1-M3 | `platform` profile base 组合包内全部插件过 TCK；`qian` CLI 与官方市场仓库可用，五种表面热重载；租户 scope patch 替代数据库开关；文档生成与 CI 门禁上线；yue 令牌层 + 核心组件 + 后台壳；§06 S1 Demo 由运行时外的开发者走通                                                      | ⬜  |
| S1-M4 | 跨租户渗透测试、多副本消费集成测试、修订回切演练通过；退出评审                                                                                                                                                                           | ⬜  |

### 依赖与风险

| 依赖 / 风险             | 说明                                                               | 应对                                                          |
| ------------------- | ---------------------------------------------------------------- | ----------------------------------------------------------- |
| Kun 维护带宽            | 单人维护、节奏放缓；上游 PR 回合不确定                                            | 上游 PR 优先，设 1 个迭代回合入期限，否则维护 `Kun.*.Patches` 补丁包（ADR-001）     |
| 身份核心与鉴权引擎同时自建       | 两者都是「很大」量级且互为前置；安全责任全在自己                                         | 显式列安全预算（OIDC 一致性测试、渗透测试）；端口在内核、实现为 system 插件；分 M2 / M3 两步交付 |
| Apollo 隐性成本         | 需新增 MySQL（现网全 PG）；官方 .NET 客户端停滞                                  | 集群内 operator 先跑 staging、生产托管；自研 provider；首发 2.5.x           |
| 现网单点与治理缺失           | 三节点只跨两台宿主、几乎全钉首节点、无 NetworkPolicy / ResourceQuota / Alertmanager | S1 补齐；生产与兄弟系统共集群但独立节点池                                      |
| AOT 与动态性            | 严格 AOT 不能热插；低代码需运行时元数据                                           | AOT 只留网关 / gRPC 边缘；业务服务 JIT；运行时装载只对 package / service 成立    |
| Base UI 成熟度         | 原语覆盖未经验证                                                         | 首迭代验证门，不过整体切 Radix，不混用                                      |
| 与兄弟系统（Agent 运行时）的对齐 | S2S claim、事件信封、工具元数据、MCP 版本（对方钉在旧版）                              | 制定对齐计划；Qian 以 2026-07-28 为目标版本并兼容旧客户端                       |

***

## S2 低代码构建 ⬜

### 目标

业务搭建者不写代码建出带审批的应用并授权给租户用户；低代码应用可打包分发；平台自营能力以包形式交付（吃自己的狗粮）。

### 退出标准（ADR-018）

- 跑通「建应用 → 建表单与列表 → 走审批 → 授权给租户用户使用」。
- 同一应用包在两个租户分别安装、各自配置、独立升级互不影响。
- 可测项 E2-1 … E2-13 见 PRD §16.1。

### 核心交付

| 类别                              | 交付物                                                                                                                                                                                                                                                                                                                                                                                                     | 优先级              |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------- |
| 可部署单元（经 ADR-020：以 profile 形式新增） | 新增第 6 个可部署单元 `qian-lowcode`（JIT 宿主 / 非 AOT 孤岛；v2 口径为 profile `lowcode`；`qian-workflow` 为 profile `workflow`，`qian-file-preview` 为挂在 `lowcode` 的 `file-preview` 组合包，R31），承载 `qian.metadata`、`qian.dynamic-data`、`qian.schema-migrator`、`qian.expression`、`qian.forms` / `qian.lists`（服务端部分）、`qian.widgets`（Field Type Provider）、`qian.data-model`、`qian.data-api`；`qian.operations` 住 `platform` profile | P0               |
| 元数据与动态数据                        | `qian.metadata` 元数据注册表与版本化仓储（草稿 / 发布 / 快照、diff、回滚、校验器、包层 / 租户层分层）、`qian.dynamic-data`（混合存储、Filter AST → SQL、泛型 CRUD 命令、`qian.record.changed.v1`）、Field Type Catalog、`qian.schema-migrator`（扩展 → 迁移 → 收缩、逐租户分批）、`qian.expression`（JSON AST 解释器 + 函数注册表）                                                                                                                                                  | P0               |
| 设计器与运行时                         | 表单 / 列表设计器（三栏、单一 Schema + 端可见性、结构化校验错误）、表单 / 列表运行时（三态、子表、三层联动）、控件注册表交付控件集、发布向导（编译 → 迁移 → 切流 → 挂载）、权限资源同步                                                                                                                                                                                                                                                                                                | P0               |
| 审批                              | 自研 .NET 轻量审批内核最小集（发起 / 审批 / 结束、三种网关、抄送、四种审批人来源、兜底三档、或签 / 会签 / 依次、八类动作、子流程、超时催办）、纵向节点树设计器、工作台四列表、`actions` 契约、`node.stalled`                                                                                                                                                                                                                                                                             | P0               |
| 权限                              | 数据权限编译器、字段权限、授权界面（含字段 / 数据）、创建者自动 Owner、责任人解析契约                                                                                                                                                                                                                                                                                                                                                         | P0               |
| 插件机制                            | `package` 元数据插件、`template / managed` + 元数据分层、升级同意规则、三方比较与冲突清单、逐租户批量迁移、Operation/Task、元数据生成闭环（人用）、字段敏感度                                                                                                                                                                                                                                                                                                  | P0               |
| 声明式表面（低代码，ADR-020）              | 表单 / 列表 / 流程 / 页面作为声明式表面（`forms/ lists/ flows/ pages/`）进入组件目录；设计器的保存即产出组件文件夹（可进 git、进市场、被 patch）；profile `lowcode` / `workflow` 新增（R31）；官方代码插件热装卸不进 S2 核心交付：RT 领域 5 天时间盒 spike 验证 collectible `AssemblyLoadContext` 与 Kun 约束、结论写回 ADR-020，生产默认滚动重启（R27；ADR-020 §7 第 5 条已拍板）                                                                                                                             | P0（热装卸 spike P2） |
| 数据与集成                           | 数据建模、接口设计器、API 接口与日志、短信通道 UI、群机器人、监控与任务管理 UI、打印全套（模板、版本、绑定、浏览器打印）、文件预览提供者、模型目录与按用途路由                                                                                                                                                                                                                                                                                                                    | P0（打印设计器 S2 后期）  |
| 身份连接器                           | 钉钉、飞书连接器（P0）；微信网站扫码连接器（P1，R4）                                                                                                                                                                                                                                                                                                                                                                           | P0 / P1          |
| 前端 yue                          | DataTable 进阶、ImportWizard / ExportDialog、SuperQuery、ConditionBuilder、CodeEditor / Cron、业务选择器扩展、三大设计器基础设施、组件 manifest 全量、视觉回归                                                                                                                                                                                                                                                                            | P0               |
| 表达式与脚本                          | JSON AST 表达式解释器 + 函数注册表；服务端 before / after Hook；前端 QuickJS 沙箱兜底（可选）                                                                                                                                                                                                                                                                                                                                     | P0 / P1          |
| 可选                              | Wasm `function` 运行时（P2，ABI 中期拍板；服务端公式与规则先用 JSON AST 解释器）、`connector` 运行时、自动化（任务流程）、配置路径型门户、报表子集（列表 + 分组汇总 + XLSX）、视图（只读数据源）、加签 / 减签、自由画布视图、H5 渲染器（不做，预留端覆盖）、代码生成薄壳（后期）、富文本 / Markdown 选型调研（S2 前）                                                                                                                                                                                                        | P1 / P2          |

### 里程碑占位

| 里程碑   | 内容                                                                  | 状态 |
| ----- | ------------------------------------------------------------------- | -- |
| S2-M1 | 元数据 Schema 与校验器、动态数据引擎、Schema Migrator 可用；首个动态实体从建模到 Records API 跑通 | ⬜  |
| S2-M2 | 表单 / 列表设计器与运行时、发布向导、权限资源同步；§06 S2 Demo 的表单与列表部分走通                   | ⬜  |
| S2-M3 | 审批内核最小集 + 节点树设计器 + 工作台；数据 / 字段权限；§06 S2 Demo 全流程走通                  | ⬜  |
| S2-M4 | `package` 打包、两租户独立安装与升级、冲突清单；平台自营应用以包形式交付；退出评审                      | ⬜  |

### 依赖与风险

| 依赖 / 风险             | 说明                                 | 应对                                        |
| ------------------- | ---------------------------------- | ----------------------------------------- |
| 逐租户迁移与三方合并          | 大量租户 DDL、半升级态、租户定制与新版本冲突无完美方案      | 批次 / 并发 / 人工介入；冲突预检；升级前快照；不承诺自动回滚         |
| 元数据 Schema 与迁移管道从零建 | 参考平台无 Schema、改表即重建是反例              | 版本化 Schema + 前后端共用校验器；diff → 计划 → 审核 → 执行 |
| 动态引擎 SQL 安全         | 拼 SQL、租户字面量是反例                     | 参数化 + 标识符白名单 + 查询构建器；注入测试进退出准则            |
| 审批逆向语义交叉            | 退回 / 撤回 / 撤销 / 拒绝续流与子流程、加签、计时器相互影响 | 回归集 + 属性测试；只追加流转记录；S2 流程域是 3–4 人数个迭代的主线   |
| 自建数据密集组件人力          | 与 S2 窗口冲突                          | S1 最小集先行；备选 antd v6 限控制台域并约定迁移期限          |
| 两套引擎复燃 / DSL 表达力不足  | 倒退回 eval 或新建第二套表单引擎                | lint 门禁；三层联动策略；自由 JS 按钮明确不做               |
| Wasm 宿主缺口           | wasmtime-dotnet 不支持组件模型            | ABI S2 中期拍板；公式与规则先用 JSON AST 解释器          |

***

## S3 面向客户 ⬜

### 目标

租户能从应用市场选购并开通应用，自助配置 IdP 与品牌；开放 ISV 与在线交易；提供高隔离档与私有化交付。

### 退出标准（ADR-018）

- 租户能从应用市场选购并开通应用，自助配置 IdP 与品牌。
- 可测项 E3-1 … E3-11 见 PRD §16.1。

### 核心交付

| 类别              | 交付物                                                                                                                                                        | 优先级                   |
| --------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------- |
| 租户自助            | 完整租户状态机与数据销毁、自助申请审核、多管理员与邀请、套餐下发、租户多连接 + HRD、国内三家 + ISV 第三方应用模式、SAML / CAS / LDAP、Passkey、目录同步（国内源 + SCIM）、自助注销与导出、品牌与白标域名、安装即建 client                     | P0（白标域名 / Passkey P1） |
| 市场 M2 → M3      | 私有目录型市场（目录 / 详情 / 安装向导 / 试用 / 升级中心 / 审核台；受邀 ISV、线下结算）→ 开放 ISV 入驻与席位、在线支付与订阅、分成结算、评分评价、自动化安全扫描；Plan / Seat / Meter / Trial；三方合并冲突预检；工具描述哈希钉死；managed 应用禁止导出 | P0（支付 / 评分 P1）        |
| 商业市场叠加（ADR-020） | 商业市场（授权、计费、审核、ISV 入驻）叠在 `marketplace.json` 之上，不另起格式；`policy.authentication: ON_USE` 的连接器组件；租户私有市场与社区市场发现                                                   | P0                    |
| 隔离与部署           | DatabasePerTenant 高隔离档（独立 ns + PG / Redis，ApplicationSet 生成）、私有化整套 overlay 与离线包 + 授权文件、动态路由（租户 × 已安装应用）、接线 Application 生成器                                 | P0                    |
| 权限              | OpenFGA 插件（资源实例共享与市场授权）、应用中心 / 后台权限区分、组织角色 / 身份切换等高级模式（可选）、Explain 工具                                                                                      | P1                    |
| 呈现              | 门户设计型全套、表格报表（实体、版本、数据集、XLSX）、开放 API、分享链接内核、工作台、通知抽屉、多布局与偏好抽屉（可选）、服务端 PDF（可选）、大屏（可选，不复用参考源码）                                                                | P0（可选项 P2）            |
| 流程              | 拒绝 `continue`、归档、签收、委托 / 代理、撤销申请、暂停 / 恢复、协办、超时自动处置、决策流程（调用式）、自由流程、批量审批、自由画布视图、BPMN 导出器                                                                     | 按评审分 P1 / P2          |
| 安全              | 不可信 UI 沙箱（Worker 远程渲染 / iframe）、第三方 `service` 独立工作负载身份（私有化运维行为）、授权服务器对齐 MCP（RFC 9728 / 8414 / 8707）、成本与配额、租户自助 Agent 授权管理页（可选）                             | P0 / P1               |
| 前端 yue          | 门户 / 报表 / 打印设计器与运行时、移动 H5、UI 沙箱、shadcn 私有 registry（可选）                                                                                                     | P0 / P1               |
| S4 启动前置         | Higress 升级到 ≥ 2.2.4 并开启 MCP 托管（第一批 MCP Server 在 Higress 注册），放在 S3 末；未完成不启动 S4-M1（PRD §10 10.2 / 10.5.8，R14）                                                | P0（S3 末）              |

### 里程碑占位

| 里程碑   | 内容                                                                        | 状态 |
| ----- | ------------------------------------------------------------------------- | -- |
| S3-M1 | 租户自助申请 → 开通 → IdP → 品牌闭环；高隔离档 TCK 通过                                      | ⬜  |
| S3-M2 | 私有目录型市场（M2）上线：自营 + 受邀 ISV、试用、升级中心、审核台；§06 S3 Demo 走通                      | ⬜  |
| S3-M3 | 开放 ISV 与在线交易（M3）；不可信 UI 沙箱；计量可拆分；Higress ≥ 2.2.4 + MCP 托管就绪（S4 启动前置）；退出评审 | ⬜  |

### 依赖与风险

| 依赖 / 风险          | 说明                     | 应对                                                                 |
| ---------------- | ---------------------- | ------------------------------------------------------------------ |
| 第三方代码上架边界        | 已裁决不允许第三方 `service` 上架 | 只开放 package / function / connector / 受限 ui；私有化客户装自己的 service 属运维行为 |
| 国内生态 ISV 模式      | 开源 IdP 普遍缺国内第三方应用模式    | 自研连接器；资质门槛 + 安全审查并行                                                |
| 计费与合规            | 发票、分成、个人信息处理与跨境声明      | 首版「免费 / 联系销售 / 授权码」三种开通方式；在线支付后置                                   |
| 与兄弟系统的市场 / 注册表合并 | 两个市场是否合成一个注册表待定        | 以 Qian 注册表为真相源，对外接口参照官方 MCP Registry OpenAPI                       |

***

## S4 AI 原生 Agent 平台 ⬜

### 目标

Agent 能发现、调用、编排乃至生成平台能力与应用；Qian 作为能力提供方与业务主数据权威，经 MCP / HTTP 对接外部 Agent 运行时，不自建 Agent loop。

### 退出标准（ADR-018）

- Agent 能发现、调用、编排乃至生成平台能力与应用。
- 可测项 E4-1 … E4-8 见 PRD §16.1。

### 核心交付

| 类别                  | 交付物                                                                                                                                                            | 优先级     |
| ------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------- |
| MCP 门面              | 每租户 MCP 逻辑门面（Higress 托管，2026-07-28 无状态、`server/discover`、头部路由、可缓存列表）、按插件 / 应用生成 MCP Server（精选意图级工具 + 能力搜索工具）、授权服务器对齐                                           | P0      |
| Agent 主体            | `agent` 主体类型、`act` 委托链、令牌交换（RFC 8693）、Agent 有效权限 = 委托人 ∩ scope、`humanOnly` 强制、CIMD                                                                             | P0      |
| 元数据生成闭环             | Agent 提交 patch → 校验 → 预览 → 人工批准 → 发布 → 回滚；Agent 生成 `package`；`generatedBy / promptHash`；代码只落 `function` 沙箱                                                     | P0      |
| 长任务                 | Operation ↔ MCP Tasks；`input_required` ↔ MRTR；监督收件箱                                                                                                            | P1      |
| 技能与应用               | 插件包内 `skills/` 目录（agentskills.io；skills 表面本身 S1 已支持，ADR-020，此处只剩提供方式）经 Skills over MCP 提供；MCP Apps 视图（yue 表单 / 列表作为 `ui://` 资源）；`@yue/agent`；shadcn MCP Server | P1      |
| 市场 M4               | 市场兼任私有 MCP registry（参照官方 Registry OpenAPI）；Agent 可检索的市场 API；agent-kit 包类型；Agent 生成应用的自动审核门禁                                                                    | P1      |
| extensions（ADR-020） | 模型在策略下挂载与卸载插件（运行时自修改），建立在 S1 同一套装载器上（S1 只开放给人类 CLI）；Agent 生成的组件落为草稿组件文件夹并经同一校验器；每租户 MCP 门面由 `ctx.Tools` 的 scope 视图派生                                           | P0      |
| 对接                  | 与外部 Agent 运行时（MCP / HTTP）与沙箱（CubeSandbox 类）对接；A2A 端点（可选）；急停开关（可选）                                                                                              | P1 / P2 |
| 流程与数据               | AI 建流程草稿（只生成「设计中」）；Agent 作审批人解析器 / 外部审批者 / 事件消费者 / 自动化动作；AI 建表 / 生成字段；接口即工具；注册表 → MCP 映射；模板 validate / render / explain                                        | P1      |
| 成本                  | 租户 / 应用 / Agent 维度的 token 与调用计量、预算与熔断（经模型服务网关）                                                                                                                 | P0      |

### 里程碑占位

| 里程碑   | 内容                                           | 状态 |
| ----- | -------------------------------------------- | -- |
| S4-M1 | 租户 MCP 门面 + 授权发现 + 按权限过滤的工具列表；Agent 主体与委托链签发 | ⬜  |
| S4-M2 | 元数据生成闭环；§06 S4 Demo 走通；监督收件箱                 | ⬜  |
| S4-M3 | Skills / MCP Apps / 市场兼任 registry；成本配额；退出评审  | ⬜  |

### 依赖与风险

| 依赖 / 风险           | 说明                                           | 应对                                             |
| ----------------- | -------------------------------------------- | ---------------------------------------------- |
| 协议演进              | MCP 路线图仍在推进（DPoP、Agent 身份与委托、渐进式发现）；注册与发现未定型 | 以官方扩展形式承载自定义语义；私有 registry 实现官方 OpenAPI；跟踪弃用窗口 |
| 外部 Agent 运行时的协议版本 | 对方客户端钉在旧版                                    | `server/discover` 兼容旧客户端；对齐计划                  |
| 提示注入与工具投毒         | 不可信内容摄入后的动作、工具描述篡改                           | 确定性控制优先：副作用分级默认策略、哈希钉死、人在回路、沙箱、审计              |
| 成本失控              | 全量工具目录塞入上下文、循环调用                             | 精选意图级工具 + 能力搜索；网关 token 限流与缓存；预算熔断             |

***

## 跨阶段约束（S1 起生效、否则 S4 返工）

1. 所有写操作走带 Schema 的命令（含副作用等级、幂等键、`dryRun`）。
2. 结构化、可自纠正的错误。
3. 统一主体模型（`user / service / agent`）与委托链字段。
4. 可解释、可预检的权限判定。
5. 一份契约多处生成（OpenAPI、MCP 工具、权限点、SDK、UI Schema）。
6. 租户上下文只来自令牌；`inputSchema` 禁止租户字段。
7. 审计含主体链与来源通道。
8. 追踪与计量贯通。
9. 事件契约化（稳定 ID + JSON Schema）。
10. 插件包签名与反向权限审批。
11. 第三方代码不进共享进程；制品不可变按 digest。
12. 一切皆插件、没有特权内核；组件 = 文件夹 + 一份清单 + 声明式表面 + 可选代码；任一注册要么全局、要么归属恰好一个 scope（ADR-020）。

## 修订历史

- 2026-10-06：草案 v1，依据 `docs/decisions/key-decisions-v0.md` D18 与基线汇编阶段矩阵整理。
- 2026-10-06：一致性复核 R1–R20 落实——S1 系统插件 18 个；Wasm `function` S2 可选（P2）；微信网站扫码 S2 P1；Operation/Task 契约 S1 预留；Saga 其余修补 P1 可选；Higress ≥ 2.2.4 + MCP 托管列为 S4 启动前置（S3 末）。
- 2026-10-06：组件模型 v2 回填（ADR-020，Proposed）——S1 目标 / 退出标准 / 核心交付按 ADR-020 §6 第一行更新：新增 `Qian.Runtime` / `@qian/runtime`、`qian` CLI、官方市场仓库、五种表面、五个 profile、租户 scope patch、文档生成与 CI 门禁；「系统插件 ×18」改为 `platform` profile base 组合包；退出标准加「空白项目 `qian add identity authz files` 30 分钟跑通」；S2 加低代码声明式表面；S3 加商业市场叠加；S4 加 `extensions`；跨阶段约束加第 12 条。
- 2026-10-06：裁决 R27（热装卸口径）、R30（平台插件术语）、R31（原可部署单元 ↔ v2 落位映射表，S1 核心交付之后）落实。
- 2026-10-07：ADR-020 §7 八条与 ADR-019 §7 待确认项全部拍板，ADR-019 / ADR-020 转为 Accepted——S1 CLI 行补 Node / TS 实现与 `npx qian@latest`（Node ≥ 22，`dotnet qian` 薄包装 P2）；官方市场行补仓库名 `qian-plugins`（S1 私有）；S2 热装卸改为 RT 领域 5 天 spike、不进核心交付；S1-M1 的 ADR 定稿项按已拍板更新。
