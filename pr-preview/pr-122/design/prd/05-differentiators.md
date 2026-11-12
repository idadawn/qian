> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# 5. 核心差异化

> 本页由构建脚本镜像自仓库 [`docs/prd/05-differentiators.md`](https://github.com/idadawn/qian/blob/main/docs/prd/05-differentiators.md)，请修改源文件，不要改本页。

> 状态：草案 v1 · 更新日期：2026-10-06 · 决策引用：ADR-001、ADR-002、ADR-003、ADR-004、ADR-005、ADR-006、ADR-011、ADR-014、ADR-015、ADR-016、ADR-019、ADR-020 · 上级索引：[README](/pr-preview/pr-122/design/prd/index.md)

每根支柱：**名称（起始阶段）→ 承诺一句 → 具体措施清单**。措施只写「做什么、到什么程度」，实现细节与接口见 §07–§10。支柱之间的关系：支柱 1 是方法，支柱 2 是面向未来的契约层，支柱 3 是面向开发者的交付面，支柱 4 是不可绕过的安全基座，支柱 5 是把前四者变成商业形态的管道，支柱 6 是把一切呈现给人的那一层。

## 支柱 1：一切皆插件、文件即组件、一键安装，且平台吃自己的狗粮（S1）

**承诺。** 平台自带的每一项能力与第三方插件走同一份清单、同一运行时语义、同一 TCK；开发者遇到的每个坑，官方插件先踩过。组件化按 Agent 时代的做法而不是传统平台设计：没有特权内核，组件 = 文件夹 + 清单 + 声明式表面 + 可选代码（无代码组件是常态），`qian add <component>` 一条命令装进任何项目（ADR-020）。

**措施。**

- 平台插件（原称系统插件）= `platform` profile 的 base 组合包，数量由组合包定义而不是决策（ADR-020 D20.7；S1 默认内容见 §04 4.1 读图要点与 §07 7.0.4）：全部有 `.qian-plugin/plugin.json`，全部过 TCK，各自按 seam 三角色（Definition / Provider / Consumer）拆包，每一个都能被 patch 替换、被租户 scope 遮蔽。
- 没有特权内核：运行时 `Qian.Runtime` 只剩上下文 `ctx`、服务注册、类型化事件、可逆副作用与装载器；原内核的注册表、生命周期控制器、能力目录本身也是插件（`qian.registry` / `qian.lifecycle` / `qian.capabilities`），每一项都能从配置替换；扩展 Qian 的方式是把插件挂到其他插件旁边，而不是修改内核（ADR-020 D20.1，取代 ADR-003 D3.1）。
- 清单只有一份（`plugin.json`）：代码部分的字段由源生成器从代码派生并合并进同一份、CI 校验一致；宿主不执行插件代码即可派生路由、权限点、菜单、配置命名空间、OpenAPI 聚合、MCP 工具描述。
- 文件即组件，无代码组件是常态：skill / agent / command / hook / MCP 服务器等声明式表面是 Markdown 与 JSON，Agent 可直接读写，S1 支持运行中重载；一个只含 `skills/` 的组件没有任何构建步骤；S2 设计器保存一张表单，产出的就是组件文件夹里的 `forms/<name>.json`（ADR-020 D20.6 / D20.9）。
- 一键安装与目录市场：`qian add <component>[@version]` 把声明式表面复制进项目、把代码部分加为 NuGet / npm 依赖、把配置条目写进当前 profile 的 patch，配套 `remove / list / doctor / dump-config / init --template <stack>`；目录市场是 git 仓库里的一份 `.qian/marketplace.json`，官方、租户私有、社区多市场并存；空白项目 `qian add identity authz files` 后 30 分钟跑通登录、权限检查与上传是 S1 退出标准之一（ADR-020 D20.8 / §6）。
- 平台级生命周期（制品 → 修订 → 激活，可回切）与「授权 ≠ 开关」在 S1 落地；租户级「开没开」由租户 scope 的 patch 表达，租户定制 = 带作用域的遮蔽注册（取代数据库开关，ADR-020 D20.5）；一个官方 `runtime.form: module` 插件能独立出修订、逐租户迁移、回切，是 S1 退出标准之一（ADR-018）。
- 信任等级（`policy.tier`）决定允许的 `runtime.form` 与 `policy.sandbox`：第三方代码永不进共享进程；S3 市场对第三方只开放声明式表面、`package` / `function` / 受限 `ui` 与为某个 seam 提供 Provider 的组件（原 connector，R28）。
- 平台自营的低代码能力（流程、报表、门户等）从 S2 起也以组件（组合包 + marketplace 条目）形式交付，走市场同一条流水线。
- 明确不做：为第三方代码与可 AOT 的边缘 profile 热插 DLL（R27）、进程共享脚本引擎、默认 sidecar、许可证受限的开源平台代码、第二套清单格式、按物理形态做顶层分类（ADR-002、ADR-020 D20.11）。

## 支柱 2：Agent 就绪的契约层（S1 定义，S4 启用）

**承诺。** 不为 Agent 另造一套 API、权限与审计——S1 为开发者写下的能力契约，就是 S4 Agent 读到的工具描述。

**措施。**

- 能力四类（命令 / 查询 / 事件 / 实体）注册进能力目录；每条带意图式描述、JSON Schema 2020-12、`sideEffect`、`policyHint`、`requiredScopes`、`schemaSha256`、错误码与示例（ADR-016）。
- 所有写操作走带 Schema 的命令：含副作用等级、幂等键、`dryRun`；无 Schema 的写入 CI 失败。
- 统一主体模型 `user / service / agent` 与 `act` 委托链字段在令牌中预留；S1 支持 `client_credentials`，S4 启用令牌交换。
- 权限判定 `check / batchCheck / listAllowedActions / explain` 可解释、可预检，同一 API 服务 UI、网关、MCP。
- 审计含主体链、能力 ID、契约版本、输入摘要、trace id、来源通道；`actor_type` 含 `agent`（ADR-009）。
- 元数据版本化、带 Schema 与语义校验器、可 diff 可回滚、记录 `generatedBy / promptHash`（ADR-011）；Agent 生成应用 = 生成 `package`，经校验 + TCK + 差异审阅 + 人工确认。
- Operation/Task 状态机对齐 MCP Tasks 与 A2A Task；审批待办与 Agent 补参同一模型。
- 以 MCP 2026-07-28 为目标版本：端点无状态、授权服务器对齐 RFC 8414 / 9728 / 8707、`iss` 校验、规划 CIMD；工具描述哈希钉死防篡改。
- 错误结构化、可被模型自我纠正；`inputSchema` 禁止租户字段；追踪与计量从 S1 贯通。
- 边界清晰：Qian 不写 Agent loop、沙箱、记忆、技能引擎；S4 经 MCP / HTTP 对接外部 Agent 运行时（ADR-016）。

## 支柱 3：开发者优先的 SDK / CLI / TCK（S1）

**承诺。** 一个 .NET 开发者从空仓库到部署好一个多租户应用，半天内完成；在空白项目里 `qian add identity authz files` 后 30 分钟内跑通登录、权限检查与上传，不部署 Qian 平台（ADR-020 §6）；不写一行租户过滤、鉴权判断或审计代码。

**措施。**

- SDK：源生成器从代码派生清单的代码部分字段与 Schema，合并进同一份 `plugin.json`；`MapCommand` 类端点模式自动获得租户注入、权限判定、审计、幂等、Outbox；组件库只依赖 `Qian.Runtime`（ADR-019 经 ADR-020 修订）。
- CLI（R33）：`qian init --template <stack>`（按技术栈脚手架）、`qian add <component>[@version]` / `remove` / `list`（一键安装与目录市场，等价 `npx qian@latest --skill x --agent y --plugin z --yes`）、`doctor`（健康检查与清单 / 契约 lint，含「inputSchema 不得含 tenant 字段」等规则）、`dump-config`（打印整棵插件树）、`dev`（本地依赖与宿主一键拉起）、`pack`（代码部分 OCI artifact）、`sign`、`publish`（推制品并更新 marketplace 条目）；原 `validate / install / provision / tck` 并入上述动词。
- TCK 三套（§14）：运行时语义（可逆副作用、scope 过滤、五种分发）、独立模式（`standalone` 宿主下 `qian add` 后可用）、组件一致性（多租户隔离、权限强制、审计完整、事件至少一次、修订回切、逐租户钩子幂等等用例）；官方插件与第三方插件同一套。
- 契约生成：OpenAPI 3.1、TypeScript 客户端（供 yue）、MCP 工具描述、权限点、默认角色模板由同一契约生成；前后端校验器共用。
- Kun 以 NuGet + CalVer 消费，公共 API 兼容承诺、弃用窗口与变更日志；S1 必修的 Kun 关键缺口（后台链路丢租户、至多一次、Outbox 无锁、网关不强制认证、gRPC 流式绕过鉴权、OIDC 客户端缺口）以集成测试实证（ADR-001）。
- 开发者租户与本地开发体验：一键起本地依赖、种子租户、模拟 IdP；S2 可选远程调试。
- 文档从契约生成：能力目录即 API 文档；安全默认值是强制装配而非 opt-in。

## 支柱 4：多租户与鉴权做成不可绕过的强制执行点（S1）

**承诺。** 租户与权限不是「开发者记得加」的东西，而是 `platform` profile base 组合包里的强制执行点（waterfall 监听器，profile 标记为不可移除、patch 动不了，ADR-020）；绕过它们在 Qian 里没有代码路径。

**措施。**

- 租户只来自已校验令牌，fail-closed；生产禁用请求头兜底与默认租户回退；消息消费、Saga、Outbox 发布、调度回调全部恢复租户上下文（ADR-004、ADR-001）。
- 共享库 + 行级过滤 + PostgreSQL RLS 双保险为默认；高隔离独立库作 S3 付费档；跨租户渗透测试 0 漏洞是 S1 退出准则。
- 平台唯一 issuer（OpenIddict）：Person + Membership、会话中心、后通道登出、refresh 轮换、`identity_revision`、TOTP、短信；外部 IdP 一律连接器插件（ADR-005）。
- 权限七层 L0–L6；权限点由清单声明；租户域 RBAC；数据权限编译为查询谓词在本地执行、RLS 兜底；字段权限读投影 / 写白名单；每次判定可解释并入审计（ADR-006）。
- 权限不进令牌；权限变更即时失效；鉴权唯一入口服务 UI / API / MCP；低代码数据引擎在唯一入口做「资源 + 动作 + 行级」三段式校验。
- 网关强制认证、路由级限流、密钥热轮换、Cookie 会话换 S2S、按 cluster 的 audience（ADR-010）。
- 审计一套：命令级哈希链落 PostgreSQL + 实体级同事务写入；实现 `IAuditReader`（ADR-009）。
- 审批兜底链不再兜底到全局管理员；超管旁路显式并审计；平台超管对租户不可见。

## 支柱 5：应用市场从 S1 的包格式起步（S1 定型，S3 开市）

**承诺。** 市场不是 S3 才开始的子系统；S1 末「一切皆包」跑通，之后几乎改不了的三件事——包格式与可定制性契约、升级与冲突处理、授权与权益 API——在 S1 做对。

**措施。**

- 应用 = 组合包 + marketplace 条目（ADR-014 经 ADR-020 修订，R28）：组合包携带 `package` 元数据、`ui`、可选 `function` / `service`、种子数据、迁移脚本、依赖清单与 id 重映射表，OCI 制品只用于代码部分；导出包不得缺菜单 / 权限 / 字典。
- 两种分发都支持：`template` 拷贝即拥有（同源导入合并 / 覆盖升级）、`managed` 托管锁定可升级（租户层叠加定制；三方比较；冲突清单三选一）；默认 managed。
- 组件与属性带可定制性标记（锁定 / 发布方可改 / 租户可改 / 受保护），由清单声明、平台强制。
- 升级策略由「SemVer + 是否新增权限」两个维度决定：无新权限可自动或推送，有新权限需租户管理员同意。
- 授权 v0（S1，手工授予）→ 权益 API（Plan / Entitlement / Seat / Meter / Trial）→ S3 中在线交易；授权是否买了与开关是否开分开存。
- 演进 M0–M4：S1 末组件格式 v0（组件文件夹 + marketplace 条目）+ `.qian/marketplace.json` 目录市场 + 代码制品库 + `qian add / remove` 安装 / 升级引擎 → S2 低代码应用可打包、自营应用吃狗粮 → S3 初商业市场叠在同一格式之上（目录 / 详情 / 安装向导 / 试用 / 升级中心 / 审核台，受邀 ISV、线下结算）→ S3 中开放 ISV 与在线交易 → S4 市场兼任私有 MCP registry。
- 审核分级：免费模板快审；带代码或付费的插件走安全审查；权限扩大触发复审；插件签名与反向权限审批。
- 市场购买的 managed 应用默认禁止导出；卸载后数据保留期与删除 SLA。

## 支柱 6：一套前端引擎贯穿后台与低代码（S1 起）

**承诺。** 后台 CRUD、低代码运行时、设计器预览、未来的 Agent 生成 UI 用的是同一套表单 / 表格 / 弹层引擎与同一份控件契约；不存在「两套并存」。

**措施。**

- yue 六层分层（tokens → core → shell / data / form → biz → runtime / widgets → designer），依赖方向用 lint 写死进 CI（ADR-013）。
- 只保留一套表单 / 表格 / 弹层引擎；业务组件只做叶子，不得新建引擎。
- 控件注册表：一次声明 `edit / readonly / cell` 三态，meta 含 props JSON Schema、`devices[]`、能力位；一份注册表同时作设计器左栏与 `GET /registry/widgets`。
- 一份元数据 + `device` 维度覆盖，不要两份；端支持矩阵显式降级。
- Module Federation 2 外壳 + 带版本号的插槽注册表；统一一个壳按「平台控制台 / 应用」上下文切换；不可信 UI 走 Worker 远程渲染或 iframe（S3）。
- 三层联动（声明式动作 → 表达式 DSL → 隔离沙箱）；不用 `eval`；令牌不进 URL / iframe。
- 令牌三层结构、明暗两态、对比度单测进 CI；`data-theme / data-brand` 由服务端按租户下发。
- Base UI 首迭代设验证门，不过则整体切 Radix、不混用。

## 修订历史

- 2026-10-06：草案 v1。
- 2026-10-06：组件模型 v2 回填（ADR-020）——支柱 1 合并「文件即组件、一键安装、无代码组件、一切皆插件」为差异化点并改「内核 / 系统插件 ×18」口径；支柱 3 CLI 动词按 R33、TCK 三套；支柱 4 强制执行点落位；支柱 5 应用 = 组合包 + marketplace 条目、商业市场叠加。
