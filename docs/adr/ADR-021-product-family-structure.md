---
title: 产品族结构：Kun / Qian / yue 的定位、仓库关系与统一文档
status: Accepted
date: 2026-10-07
decision_id: D21
supersedes: none
amends: ADR-001, ADR-013
---

# ADR-021 产品族结构：Kun / Qian / yue 的定位、仓库关系与统一文档

> 起因：产品负责人 2026-10-07 提问「Kun 和 Qian 如何定位？是不是创建一个大的项目把各个子项目关联起来？Kun 也需要体现在文档中。」本 ADR 回答这三问，修订 [ADR-001](ADR-001-kun-relationship.md)（补充口径与文档规则，D1.1–D1.8 不变）与 [ADR-013](ADR-013-frontend-yue.md) D13.18（文档站范围扩为产品族）。仓库归属一项由产品负责人拍板（§7、任务 S1-DEC-009），其余条目即日生效。

## 1 背景与问题（Context）

三个名字已经在用：**Kun**（底座框架）、**Qian**（平台）、**yue**（前端组件库），周边还有官方市场仓库 `qian-plugins`、兄弟系统 iClaw、部署仓库与模型服务仓库。它们之间的层次关系、仓库怎么关联、Kun 文档从哪来，此前只在 ADR-001 讲了「Kun 是底座 SDK、以 NuGet + CalVer 锁版本消费」，没有统一口径。现状事实：

- **Kun**：仓库 `CavaiLAB/kun`，私有，MIT；.NET 10 微服务框架，32 个 `Kun.*` 模块；CalVer（`vYYYY.M.D`，最新 `v2026.9.13`）；NuGet 包发到仓库所属组织的 GitHub Packages。文档是仓库内 `docs/` 的 Markdown（白皮书、模块地图、上手教程、6 篇指南、3 篇安全专题、16 份 Kun 自己的 ADR、发布说明与变更日志），**没有文档站**；Kun 还有其它下游（iClaw 等），README 与部分文档面向那些下游写。
- **Qian**：仓库 `idadawn/qian`，单仓：`docs/`、`pm/`、`website/`（文档站，S1-DOCS-001 在途），S1 起 `packages/`、`plugins/`、`profiles/`、`samples/`；`Qian.*` SemVer 锁步，Apache-2.0，S1 发 GitHub Packages 私有 feed（ADR-019 D19.7）。
- **yue**：随 Qian 单仓以 pnpm workspace 交付（PRD §07 7.15.2），`@yue/*` 发私有 npm，Changesets + SemVer。
- **周边**：`qian-plugins` 规定为「与 Qian 同组织下的独立仓库」，但组织名仍是占位（specs/component-model.md §8）；iClaw 源码在 `CavaiLAB/iclaw`（ADR-016）；部署仓库 `idadawn/iclaw-deploy`（ADR-017）；模型服务 `idadawn/MaaS`（ADR-015）。两个 GitHub 所有者并存，意味着两个 GitHub Packages feed。
- **文档**：docs-site.md 首版把 Kun 写成「外链不重写」，但 Kun 没有可外链的站点，且产品负责人在确认信息架构时就把「Kun SDK」列为 SDK 部分的第一节。

要回答的问题：三层怎么说；「大项目」是什么形态；Kun 文档从哪来、谁维护、怎么与 Qian 锁定的 Kun 版本保持一致。

## 2 决策（Decision）

- **D21.1 三层口径**。

  | 层 | 名字 | 是什么 | 什么内容归它 | 版本 |
  |---|---|---|---|---|
  | 框架层 | Kun | 面向任何 .NET 微服务的开发框架，Qian 的底座 SDK | 无产品观点的通用基础设施：Mediator、Outbox、Saga、多租户上下文与 RLS、网关与 S2S、存储、缓存、可观测、测试 | CalVer |
  | 平台层 | Qian | 基于 Kun 的多租户 PaaS：极小运行时 `Qian.Runtime` + 平台插件 + profile + `qian` CLI + 目录市场 | 带多租户或业务语义的能力：租户主档、IdP、权限引擎、带配额与策略的文件、配置中心接入、事件桥接、审计装配、低代码、Agent 表面 | SemVer 锁步 |
  | 前端层 | yue | React 组件库与外壳 | UI 组件、表单 / 表格 / 弹层引擎、MF2 外壳与插槽 | SemVer（Changesets） |

  对外一句话：「**Qian 平台基于 Kun 框架构建，前端使用 yue 组件库**」。不说「Kun 是 Qian 的子项目」（Kun 有独立下游与发布节奏），也不说「Qian 是 Kun 的扩展」（Qian 有自己的运行时与产品语义）。归属判据沿用决策清单 D19 的分界：无产品观点的通用基础设施上游进 Kun，带多租户 / 业务语义的是 Qian 包。ADR-001 D1.1–D1.8 不变。
