# Qian（乾）

基于 Kun（.NET 10 云原生微服务框架）构建的 PaaS 平台：以「一切皆插件」组织一切能力，组件先是文件夹里的清单与声明式表面、一条命令就能装进任何项目；先做面向开发人员的技术底座，再做低代码构建与应用市场，终态是 AI 原生的 Agent 平台。

- 产品 = Qian · 底座框架 = Kun · 前端组件库 = yue · 运行时 = `Qian.Runtime` · 组件 ID 前缀 = `qian.` · 包前缀 = `Qian.*` / `@qian/*` / `@yue/*`
- 阶段：S1 技术底座 → S2 低代码构建 → S3 面向客户 → S4 AI 原生 Agent 平台
- 当前状态：设计文档完成（决策 D1–D18 已认可，ADR-019 / 020 / 021 已 Accepted），S1 任务已拆解并开始交付（CI 基础与文档站脚手架已合入，契约冻结在途）；文档站见 `website/`

## 产品族：Kun、Qian、yue 与周边仓库

三个名字、三层，不是一个大单仓（[ADR-021](docs/adr/ADR-021-product-family-structure.md)）。对外一句话：**Qian 平台基于 Kun 框架构建，前端使用 yue 组件库**。Kun 不是 Qian 的子项目（它有自己的下游与发布节奏），Qian 也不是 Kun 的扩展（它有自己的运行时与产品语义）。把它们关联起来的是四样东西：一个 GitHub 所有者 `idadawn`（2026-10-07 拍板，Kun 从 CavaiLAB 迁入，见任务 S1-PM-003）、一个文档站、一张自动生成的版本矩阵、一个任务看板。

| 名字 | 层 | 是什么 | 仓库 | 版本 · 许可 | 文档在哪 |
|---|---|---|---|---|---|
| Kun | 框架层（底座） | .NET 10 云原生微服务框架，32 个 `Kun.*` 模块：Mediator、Outbox、Saga、多租户与 RLS、网关与 S2S、存储、缓存、可观测 | `idadawn/kun`（2026-10-08 从 CavaiLAB 迁回，旧地址重定向） | CalVer · MIT · 正式包在 nuget.org | 文档站第 3 部分「Kun 框架」，按 `KunVersion` 锁定版本从 kun 仓库 `docs/` 镜像 |
| Qian | 平台层 | 基于 Kun 的多租户 PaaS：`Qian.Runtime` + 平台插件 + profile + `qian` CLI + 目录市场 | `idadawn/qian`（本仓库） | SemVer 锁步 · Apache-2.0 | 本仓库 `docs/` 与文档站全部七个部分 |
| yue | 前端层 | React 组件库与外壳，`@yue/*` | 本仓库（pnpm workspace，S1 第 2 波建立） | SemVer（Changesets）· Apache-2.0 | 文档站第 2 部分（活演示 + props 表） |
| `qian-plugins` | 市场 | 官方目录市场 `.qian/marketplace.json` | `idadawn/qian-plugins`（S1 私有，待建） | — | 文档站第 6 部分 |
| iClaw | 兄弟系统 | Agent 运行时与 AI 资产控制面；Qian 不写 Agent loop（ADR-016） | `idadawn/iclaw` | — | 文档站 AI SDK「Agent 运行时对接」 |
| 部署与模型服务 | 运维 | GitOps 部署仓库（ADR-017）；GPUStack + Higress 模型服务（ADR-015） | `idadawn/iclaw-deploy`、`idadawn/MaaS` | — | 文档站第 5 部分 |

Kun 的版本只在 `Directory.Packages.props` 的 `KunVersion` 写一次；升级 Kun = 改这一处、跑 TCK、文档自动跟随。要改 Kun 的代码或文档，去 kun 仓库提 PR，Qian 的任务里只回链。

## 仓库里有什么

| 目录 | 内容 | 入口 |
|---|---|---|
| `docs/` | 决策清单、21 份 ADR、PRD 17 章与四份专题规格、迁移方案、路线图 | [docs/README.md](docs/README.md) |
| `pm/` | 任务文件、离线看板、进度概览（任务即文件，git 是唯一真相源） | [pm/README.md](pm/README.md) · [进度概览](pm/STATUS.md) |
| `scripts/` | `pm.py`（任务校验 / 生成 / 改状态）、`check_docs.py`（文档门禁）、`check_deps.py`（.NET 依赖图门禁） | — |
| `tests/` | 集成测试共用的 Testcontainers fixture（PostgreSQL 16 / Redis 7 / RabbitMQ / Apollo 替身）、门禁自测 | [tests/README.md](tests/README.md) |
| `AGENTS.md` | 给 Codex、Claude Code 等编码代理的入口约定（`CLAUDE.md` 指向它） | [AGENTS.md](AGENTS.md) |
| `website/` | 文档站（Rspress 2.0）：七个部分、首页、主题；设计文档在站内「设计与路线」镜像 | [website/README.md](website/README.md) |
| `packages/yue/` · `apps/` | yue 前端工作区：`@yue/*` 八个 S1 包的骨架（pnpm workspace + `catalog:` + Turborepo）、六层分层边界、八项 CI 门禁、Base UI 验证门靶场 | [packages/yue/README.md](packages/yue/README.md) |
| `.github/` | CI：文档门禁 + 任务校验 + 看板生成物新鲜度；.NET 构建、PublicAPI 与依赖图门禁、单元与集成测试；文档站构建、PR 预览与发布 | — |
| `deploy/` | 开发期工具的 K3s 清单：`pm-board/` 把看板放进集群、`docs-site/` 托管文档站（都是 git-sync + nginx） | [deploy/pm-board/README.md](deploy/pm-board/README.md) · [deploy/docs-site/README.md](deploy/docs-site/README.md) |

