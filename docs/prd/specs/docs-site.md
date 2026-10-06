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
| 3 | SDK | `/sdk/` | **Kun 框架**（第一节 `/sdk/kun/`：底座框架的概览、模块地图、上手、指南、安全、Kun 的 ADR、变更日志与 API，按锁定版本从 kun 仓库镜像，另有唯一手写页「与 Qian 的关系」，规则见 §2b）、后端 SDK（.NET：`Qian.Runtime`、`Qian.Plugin.Sdk`、`Qian.*`）、前端 SDK（`@qian/runtime`、`@qian/plugin-sdk`、`@yue/sdk`、MF2 远程模块）、AI SDK（对外统一名称，见 2a.2：在插件与前端里调模型 / Embedding 与能力契约、配额；写 skills / agents / commands / hooks / tools 表面、工具副作用与策略、能力目录、MCP 门面、iClaw 接入；不教写 Agent loop）；每节「编程模型 → 示例 → API 参考」 | 手写 MDX + TypeDoc / DocFX 生成 + Kun 镜像（§2b） | 是（Kun 小节随该 Qian 版本的 `KunVersion`） |
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
- 文档站多版本时，每个 Qian 文档版本内嵌各自 `KunVersion` 的镜像，Kun 小节不单独选版本。

### 2b.2 镜像范围（白名单）

| kun 仓库来源 | 站内路径 | 说明 |
|---|---|---|
| `README.md` 的定位段与模块矩阵、`docs/architecture/module-map.md` | `/sdk/kun/`、`/sdk/kun/modules` | 概览页由脚本拼装：一句话定位、版本、模块矩阵、指向「与 Qian 的关系」 |
| `docs/getting-started.md` | `/sdk/kun/getting-started` | 上手 |
| `docs/guides/*.md`、`docs/observability/*.md`、`docs/schema-per-service.md` | `/sdk/kun/guides/<slug>` | 指南 |
| `docs/security/*.md` | `/sdk/kun/security/<slug>` | 安全 |
| `docs/architecture/why-*.md`、`docs/architecture/when-to-use-*.md`、`docs/architecture/ADR-*.md` | `/sdk/kun/architecture/<slug>` | 设计说明与 Kun 的 ADR；不与第 7 部分 Qian 的 ADR 混放 |
| `docs/whitepaper.md` | `/sdk/kun/whitepaper` | 白皮书 |
| `CHANGELOG.md`、`docs/releases/*.md` | `/sdk/kun/releases` | 变更日志与发布说明 |
| DocFX（锁定版本 `Kun.*` 包的 XML 注释） | `/sdk/api/kun/` | API 参考，与 `/sdk/api/dotnet/`（`Qian.*`）并列 |
| 本仓库手写 `website/docs/sdk/kun/with-qian.mdx` | `/sdk/kun/with-qian` | 唯一的 Qian 自写页：Kun 模块 ↔ 平台插件 / seam 映射、canonical API 选择（§07 7.1「插件化定位」）、Qian 的强制装配与禁用项、补丁包状态 |

不镜像：面向其它下游产品的差距分析、路线图与 backlog、AOT 审计与基准报告、贡献者文档（发布清单、OpenAPI 模板）、各级 `AGENTS.md`。白名单外的文件进站即构建失败；要扩白名单改本表。

### 2b.3 页面规则

- slug 取源文件名小写并把 `.` 与空格换成 `-`（`ADR-003-Kun.Audit-module.md` 变为 `adr-003-kun-audit-module`）。
- 每页顶部横幅：「本页镜像自 kun 仓库 `<path>`（`v<KunVersion>`）。Kun 文档与代码不一致处以代码为准（ADR-001 D1.8）；Qian 对 Kun 的选用与约束见 `/sdk/kun/with-qian`。改动请提到 kun 仓库。」
- frontmatter：`project: kun`、`part: sdk`、`version: <KunVersion>`、`source: <owner>/kun@v<KunVersion>:<path>`、`updated` 取源文件在该 tag 的最后提交日期；`title` 取首个一级标题，`description` 取首段（截到 120 字）；`audience: [developer]`。
- 站内相对链接：目标在白名单内改写为站内路径，否则改写为 kun 仓库该 tag 的文件链接。
- 镜像页同样过 `scripts/check_docs.py`（禁用词、表格、断链）。

### 2b.4 产品族页

- `/start/family`（手写）：Kun / Qian / yue 与周边仓库（官方市场、Agent 运行时、部署与模型服务）的层、是什么、仓库、版本与许可、文档位置；与仓库 README 的「产品族」表同一内容。
- `/start/versions`（生成）：每个文档版本对应的 Kun CalVer（`KunVersion`）、`Qian.*` 版本、`@yue/*` 版本、.NET SDK（`global.json`）、Node、K3s 基线；由 `build-versions.ts` 从仓库钉版文件产出，不手改。

## 3 组件页模板

第 2 部分每页由收集脚本从组件文件夹拼装，章节顺序固定，缺「模型体验」或「已知限制」即构建失败（与 S1-QA-006 的 README 契约同一规则）：

1. **用途**：一句话 + 适用 / 不适用场景（来自组件 `README.md` 首段）
2. **安装**：`qian add <id>`、所属 profile、`runtime.nuget[]` / `npm[]` 包名与版本
3. **配置**：`plugin.json` 中 `config` 的字段表（从 JSON Schema 生成）与一份最小 patch 示例
4. **表面**：该组件提供的 skills / agents / commands / hooks / MCP / tools / ui 列表，各自链接
5. **API**：服务键与 Definition 接口（.NET 来自 DocFX，TS 来自 TypeDoc）；yue 组件为 props 表与活演示
6. **示例**：组件 `docs/*.mdx` 的正文
7. **模型体验**：Agent 怎么发现与调用它、工具的副作用与策略提示（来自 README 同名节）
8. **已知限制与延期项**（来自 README 同名节）
9. **变更**：该组件的 CHANGELOG 节选

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

