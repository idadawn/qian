# 文档站信息架构与规范（Rspress 2.0）

> 状态：已确认（产品负责人 2026-10-07；Kun 进站与产品族页同日经 ADR-021 补充） · 决策引用：ADR-013 D13.18、ADR-020 D20.9、ADR-015、ADR-016、ADR-021 · 实施任务：S1-DOCS-001 / 002 / 003 / 005 / 006、S2-DOCS-001
> 本文回答三件事：文档站分几部分、每部分给谁看从哪来、页面与元数据长什么样。站点源码在仓库 `website/`，文档内容的真相源仍是各组件文件夹与 `docs/`。

## 1 读者与目标

| 读者 | 要解决的问题 | 主要去的部分 |
|---|---|---|
| 新开发者 | 30 分钟内在空白项目里用上 Qian 的组件 | 1 开始、2 组件目录 |
| 用组件的开发者 | 某个组件怎么装、怎么配、暴露了什么 | 2 组件目录、4 CLI 与规格 |
| 写组件的开发者 | 编程模型、API、怎么让 Agent 用上我的组件 | 3 SDK、4 CLI 与规格 |
| 用 Kun 写服务的开发者 | Kun 模块怎么用、Qian 锁定的是哪个 Kun 版本、哪些 Kun API 是 Qian 认可的 canonical 实现 | 3 SDK 的「Kun 框架」小节、1 开始的版本矩阵 |
| 部署与运营 | profile 怎么上 K3s、怎么升级回切、模型网关怎么接 | 5 运维 |
| 第三方 / ISV | 怎么上架、信任等级与签名 | 6 市场与 ISV |
| Agent 与知识库 | 机器可读的全文与元数据 | 全站的 `.md` 产物与 `kb-index.json` |
| 决策者与贡献者 | 为什么这么设计 | 7 设计与路线 |

## 2 七个部分

| # | 部分 | 路径 | 内容 | 来源 | 版本化 |
|---|---|---|---|---|---|
| 1 | 开始 | `/start/` | 30 分钟跑通 `qian add identity authz files`；核心概念五页：组件、表面、scope、seam、profile / patch；产品族地图 `/start/family`（Kun / Qian / yue 与周边仓库的层次与关系）与版本矩阵 `/start/versions`（§2b.4） | 手写 MDX（版本矩阵生成） | 否 |
| 2 | 组件目录 | `/components/` | 每个组件一页（模板见 §3）：yue UI 组件带活演示与 props 表；平台插件（身份、鉴权、文件、配置、事件…）同一模板 | 从各组件文件夹自动收集（S1-DOCS-002），**生成目录勿手改** | 是，随包版本 |
| 3 | SDK | `/sdk/` | **Kun 框架**（第一节 `/sdk/kun/`，排在本部分概览页之后（已裁决 R57）：底座框架的概览、模块地图、上手、指南、安全、Kun 的 ADR、变更日志与 API，按锁定版本从 kun 仓库镜像，另有唯一手写页「与 Qian 的关系」，规则见 §2b）、后端 SDK（.NET：`Qian.Runtime`、`Qian.Plugin.Sdk`、`Qian.*`）、前端 SDK（`@qian/runtime`、`@qian/plugin-sdk`、`@yue/sdk`、MF2 远程模块）、AI SDK（对外统一名称，见 2a.2：在插件与前端里调模型 / Embedding 与能力契约、配额；写 skills / agents / commands / hooks / tools 表面、工具副作用与策略、能力目录、MCP 门面、iClaw 接入；不教写 Agent loop）；每节「编程模型 → 示例 → API 参考」；API 参考统一挂在 `/sdk/api/` 下：`kun/`、`dotnet/`、`frontend/`、`http/`（平台 OpenAPI），不另设顶层 `/api/`（已裁决 R56） | 手写 MDX + TypeDoc / DocFX 生成 + Kun 镜像（§2b） | 是（Kun 小节随该 Qian 版本的 `KunVersion`） |
| 4 | CLI 与规格 | `/cli/` | `qian` 十个命令；`plugin.json`、`marketplace.json`、profile / patch 字段参考 | 从 `docs/prd/specs/component-model.md` 同步，不另写 | 是 |
| 5 | 运维 | `/ops/` | profile 部署到 K3s、Apollo、升级与回切、模型网关接线（GPUStack → Higress）、监控与审计 | 手写 MDX | 否 |
| 6 | 市场与 ISV | `/marketplace/` | 组件上架流程、信任等级、签名、租户私有市场；S3 前只放规则 | 手写 MDX | 否 |
| 7 | 设计与路线 | `/design/` | ADR、PRD、路线图、变更日志 | 直接镜像 `docs/` | 否 |

两条命名约定：导航上用中文名；路径用上表的英文段，页面 slug 用小写短横线。「组件怎么用」只写在第 2 部分，SDK 部分只写编程模型与 API，两边互相链接而不重复。

## 2a 首页与顶部产品导航

