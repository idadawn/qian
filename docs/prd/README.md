# Qian（乾）产品需求文档（PRD）— 索引

> 状态：**草案 v1（组件模型 v2 修订中）** · 更新日期：2026-10-06 · 决策真相源：`docs/adr/`（拍板前为 `docs/decisions/key-decisions-v0.md`，D1–D18 与补充裁决已于 2026-10-06 全部获认可；D19 库优先、D20 组件模型 v2 为同日方向修正新增，ADR-019 / ADR-020 Proposed，PRD 引用时标注） · 路线真相源：[`docs/ROADMAP.md`](../ROADMAP.md) · 研究基线：`docs/research/baseline-digest.md`

## 命名约定

| 项 | 约定 |
|---|---|
| 产品 | **Qian**（乾） |
| 底座框架 | **Kun**（自研 .NET 10 微服务框架，NuGet + CalVer 消费） |
| 前端组件库 | **yue**（React；包前缀 `@yue/`） |
| 插件 ID 前缀 | `qian.`（反向域名风格，发布后不可改；第三方以自有域名前缀） |
| 可部署单元 | `qian-<name>`（S1：`qian-gateway`、`qian-identity`、`qian-platform`、`qian-model-gateway-adapter`、`qian-shell`）；经 ADR-020：可部署单元 = profile（S1 交付 `platform` / `gateway` / `identity` / `standalone` / `minimal`）；`qian-<name>` ↔ profile / 组合包的定稿映射见下表（R31） |
| 运行时 / CLI | `Qian.Runtime`（.NET）/ `@qian/runtime`（TS）；`qian` CLI（`qian add <component>[@version]`）；清单 `.qian-plugin/plugin.json`；市场 `.qian/marketplace.json`（ADR-020，Proposed） |
| 阶段 | **S1** 技术底座 → **S2** 低代码构建 → **S3** 面向客户 → **S4** AI 原生 Agent 平台 |
| 优先级 | **P0** 当期退出标准关键路径 · **P1** 当期必做非关键路径 · **P2** 可选 / 可滑到下一阶段；功能项标题写成 `（P0 · S1）` |
| 决策引用 | `ADR-0xx`（001 Kun 关系、002 插件模型、003 内核边界、004 数据库与隔离、005 身份、006 鉴权、007 Apollo、008 事件、009 审计、010 网关、011 元数据与动态数据、012 流程、013 前端 yue、014 市场、015 模型网关、016 与兄弟系统边界、017 部署、018 阶段、019 库优先、020 组件模型 v2） |
| 章节引用 | `§07`、`§13.2` 等；专题 spec 引用 `specs/component-model.md`（权威）、`specs/plugin-manifest.md`（v1，迁移对照）、`specs/yue.md` |
| 参照来源 | 被借鉴的商业低代码产品一律称「参考平台」，全文不出现其品牌、公司、域名与源码路径 |
| 状态符号 | ✅ 已完成 / 可做 · 🚧 进行中 · ⬜ 未开始 · ❌ 不可做 · ⚠️ 有条件 · 🔒 保留与隔离 |
| 平台插件 | 规范术语（R30）：`policy.tier: system`、`platform` profile base 组合包成员；「系统插件」为别名 |

### 原可部署单元 ↔ v2 落位（R31，S1 默认）

| 原可部署单元 | v2 落位 |
|---|---|
| `qian-platform` | profile `platform` |
| `qian-gateway` | profile `gateway`（可 AOT） |
| `qian-identity` | profile `identity` |
| `qian-shell` | 不是 profile；yue 外壳为 `platform` profile 的 `shell` 组合包（静态前端制品，经 gateway 提供） |
| `qian-model-gateway-adapter` | `model-gateway` 组合包，默认挂在 `platform`，可选独立 profile `model-gateway`（S1 · P1） |
| S2 `qian-lowcode` | profile `lowcode` |
| S2 `qian-workflow` | profile `workflow` |
| S2 `qian-file-preview` | `file-preview` 组合包，挂在 `lowcode` profile |
| `standalone` | 消费者自己的宿主，装 `standalone` profile 的最小组合包 |
| `minimal` | 测试与 SDK 用的最小树 |

