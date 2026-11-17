> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# §10 S4 AI 原生 Agent 平台与模型服务网关

> 本页由构建脚本镜像自仓库 [`docs/prd/10-s4-agent.md`](https://github.com/idadawn/qian/blob/main/docs/prd/10-s4-agent.md)，请修改源文件，不要改本页。

> 本章粒度：S4 写到范围与退出标准；**模型服务网关（10.5）是本期唯一详细规划的 AI 范围**，按《GPUStack + Higress 模型服务整合方案》写细，并结合现网已有 Higress 设施的差距分析。决策引用写 `ADR-0xx`（ADR-015 模型网关、ADR-016 iClaw 边界、ADR-010 网关分层、ADR-007 配置中心、ADR-009 审计、ADR-018 阶段）。S1 底座见 §07，S2 低代码构建见 §08，S3 市场与计量见 §09。网关地址一律用占位 `gw.example.com`，不出现任何内部域名与密钥。决策清单主表的待拍板问题写 `Q1–Q18`，补充裁决（基线汇编编号）写 `补充裁决 Qxx`。
>
> 优先级口径同 §08：P0 = S4 退出标准关键路径；P1 = S4 必做非关键路径；P2 = S4 可选。10.5 另按整合方案的「第 1–4 阶段」标注，第 1–2 阶段为本期必做（接入范围先 LLM + Embedding，Q17）。

### 10.1 目标与边界

**组件模型 v2 口径（ADR-020 D20.9）**。Agent 表面从 S1 起即是一等公民：skills / agents / commands / hooks / MCP / tools 是组件文件夹里的声明式表面（§07 7.2.2），所有模型可见的东西注册在 `ctx.Tools` 等注册表里并受 scope 过滤。S4 增加的是三件事：`extensions`（模型在策略下挂载与卸载插件、运行时自修改，建立在 S1 同一套装载器上，S1 只开放给人类 CLI）、每租户 MCP 门面（由 `ctx.Tools` 的租户 scope 视图派生，不是另一套注册表）、Agent 生成组件（产物落为草稿组件文件夹，经与人工相同的校验器与 TCK）。本章凡写「能力目录」「工具」处，均指 S1 已有注册表的 scope 视图。

#### 10.1.1 目标（ADR-018）

S4 退出标准：**Agent 能发现、调用、编排乃至生成平台能力与应用**——

1. 发现：Agent 经每租户 MCP 门面列出该租户已启用插件的能力（按用户委托作用域过滤），拿到 schema、副作用、所需 scope、示例。
2. 调用：Agent 以委托某用户的身份调用一个写类能力（如新增记录、发起审批），审计记录「哪个 Agent 以谁的名义做了什么」，权限 = 委托人权限 ∩ 授予 scope。
3. 编排：Agent 按 Arazzo 描述完成一个跨三个能力的多步业务（查 → 建 → 发起审批），审批人工步骤以 Operation `input_required` 中断并恢复。
4. 生成：Agent 根据自然语言生成一个 `package` 应用包（数据模型 + 表单 + 列表 + 流程），经校验 + TCK + 差异审阅 + 人工确认后安装到租户。

#### 10.1.2 边界（ADR-016）

| 能力                                                                                                                      | 归属                                | 说明                                                                |
| ----------------------------------------------------------------------------------------------------------------------- | --------------------------------- | ----------------------------------------------------------------- |
| Agent 推理引擎、Sub-agent、Workflow 编排、路由器、记忆四层、Execution Trace、Skills 引擎、工具协议与门禁（ToolSpec / ToolPolicy / RiskGate / confirm） | **iClaw**                         | Qian 不写任何 Agent loop                                              |
| 沙箱                                                                                                                      | iClaw 已接的 CubeSandbox（E2B 兼容 API） | Qian 只做对接层 `qian.sandbox`                                         |
| 模型数据面                                                                                                                   | **Higress**（共用，10.5）              | Higress 对租户无感；租户/配额/计量在平台侧                                        |
| 身份/审计/Outbox 基座                                                                                                         | **Kun**（共用）                       | S2S claim 同形、事件信封同形                                               |
| 能力提供方 + 业务主数据权威（租户、组织、元数据、数据、流程）                                                                                        | **Qian**                          | 以 MCP + 事件暴露；租户主数据权威归 Qian（Q15）                                   |
| 插件机制、应用市场、私有 MCP registry                                                                                               | Qian                              | 注册表形状对齐 iClaw 的 Package / Release / Publication / Installation 四层 |

一句话：**Qian = 能力提供方 + 业务主数据权威；iClaw = Agent 运行时 + AI 资产控制面；Higress = 共用数据面；Kun = 共用基座。** 与 iClaw 的对齐项：S2S JWT claim 形状（`iss / aud[] / sub / tenant_id / project_id? / roles[] / scope / session_id / jti`，exp−iat ≤ 300s）、事件信封（`version / event_id / subject / occurred_at / tenant_id / payload`）、工具元数据（`sideEffect / policyHint / requiredScopes / requiredSecrets / schemaSha256 / contractVersion`）、契约格式（OpenAPI 3.1 + JSON Schema 2020-12，`additionalProperties: false`）、协议版本（MCP 2026-07-28，见 10.2）。

**AI 资产注册权威（R12）**。Qian 自身能力（插件、能力目录、由其派生的 MCP 工具与 resources、应用市场条目）以 Qian 注册表为真相源，向 Nacos AI Registry / iClaw Platform facade 的注册只是经 adapter 的投影，投影失败或滞后不改变 Qian 侧状态；iClaw 自有 AI 资产（智能体、提示词库、知识库、Skills、工具策略）的权威归 iClaw，Qian 只消费其只读视图，不复制、不改写（ADR-016 D16.8）。

#### 10.1.3 S4 新增单元与插件

| 单元 / 插件                                    | 形态                                                                                     | 职责                                                                                                                             | 级别      |
| ------------------------------------------ | -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ | ------- |
| `qian.mcp-facade`                          | service（可放非 AOT 独立服务，若 C# MCP SDK 过不了 Kun 的 AOT 门禁；`runtime.form: service`，自带 profile） | 每租户逻辑 MCP 门面：由 `ctx.Tools` 的租户 scope 视图派生工具 / resources（§07 7.2.1），经 Higress MCP 路由暴露                                          | P0      |
| `extensions`（`Qian.Runtime` 装载器的 Agent 入口） | 运行时能力                                                                                  | 模型在策略（`policyHint`、租户管理员批准）下挂载与卸载组件到 Agent 会话 scope（`agent-session:<id>`），会话结束逆序撤销；挂载到应用 scope 需应用管理员确认、租户 scope 需租户管理员确认（R36） | P1      |
| `qian.identity`（S4 增量）                     | module                                                                                 | Agent 客户端类型、RFC 8693 令牌交换与 `act` 链、CIMD、租户自助 Agent 授权管理页（P2）                                                                   | P0      |
| `qian.metadata`（S4 增量）                     | module                                                                                 | Agent 用的元数据生成闭环（人用版 S2 已有）：Agent 生成的组件落为草稿组件文件夹并经 §07 7.2.2 同一校验器、`generatedBy / promptHash / 审核人`、AI 生成内容标识                   | P0      |
| `qian.skills`                              | 打包能力                                                                                   | `skills/<name>/SKILL.md` 自 S1 起即是组件表面（§07 7.2.2，agentskills.io 格式）；S4 增加 Skills over MCP 分发与摘要清单                               | P1      |
| `qian.sandbox`                             | 对接层                                                                                    | 代码模式：契约生成的强类型 SDK 在 CubeSandbox 执行                                                                                             | P2      |
| `qian.model-gateway`（S4 完整）                | service adapter                                                                        | 10.5                                                                                                                           | P0      |
| `qian.operations`（S4 增量）                   | module                                                                                 | Tasks / MRTR 映射、A2A Task 中断态；急停与监督收件箱（P2）                                                                                      | P1 / P2 |
| `qian.shell`（S4 增量）                        | ui                                                                                     | `@yue/agent`、MCP Apps 视图（P2）、WebMCP（P2）                                                                                        | P2      |

### 10.2 发现 / 调用 / 编排 / 生成四条路径与每租户 MCP 门面（P0 · S4）

#### 范围与边界

**发现**

- ✅ 能力目录（S1 起）是发现入口：`list/search` 返回 schema、副作用、所需 scope、示例；结果按调用者权限过滤并带 `ttlMs / cacheScope: private`（MCP 2026-07-28 要求列表可缓存，按授权变化的列表须标 private）。
- ✅ **每租户一个逻辑 MCP 门面**：工具 = `ctx.Tools` 注册表在租户 scope 下的视图（已挂载组件的工具 ∩ 租户 scope `restrict` ∩ 用户委托作用域，§07 7.2.1）；元数据（实体 Schema、流程定义、控件目录）以 MCP resources 只读暴露；Higress 托管，客户端指向 `https://gw.example.com/mcp/<tenant-facade>`。
- ✅ **S4 启动前置（R14）**：Higress 升级到 ≥ 2.2.4 并开启 MCP 托管，列为 S3 末 / S4 启动前置项（10.5.8 第 4 阶段、10.5.10；ROADMAP S3 核心交付与 S3-M3）；未完成不进入 S4-M1。
- ✅ 不只暴露 CRUD：流程发起、审批动作、列表动作、数据接口都直接成为工具（「元数据资产 = 工具库」）；平台级同时提供 (a) 精选意图级工具 (b) 能力目录搜索工具 (c) 契约生成的强类型 SDK + 沙箱执行（代码模式，P2）；不做「每操作一个工具」的全量平铺。
- ✅ 能力随组件携带 `skills/`（S1 起即是声明式表面，§07 7.2.2），S4 经 Skills over MCP 分发（带摘要清单）。
- ✅ 市场兼任私有 MCP registry（`server.json` 兼容，§09）。

**调用**

- ✅ Agent 不直连 Qian 服务裸 URL，一律经 Higress MCP 路由（chokepoint、出网 deny-by-default、consumer 头计量）。
- ✅ 「知道 ID 不构成权限」：授权来自「发起用户的委托 + 本 run 的授权快照」；Qian RBAC 接受 S2S JWT 的 `sub + tenant_id + scope` 做用户态鉴权（10.3）。
- ✅ 令牌受众绑定（RFC 8707）、禁止令牌透传、最小 scope + 逐步提权（403，`WWW-Authenticate` 的 `error="insufficient_scope"`，响应体 `code = INSUFFICIENT_SCOPE`，R45）；网关按 `Mcp-Method / Mcp-Name` 头鉴权限流（配置方式需 PoC，敏感参数不得映射到头）。
- ✅ 写类工具必须幂等（`idempotencyKey`）：2026-07-28 删除了 SSE 断点续传，断流重发会重复执行。

**编排**

- ✅ 多步业务流程用 Arazzo 描述（`agent.workflows` 清单字段），供 Agent 按确定性步骤编排；已发布流程本身是一个意图级能力（「工作流即工具」，§08 8.14）。
- ✅ 长任务与审批 = Operation/Task 资源（§08 8.12），`input_required` 携带结构化输入请求，对齐 MCP Tasks（`tasks/get/update/cancel`）与 A2A Task 两个中断态。
- ❌ A2A 作为 Qian 内部编排协议（只作 S4 对外互通，签名 Agent Card，P2）。

**生成**

- ✅ Agent 生成应用 = 生成 `package` 插件包（10.4）；Agent 生成的代码只允许落在 `function`（Wasm 沙箱）——S4 不放松任何 S3 的安全边界。

**协议版本**

- ✅ 目标 **MCP 2026-07-28**：无状态、`server/discover`、MRTR 取代服务端反向请求、头部路由、列表可缓存、JSON Schema 2020-12、Tasks/Apps/Skills 为官方扩展；经 `server/discover` 兼容旧客户端。iClaw 客户端仍钉旧版，与 iClaw 制定协议对齐计划（ADR-016）。Higress 自 v2.2.4 起实现该版本并支持新旧双模式。
- ✅ 官方 C# MCP SDK 能否过 Kun `PublishAot /warnaserror` 门禁需实测；若不能，MCP 端点放在非 AOT 独立服务或交给 Higress 由契约生成。

#### 验收标准

- [ ] 租户 A 的用户 U 授权 Agent 后，Agent 经 `https://gw.example.com/mcp/<A>` 列出的工具恰为 U 有权限的能力；租户 B 的能力不可见；U 权限变更后 `ttlMs` 内列表刷新。**测法**：两租户 + 权限变更。
- [ ] 同一写类工具带相同 `idempotencyKey` 调用两次只执行一次。**测法**：接口测试。
- [ ] 旧版 MCP 客户端经 `server/discover` 协商后可完成一次工具调用。**测法**：双版本客户端。
- [ ] Arazzo 描述的「查员工 → 建请假单 → 发起审批」被 Agent 执行，审批节点以 Operation `input_required` 中断，审批人处理后 Agent 恢复并拿到结果。**测法**：端到端（S4 退出标准第 3 条）。

### 10.3 Agent 主体与委托链、授权快照、安全防线（P0 · S4）

#### 范围与边界

- ✅ **统一主体模型**（S1 设计、S4 启用）：`user / service / agent` 三类 actor；Agent 是客户端注册表中的一种客户端类型（有归属人、可吊销、可审计）；代表用户执行用 RFC 8693 令牌交换，令牌带 `act` 委托链；客户端注册优先 CIMD，DCR 兼容保留。
- ✅ **有效权限 = 委托人权限 ∩ 授予 Agent 的 scope**；权限点带风险级别与 step-up；危险操作 Agent 只能发起申请（「待确认票据」契约）；签名类动作 `humanOnly`；转审、协办、撤销对 Agent 禁止（补充裁决 Q26）。
- ✅ **运行授权快照**：工具 schema 哈希进入 `ToolGrant`，schema 变即授权失效；工具描述哈希钉死（§09）是其供给侧。
- ✅ **审计**：`actor_type=agent`、`on_behalf_of`、`capability_id`、`contract_version`、输入哈希、来源通道（UI / API / MCP / A2A），接入 ADR-009 哈希链；`trace_id` 与 iClaw Execution Trace 贯通。
- ✅ **授权服务器对齐 MCP**（S3 必做、S4 前验证）：RFC 8414 / OIDC 发现、PKCE、`resource` 参数、RFC 9728 受保护资源元数据、`iss` 回传。
- ✅ `Check / ListAllowedActions / Explain` 以工具形式暴露；结构化错误码与多语言文案导出为 JSON。
- ❌ Qian 侧实现 ToolPolicy / RiskGate 运行时权威（归 iClaw；Qian 只提供 `sideEffect / policyHint` 默认值）。

#### 安全防线表（确定性控制优先）

| 风险                   | 平台防线                                                                                                      | 阶段                  |
| -------------------- | --------------------------------------------------------------------------------------------------------- | ------------------- |
| 提示注入（直接/间接，含工具描述藏指令） | 外部内容与工具返回视为不可信；摄入不可信内容后对后续动作降权（Rule of Two）；高风险动作强制人工确认；不依赖检测器                                            | S4（S1 起副作用分级就位）     |
| 工具投毒 / rug pull      | 已批准工具「名称 + 描述 + 规范化 schema」哈希钉死，变更即下线复审；Skills 清单摘要校验                                                     | S3 必做               |
| 越权 / 混淆代理            | 令牌受众绑定 + 禁止透传；下游各跳重新验签；最小 scope + 逐步提权；按调用者权限过滤工具列表；句柄绑定主体；Agent 只拿委托人权限子集                                | S1（令牌与权限）/ S4（列表过滤） |
| 数据外泄                 | 沙箱与连接器出网白名单/出口代理（阻断私网与云元数据；平台自有 S1 见 §13.8，低代码与第三方出网点 S2 见 §08 8.14）；字段敏感度驱动脱敏；敏感参数不入 HTTP 头；对外发送类工具默认需确认 | S1 / S2 基线 / S4     |
| 破坏性操作 / 失控循环         | 副作用分级 + 默认策略（读放行、写确认、破坏性二次确认或禁止）；`dryRun`；软删除与回收站；元数据版本回滚；调用频率上限；急停开关（按 Agent/租户/工具，P2）                   | S1（分级）/ S4（急停）      |
| 成本失控                 | Higress token 限流与缓存（10.5）；租户/应用/Agent 预算与告警；工具列表确定性排序提高提示缓存命中；渐进式发现/代码模式                                  | S3 必做               |
| 供应链                  | 插件签名与哈希、命名空间验证、安装时展示完整权限与命令、沙箱默认最小权限、按 digest 引用                                                          | S1 必做               |
| 第三方插件代码              | 永不进共享进程；`service` 独立工作负载身份；每插件独立 PG schema 与数据库角色；事件主题 ACL；出口 NetworkPolicy；CSP 由清单生成；Wasm 燃料/内存/超时限额     | S1 规范 / S3 对第三方生效   |
| 合规                   | AI 生成内容显式/隐式标识；模型路由按数据驻留策略；审计留痕                                                                           | S4                  |

#### 验收标准

- [ ] Agent 以委托 U 的令牌调用 U 无权限的能力返回 403 `INSUFFICIENT_SCOPE`（头部 `error="insufficient_scope"`，R45）；调用 `humanOnly` 动作返回 `ACTION_HUMAN_ONLY`；审计条目含 `actor=agent, on_behalf_of=U, channel=MCP`。**测法**：S2S 令牌模拟。
- [ ] 已授权工具的 schema 变更后，旧 `ToolGrant` 失效，再次调用要求重新授权。**测法**：注册表接口。
- [ ] 带指令的工具返回内容不会触发后续破坏性动作（策略降权生效）。**测法**：注入用例集（与 iClaw 联测）。

### 10.4 Agent 生成应用 = 生成 package 的闭环（P0 · S4）

#### 范围与边界

- ✅ 产物形态：`template` 或 `managed` 的 `package` 组件（数据模型、表单、列表、流程、菜单、角色模板、种子数据），**生成格式 = 导入格式**（§08 8.1）；代码只能落 `function` Wasm。产物落为**草稿组件文件夹**（`.qian-plugin/plugin.json` + `forms/` `lists/` `flows/` `pages/` + 可选 `skills/`），经 §07 7.2.2 的同一校验器与 7.2.3 TCK，再进 marketplace 条目（ADR-020 D20.9）。
- ✅ `extensions`：模型在策略下经同一装载器把组件挂载到 Agent 会话 scope（`agent-session:<id>`），会话结束即逆序撤销；挂载到应用 scope 需应用管理员确认、租户 scope 需租户管理员确认（R36）；S1 的装载器不开放给模型。
- ✅ 闭环：Agent 提交 patch/包 → Schema 与语义校验（控件目录、Field Type Catalog、节点 Schema 全部是数据，Agent 只能产出目录内类型）→ TCK（清单、契约、幂等、越权探测）→ 预览（表单/列表渲染、流程节点树）→ 差异审阅（JSON Patch + 人读摘要）→ 人工确认 → 安装/发布 → 可回滚。校验是发布前的强制门禁。
- ✅ 元数据记录 `generatedBy / promptHash / 审核人`；按《人工智能生成合成内容标识办法》做显式/隐式标识。
- ✅ 工具最小集（§08 预留契约落地）：`listWidgets`、`listDataSources`、`generateFormFromTable`、`insertFields`、`validateForm / validateList`、`previewForm`、`publish(dryRun)`、`mountMenu`、`workflow:validate`、`bundles.install(dryRun)`；AI 建表/生成字段、AI 建流程（只生成「设计中」草稿，标出待补项）、OCR 控件库在此阶段落地，模型调用全部经 10.5。
- ✅ 市场可被 Agent 检索（机器可读包摘要）；Agent 只能提议安装/升级，付费与权限动作需人工批准；Agent 生成的应用上架走同一审核流水线与自动审核门禁（`generatedBy` 包的权限最小化检查）。
- ❌ Agent 直接写运行时数据库或绕过发布向导；Agent 生成的进程内代码（module/service）；Agent 绕过装载器直接改 profile / patch 文件或 `scope_patches` 表。

#### 验收标准

- [ ] 输入「做一个报销申请应用：报销单含明细子表、金额公式、部门主管审批、财务复核」，Agent 产出 package 通过校验与 TCK，预览可见，人工确认后安装到租户并可完整走一遍审批；元数据带 `generatedBy / promptHash`。**测法**：S4 退出标准第 4 条端到端。
- [ ] 产物中出现目录外控件类型或含 `module` 组件时，在校验步被拒且错误可被 Agent 据以自纠正（结构化、带允许值）。**测法**：坏样例。
- [ ] 安装到两个租户后，其中一个租户的 `tenantEditable` 修改在包升级后保留（复用 §08 退出标准第 2 条脚本）。**测法**：脚本复用。
- [ ] Agent 产出的草稿组件文件夹不过 §07 7.2.2 校验器时被拒并返回带文件路径与 JSON Pointer 的错误；通过后 `qian add` 到沙箱租户 scope 可用，会话结束后 `extensions` 挂载的组件被逆序撤销且 `dump-config` 无残留。**测法**：坏样例 + scope 断言。

### 10.5 模型服务网关（P0 · 本期唯一详细规划的 AI 范围）

> 依据：ADR-015（并入团队现有 Higress / MaaS 基础设施；GPUStack 作为 OpenAI 兼容上游 + Provider Adapter；不自写模型代理；不把 SSO 交给 Higress）、《GPUStack + Higress 模型服务整合方案》第 1–4 阶段、K 基线 §6.4 现网基线。接入范围先 **LLM + Embedding**（Q17），多模态按需。Qian 侧交付见 10.5.11。

#### 10.5.1 目标架构与四条约定

三层：**所有客户端只对接 Higress；Higress 后面挂 GPUStack（自建 GPU 推理）与云 API（OpenAI、Anthropic、OpenRouter 作 fallback）；GPUStack 只管 GPU 与模型。** 实线主路、虚线 fallback；Redis 与观测栈只服务于网关，客户端和后端都不直接碰它们。

四条约定，任何一条破了，换网关或换模型都会牵动客户端：

1. **客户端只用两种协议**：OpenAI（`/v1/chat/completions`、`/v1/embeddings`、`/v1/images/*`、`/v1/audio/*`）与 Anthropic（`/v1/messages`）；不出现任何供应商私有格式。
2. **客户端只用逻辑模型名**（`chat-default`、`chat-strong`、`chat-reasoning`、`vision-default`、`embed-default`、`image-gen-default`、`tts-default`、`asr-default`、`rerank-default`……），真实模型名只在 Higress 的模型映射里出现。
3. **一把 key 对应一个消费者**（一个平台或一个 Agent 工具），由 Higress 消费者签发；GPUStack 与云 API 的 key 只有 Higress 持有。
4. **Higress 是逻辑模型名的唯一真源**：映射、fallback、配额都只在这一层配，GPUStack 里不做任何路由。

与 ADR-010 的关系：`qian.gateway`（Kun.Gateway 加固）是业务 API 边界（外部 JWT → S2S），Higress 只管模型与 MCP 流量；两套网关职责从 S1 画清、不互相承担；Higress 对租户无感，租户/配额/计量在平台侧归集。

#### 10.5.2 组件清单与阶段

最小可上线集合 5 个组件：Higress Gateway、Higress Console、Redis、GPUStack Server、GPUStack Worker；其余按阶段加。

| 组件                          | 作用                             | 部署位置                | 阶段     | 现网状态（K §6.4）                      |
| --------------------------- | ------------------------------ | ------------------- | ------ | --------------------------------- |
| Higress Gateway（Envoy 数据面）  | 唯一对外入口：认证、路由、fallback、配额、限流、统计 | CPU 节点，2 副本起        | 第 1 阶段 | **已有** 2.2.3 两副本 + PDB            |
| Higress Console             | 配置供应商、路由、消费者、插件                | 同集群内网               | 第 1 阶段 | **已有**（内网）                        |
| Redis                       | ai-quota 配额、token 限流、语义缓存存储    | 同集群，主从/哨兵           | 第 1 阶段 | **已有** 网关专用 Redis HA              |
| GPUStack Server             | 模型目录、调度、API key、用量看板           | CPU 节点或 GPU 主节点     | 第 1 阶段 | **缺**（推理层现为其他方案，需切换；GPU 归属待定）     |
| GPUStack Worker             | 拉起 vLLM / SGLang / 自定义后端       | 每台 GPU 机器一个         | 第 1 阶段 | **缺**                             |
| Prometheus + Grafana（或日志服务） | AI 大盘、按消费者 Token 统计、计费数据源      | 同集群                 | 第 1 阶段 | **已有** 官方 AI 大盘 + Loki 90 天访问日志   |
| 价格表 + 账单脚本                  | ai-statistics 数据算金额，出周账单       | 一个定时任务（走 §07 调度）    | 第 2 阶段 | **缺**                             |
| 对象存储（MinIO / OSS）           | 多模态输入输出文件，预签名 URL              | 与 GPU 同机房           | 第 3 阶段 | 复用 `qian.files` 的 S3/OSS provider |
| 开发者门户（HiMarket，可选）          | 自助注册、订阅模型产品、领凭证、看用量            | 同集群                 | 第 3 阶段 | **缺**；平台超过 5 个或需自助领 key 时再评估      |
| 视频任务服务（自建）                  | 统一提交/查状态/取结果，适配各视频供应商          | 普通服务，注册为 Higress 路由 | 第 4 阶段 | **缺**；本期不做                        |

部署形态：沿用现有 K3s，Helm 装 Higress 并兼作模型流量 Ingress，GPUStack 用其 K8s 方式装 Worker。网络只开三条线：公网到 Higress 的 443；Higress 到 GPUStack API 端口；Higress 到公网（云供应商出口，含 OpenRouter 出网白名单放行）。GPUStack 端口不对外、也不对其他内网服务开放。

#### 10.5.3 Higress 配置方案

配置分六块，顺序即上线顺序：**供应商 → 路由 → 模型映射 → 消费者 → 配额限流 → 可观测**；MCP 托管放最后。全部配置导出进 git（GitOps，ADR-017），Higress 锁定小版本，升级前预发验证。

**（1）AI 服务提供者（4 个）**

| 供应商        | 插件 type | 地址                                    | 说明                                                                        |
| ---------- | ------- | ------------------------------------- | ------------------------------------------------------------------------- |
| gpustack   | openai  | `http://<gpustack-server>/v1`（集群内服务名） | 自定义 OpenAI 兼容地址，token 为 GPUStack 签发的唯一一把 `higress-gateway` key（secretRef） |
| openai     | openai  | 官方                                    | 配 2 个以上 token，插件随机轮换                                                      |
| anthropic  | claude  | 官方                                    | `/v1/messages` 原生转发                                                       |
| openrouter | openai  | 官方                                    | 自定义地址；模型名带厂商前缀；只作 fallback                                                |

每个供应商开启令牌降级策略：异常响应超阈值后暂停该 token，健康检查连续正常后恢复。

**（2）AI 路由（一条路由 = 一个逻辑模型）**

| 路由                        | 主目标              | fallback   | 允许的消费者 | 本期         |
| ------------------------- | ---------------- | ---------- | ------ | ---------- |
| chat-default              | gpustack         | openrouter | 全部平台   | 第 1 阶段必做   |
| chat-strong               | anthropic        | openrouter | 全部平台   | 第 1 阶段必做   |
| chat-reasoning            | openai           | openrouter | 按需授权   | 第 2 阶段     |
| embed-default             | gpustack         | 无          | 全部平台   | 第 1 阶段必做   |
| rerank-default            | gpustack（普通路由直转） | 无          | 按需授权   | 可选（Q17）    |
| vision-default            | gpustack         | openrouter | 全部平台   | 第 3 阶段     |
| image-gen-default         | openai           | 无          | 按需授权   | 第 3 阶段     |
| tts-default / asr-default | gpustack         | openai     | 按需授权   | 第 3 阶段（可选） |

模型匹配用精确匹配逻辑名；同一域名下所有路由共用 `/v1` 前缀，客户端只改 `model` 字段就切模型；沿用现网 `maas-*` 路由前缀约定。

**（3）模型映射**：每个供应商的 ai-proxy 里配 `modelMapping`，把逻辑名翻译成该供应商的真实模型名；fallback 到 OpenRouter 时映射到同款模型。真实模型名以后只在这里改，换模型不通知任何平台。示意（字段名以部署版本的插件文档为准）：

```yaml
provider:
  type: openai
  apiTokens: ["<secretRef:gpustack-higress-gateway-key>"]
  openaiCustomUrl: "http://<gpustack-server>/v1"
  modelMapping:
    chat-default: "<gpustack 中的部署名，如 qwen3-32b>"
    vision-default: "<如 qwen3-vl-32b>"
    embed-default: "<如 bge-m3>"
```

**（4）消费者（一平台一个，一 Agent 工具一个）**：用 key-auth 创建，例如 `platform-qian`（Qian 平台自身：AI 建表、公式建议等 S4 功能）、`platform-<其他内部平台>`、`agent-<iClaw 工具名>`、`billing-admin`（只用于配额管理接口）。key-auth 同时识别 `Authorization: Bearer` 与 `x-api-key`，OpenAI SDK 与 Anthropic SDK 用同一把 key。消费者授权在路由上勾选，勾了哪些路由就是该消费者的模型白名单。沿用现网 key-auth 双形态 consumer。

**（5）配额与限流（都要 Redis）**

- ai-quota：每个消费者一个固定 token 配额；`admin_consumer` 设为 `billing-admin`，经 `<路由>/quota/refresh`、`/quota/delta`、`/quota` 三个管理接口刷新、增减、查询；月初由账单脚本按预算换算后刷新。
- ai-token-ratelimit：按消费者限每分钟 token 数；Agent 工具的额度给平台的 3 到 5 倍（长工具循环、突发多）。
- key-rate-limit：普通 QPS 限流兜底防打爆。
- Redis 不可达时按放行处理并告警（可用性优先于配额精确）。

**（6）可观测**

- ai-statistics 全局开启，日志带消费者、路由（逻辑模型）、真实模型、输入/输出 token、首包时间、总耗时、是否 fallback（`x-higress-fallback-from`）。
- 指标推 Prometheus，导入 Higress AI 大盘：按消费者、模型看 Token 用量、QPS、成功率、首包响应、限流次数。
- 日志落 Loki（现网 MaaS 90 天）或日志服务，按请求 ID 去重，是账单的原始数据。

**（7）MCP 托管（整合方案第 4 阶段；Qian S4 的 10.2 依赖它）**：MCP Server 在 Higress 上注册后走同一套消费者和 key；Agent 的 MCP 客户端直接指向 `https://gw.example.com/mcp/<name>`；现有 REST API 可用 openapi-to-mcp 转工具，但 Qian 的工具一律由能力目录派生（不手写）。Higress v2.2.4 起支持 MCP 2026-07-28 双模式；按 `Mcp-Method / Mcp-Name` 头路由限流的配置方式需 PoC。

#### 10.5.4 GPUStack 配置与网络隔离

GPUStack 只做三件事：**部署模型、给 Higress 一把 key、把用量看板留给自己排障**；所有路由、配额、别名都不在这里配。

- **首批模型**（按显存从小到大上；Q17 先 LLM + Embedding）：`chat-default`（单卡 80 GB 可跑的中文与工具调用均衡开源模型，vLLM，先上验证整条链路）；`embed-default / rerank-default`（显存小、可同卡）；第二阶段视 GPU 归属补 `chat-fast`（高并发廉价任务）；`vision-default`、`asr/tts`、图片/视频生成为第 3–4 阶段按需。GPUStack 里的**部署名就是 Higress `modelMapping` 右边的真实名**，两边一致避免多一层翻译。
- **API key**：GPUStack 只签发一把 key，命名 `higress-gateway`，以 secretRef 填进 Higress 的 gpustack 供应商；不给任何平台或个人发 GPUStack 的 key；调试临时 key 用完即删。GPUStack 自带 Usage 页按这把 key 统计 = 网关总量，用于对账。
- **网络隔离**：GPUStack Server API 端口只允许 Higress 网关地址段访问（K8s NetworkPolicy）；控制台走内网/VPN；Worker 与 Server 间端口只在 GPU 网段开放；GPUStack 不访问公网，模型文件经内网镜像仓库或预下载到共享存储。
- **对接检查**：`GET /v1/models` 用 `higress-gateway` key 能列出全部部署；流式请求最后一个 chunk 带 `usage`（Higress 统计依赖它）；一台 Worker 停掉后 GPUStack 在其他 Worker 重新拉起模型，Higress 路由不需要改。

#### 10.5.5 计费与用量核算

Higress 记用量不算钱，金额化计费由**一张价格表 + 一个账单脚本**补上，配额控制仍交给 ai-quota。分工：**Higress 统计 → 脚本算钱 → 脚本反过来刷新 Higress 的 token 配额**。

- **数据源**：ai-statistics 每条记录至少含消费者、路由、真实模型、输入 token、输出 token、是否 fallback、首包时间、总耗时；按天聚合为「日期 × 消费者 × 真实模型 → 输入 token、输出 token、请求数」。
- **价格表（git 维护）**：单位元 / 百万 token，按次计费用 `per_call`；自建模型单价按 GPU 成本摊（月 GPU 费 ÷ 月总 token，季度校一次）；云 API 按官网价；OpenRouter 用其响应里的实际成本 × 系数（如 1.1）。示意：

```yaml
<自建模型部署名>:   { input: 0.5,  output: 1.5 }
<云端强模型名>:     { input: 21,   output: 105, cache_read: 2.1 }
<图片生成模型名>:   { per_call: 0.3 }
openrouter/*:        { markup: 1.1 }
```

- **账单脚本**（每周跑，月初结账；以 §07 调度器任务承载）：拉取上一周期聚合表 → 金额 = Σ（输入 token × 输入单价 + 输出 token × 输出单价）+ Σ（按次请求数 × 单价）→ 按消费者出账单（附 top 模型与 fallback 占比）→ 月初按预算换算 token 数调用 `/quota/refresh`。
- **配额换算取舍**：ai-quota 按 token 不按金额。先用简单做法（按该消费者上月加权平均单价换算，误差 10–20%，对内部结算够用）上线；有超支争议再切精确做法（脚本每小时算实际金额，超预算把配额刷成 0，相当于金额停机）。
- **对账**：每月拿 GPUStack Usage 总 token 与 Higress 按 gpustack 供应商汇总的 token 对一次，差异 > 2% 说明有请求绕过网关或丢了流式 usage。
- **与 Qian 计量的衔接**（§09 9.12）：Higress 消费者 = 平台级粒度；租户/项目级归集在平台侧——Qian 的模型调用经 `platform-qian` 消费者，由 `qian.model-gateway` 在请求侧打 `tenant_id / project_id?` 标签（请求头或自有日志）并把用量写入 Qian 计量事件；Usage Ledger 是否与 iClaw 共用待拍板（Q15）。

#### 10.5.6 多模态接入路径（第 3–4 阶段，按需）

图片与语音走 OpenAI 标准端点直通，Higress 原生够用；视频没有标准端点，需自建任务服务（本期不做）。文件一律走对象存储 URL，不让 base64 穿过网关。

| 模态             | 客户端端点                                         | Higress 处理              | 后端                             | 阶段                      |
| -------------- | --------------------------------------------- | ----------------------- | ------------------------------ | ----------------------- |
| 图片理解           | `/v1/chat/completions` 带 `image_url`          | ai-proxy 正常转发           | GPUStack 视觉模型、OpenAI、Anthropic | 第 1 阶段即可（随 vision 模型上线） |
| 图片生成           | `/v1/images/generations`                      | capabilities 直通原生兼容的供应商 | OpenAI；后续 GPUStack 自定义后端       | 第 3 阶段                  |
| 语音合成 / 识别      | `/v1/audio/speech`、`/v1/audio/transcriptions` | 同上                      | GPUStack 自定义后端、OpenAI          | 第 3 阶段（可选）              |
| 实时语音           | `/v1/realtime` WebSocket                      | 普通路由直通，不经 ai-proxy      | OpenAI                         | 按需                      |
| 视频 / 3D / 音乐生成 | `/v1/tasks`（自建）                               | 普通路由 + 消费者认证 → 任务服务     | 各云供应商；后续自建                     | 第 4 阶段，本期不做             |

验证点：capabilities 直通是按供应商声明的——OpenAI 与 GPUStack 原生兼容这些路径，OpenRouter 与 Anthropic 不提供；每条路由上线前用真实文件跑一遍，确认响应体与 `usage` 完整；图片生成按次计费记请求数。文件流转：输入先传对象存储、请求只传 URL；输出写对象存储返回带过期时间（默认 7 天）的预签名 URL。内容审核：文本用 Higress 的内容安全插件在网关层拦截；图片/视频审核放在生成返回路径。

#### 10.5.7 客户端接入模板要点

客户端只需要两样东西：网关地址与消费者 key。

- **OpenAI SDK**：`base_url="https://gw.example.com/v1"`，`model="chat-default"`，流式请求**必带** `stream_options={"include_usage": True}`，否则 token 统计缺最后一段。
- **Anthropic SDK**：`base_url="https://gw.example.com"`（不带 `/v1`，SDK 自己拼），`model="chat-strong"`；`cache_control` 原样到达 Anthropic，prompt caching 生效。
- **Agent 运行时（iClaw 及其他）**：按 API 形态拆两个 provider、同一把 key：`openai-completions` 形态 baseUrl 带 `/v1`，`anthropic-messages` 形态不带；每个逻辑模型声明 `reasoning / input 模态 / contextWindow / maxTokens`。
- **Qian 平台自身**（`qian.model-gateway` 的 .NET 客户端）：只用逻辑模型名；用途槽位（如 `form-generation → chat-default`、`embedding → embed-default`）在平台配置 Schema 中声明，不在代码写死。
- **发 key 时一并交付**：网关地址、key、被授权的逻辑模型列表、每分钟 token 上限与月配额、`/v1/models` 可自助列出可用模型、出问题联系谁（日志按消费者名检索）。
- 已知坑：个别 Agent 运行时的 custom provider 指向网关后会先探测 Ollama 风格端点，404 后曾静默挂起——接入时验证第一条请求 30 秒内返回。

#### 10.5.8 实施阶段与放行标准

| 阶段                     | 内容                                                                                                                                                                                                                                                           | 放行标准                               | 本期                                                     |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------- | ------------------------------------------------------ |
| 第 1 阶段：文本链路打通（约 1 周）   | 装/复用 Higress + Redis；GPUStack Server + 1 台 Worker；部署 chat-default 与 embed-default；签发 `higress-gateway` key；配 4 个供应商、chat-default / chat-strong / embed-default 三条路由与映射；1 个测试消费者，OpenAI 与 Anthropic SDK 各跑通一次流式；停掉 GPUStack 确认 fallback；AI 大盘可见消费者与模型维度 token | 连续 24 小时压测无 5xx；fallback 切换客户端无感知  | **必做**                                                 |
| 第 2 阶段：给平台发 key（约 1 周） | 每平台一个消费者并按需勾选路由；ai-quota 与 ai-token-ratelimit 上线，`billing-admin` 能刷新配额；价格表进 git，账单脚本出第一份周账单；接入 Qian（`platform-qian`）与一个 Agent 工具试点；日志按消费者可检索单次请求                                                                                                             | 第一份账单与 GPUStack Usage 对得上，差异 \< 2% | **必做**                                                 |
| 第 3 阶段：多模态与门户（约 2 周）   | 对象存储预签名流程；图片生成、TTS、ASR 直通验证（OpenAI 先行，GPUStack 后端跟上）；vision-default 上线；评估开发者门户                                                                                                                                                                               | 三种多模态请求用量出现在账单，图片按次计费正确            | 可选（Q17）                                                |
| 第 4 阶段：视频、MCP、审核（按需）   | 视频任务服务；第一批 MCP Server 在 Higress 托管并被 Agent 接入；文本审核插件；评估自建视频生成                                                                                                                                                                                                | Agent 工具通过同一把 key 同时拿到模型和工具        | MCP 托管（含 Higress ≥ 2.2.4）为 S4 启动前置，排在 S3 末（R14）；视频本期不做 |

#### 10.5.9 风险与必验清单

最大风险不在功能，在**统计口径**：流式请求丢了 usage，账单和配额就全错。

**上线前必验**

- [ ] SSE 流式：网关不缓冲，首 token 延迟比直连 GPUStack 增加 ≤ 20 ms。
- [ ] 流式 usage：OpenAI 格式带 `stream_options.include_usage`，Claude 格式 `message_delta` 里有 usage，两种都进 ai-statistics。
- [ ] `/v1/messages` 透传：`cache_control`、`thinking`、`tool_use / tool_result`、图片块原样到达，响应 `cache_read_input_tokens` 不为 0。
- [ ] 路由超时：chat 类 600 秒，图片 300 秒，任务提交接口 30 秒。
- [ ] 请求体上限：默认值下带 3 张 URL 图片正常；base64 大图被拒并返回明确错误。
- [ ] Realtime：WebSocket 升级经 Higress 成功，10 分钟长连接不断。
- [ ] fallback：GPUStack 5xx 或超时后切 OpenRouter，`x-higress-fallback-from` 出现在日志。
- [ ] 配额：消费者 token 用尽返回 429，`/quota/refresh` 后立即恢复。
- [ ] Agent 运行时两种 provider 形态各跑一次模型状态检查。

**风险与对策**

| 风险                             | 影响               | 对策                                              |
| ------------------------------ | ---------------- | ----------------------------------------------- |
| Redis 单点                       | 配额与限流失效，请求放行或全拒  | 复用现网网关专用 Redis HA；插件配置 Redis 不可达时按放行处理并告警       |
| ai-quota 按 token 不按金额          | 高价模型用得多的平台超预算    | 账单脚本每小时算金额，超预算刷配额为 0                            |
| OpenRouter 同一模型换底层供应商          | 输出风格与价格波动        | 只作 fallback 不作主路；需固定时在请求里指定 provider            |
| 出口网络                           | 国内部署时云供应商不可达     | 网关放可直连节点，或给供应商配出口代理（出网白名单）                      |
| GPUStack key 泄漏                | 绕过网关直接调用         | 只有网关持有；端口网段限制；每月对账发现绕行                          |
| Higress 版本升级改插件字段              | 路由或映射失效          | 锁定小版本，配置导出进 git，升级前预发验证                         |
| 图片/语音 capabilities 直通对部分供应商不生效 | 路由 404 或格式错      | 逐供应商验证，不生效的先只挂 OpenAI                           |
| GPU 归属未定（首节点两块卡已借出一块）          | 第 1 阶段无法起 Worker | 拍板 GPU 归属（Q17）；过渡期 chat-default 主路暂指 OpenRouter |
| 开发者门户较新                        | 功能变动快            | 第 3 阶段再评估，先用控制台手工发 key                          |

**明确放弃**：充值与兑换码、渠道模型一键拉取、价格倍率同步、Gemini 原生入口、第三方绘图/音乐代理、渠道余额查询。若将来需要，在 Higress 后面再加一层专门运营层，客户端不改。

#### 10.5.10 与现网已有 Higress 设施的差距与复用（K 基线 §6.4）

| 项                                                | 现网            | 处理                                                                       |
| ------------------------------------------------ | ------------- | ------------------------------------------------------------------------ |
| Higress 2.2.3 两副本 + PDB、Console 内网               | 已有            | **复用**；升级到 ≥ 2.2.4 以获得 MCP 2026-07-28 双模式并开启 MCP 托管（S4 启动前置，排在 S3 末，R14） |
| `maas-*` 路由前缀、key-auth 双形态 consumer              | 已有            | **复用**命名与消费者形态；逻辑模型名路由按 10.5.3 补齐                                        |
| 网关专用 Redis HA                                    | 已有            | **复用**为 ai-quota / ai-token-ratelimit 存储                                 |
| MaaS Loki 90 天访问日志（按请求 ID 去重）、官方 AI 大盘           | 已有            | **复用**为账单原始数据与可观测                                                        |
| McpBridge 上游注册                                   | 已有            | **复用**为 GPUStack 上游与 S4 MCP Server 注册方式                                  |
| ai-proxy 供应商与逻辑模型名映射入 git                        | 缺             | 第 1 阶段补齐                                                                 |
| ai-quota / ai-token-ratelimit / ai-statistics 绑定 | 缺             | 第 2 阶段补齐                                                                 |
| 价格表与账单脚本                                         | 缺             | 第 2 阶段补齐（走 §07 调度）                                                       |
| 推理层：现为其他推理方案                                     | 需切换到 GPUStack | 第 1 阶段；GPU 归属待确认；仓外是否已有 GPUStack 控制面待确认                                  |
| OpenRouter 出网白名单放行                               | 缺             | 第 1 阶段随出口策略补齐                                                            |
| SSO / S2S 交给 Higress                             | 不做            | ADR-015：S1 不把 SSO/S2S 交给 Higress；Agent 鉴权在 S4 走 MCP 授权（10.2/10.3）        |

#### 10.5.11 Qian 侧交付（`qian.model-gateway`）

Qian **只做**以下四件事，不另写模型代理层：

1. **GPUStack 作为 OpenAI 兼容上游注册进 Higress**（McpBridge / 供应商配置经 GitOps 落地）。
2. **Provider Adapter**（`model-gateway` 组合包，`runtime.form: service`，默认挂在 `platform` profile，可选独立 profile `model-gateway`（镜像 `qian-model-gateway-adapter`），S1 已预留，R31）：发现 GPUStack 模型部署、健康探测、生命周期（上线/下线/替换）同步到 Higress 映射的变更提案（人工批准后入 git），不直接改数据面。
3. **填写 `model-capabilities` 契约**（对齐 iClaw）：模型目录含逻辑名、类型（chat / embedding / rerank / vision / tts / asr）、能力标签（工具调用、视觉、推理、缓存）、上下文长度、最大输出、配额、可用租户；用途槽位（`form-generation`、`flow-generation`、`embedding`、`ocr` 等）→ 逻辑名的映射放平台配置 Schema。
4. **租户/配额/计量在平台侧**：Qian 的所有模型调用经 `platform-qian` 消费者；`qian.model-gateway` 客户端为每次调用打 `tenant_id / project_id?` 并写计量事件（§09 9.12）；租户级配额与预算告警在平台执行，Higress 对租户无感；密钥全部 secretRef（ADR-007）。

**验收标准**

- [ ] 以 `platform-qian` 消费者用 OpenAI SDK 与 Anthropic SDK 各跑通流式调用，`usage` 进入 ai-statistics；账单脚本输出与 AI 大盘一致。**测法**：第 1–2 阶段放行脚本。
- [ ] `GET /model-gateway/catalog` 返回按 `model-capabilities` 契约的目录，租户 A 只看到其可用模型；用途槽位 `form-generation` 改指向另一逻辑名后，S4 的 AI 建表调用随之切换且无需改代码。**测法**：配置切换 + 接口。
- [ ] 租户 A 当月模型用量 ≥ 预算时平台返回 `MODEL_BUDGET_EXCEEDED` 并发告警，租户 B 不受影响；Higress 消费者配额未触发。**测法**：注入用量。
- [ ] 任意平台代码、配置、日志中不出现 GPUStack 或云供应商 key 明文；GPUStack 端口对网关之外的 Pod 不可达。**测法**：密钥扫描 + NetworkPolicy 探测。
- [ ] 月度对账 GPUStack Usage 与 Higress 汇总差异 \< 2%。**测法**：对账脚本。

### 10.6 S4 范围外

| 不做                                           | 理由                           | 替代                                                |
| -------------------------------------------- | ---------------------------- | ------------------------------------------------- |
| 自建 Agent 运行时（推理引擎、Sub-agent、Workflow 编排、路由器） | ADR-016：复用 iClaw             | S4 经 MCP/HTTP 对接 iClaw                            |
| 自建沙箱、记忆、技能引擎、工具门禁运行时                         | 同上                           | CubeSandbox、iClaw 记忆四层、Skills、ToolPolicy/RiskGate |
| 自写模型代理层（绕过 Higress）                          | ADR-015                      | Higress 是唯一模型与 MCP chokepoint                     |
| 把 SSO / S2S 交给 Higress                       | ADR-015 / ADR-010            | 业务鉴权在 `qian.gateway` 与 `qian.identity`            |
| 视频/3D/音乐生成任务服务、HiMarket 开发者门户                | 本期范围外（Q17 先 LLM + Embedding） | 第 3–4 阶段按需                                        |
| 智能体管理、提示词库、知识库、MCP 工具目录 UI（参考平台 AI 中心形态）     | 归 iClaw 的 AI 资产控制面           | Qian 的能力注册表是其唯一来源                                 |
| 「每插件一个 MCP Server、每操作一个工具」全量平铺               | 工具列表膨胀、提示缓存命中差               | 意图级工具 + 目录搜索 + 代码模式                               |
| A2A 作为内部编排协议、WebMCP、MCP Apps 视图作为必做          | 协议工具链仍预览级                    | P2，待浏览器与宿主支持稳定                                    |
| 自研 OCR 识别服务                                  | 视觉模型经模型网关即可                  | OCR 控件库经 10.5 vision 路由                           |
| 以 OTel GenAI 语义约定属性名作持久化 schema              | 约定仍为 Development 级           | 计量与审计用 Qian 自有 schema                             |

### 10.7 待确认

1. **GPU 归属与接入范围**（Q17 默认先 LLM + Embedding，GPU 归属由产品负责人定）：决定第 1 阶段能否起 GPUStack Worker；过渡方案是 chat-default 主路暂指 OpenRouter。Rerank 是否随 Embedding 一并接入（成本极低）待确认。
2. **Usage Ledger / 租户 API Key 是否与 iClaw 共用**（Q15）：决定 10.5.5 的租户归集落在 Qian 自有 Ledger 还是 iClaw；本章按 Qian 自有写。
3. **Provider Adapter 由谁维护**（D15 待拍板）：Qian 团队还是 MaaS 基础设施团队；影响 `qian-model-gateway-adapter` 的发布节奏。
4. **MCP 托管的阶段对齐**：已裁决（R14）——Higress ≥ 2.2.4 升级与开启 MCP 托管列为 S4 启动前置，放在 S3 末（ROADMAP S3 核心交付「S4 启动前置」行与 S3-M3）。
5. **按 `Mcp-Method / Mcp-Name` 头路由限流的配置方式**与 **C# MCP SDK 能否过 Kun AOT 门禁**需 PoC；结论决定 `qian.mcp-facade` 的部署形态。
6. **iClaw 协议对齐计划**（其 MCP 客户端钉旧版且只有 stdio）：谁牵头、何时切到 2026-07-28；否则 S4 发现/调用路径只能靠 `server/discover` 兼容。
7. **是否与 iClaw 共用同一 Higress 实例**（Q16 默认共用集群、独立节点池）：影响消费者命名空间与配额隔离。
8. **MaaS 现网推理层切换到 GPUStack 的窗口**与「仓外是否已有 GPUStack 控制面」需与基础设施团队确认。
9. **组件模型 v2 对本章的影响（ADR-020）**：(a) 已裁决（R36）——`extensions` 的自动挂载边界：Agent 会话 scope 自动、应用 scope 需应用管理员确认、租户 scope 需租户管理员确认；Agent 会话 scope 的生命周期与审计字段随 specs/component-model.md 定稿；(b) 草稿组件文件夹的存放：沿 R36 租户私有市场默认平台托管（Agent 产物上传为组件文件夹，平台生成私有 marketplace 条目），进入官方市场前的审核流水线归 §09 9.1 审核台；(c) 已拍板（ADR-020 §7 第 1 条）：TS 侧公共 API 为 `@qian/runtime`、S1 内部以固定版本 vendor 的 Cordis 作为实现，`@yue/agent` 与 MCP Apps 视图作为 `@qian/runtime` 插件树成员交付，不依赖实现选择；(d) 每租户 MCP 门面派生自 `ctx.Tools` scope 视图后，10.3 的 `ToolGrant` 哈希与 §07 `schemaSha256` 需同一计算口径（与 §09 9.14 第 8 条合并处理）。