首页借鉴一家云平台的对标产品文档首页的组织方式：先给三条能立刻跑的命令，再按「用途」而不是按「技术栈」把产品能力分成几栏，每项一句话加链接。两条取舍原则：**不放公司信息**（定价、企业版、博客、招聘、状态页、客户案例一律不出现）；**没有的能力不放**，规划中的能力保留但打阶段标记（`S2` / `S3` / `S4`），不假装已有。

### 2a.1 首页区块（自上而下）

| 区块 | 内容 | 来源 |
|---|---|---|
| 标题与搜索 | 「Qian 文档」一句话定位（点明基于 Kun 框架构建、前端用 yue）+ 搜索框 | 手写 |
| 三条起步命令 | ① 装一个组件：`qian add identity authz files`；② 配置你的编码代理：读 `AGENTS.md`、安装 Qian 技能、接入 MCP；③ 调一个模型：经模型网关的 OpenAI 兼容端点发一条请求 | 手写，命令与第 1 部分「开始」同源 |
| 配置你的编码代理 | Qian 技能（组件的 `skills/` 表面可直接装进 Claude Code / Codex 等代理）、`AGENTS.md` 约定、Qian MCP（S1 文档检索 MCP 可选，S4 能力门面）、代理用 Qian 的提示词模板 | 第 3 部分 AI SDK 与 pm/README 第 7 节 |
| 最新动态 | 最近 4 条变更日志条目，链接到完整变更日志 | `CHANGELOG`（第 7 部分） |
| 产品能力分栏 | 见 2a.2 的五栏 | 各部分 |
| 指南与教程 | Agent 栈、后端模式、安全、集成、从参考平台迁移 | 第 1 / 5 部分与 `docs/migration/` |
| 页脚 | 站点地图、产品族仓库（Qian、Kun、yue、`qian-plugins`）、许可证（Qian Apache-2.0、Kun MIT）；没有公司链接 | 手写 |

### 2a.2 产品能力五栏与条目映射

对标产品的栏目与 Qian 的对应关系如下；「处理」列说明保留、改名或移除的理由。路径指向 §2 的七个部分。

| 栏 | 对标条目 | Qian 条目 | 阶段 | 落点 | 处理 |
|---|---|---|---|---|---|
| 用 AI 构建 | AI Gateway | 模型网关：一个端点调所有模型，配额、回退、计量（ADR-015） | S1·P1 | `/sdk/ai/gateway`、`/ops/model-gateway` | 保留，改名 |
| 用 AI 构建 | AI SDK | AI SDK：在插件与前端里调模型与 Embedding、写 skills / agents / commands / hooks / tools 表面、声明工具副作用与策略 | S1 | `/sdk/ai/` | 保留；原「Agent SDK + 模型服务 SDK」合并为此名 |
| 用 AI 构建 | 后端 Agent 框架 | Agent 运行时对接：Qian 不写 Agent loop，运行时为 iClaw（ADR-016），文档只写接入方式 | S1 | `/sdk/ai/agent-runtime` | 保留，改为「对接」而非产品 |
| 用 AI 构建 | Sandbox | 沙箱：隔离执行不可信或 Agent 生成的代码（CubeSandbox 对接） | S4 | `/sdk/ai/sandbox` | 保留，打 S4 标记 |
| 用 AI 构建 | Workflows | 流程与自动化：审批内核与触发器（ADR-012） | S2 | `/components/platform/qian.workflow` | 保留，打 S2 标记 |
| 用 AI 构建 | MCP Servers | MCP：组件的 `.mcp.json` 表面（S1）、每租户 MCP 能力门面（S4） | S1 / S4 | `/sdk/ai/mcp` | 保留 |
| 构建应用 | Frameworks | 宿主与框架：ASP.NET Core 独立模式、`platform` profile、React + yue、与 Kun 的关系 | S1 | `/start/hosts`、`/sdk/kun/with-qian` | 保留，改名 |
| 构建应用 | （新增） | Kun 框架：Qian 的底座，.NET 10 微服务框架的模块地图、指南、安全与 API；哪个 Qian 配哪个 Kun 看版本矩阵 | S1 | `/sdk/kun/`、`/start/versions` | Qian 独有，新增（ADR-021） |
| 构建应用 | Functions | 能力与函数：能力目录（命令 / 查询 / 事件 / 实体）、`function` 表面（Wasm，S2 可选） | S1 / S2 | `/sdk/dotnet/capabilities` | 保留，函数部分打 S2 标记 |
| 构建应用 | Routing Middleware | 事件与中间件：五种分发模式，`waterfall` 即环绕中间件 | S1 | `/sdk/dotnet/events` | 保留，改名 |
| 构建应用 | Incremental Static Regeneration | — | — | — | 移除，无对应 |
| 构建应用 | Image Optimization | — | — | — | 移除，无对应 |
| 构建应用 | Environments | profile 与 patch：环境 = profile + 分层 patch，`qian dump-config` | S1 | `/start/concepts/profile` | 保留，改名 |
| 构建应用 | （新增） | 组件与 `qian add`、yue 组件库、微前端（外壳与远程模块）、存储（文件与数据）、低代码（表单 / 列表，S2） | S1 / S2 | `/components/`、`/sdk/frontend/` | Qian 独有，新增 |
| 多租户平台 | Platforms（多租户 SaaS） | 租户、身份与 SSO、鉴权、权益与计费（S3）、配置中心、事件与消息、调度与实时、市场（S3） | S1 / S3 | `/components/platform/` | Qian 的核心，单独成栏 |
| 保护应用 | Web Application Firewall | 网关防护：强制认证、路由级限流、可信代理、出网 SSRF 基线（ADR-010） | S1 | `/components/platform/qian.gateway` | 保留，改名 |
| 保护应用 | Bot Management / BotID | — | — | — | 移除，无对应 |
| 保护应用 | Deployment Protection | 发布保护：制品验签、修订回切、灰度开关 | S1 | `/ops/release` | 保留，改名 |
| 保护应用 | （新增） | 审计与密钥：命令级哈希链审计、`secretRef`、密钥轮换 | S1 | `/components/platform/qian.audit`、`/ops/secrets` | Qian 独有，新增 |
| 部署与运维 | Deploy from Git | GitOps 部署：Argo CD app-of-apps、Kustomize、digest 回写（ADR-017） | S1 | `/ops/deploy` | 保留，改名 |
| 部署与运维 | Domains | — | S3 评估 | — | 移除；租户自助品牌与域名 S3 再议 |
| 部署与运维 | CDN | — | — | — | 移除，无对应 |
| 部署与运维 | Rolling Releases | 灰度发布：`IFeatureFlags` 灰度 + profile 滚动更新 | S1 | `/ops/release` | 保留，改名 |
| 部署与运维 | Instant Rollback | 修订回切：`qian.lifecycle` 的 `Rollback(toRevision)` | S1 | `/ops/release` | 保留 |
| 部署与运维 | Observability | 可观测：OTel、租户 / 用户 / 插件 baggage、计量事件 | S1 | `/components/platform/qian.observability` | 保留 |
| 指南 | Agent Stack / Backend / Services / Security / Integrations / Migrations | Agent 栈、后端模式、多 profile 组合、安全、连接器与集成、从参考平台迁移 | — | `/start/guides/`、`docs/migration/` | 保留，「Services」改为「多 profile 组合」 |