> profile 可以被 patch 合并或拆分，表中为 S1 默认（R31）。

## 相对上一版的主要变更

- 首版（草案 v1）：按决策清单 D1–D18 与补充裁决、七领域基线汇编、市场与 Agent 原生形态调研整理为 17 章 + 2 份专题 spec；全部描述目标态，Kun 当前缺口以带日期的「过渡态」块标注。
- 组件模型 v2 修订（ADR-020，Proposed；§02/§03/§07–§12、ROADMAP、specs 已回填，migration 待回填）：§02 新增原则 2.7a / 2.7b；§03 的 3.4 改写、新增 3.4a、3.11 补两组术语；§11 词表新增 v2 术语；§12 新增 C1–C7 与待确认；ROADMAP S1 交付更新。§07–§10 由另一人同时回填；`specs/component-model.md` 草案 v1，`specs/plugin-manifest.md` 降为 v1 迁移对照。

## 章节索引

| # | 章节 | 文件 | 关键概念 |
|---|---|---|---|
| 01 | 愿景与定位 | [01-vision.md](01-vision.md) | 一句话定位；与传统低代码 / Agent 平台的分化对比；为什么现在、为什么是插件思维、为什么先做开发者底座 |
| 02 | 设计原则 | [02-principles.md](02-principles.md) | 插件思维七条（内核最小集、系统能力也是插件、清单先于代码且从代码派生、信任等级决定形态、两层生命周期、一份契约多处生成、制品按 digest）+ 组件模型 v2 两条（2.7a 一切皆插件没有特权内核、2.7b 声明式优先）；Agent 就绪六条；安全硬线六条；工程原则三条；每条含铁律 |
| 03 | 核心概念模型 | [03-concepts.md](03-concepts.md) | 租户、自然人与成员、应用、插件 / 组件（文件夹 + 清单 + 声明式表面 + 可选代码；`runtime.form` × role × distribution × `policy.tier`）、3.4a 组件模型 v2 六概念（表面、作用域、能力 seam、profile 与组合包、patch、市场条目）、能力、元数据与版本、功能与发布、权限点与数据范围、事件、Operation/Task；易混术语精确定义（启用/停用 vs 上架/下架、授权 vs 开关、安装 vs 开通、template vs managed、撤回/撤销/催办、插件 vs 组件 vs 组合包、租户启用 vs 授权） |
| 04 | 形态与目标市场 | [04-market.md](04-market.md) | ASCII 架构总图（Traefik → qian.gateway → 内核与系统插件 / Higress → GPUStack；Apollo、PostgreSQL、Redis、RabbitMQ、对象存储；yue 外壳）；按阶段客群画像；共享 SaaS / 高隔离 / 私有化三档交付；目标数量级（待定 / 区间） |
| 05 | 核心差异化 | [05-differentiators.md](05-differentiators.md) | 六支柱：一切皆插件且吃自己的狗粮；Agent 就绪的契约层；开发者优先的 SDK / CLI / TCK；多租户与鉴权不可绕过；市场从 S1 包格式起步；一套前端引擎 |
| 06 | 用户与场景 | [06-users-scenarios.md](06-users-scenarios.md) | 七角色（平台运营、平台开发者、ISV 开发者、租户管理员、业务搭建者、终端用户、Agent）；四阶段 Demo（S1 开发者半天多租户应用、S2 搭建者表单与审批、S3 租户市场安装、S4 Agent 生成草稿经人审批） |
| 07 | S1 技术底座功能规格 | [07-s1-foundation.md](07-s1-foundation.md) | Kun 缺口修复；`qian.kernel` / `plugin-sdk` / `tenancy` / `identity` / `authz` / `data` / `files` / `config` / `gateway` / `events` / `audit` / `observability` / `notify` / `scheduler` / `realtime` / `entitlement` / `shell` / `model-gateway`；每项含设计理念、边界、技术落点、过渡态、插件化定位、Agent 契约、验收 |
| 08 | S2 低代码构建 | [08-s2-lowcode.md](08-s2-lowcode.md) | 元数据注册表与版本化、动态数据引擎、Schema Migrator、Field Type Catalog、控件注册表、表单 / 列表设计器与运行时、发布向导、审批内核最小集与节点树设计器、数据 / 字段权限编译、package 与 bundle、Operation/Task |
| 09 | S3 面向客户 | [09-s3-customer.md](09-s3-customer.md) | 私有目录型市场 → 开放 ISV；安装向导、试用、升级中心、审核台；权益（Plan / Seat / Meter / Trial）；租户自助（IdP、品牌、套餐、SCIM）；高隔离档；不可信 UI 沙箱；门户、报表、开放 API |
| 10 | S4 AI 原生 Agent 平台与模型服务网关 | [10-s4-agent.md](10-s4-agent.md) | 模型服务网关（GPUStack + Higress，本期唯一详细规划的 AI 范围）；每租户 MCP 门面；Agent 主体与委托链；元数据生成闭环；与外部 Agent 运行时的边界 |
| 11 | 体验原则与产品词表 | [11-ux-glossary.md](11-ux-glossary.md) | 渐进披露、一套表单/表格/弹层体验、无端差异的元数据、可解释的拒绝、可预览与可回滚、授权与开关分开可见、一个收件箱；产品词表（术语 / 定义 / 标识 / 状态；含 v2 术语：表面、作用域、遮蔽、restrict、seam、profile、组合包、patch、市场条目、`qian add`、`Qian.Runtime`） |
| 12 | 能力边界 | [12-boundaries.md](12-boundaries.md) | 按阶段「明确支持」清单（含组件模型 v2 边界 C1–C7）；安全底线（从后期提前到 S1）；明确不支持（长期承诺）；本期不规划的 AI 功能；待确认引用 ADR-020 §7 |
| 13 | 权限与安全 | [13-security.md](13-security.md) | 角色与主体模型；权限七层 L0–L6；令牌与会话；纵深防御分层；需二次确认场景；插件安全；密钥与配置；数据安全；审计；前端安全硬线；Agent 威胁与防线；DDL 示意；过渡态 |
| 14 | 质量与评估 | [14-quality.md](14-quality.md) | 测试金字塔与门禁；机器可执行用例格式；CI 门禁清单；阶段退出准则；「合并 ≠ 完成」验收纪律；缺陷与安全事件分级 |
| 15 | 演进路线（指针） | [15-roadmap.md](15-roadmap.md) | 阶段总览表；唯一真相源在 `docs/ROADMAP.md` |
| 16 | 成功指标 | [16-metrics.md](16-metrics.md) | 按阶段退出准则细化为可测项（E1–E4 系列：跨租户渗透 0 漏洞、多副本消费 0 丢失、两租户独立升级互不影响等）；按阶段 KPI（采用 / 效率 / 质量 / Agent 就绪） |
| 17 | 借鉴声明 | [17-references.md](17-references.md) | 开源项目 / 许可证 / 借鉴内容 / 态度（引入代码、只借鉴设计、运维侧使用、不采用）；只借鉴设计的开源低代码 / AI 平台；商业平台公开设计模式；贡献意向 |
| spec | 组件模型 v2（权威） | [specs/component-model.md](specs/component-model.md) | `.qian-plugin/plugin.json` 清单字段族、五种表面、scope / 遮蔽 / restrict、seam 三角色、profile / 组合包 / patch、`marketplace.json`、`qian` CLI、运行时语义与 TCK；草案 v1（另有人负责），落盘前以 ADR-020 为准 |
| spec | 插件清单（v1，迁移对照） | [specs/plugin-manifest.md](specs/plugin-manifest.md) | `qian-plugin.yaml` 字段草案、JSON Schema、十类派生物、签名与 digest、反向权限、`capabilities` 条目；经 ADR-020 降为 v1 字段迁移对照（`kind` → `runtime.form`），不再是权威规格 |
| spec | yue 组件库 | [specs/yue.md](specs/yue.md) | 本版关键决策 D1–D16；六层分层；控件注册表契约；MF2 外壳与插槽；选型与验证门；S1 最小集验收 |

