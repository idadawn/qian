# 文档站信息架构与规范（Rspress 2.0）

> 状态：已确认（产品负责人 2026-10-07） · 决策引用：ADR-013 D13.18、ADR-020 D20.9、ADR-015、ADR-016 · 实施任务：S1-DOCS-001 / 002 / 003、S2-DOCS-001
> 本文回答三件事：文档站分几部分、每部分给谁看从哪来、页面与元数据长什么样。站点源码在仓库 `website/`，文档内容的真相源仍是各组件文件夹与 `docs/`。

## 1 读者与目标

| 读者 | 要解决的问题 | 主要去的部分 |
|---|---|---|
| 新开发者 | 30 分钟内在空白项目里用上 Qian 的组件 | 1 开始、2 组件目录 |
| 用组件的开发者 | 某个组件怎么装、怎么配、暴露了什么 | 2 组件目录、4 CLI 与规格 |
| 写组件的开发者 | 编程模型、API、怎么让 Agent 用上我的组件 | 3 SDK、4 CLI 与规格 |
| 部署与运营 | profile 怎么上 K3s、怎么升级回切、模型网关怎么接 | 5 运维 |
| 第三方 / ISV | 怎么上架、信任等级与签名 | 6 市场与 ISV |
| Agent 与知识库 | 机器可读的全文与元数据 | 全站的 `.md` 产物与 `kb-index.json` |
| 决策者与贡献者 | 为什么这么设计 | 7 设计与路线 |

## 2 七个部分

| # | 部分 | 路径 | 内容 | 来源 | 版本化 |
|---|---|---|---|---|---|
| 1 | 开始 | `/start/` | 30 分钟跑通 `qian add identity authz files`；核心概念五页：组件、表面、scope、seam、profile / patch | 手写 MDX | 否 |
| 2 | 组件目录 | `/components/` | 每个组件一页（模板见 §3）：yue UI 组件带活演示与 props 表；平台插件（身份、鉴权、文件、配置、事件…）同一模板 | 从各组件文件夹自动收集（S1-DOCS-002），**生成目录勿手改** | 是，随包版本 |
| 3 | SDK | `/sdk/` | 后端 SDK（.NET：`Qian.Runtime`、`Qian.Plugin.Sdk`、`Qian.*`；Kun 外链不重写）、前端 SDK（`@qian/runtime`、`@qian/plugin-sdk`、`@yue/sdk`、MF2 远程模块）、Agent SDK（表面编写、工具副作用与策略、能力目录、MCP 门面、iClaw 接入；不教写 Agent loop）、模型服务 SDK（在插件与 Agent 里调模型 / Embedding、能力契约、配额）；每节「编程模型 → 示例 → API 参考」 | 手写 MDX + TypeDoc / DocFX 生成 | 是 |
| 4 | CLI 与规格 | `/cli/` | `qian` 十个命令；`plugin.json`、`marketplace.json`、profile / patch 字段参考 | 从 `docs/prd/specs/component-model.md` 同步，不另写 | 是 |
| 5 | 运维 | `/ops/` | profile 部署到 K3s、Apollo、升级与回切、模型网关接线（GPUStack → Higress）、监控与审计 | 手写 MDX | 否 |
| 6 | 市场与 ISV | `/marketplace/` | 组件上架流程、信任等级、签名、租户私有市场；S3 前只放规则 | 手写 MDX | 否 |
| 7 | 设计与路线 | `/design/` | ADR、PRD、路线图、变更日志 | 直接镜像 `docs/` | 否 |

两条命名约定：导航上用中文名；路径用上表的英文段，页面 slug 用小写短横线。「组件怎么用」只写在第 2 部分，SDK 部分只写编程模型与 API，两边互相链接而不重复。

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
| `audience` | 是 | 数组：`developer` / `operator` / `isv` / `agent` / `decision-maker` | 知识库与「问 AI」按读者过滤 |
| `component` | 组件页必填 | 组件 id，如 `qian.files` | 关联组件 |
| `version` | 组件页与 SDK 页必填 | SemVer | 多版本切换 |
| `profile` | 组件页 | 默认 profile 名 | 运维检索 |
| `surface` | 表面页 | `skill` / `agent` / `command` / `hook` / `mcp` / `tool` / `ui` | 按表面检索 |
| `stage` | 否 | `S1` – `S4` | 路线对照 |
| `updated` | 是 | `YYYY-MM-DD` | 新鲜度 |
| `tags` | 否 | 数组 | 站内分类 |

## 5 版本、语言与机器可读产物

- **版本**：第 2、3、4 部分随 Qian 包族的 SemVer 锁步版本走多版本；第 1、5、6、7 部分只有当前版。
- **语言**：中文为唯一一等语言；英文在 S3 评估，目录结构预留 `/en/` 前缀，不提前翻译。
- **机器可读**：开启 `llms: true`，构建产出 `llms.txt`、`llms-full.txt`、每页 `.md`（SSG-MD），加 `sitemap.xml` 与 `kb-index.json`（每页 url、md url、标题、frontmatter、内容 sha256、更新时间）。知识库只读这些产物，不解析 MDX 源。每页提供「复制为 Markdown」入口。
- **站内「问 AI」**：S2 由 yue 组件 `<AskDocs>` 提供，检索基于上述产物，模型只经模型网关（S2-DOCS-001）。

## 6 `website/` 目录骨架

```text
website/
  rspress.config.ts            llms: true；多版本；插件 preview / playground / api-docgen / typedoc
  package.json
  README.md                    本规范的实施说明、本地运行与发布方式
  docs/
    index.mdx                  首页
    start/                     1 开始
    components/                2 组件目录（生成，勿手改；收集脚本输出）
      yue/<name>.mdx
      platform/<id>.mdx
    sdk/                       3 SDK
      dotnet/  frontend/  agent/  models/
      api/                     TypeDoc / DocFX 生成物
    cli/                       4 CLI 与规格（从 specs 同步）
    ops/                       5 运维
    marketplace/               6 市场与 ISV
    design/                    7 设计与路线（镜像 docs/）
  scripts/
    collect-components.ts      收集组件文件夹 → docs/components/
    sync-specs.ts              specs → docs/cli/、docs/ → docs/design/
    build-kb-index.ts          产出 kb-index.json
  theme/                       主题覆盖：接入 @yue/tokens、「复制为 Markdown」、AskDocs 挂载点
```

## 7 门禁

- `scripts/check_docs.py` 覆盖 `website/docs`：禁用词、表格、断链、frontmatter 必填与枚举。
- CI 构建站点；`llms.txt`、每页 `.md`、`kb-index.json` 与源文档不同步即失败。
- 生成目录（`components/`、`sdk/api/`、`cli/`、`design/`）不接受手改 PR。

## 8 验收

- [ ] 七个部分的路径、来源与版本策略按 §2 实现，导航中文、路径英文
- [ ] 任一组件页满足 §3 九节顺序；缺节组件构建失败
- [ ] 全站页面 frontmatter 通过 §4 校验
- [ ] §5 的四类机器可读产物齐全并经 CI 新鲜度校验
- [ ] 第 2 部分 yue 页有活演示与 props 表，平台插件页有配置字段表与最小 patch 示例

## 修订历史

- 2026-10-07：首版。信息架构（七个部分）由产品负责人确认；frontmatter 字段与 S1-DOCS-003 对齐。