五栏的最终名称：**用 AI 构建 · 构建应用 · 多租户平台 · 保护应用 · 部署与运维**。每栏条目不超过 8 个，超出的放进该部分的二级导航。

### 2a.3 与七个部分的关系

首页与顶部导航是「按用途找产品」的入口，七个部分是「按读者组织内容」的骨架；一个能力在首页出现一次，正文只在一个部分写一次，其余位置用链接。例如模型网关：首页在「用 AI 构建」，开发者文档在第 3 部分 AI SDK，运维接线在第 5 部分运维，组件页在第 2 部分。

## 2b Kun 框架小节与产品族页（ADR-021）

Kun 是 Qian 的底座框架，没有自己的文档站，读者与本站 SDK 读者高度重叠，所以 Kun 文档在本站以**镜像**方式出现：真相源仍是 kun 仓库的 `docs/`，本站不改写、不另写一份；要改 Kun 文档去 kun 仓库提 PR。首版规格「Kun 外链不重写」作废。

### 2b.1 版本与来源

- 镜像哪个版本由仓库根 `Directory.Packages.props` 的 `<KunVersion>` 属性决定（如 `2026.9.13` 对应 kun 仓库 tag `v2026.9.13`）；属性缺失时构建失败，不回退到 `main`。全部 `Kun.*` PackageVersion 引用同一属性，所以「代码用的 Kun」与「文档里的 Kun」永远是同一个版本（ADR-021 D21.3）。
- CI 以只读凭据（deploy key 或细粒度 token）读取私有 kun 仓库；本地构建可用环境变量 `KUN_DOCS_PATH` 指向本机克隆。
- 构建因此需要 .NET SDK（DocFX）与 kun 仓库的读权限。fork 来的 PR 与 Dependabot 拿不到本仓库的 Actions Secret，文档站不在 fork 上构建（仓库私有，协作者在本仓库的分支上工作）；接入 Dependabot 时在 Dependabot secrets 里配同名 Secret（已裁决 R60）。
- DocFX 复用仓库根的 `NuGet.config`（`packageSourceMapping` 把 `Kun.*` 映射到 nuget.org），无凭据还原同版本的 `Kun.*`。`KunVersion` 默认钉 nuget.org 上的正式版；若改钉只在 GitHub Packages 上的预发布版，docs-site 工作流也要配该源的读凭据，否则构建失败（已裁决 R62）。Kun 的同日修复版 `-hotfix.N` 也推送 nuget.org（NuGet 按预发布排序），无需额外凭据；`Kun.*` 以 `[$(KunVersion)]` 精确钉定（S1-DEC-019 R65）。
- 文档站多版本时，每个 Qian 文档版本内嵌各自 `KunVersion` 的镜像，Kun 小节不单独选版本。

### 2b.2 镜像范围（白名单）