## 文档体系：各管一事

| 层 | 位置 | 负责 | 明确不负责 |
|---|---|---|---|
| 产品规范 PRD | `docs/prd/`（本目录，17 章 + `specs/`） | 愿景、原则、概念模型、功能规格、边界、安全、质量、指标、借鉴声明 | 阶段 / 里程碑细节（指向 ROADMAP）、决策理由（指向 ADR）、实施状态 |
| 架构决策 ADR | `docs/adr/ADR-0xx-*.md` | Status / Context / Decision / Rationale / Consequences；被推翻标 Superseded，不删不重命名 | 不当状态报告；不复述规范 |
| 决策清单（过渡） | `docs/decisions/key-decisions-v0.md` | 拍板前把牵动整体的决策摆到一处；拍板后逐条转为 ADR | ADR 定稿后只作历史记录 |
| 阶段路线 | `docs/ROADMAP.md` | 阶段、退出标准、核心交付、里程碑占位、依赖与风险的唯一真相源 | 任务状态、功能细节 |
| 研究基线 | `docs/research/baseline-digest.md`（全文在仓库外） | 七领域调研的全貌、分级结论、难点与待拍板问题 | 不作为规范；只作依据 |
| 迁移方案 | `docs/migration/`（草案 v1） | 从参考平台功能集到 Qian 的映射、取舍与迁移路径 | 不复制 PRD 功能规格 |
| 专题 spec | `docs/prd/specs/*.md` | 单一能力的完整规格（范围 → 非目标 → 数据模型 → 流程 → 职责映射 → 选型 → 安全 → 验收 → 后续），顶部列 D1…Dn | — |