## 5 版本、语言与机器可读产物

- **版本**：第 2、3、4 部分随 Qian 包族的 SemVer 锁步版本走多版本；第 1、5、6、7 部分只有当前版。第 3 部分的 Kun 小节不单独选版本：每个 Qian 文档版本内嵌该版本 `KunVersion` 锁定的 Kun CalVer（ADR-021 D21.3）。
- **语言**：中文为唯一一等语言；英文在 S3 评估，目录结构预留 `/en/` 前缀，不提前翻译。
- **机器可读**：开启 `llms: true`，构建产出 `llms.txt`、`llms-full.txt`、每页 `.md`（SSG-MD），加 `sitemap.xml` 与 `kb-index.json`（每页 url、md url、标题、frontmatter 含 `project` / `version` / `source`、内容 sha256、更新时间）；`llms.txt` 按 `project` 分节（Qian / Kun / yue）。知识库只读这些产物，不解析 MDX 源。每页提供「复制为 Markdown」入口。
- **站内「问 AI」**：S2 由 yue 组件 `<AskDocs>` 提供，检索基于上述产物，模型只经模型网关（S2-DOCS-001）。

## 6 `website/` 目录骨架

```text
website/
  rspress.config.ts            llms: true；多版本；插件 preview / playground / api-docgen / typedoc
  package.json
  README.md                    本规范的实施说明、本地运行与发布方式
  docs/
    index.mdx                  首页
    start/                     1 开始（family.mdx 产品族地图；versions.mdx 由 build-versions.ts 生成）
    components/                2 组件目录（生成，勿手改；收集脚本输出）
      yue/<name>.mdx
      platform/<id>.mdx
    sdk/                       3 SDK
      kun/                     Kun 框架小节：sync-kun-docs.ts 按 KunVersion 镜像 kun 仓库 docs/（生成，勿手改）；with-qian.mdx 为唯一手写页
      dotnet/  frontend/  ai/            ai = AI SDK（模型调用 + Agent 表面 + MCP + 沙箱）
      api/                     TypeDoc / DocFX 生成物（dotnet/ = Qian.*，kun/ = 锁定版本的 Kun.*，frontend/）
    cli/                       4 CLI 与规格（从 specs 同步）
    ops/                       5 运维
    marketplace/               6 市场与 ISV
    design/                    7 设计与路线（镜像 docs/）
  scripts/
    collect-components.ts      收集组件文件夹 → docs/components/
    sync-specs.ts              specs → docs/cli/、docs/ → docs/design/
    build-kb-index.ts          产出 kb-index.json
    sync-kun-docs.ts           kun 仓库 v<KunVersion> 的 docs/ 白名单 → docs/sdk/kun/（§2b）
    build-versions.ts          Directory.Packages.props / global.json / pnpm catalog → docs/start/versions.mdx
  theme/                       主题覆盖：接入 @yue/tokens、「复制为 Markdown」、AskDocs 挂载点
```

## 7 门禁

- `scripts/check_docs.py` 覆盖 `website/docs`：禁用词、表格、断链、frontmatter 必填与枚举。
- CI 构建站点；`llms.txt`、每页 `.md`、`kb-index.json` 与源文档不同步即失败。
- 生成目录（`components/`、`sdk/api/`、`sdk/kun/`（`with-qian.mdx` 除外）、`cli/`、`design/`、`start/versions.mdx`）不接受手改 PR。
- Kun 小节：镜像的 Kun tag 与 `Directory.Packages.props` 的 `KunVersion` 不一致即失败；§2b.2 白名单外的文件进站即失败；`KunVersion` 缺失时构建失败而不是取 `main`。

## 8 验收

- [ ] 七个部分的路径、来源与版本策略按 §2 实现，导航中文、路径英文
- [ ] 任一组件页满足 §3 九节顺序；缺节组件构建失败
- [ ] 全站页面 frontmatter 通过 §4 校验
- [ ] §5 的四类机器可读产物齐全并经 CI 新鲜度校验
- [ ] 第 2 部分 yue 页有活演示与 props 表，平台插件页有配置字段表与最小 patch 示例
- [ ] 第 3 部分「Kun 框架」小节按 §2b 生成：版本与 `KunVersion` 一致、每页有来源横幅与 `project: kun`、`/sdk/api/kun/` 可用、`/sdk/kun/with-qian` 手写页存在
- [ ] `/start/family` 与 `/start/versions` 存在，后者由脚本生成并与仓库钉版一致

## 修订历史

- 2026-10-07：首版。信息架构（七个部分）由产品负责人确认；frontmatter 字段与 S1-DOCS-003 对齐。
- 2026-10-07：新增 §2a 首页与顶部产品导航（借鉴对标产品文档首页：三条起步命令、编码代理配置、五栏产品能力、无公司信息、无对应能力移除、规划能力打阶段标记）；第 3 部分「Agent SDK + 模型服务 SDK」合并为对外名称「AI SDK」。
- 2026-10-07：Kun 进站与产品族页（ADR-021）：第 3 部分新增第一节「Kun 框架」`/sdk/kun/`，按 `KunVersion` 从 kun 仓库镜像 + DocFX，取代首版「Kun 外链不重写」；第 1 部分新增产品族地图 `/start/family` 与版本矩阵 `/start/versions`；frontmatter 新增 `project` / `source`；§1、§2a、§2b、§5、§6、§7、§8 相应更新。