| kun 仓库来源 | 站内路径 | 说明 |
|---|---|---|
| `README.md` 的定位段与模块矩阵、`docs/architecture/module-map.md` | `/sdk/kun/`、`/sdk/kun/modules` | 概览页由脚本拼装：一句话定位、版本、模块矩阵、指向「与 Qian 的关系」 |
| `docs/getting-started.md` | `/sdk/kun/getting-started` | 上手 |
| `docs/guides/*.md`、`docs/observability/*.md`、`docs/schema-per-service.md` | `/sdk/kun/guides/<slug>` | 指南 |
| `docs/security/*.md` | `/sdk/kun/security/<slug>` | 安全 |
| `docs/architecture/why-*.md`、`docs/architecture/when-to-use-*.md`、`docs/architecture/ADR-*.md`、`docs/architecture/adr-*.md` | `/sdk/kun/architecture/<slug>` | 设计说明与 Kun 的 ADR；不与第 7 部分 Qian 的 ADR 混放。Kun 的 ADR 文件名大小写不统一，大写、小写都收（已裁决 R58） |
| `docs/whitepaper.md` | `/sdk/kun/whitepaper` | 白皮书 |
| `CHANGELOG.md`、`docs/releases/*.md` | `/sdk/kun/releases` | 变更日志与发布说明 |
| DocFX（锁定版本 `Kun.*` 包的 XML 注释） | `/sdk/api/kun/` | API 参考，与 `/sdk/api/dotnet/`（`Qian.*`）并列。API 页不进侧栏，命名空间页与类型页也不渲染侧栏（`sidebar: false`），只有两个概览页保留侧栏；入口是 SDK 侧栏末尾的「API 参考」、各概览页、包 → 命名空间 → 类型的链接与全文搜索（已裁决 R54） |
| 本仓库手写 `website/docs/sdk/kun/with-qian.mdx` | `/sdk/kun/with-qian` | 唯一的 Qian 自写页：Kun 模块 ↔ 平台插件 / seam 映射、canonical API 选择（§07 7.1「插件化定位」）、Qian 的强制装配与禁用项、补丁包状态 |

不镜像：面向其它下游产品的差距分析、路线图与 backlog、AOT 审计与基准报告、贡献者文档（发布清单、OpenAPI 模板）、各级 `AGENTS.md`。白名单外的文件进站即构建失败；要扩白名单改本表。

### 2b.3 页面规则

- slug 取源文件名小写并把 `.` 与空格换成 `-`（`ADR-003-Kun.Audit-module.md` 变为 `adr-003-kun-audit-module`）。
- 每页顶部横幅：「本页镜像自 kun 仓库 `<path>`（`v<KunVersion>`）。Kun 文档与代码不一致处以代码为准（ADR-001 D1.8）；Qian 对 Kun 的选用与约束见 `/sdk/kun/with-qian`。改动请提到 kun 仓库。」
- frontmatter：`project: kun`、`part: sdk`、`version: <KunVersion>`、`source: <owner>/kun@v<KunVersion>:<path>`、`updated` 取源文件在该 tag 的最后提交日期；`title` 取首个一级标题，`description` 取首段（截到 120 字）；`audience: [developer]`。
- 站内相对链接：目标在白名单内改写为站内路径，否则改写为 kun 仓库该 tag 的文件链接。
- 镜像页同样过 `scripts/check_docs.py`（禁用词、表格、断链）。
- 上游文档的问题（越出仓库根的相对链接、比表头少列的表格行）由脚本机械处理并告警，不让构建失败；源头修复提到 kun 仓库，随下一个 Kun 版本进站。DocFX 的 `InvalidCref` 告警若来自继承的第三方 XML 文档（如 `<inheritdoc/>` 带进来的 EF Core `O:DbContext.SaveChanges*`），不处理，页面显示为代码文本（已裁决 R59）。

### 2b.4 产品族页

- `/start/family`（手写）：Kun / Qian / yue 与周边仓库（官方市场、Agent 运行时、部署与模型服务）的层、是什么、仓库、版本与许可、文档位置；与仓库 README 的「产品族」表同一内容。
- `/start/versions`（生成）：每个文档版本对应的 Kun CalVer（`KunVersion`）、`Qian.*` 版本、`@yue/*` 版本、.NET SDK（`global.json`）、Node、K3s 基线；由 `build-versions.ts` 从仓库钉版文件产出，不手改。钉版文件：`Directory.Packages.props`（Kun）、`packages/*/*.csproj` 与 `Directory.Build.props`（`Qian.*`，写了 `IsPackable` 的项目；版本随首个包族发布钉入）、`pnpm-workspace.yaml` 里的 `@yue/*` 包、`global.json`、根 `package.json`（`engines.node`、`packageManager`）、`deploy/baseline.json`（K3s，S1-DEP-001 维护）。多版本开启前只有 `main`（开发版）一行。产品族页的表与仓库 README 的「产品族」表前五列由单测核对一致（`website/scripts/family.test.ts`）。

## 3 组件页模板

第 2 部分每页由收集脚本从组件文件夹拼装；平台插件按以下九节排列，yue 交互组件按 §3.1 编排并保留适用元数据、模型体验、已知限制与变更。缺「模型体验」或「已知限制」即构建失败（与 S1-QA-006 的 README 契约同一规则）：