- **D21.2 不建大单仓、不建 submodule 元仓库**（ADR-001 §4 已否决单仓）。「大项目」= **产品族**，用四样东西关联：
  1. **一个 GitHub 组织**：产品族全部仓库放同一所有者，得到一个 GitHub Packages feed、一份 `packageSourceMapping`、`qian-plugins` 不再占位。推荐 **CavaiLAB**：Kun 与 iClaw 源码已在、产品负责人是管理员、Kun 的 CI 与 feed 零改动；Qian 迁入只需改看板与部署的四处仓库地址。是否迁、何时迁由产品负责人拍板并执行（S1-DEC-009）；拍板前文档继续写 `<org>` 占位。
  2. **一个文档站**：「Qian 文档」覆盖整个产品族。Kun 为第 3 部分 SDK 的第一节 `/sdk/kun/`（导航名「Kun 框架」），内容按 Qian 锁定的 Kun 版本从 kun 仓库镜像并经 DocFX 生成 API（D21.4）。站名不改，首页一句话定位点明基于 Kun、前端 yue。
  3. **一张版本矩阵**：`/start/versions` 自动生成：每个 Qian 版本对应的 Kun CalVer、`Qian.*`、`@yue/*`、.NET SDK、Node、K3s 基线；来源是仓库内的钉版文件（`Directory.Packages.props`、`global.json`、pnpm catalog）。
  4. **一个看板**：跨仓库工作在 Qian 的 `pm/` 跟踪（KUN 领域），kun 仓库的 PR 写进任务 `refs` 与进度日志回链；不另建 GitHub Project。
- **D21.3 Kun 版本单点钉死**。`Directory.Packages.props` 定义 `<KunVersion>`，全部 `Kun.*` 的 PackageVersion 引用 `$(KunVersion)`；`NuGet.config` 的 Kun 源与 `packageSourceMapping` 在首个引用 `Kun.*` 的任务加入（既有约定）；文档同步脚本与版本矩阵读同一属性。升级 Kun = 改这一处 → TCK 两种模式通过 → Kun 文档镜像自动跟随（CI 比对 tag）。
- **D21.4 Kun 文档进站规则**（细则见 [specs/docs-site.md §2b](../prd/specs/docs-site.md)）：
  - 真相源在 kun 仓库 `docs/`，Qian 站**只镜像不改写**，与第 7 部分镜像 `docs/` 同一原则；改 Kun 文档去 kun 仓库提 PR。Qian 自写的只有一页「与 Qian 的关系」（Kun 模块 ↔ 平台插件 / seam 映射、canonical API 选择、Qian 的强制装配与禁用项、补丁包状态）以及首页与导航文案。
  - 镜像范围是白名单：README 的定位与模块矩阵、上手教程、模块地图、设计说明与 Kun 的 ADR、指南、可观测、安全、白皮书、变更日志与发布说明。不镜像面向其它下游的差距分析、路线图与 backlog、AOT 审计与基准报告、贡献者文档、各级 `AGENTS.md`。
  - 每页横幅注明来源路径与 Kun 版本、「Kun 文档与代码不一致处以代码为准（ADR-001 D1.8）」、指向「与 Qian 的关系」页；frontmatter `project: kun`、`version: <CalVer>`、`source: <owner>/kun@<tag>:<path>`。
  - API 参考由 DocFX 从锁定版本的 `Kun.*` 包生成，挂 `/sdk/api/kun/`，与 `Qian.*` 的 `/sdk/api/dotnet/` 并列。
  - 门禁：`sdk/kun/` 为生成目录不接受手改；镜像 tag 与 `KunVersion` 不一致、白名单外文件进站、`KunVersion` 缺失，均构建失败。
  - 公开时点耦合：文档站公开（ADR-019 §7：S2 末或首个外部宿主试用时）会连带公开 Kun 文档；届时 Kun 仓库须同步公开或 Kun 维护人书面同意，否则公开站的 Kun 小节降级为「版本矩阵 + 与 Qian 的关系」两页。
- **D21.5 产品族可见性**。各仓库 README 开头放同一段「产品族地图」（Qian 立即落实；kun、`qian-plugins` 等在下次变更时补一行回链，不作 S1 退出条件）；文档站第 1 部分有 `/start/family` 产品族地图页；全站 frontmatter 增加 `project`（`qian` / `kun` / `yue`），`llms.txt` 按它分节、知识库按它过滤。