代码目录（`packages/`、`plugins/`、`profiles/`、`samples/`）由 S1 第一波任务建立，布局以 [组件模型规格](docs/prd/specs/component-model.md) 为准。

## 从哪里开始读

1. [ADR-020 组件模型 v2](docs/adr/ADR-020-component-model-v2.md)：一切皆插件、声明式表面、`qian add` 一键安装、目录市场、seam 三角色、scope、profile / patch。
2. [组件模型规格](docs/prd/specs/component-model.md)：实现时以它为准（清单字段、表面格式、运行时 API、CLI、市场、TCK）。
3. [S1 技术底座功能规格](docs/prd/07-s1-foundation.md)：每个组件的范围、技术落点、验收。
4. [决策清单](docs/decisions/key-decisions-v0.md) 与 [ADR 索引](docs/adr/README.md)：为什么这么定。
5. [路线图](docs/ROADMAP.md)：阶段退出标准与里程碑。

## 项目管理：怎么看进度、怎么维护

**任务是文件。** `pm/tasks/<id>.md` 一个任务一个文件，YAML 头里有状态、阶段、领域、波次、优先级、负责人、估算、依赖和文档链接，正文是背景、可勾选的验收和进度日志。改任务就是改文件、提交；人和编码代理用同一套命令。

**进度是算出来的。** 运行 `python3 scripts/pm.py build` 会生成：

- `pm/index.html`：离线看板（看板 / 列表 / 进度 / 阻塞与依赖四个视图，可按阶段、领域、波次、状态、负责人筛选，点任务看详情），双击即可打开；集群里的实时副本在 <http://10.78.1.161:30810/>（WireGuard 侧）；
- `pm/STATUS.md`：按阶段、波次、领域、负责人的进度表，在 GitHub 上直接可读；
- `pm/tasks.json`：给其他工具用的数据。

常用命令（仓库根目录，只需 Python 3）：

```bash
python3 scripts/pm.py list --status todo                 # 找任务
python3 scripts/pm.py set S1-RT-001 --status doing --owner 你的名字 --note "开始"
python3 scripts/pm.py set S1-RT-001 --status done --note "验收全部通过"
python3 scripts/pm.py add --stage S1 --area FILE --title "..." --priority P0 --wave 2 --estimate 2d
python3 scripts/pm.py validate && python3 scripts/pm.py build   # 改完任务必跑，生成物一起提交
```

规则、领域代码、状态定义见 [pm/README.md](pm/README.md)；**怎么把任务下发给人或 Codex / Claude Code（含可复制的提示词模板）见其第 7 节**。CI 会在推送与 PR 时检查文档门禁、任务文件合法性和生成物是否过期。

## 开发怎么开始

S1 按四波推进（见 [迁移方案总览 §4.2](docs/migration/README.md) 与看板的「波次」筛选）：

1. **第 1 波 可信计算基**：`Qian.Runtime` 与三个平台插件、Kun 缺口修复、租户、身份唯一 issuer、鉴权端口与 RBAC、网关、事件、审计、Apollo 配置。
2. **第 2 波 开发者可用**：`qian` CLI 与 SDK、官方目录市场、文件、组织目录、消息、调度、实时、可观测、yue 最小集合与外壳。
3. **第 3 波 S2 契约冻结**：Filter AST、Field Type Catalog、表单 / 列表 Schema 草案、可发布元数据与导入导出框架等契约。
4. **第 4 波 收尾与可选**：企业微信连接器与 TOTP、模型网关底座接线、SchemaPerTenant 验证等。

开始前的三件事：

- 看板「阻塞与依赖」视图左栏列出了当前**无未完成依赖、可以直接开始**的任务；先从第 1 波 P0 挑。
- `type: decision` 的任务（领域 DEC）是尚未拍板的问题，拍板后把结论写进对应 ADR 再关闭；开发可以在默认口径下先行，默认口径写在各 ADR 的「待确认」。
- 用编码代理开发时，让它先读 [AGENTS.md](AGENTS.md)：里面有任务认领与收尾流程、术语与硬规则、提交信息格式。

## 约定

- 文档一律中文；不出现参考平台品牌名，对标项目只写「对标项目」。`python3 scripts/check_docs.py` 会检查。
- 提交信息带任务 id，例如 `feat(runtime): ctx.Effect 逆序释放 [S1-RT-001]`。
- 分支：`main` 为主干；`docs/*`、`feat/*`、`fix/*` 为工作分支，经 PR 合入。