1. **用途**：一句话 + 适用 / 不适用场景（来自组件 `README.md` 首段）
2. **安装**：`qian add <id>`、所属 profile、`runtime.nuget[]` / `npm[]` 包名与版本
3. **配置**：`plugin.json` 中 `config` 的字段表（从 JSON Schema 生成）与一份最小 patch 示例
4. **表面**：该组件提供的 skills / agents / commands / hooks / MCP / tools / ui 列表，各自链接
5. **API**：服务键与 Definition 接口（.NET 来自 DocFX，TS 来自 TypeDoc）；yue 组件为 props 表与活演示
6. **示例**：组件 `docs/*.mdx` 的正文
7. **模型体验**：Agent 怎么发现与调用它、工具的副作用与策略提示（来自 README 同名节）
8. **已知限制与延期项**（来自 README 同名节）
9. **变更**：该组件的 CHANGELOG 节选

### 3.1 yue 组件文档与演示

[ADR-022](../../adr/ADR-022-rspress-unified-component-docs.md) 将现有 Rspress 站点确定为唯一文档与演示体系，Storybook 全部退出；选型证据与默认插件限制见该 ADR。S1-YUE-005 先交付 core 接入，S1-DOCS-002 复用接线继续收集其它组件与 API，避免循环依赖。