## 3 理由（Rationale）

- **同一所有者才有一个 feed。** ADR-019 的目标是「装一个包就能用」；`Qian.*` 传递依赖 `Kun.*`，两个所有者就要配两段源加映射，第三方第一次还原就会卡住。nuget.org 公开前，这是唯一能让一次还原成功的办法。
- **镜像而不是外链或改写。** Kun 没有站点；写 Qian 插件就是在用 Kun 的 Mediator、Outbox、RLS，读者高度重叠；单站搜索与 S2 的「问 AI」才能跨两层回答。复制改写会制造双真相源，而 Kun 文档失真正是 ADR-001 记录过的教训，所以只镜像、带横幅、以代码为准。
- **版本单点与自动镜像。** Qian 对 Kun 是锁版本消费（D1.2），文档必须与锁定版本一致，否则读者看到的是另一个 Kun。
- **不建单仓。** ADR-001 §4 已否决；Kun 有独立下游与发布节奏；在单人维护的现实下合并仓库只会放大耦合。

## 4 备选方案与放弃原因（Alternatives）

- **大单仓 / submodule 元仓库**：否决。前者见 ADR-001 §4；后者对 Codex、Claude Code 的任务流程不友好，锁定的 SHA 容易漂移，也解决不了 feed 问题。
- **新建专用组织**：公开形象更干净，但要同时迁 Kun 的 feed 与 CI，Kun 的其它下游也要改源。留作 S3 公开前的再评估项：届时包已在 nuget.org / npm，组织只影响源码与市场仓库的地址。
- **Kun 自建文档站**：单人维护没有带宽，读者重叠，两套搜索。否决。
- **文档站改用产品族名**：三个名字已经够用，站名「Qian 文档」加一句副标题即可。否决。

## 5 后果与代价（Consequences）

- 正面：三层口径统一，README、PRD、文档站、AGENTS.md 同一说法；Kun 读者有站可读；版本矩阵让「哪个 Qian 配哪个 Kun」一眼可见；一个 feed、一处钉版。
- 负面：CI 需要对私有 kun 仓库的只读访问（deploy key 或细粒度 token）；镜像页继承 Kun 文档的失真，只能靠横幅与 D1.8 兜底；文档站多版本构建时间增加（每个 Qian 版本一份 Kun 镜像）。
- 若采纳迁入 CavaiLAB：GitHub 自动重定向 git 地址，但要改 `deploy/pm-board/deployment.yaml` 的 `GITSYNC_REPO`、`bootstrap.sh` 的 `REPO`、部署仓库里 ArgoCD Application 的 `repoURL` 与仓库凭据 `url`，并在新仓库重新登记只读 deploy key；CI secrets 随仓库迁移。
- 配套任务：S1-DOCS-005（Kun 小节镜像 + API）、S1-DOCS-006（产品族地图、版本矩阵、`project` 字段）、S1-DEC-009（组织归属）；S1-RT-001 验收补 `KunVersion`；AGENTS.md 硬规则两条。

## 6 对各阶段的影响

- **S1**：Kun 小节随文档站第一版上线（S1-DOCS-005 依赖 S1-DOCS-001）；`KunVersion` 在首个引用 `Kun.*` 的任务落地；组织归属拍板。
- **S2**：「问 AI」可跨 Kun / Qian 回答；公开前处理 D21.4 的公开耦合。
- **S3**：ISV 文档用同一站；组织形态再评估。
- **S4**：版本矩阵加入 MCP 协议版本与 Agent 运行时（iClaw）版本。

## 7 待确认事项

- **组织归属与迁移时点**（S1-DEC-009）：推荐 CavaiLAB；由产品负责人拍板并执行迁移，结论写回本节并替换 specs 中的组织占位。
- **Kun 仓库公开时点**：随文档站公开，需 Kun 维护人确认。
- **yue 是否拆独立仓库**：默认不拆，随 Qian 单仓；S3 公开时再议。

## 8 参考

- 相关 ADR：ADR-001（Kun 关系）、ADR-013 D13.18（文档站）、ADR-016（iClaw 边界）、ADR-017（部署）、ADR-019 §7（许可与 feed）、ADR-020 §7 第 4 条（`qian-plugins`）。
- PRD：[§01 愿景](../prd/01-vision.md) 1.1、[§07](../prd/07-s1-foundation.md) 7.1 与 7.15.2；[specs/docs-site.md](../prd/specs/docs-site.md) §2b。
- 流程：[pm/README.md](../../pm/README.md)、[deploy/pm-board/README.md](../../deploy/pm-board/README.md)、[仓库 README](../../README.md)「产品族」一节。