纪律：规范描述预期行为，与当前实现有差异时用带日期的过渡态块记录；非破坏性修订在稳定文件中更新正文与修订历史，不新增 v2 / v3 文件；章节间用 `§x.y`、`ADR-0xx` 互链而不复制内容；所有「已完成」判定附证据（命令、环境、结果）。

## 阅读顺序建议

- 产品负责人：01 → 04 → 05 → 06 → 15 → 16。
- 架构与开发：02 → 03 → 07 → 13 → 14 → specs/component-model.md（草案 v1）→ specs/yue.md；specs/plugin-manifest.md 仅作 v1 对照。
- 低代码与市场：03 → 08 → 09 → 11 → 12。
- 评审 Agent 就绪：02（第二组）→ 03.5 / 03.10 → 10 → 13.11 → 16（Agent 就绪 KPI）。

## 修订历史

- 2026-10-06：草案 v1 首版；框架章节（01–06、11、15–17）、功能规格章（07–10、12–14）、专题 spec（plugin-manifest、yue）全部落盘。
- 2026-10-06：一致性复核 R1–R20 落实（系统插件 18 个、`kind` 七值、微信网站扫码 S2 P1、规范 claim 集、权限点命名 `<plugin-id>:<resource>:<action>`、SSRF 基线两步、Operation/Task 契约预留等）；各章「待确认」中已裁决条目改写为「已裁决（R{n}）」。
- 2026-10-06：组件模型 v2 回填（ADR-020，Proposed）——索引更新为 20 份 ADR；specs 列表加 `component-model.md`（权威，草案 v1）并把 `plugin-manifest.md` 标为 v1 迁移对照；§02 / §03 / §11 / §12 与 ROADMAP 已回填，§07–§10 回填中。「系统插件 18 个」「`kind` 七值」口径经 ADR-020 修订，见各章说明。
- 2026-10-06：裁决 R27–R31 落实——R27 §12 N1 收窄（官方代码插件 `platform` 热装卸 S2 可选）；R28 connector = seam Provider 组件（`provides[]` + `policy.egress`）、bundle = 组合包、应用 = 组合包 + marketplace 条目；R29 接受 2.7a / 2.7b 编号；R30 规范术语「平台插件」；R31 原可部署单元 ↔ v2 落位映射表（本页命名约定与 ROADMAP S1）。