- **页面编排**：参考产品负责人提供的 [组件页](https://ui.shadcn.com/docs/components/aria/alert-dialog)：标题与用途、首屏可交互预览及源码展开/复制、安装、基本用法、组合结构、可切换状态的共用示例、API 与键盘/无障碍说明；保留模型体验、已知限制和变更。每个组件的普通 React 示例均可运行并查看对应源码；预览工具栏按 `docs/scenarios.json` 切换适用状态，弹层开关等交互在示例内操作，悬停需实际移入指针，右侧目录可定位章节，左侧按组件导航。采用本仓库真实 API 与令牌，功能说明以已实现能力为准。安装、预览、代码和 API 均留在现有 Rspress 页面中，用户无需访问独立演示站。
- **内容源**：README、`docs/*.mdx`、普通 React 示例保留在组件目录，由收集脚本生成 `/components/yue/` 页面；示例与 Vitest/Playwright 共用，不复制两套组件实现。core 的 51 个入口及配套部件由清单校验覆盖。
- **展示与样式**：站点内 Rsbuild 预构建编译本地 TSX，以同源 iframe 隔离弹层/Portal 和主题；独立构建显式加载 Tailwind v4、令牌与字体，并同步明暗主题。每页有使用说明、源码、适用状态示例、完整 props 表；令牌页同站可访问。
- **交互必过验收**：每个可交互组件提供可直接操作的真实示例，并登记「操作 → 可观察结果」：输入/校验反馈，选择器选中与值回写，Dialog/AlertDialog 打开、确认/取消和关闭回焦，Tabs 切换，Toast/Notification 触发与关闭，以及各组件适用的键盘路径。对静态构建的 Rspress 页面执行 Playwright 操作与结果断言，覆盖明暗两态和预览 iframe；不得以仅渲染、截图、代码片段或外站链接替代。展示型组件验证实际渲染和适用状态，不虚构交互；全量入口与示例清单相互校验，缺交互场景或断言即 CI 失败。合并后在正式站点再次实测代表性输入、选择、弹层、切换和通知交互。
- **Props**：以真实 TypeScript 为源；api-docgen 的默认过滤不足以直接验收，当前直接用 TypeScript checker 提取完整表并补源码 JSDoc；相同 DOM 属性按完整声明去重并在站内链接。继承属性、组合部件、函数/回调、必填、联合类型与默认值不得静默丢失。Button 的 variant/size/loading/disabled、Dialog 的 open/defaultOpen/onOpenChange 为回归探针；51 入口及部件的完整性由门禁检查。manifest 的 JSON Schema 不代替完整 API。
- **在线编辑**：playground 显式预打包允许的组件依赖；示例另行 typecheck。Babel、Monaco 及 worker 全部同源打包并按需加载，禁止运行时 CDN 请求；阻断外网后编辑能更新预览才算通过。编辑器只服务文档演示，不复用产品身份或租户脚本入口。
- **测试迁移**：原 126 条 Storybook 用例逐条映射到独立 Vitest 浏览器测试或 Playwright，保留默认/悬停/禁用/错误/暗色、SSR、键盘、焦点和 ARIA 覆盖；Rspress 真正构建页面及 iframe 执行 axe，critical / serious = 0；既有 64 项页面 E2E 和 Lighthouse 门禁继续有效，测试失败阻断 PR。
- **双产物与部署**：HTML 与 SSG-MD 的 API、示例说明同源；校验 llms、搜索、前缀、深链刷新、明暗首帧与离线资源。全站产物遵守 §7 的 R55 预算（现为 120 MB）。合并后验证正式站点的真实组件交互与 props 表，不以构建成功代替发布验证。
- **退出旧工具**：替代门禁接通后，同一迁移交付中删除 Storybook 应用、CSF 专用文件、直接/传递依赖、脚本与 CI 入口；普通 React 示例、Vitest/Playwright/axe 保留。同步 README/AGENTS 与 frozen-lockfile 安装验证。

## 4 页面 frontmatter 规范

全站页面统一使用以下字段；收集脚本为生成页自动填写，手写页由作者填写，`scripts/check_docs.py` 校验（S1-DOCS-003）。

| 字段 | 必填 | 取值 | 用途 |
|---|---|---|---|
| `title` | 是 | 文本 | 页面标题 |
| `description` | 是 | ≤ 120 字 | 列表摘要、`llms.txt` 条目、知识库摘要 |
| `part` | 是 | `start` / `components` / `sdk` / `cli` / `ops` / `marketplace` / `design` | 所属部分 |
| `project` | 是 | `qian` / `kun` / `yue` | 所属项目；`llms.txt` 按它分节，知识库按它过滤（ADR-021 D21.5） |
| `audience` | 是 | 数组：`developer` / `operator` / `isv` / `agent` / `decision-maker` | 知识库与「问 AI」按读者过滤 |
| `component` | 组件页必填 | 组件 id，如 `qian.files` | 关联组件 |
| `version` | 组件页与 SDK 页必填 | SemVer | 多版本切换 |
| `profile` | 组件页 | 默认 profile 名 | 运维检索 |
| `surface` | 表面页 | `skill` / `agent` / `command` / `hook` / `mcp` / `tool` / `ui` | 按表面检索 |
| `source` | 镜像页必填 | `<owner>/<repo>@<tag>:<path>` | 镜像来源（第 3 部分 Kun 小节、第 7 部分 `docs/` 镜像） |
| `stage` | 否 | `S1` – `S4` | 路线对照 |
| `updated` | 是 | `YYYY-MM-DD` | 新鲜度 |
| `tags` | 否 | 数组 | 站内分类 |

校验器（`scripts/check_docs.py`，S1-DOCS-003）：`title`、`description`（≤ 120 字）、`part`（须与所在目录一致，根目录的首页与站点地图记作 `start`）、`project`、`audience`、`updated`（有效日期）必填；镜像页（`project: kun` 或 `part: design`）必填 `source`，Kun 镜像页还必填 `version` 且只在 `sdk/` 下；`components/platform/<id>`、`components/yue/<name>` 页必填 `component`；`stage`、`surface`、`version`、`source` 写了就查枚举或格式。`version` 对组件页与 SDK 页的强制等首个包族版本发布、多版本开启时再启用。frontmatter 只用 YAML 子集：`key: 值`、`key: [a, b]`、缩进的 `- 项` 列表、双引号或单引号字符串；生成器与校验器用同一套规则（`website/scripts/repo.ts` 的 `parseFrontmatter`），嵌套映射、多行标量一律不收。手写页默认只检查进 Git 的源，生成页在 CI 里构建后显式检查（§7）。

## 5 版本、语言与机器可读产物

- **版本**：第 2、3、4 部分随 Qian 包族的 SemVer 锁步版本走多版本；第 1、5、6、7 部分只有当前版。第 3 部分的 Kun 小节不单独选版本：每个 Qian 文档版本内嵌该版本 `KunVersion` 锁定的 Kun CalVer（ADR-021 D21.3）。
- **语言**：中文为唯一一等语言；英文在 S3 评估，目录结构预留 `/en/` 前缀，不提前翻译。
- **机器可读**：开启 `llms: true`，构建产出 `llms.txt`、`llms-full.txt`、每页 `.md`（SSG-MD），加 `sitemap.xml` 与 `kb-index.json`（每页 url、md url、标题、frontmatter 含 `project` / `version` / `source`、内容 sha256、更新时间），契约见 §5.1；`llms.txt` 按 `project` 分节（Qian / Kun / yue，S1-DOCS-006；S1-DOCS-003 先按七个部分分节）。知识库只读这些产物，不解析 MDX 源。每页标题下与首页有「复制为 Markdown」「复制 Markdown 链接」入口（Rspress `themeConfig.llmsUI`，不放外部 AI 服务入口）。
- **站内「问 AI」**：S2 由 yue 组件 `<AskDocs>` 提供，检索基于上述产物，模型只经模型网关（S2-DOCS-001）。

### 5.1 知识库导入契约（S1-DOCS-003）

构建产物（`website/doc_build/`，发布后就是站点根）里的机器可读文件：

| 文件 | 内容 | 来源 |
|---|---|---|
| `llms.txt` | 全站索引：站点标题与一句话定位，按顶部导航的七个部分分节，每页一行 `- [标题](<md 地址>): 摘要`（首页不列） | Rspress `llms: true`；摘要取 frontmatter `description` |
| `llms-full.txt` | 全站正文：每页以 `---` / `url: <md 地址>` / `---` 开头，后接该页 Markdown | 同上 |
| `<路由>.md` | 每页的 Markdown：路由 `/start/hosts` 对应 `/start/hosts.md`，目录首页 `/start/` 对应 `/start/index.md`，首页是 `/index.md`；带部署前缀 | SSG-MD：构建期用 React 把页面渲染成 Markdown，不是 HTML 回转 |
| `kb-index.json` | 页面清单与元数据（下表） | `website/scripts/build-kb-index.ts`，构建末尾运行 |
| `sitemap.xml` | 每页的绝对地址（`siteUrl + base + 路由`）、`lastmod`（取 frontmatter `updated`）、优先级（首页 1.0，其余 0.5） | 同上；`siteUrl` 默认是正式站点入口，CI 可用 `DOCS_SITE_URL` 覆盖 |

`kb-index.json`（`version: 1`；字段只增不改，改语义就升 `version`）：

| 字段 | 说明 |
|---|---|
| `version`、`generatedAt` | 契约版本；生成时间（ISO 8601） |
| `base`、`siteUrl` | 部署前缀（正式站点 `/`，预览 `/pr-preview/pr-<n>/`）与站点入口 |
| `kunVersion` | 本次构建镜像的 Kun 版本（`Directory.Packages.props` 的 `KunVersion`） |
| `pages[]` | 按路由排序。每页：`route`、`url`（HTML）、`md`（Markdown 地址，两者都带前缀）、`sourceFile`（相对 `website/docs`）、`title`、`description`、`project`、`part`、`audience[]`、`version`（有则给）、`source`（镜像页）、`updated`、`sha256`（`.md` 内容的 SHA-256）、`bytes`、`frontmatter`（完整 frontmatter 解析结果） |

消费方（S2 的知识库与 `<AskDocs>`）的用法：定期拉 `kb-index.json`，按 `project` / `audience` / `part` 过滤，`sha256` 变了才重新抓该页 `.md`；引用来源给 `url`，列表摘要用 `description`。Markdown 里的约定：每页第一行是给代理的提示（指向 `llms.txt` 与 `llms-full.txt`）；Rspress 的容器语法 `:::info … :::` 原样保留；站内链接指向对应页的 `.md`；自定义组件（首页卡片、最新动态、站点地图）在 SSG-MD 下只输出标题、段落、列表与链接；JSX 容器内部的 Markdown 里不能再放行内组件（会原样残留），阶段标记写成文字。

## 6 `website/` 目录骨架

```text
website/
  rspress.config.ts            llms: true；多版本；自研同源 iframe 预览，TypeScript checker 提取 core Props
  package.json
  README.md                    本规范的实施说明、本地运行与发布方式
  docs/
    index.mdx                  首页
    start/                     1 开始（family.md 产品族地图；versions.md 由 build-versions.ts 生成，不进 Git）
    components/                2 组件目录（生成，勿手改；收集脚本输出）
      yue/core/<name>.mdx       51 个 core 入口；yue/api/ 共享继承属性表
      platform/<id>.mdx
    sdk/                       3 SDK
      kun/                     Kun 框架小节：sync-kun-docs.ts 按 KunVersion 镜像 kun 仓库 docs/（生成，勿手改）；with-qian.mdx 为唯一手写页
      dotnet/  frontend/  ai/            ai = AI SDK（模型调用 + Agent 表面 + MCP + 沙箱）
      api/                     TypeDoc / DocFX 生成物（dotnet/ = Qian.*，kun/ = 锁定版本的 Kun.*，frontend/，http/ = 平台 OpenAPI）；不进侧栏（R54、R56）
    cli/                       4 CLI 与规格（从 specs 同步）
    ops/                       5 运维
    marketplace/               6 市场与 ISV
    design/                    7 设计与路线（镜像 docs/）
  scripts/
    collect-components.ts      收集组件文件夹 → docs/components/
    yue-docs.ts                TypeScript checker 提取属性、组合结构与共用示例
    build-yue-demos.ts         Rsbuild 编译 iframe 与本地编辑器资源 → docs/public/
    sync-specs.ts              specs → docs/cli/、docs/ → docs/design/
    build-kb-index.ts          构建后产出 kb-index.json 与 sitemap.xml（§5.1）
    sync-kun-docs.ts           kun 仓库 v<KunVersion> 的 docs/ 白名单 → docs/sdk/kun/（§2b）
    build-versions.ts          Directory.Packages.props / csproj / global.json / package.json / pnpm 工作区 / deploy/baseline.json → docs/start/versions.md
  demo/                        普通 React iframe 预览、状态工具栏与离线编辑运行
  theme/                       主题覆盖：接入 @yue/tokens、首页的「复制为 Markdown」（文档页由 llmsUI 注入）、AskDocs 挂载点；自定义组件带 SSG-MD 分支
```

## 7 门禁

S1-QA-010 于 2026-10-15 按 R55 将预算从 100 MB 调整为 120 MB：完整站点实测 96.2 MB / 756 个页面（知识库收录 754 页），其中离线 Monaco / Babel 15.6 MB、API 参考目录 20.1 MB、yue 页面与完整属性表 7.7 MB；保留约 24 MB 的后续收集空间。编辑器继续点击后按需加载、全量同源，完整 TypeScript worker 约 5.75 MB，删除其余语言并不足以降到 90 MB，故本次选用预算调整路线，保留现有编辑能力。共享继承属性表已经去重，预算仍校验全部未压缩文件且超限必败；S1-DOCS-002 开工前须完成此整改，后续正常增长仍需逐次说明，不能自动放宽。

- `scripts/check_docs.py` 覆盖 `website/docs`：禁用词、表格、断链、frontmatter 必填与枚举（§4）；生成目录在 CI 里构建后显式检查（`scripts/check_docs.py website/docs/components website/docs/cli website/docs/design website/docs/sdk/kun website/docs/sdk/api`）。
- CI 构建站点后 `pnpm --filter website check`：`llms.txt`、`llms-full.txt`、每页 `.md`、`kb-index.json`、`sitemap.xml` 齐全；索引里每页的 `.md` 与 HTML 都在且 `sha256` 一致，`llms.txt`、`llms-full.txt`、`sitemap.xml` 收录每页，产物里没有索引外的 `.md`；每页 `.md` 没有 JSX / ESM 残留与空标题、空列表项，代码块不少于源页（§5.1）。
- 生成目录（`components/`、`sdk/api/`、`sdk/kun/`（`with-qian.mdx` 除外）、`cli/`、`design/`）与生成文件 `start/versions.md` 不接受手改 PR（进了 Git 即失败）。
- Kun 小节：镜像的 Kun tag 与 `Directory.Packages.props` 的 `KunVersion` 不一致即失败；§2b.2 白名单外的文件进站即失败；`KunVersion` 缺失时构建失败而不是取 `main`。
- 产物总大小超过预算即失败（`website/scripts/check-site.ts` 的 `SITE_SIZE_BUDGET_MB`，现为 120 MB），并报出最大的目录。预算挡的是结构性膨胀，比如同一块内容被重复渲染进每一页；确属正常增长时调高预算，并在 PR 里写明原因（已裁决 R55）。

## 8 验收

- [ ] 七个部分的路径、来源与版本策略按 §2 实现，导航中文、路径英文
- [ ] 任一组件页满足 §3 九节顺序；缺节组件构建失败
- [x] 全站页面 frontmatter 通过 §4 校验（S1-DOCS-003）
- [x] §5 的四类机器可读产物齐全并经 CI 新鲜度校验（S1-DOCS-003，§5.1）
- [ ] 第 2 部分 yue 页有活演示与 props 表，平台插件页有配置字段表与最小 patch 示例
- [ ] 第 3 部分「Kun 框架」小节按 §2b 生成：版本与 `KunVersion` 一致、每页有来源横幅与 `project: kun`、`/sdk/api/kun/` 可用、`/sdk/kun/with-qian` 手写页存在
- [x] `/start/family` 与 `/start/versions` 存在，后者由脚本生成并与仓库钉版一致（S1-DOCS-006）

## 修订历史

- 2026-10-15：S1-QA-010：§3 共用示例改为预览工具栏切换 scenarios.json 状态；§6 自研 iframe 预览与 TypeScript checker、页面路径 `yue/core/<name>.mdx`；§7 预算按 R55 调为 120 MB。

- 2026-10-07：首版。信息架构（七个部分）由产品负责人确认；frontmatter 字段与 S1-DOCS-003 对齐。
- 2026-10-07：新增 §2a 首页与顶部产品导航（借鉴对标产品文档首页：三条起步命令、编码代理配置、五栏产品能力、无公司信息、无对应能力移除、规划能力打阶段标记）；第 3 部分「Agent SDK + 模型服务 SDK」合并为对外名称「AI SDK」。
- 2026-10-09：S1-DOCS-006：§2b.4 写明版本矩阵的钉版文件（含 `deploy/baseline.json`）与产品族页同 README 的单测核对；`llms.txt` 先按 project 再按部分分节；§6、§7、§8 相应更新。
- 2026-10-09：S1-DOCS-003：§4 补校验器规则与 frontmatter 的 YAML 子集；§5 新增 5.1 知识库导入契约（五个机器可读文件、`kb-index.json` 字段、`.md` 地址规则、sitemap 的 lastmod）；§6、§7、§8 相应更新。
- 2026-10-08：S1-DOCS-005（PR #4）未决问题裁决 R54–R62（S1-DEC-011）：API 参考不进侧栏、API 页不渲染侧栏（R54）；产物总大小预算（R55）；API 参考路径统一在 `/sdk/api/`（R56）；Kun 小节排在 SDK 概览页之后（R57）；Kun ADR 白名单收小写文件名（R58）；上游问题与第三方 `cref`（R59）；构建依赖与 fork / Dependabot（R60）；`KunVersion` 与包源映射（R62）。§2、§2b、§6、§7 相应更新。
- 2026-10-07：Kun 进站与产品族页（ADR-021）：第 3 部分新增第一节「Kun 框架」`/sdk/kun/`，按 `KunVersion` 从 kun 仓库镜像 + DocFX，取代首版「Kun 外链不重写」；第 1 部分新增产品族地图 `/start/family` 与版本矩阵 `/start/versions`；frontmatter 新增 `project` / `source`；§1、§2a、§2b、§5、§6、§7、§8 相应更新。
