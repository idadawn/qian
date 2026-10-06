# Qian 项目管理（任务即文件）

> 一句话：**任务是 `pm/tasks/` 下的 Markdown 文件，git 是唯一真相源；看板 `pm/index.html` 和进度表 `pm/STATUS.md` 由脚本生成。** 人和编码代理（Codex、Claude Code 等）都用同一套文件、同一套命令，没有第二个系统。

## 1. 为什么这么做

- 文档、任务、代码在同一个仓库里，一次提交同时改需求与任务状态，可评审、可回溯。
- 编码代理最擅长读写文件与跑命令；给它们一个网页后台反而用不起来。
- 看板是纯静态页面，不需要服务器与账号：双击打开、放到 GitHub Pages 或任何静态托管都行。
- 进度不是手填的，是从任务状态算出来的，所以不会和实际脱节。

## 2. 目录

```text
pm/
  README.md        本文
  tasks/           任务文件，一个任务一个文件：<id>.md
  _template.html   看板模板（改样式在这里）
  index.html       生成：看板页面（数据已内嵌，可离线打开）
  tasks.json       生成：任务数据
  STATUS.md        生成：进度概览（GitHub 上直接可读）
scripts/
  pm.py            校验 / 生成 / 新建 / 改状态
  check_docs.py    文档门禁（禁用词、表格、链接）
```

## 3. 一个任务长什么样

文件名就是任务 id，例如 `pm/tasks/S1-RT-001.md`：

```markdown
---
id: S1-RT-001
title: Qian.Runtime 最小运行时：ctx / Provide / Inject / Effect
status: todo            # todo | doing | review | done | blocked
stage: S1               # S1 | S2 | S3 | S4
area: RT                # 领域代码，见下表
wave: 1                 # S1 的四波顺序；决策类任务为 0
priority: P0            # P0 | P1 | P2
type: feature           # feature | fix | decision | doc | infra | test
owner: ""               # 认领人：人名或代理名（codex / claude）
estimate: 3d            # 粗估：0.5d / 1d / 2d / 3d / 5d / 8d
depends: [S1-KUN-001]   # 前置任务，必须真实存在
refs:                   # 对应的设计文档位置
  - docs/prd/07-s1-foundation.md#7.2.1
  - docs/prd/specs/component-model.md#5
updated: 2026-10-06
---

## 背景
一两句话说明做什么、为什么在这一波。

## 验收
- [ ] 从 PRD「验收」里摘出的可勾选条目

## 进度日志
- 2026-10-06 planner：创建任务
```

id 规则：`S<阶段>-<领域>-<三位序号>`。领域代码与 PRD §07 小节对应：

| 代码 | 领域 | 代码 | 领域 |
|---|---|---|---|
| KUN | Kun 缺口修复（7.1） | NTF | 消息通知（7.12） |
| RT | 运行时 / CLI / 市场（7.2） | SCH | 调度与实时（7.13） |
| TEN | 租户（7.3） | ENT | 权益（7.14） |
| IDP | 身份与连接器（7.4） | YUE | 外壳与 yue（7.15） |
| AZ | 鉴权（7.5） | MGW | 模型网关（7.16） |
| DATA | 数据（7.6） | DEP | 部署物料（7.17） |
| FILE | 文件（7.7） | AGT | Agent 就绪（7.18） |
| CFG | 配置（7.8） | QA | 测试 / TCK / 文档门禁 |
| GW | 网关（7.9） | DEC | 待拍板决策 |
| EVT | 事件（7.10） | PM | 流程与协作 |
| DOCS | 文档站与组件文档（ADR-013 D13.18） | — | — |
| AUD | 审计与可观测（7.11） | META / FORM / LIST / WF / … | S2 及以后预留 |

## 4. 日常怎么用

所有命令在仓库根目录执行，只需要 Python 3。

| 想做什么 | 命令 |
|---|---|
| 看进度 | 打开 `pm/index.html`，或在 GitHub 上看 `pm/STATUS.md` |
| 找能开始的任务 | 看板「待办」列已按可做先后排序（绿色「可开始」在前，之后是「第 n 批」）；`pm/STATUS.md` 的「接下来可做」表；或 `python3 scripts/pm.py list --status todo` |
| 认领并开始 | `python3 scripts/pm.py set S1-RT-001 --status doing --owner 你的名字 --note "开始"` |
| 记录进展 | `python3 scripts/pm.py set S1-RT-001 --note "runtime 骨架可编译"` |
| 提交评审 / 完成 | `--status review` / `--status done`，并把「验收」里完成的项打勾 `[x]` |
| 卡住了 | `--status blocked --note "等 S1-DEC-002 拍板"` |
| 新建任务 | `python3 scripts/pm.py add --stage S1 --area FILE --title "..." --priority P0 --wave 2 --estimate 2d --depends S1-RT-001 --ref docs/prd/07-s1-foundation.md#7.7` |
| 校验 | `python3 scripts/pm.py validate` |
| 重新生成看板与进度表 | `python3 scripts/pm.py build` |

**每次改了任务文件，都要 `build` 一次并把生成物一起提交。** CI 会用 `check-fresh` 检查生成物是否过期，过期会失败。设置环境变量 `PM_ACTOR=你的名字` 可以让日志自动署名。

提交信息里带任务 id，例如 `feat(runtime): ctx.Effect 逆序释放 [S1-RT-001]`，这样从任务能追到提交，从提交能追到任务。

## 5. 状态与规则

- `todo → doing → review → done`，任何时候可以进入 `blocked`，解除后回到原状态。
- `done` 的条件：验收条目全部打勾、相关代码或文档已合并、进度日志有一条「完成」记录。
- 一个任务一个负责人；要换人就改 `owner` 并写一条日志。
- 不要在任务文件里写设计，设计写在 `docs/`，任务只放链接（`refs`）。
- 发现任务拆得不对，可以拆分或合并：新建新任务、把旧任务标 `done` 并在日志里写「拆为 …」。
- 待拍板决策是 `type: decision` 的任务（领域 DEC），拍板后把结论写进对应 ADR，再把任务标 `done`。

## 6. 给编码代理的约定

仓库根目录的 `AGENTS.md` 是代理的入口说明，和本文一致。代理开始一个任务前先 `set --status doing --owner <代理名>`，结束时更新验收勾选与状态，并在同一个提交里带上 `build` 生成物。代理不得自行新增领域代码或修改 `pm/_template.html` 以外的生成文件。

## 7. 怎么下发任务

任务只有三种来源，对应三种下发方式；无论哪种，**交接点都是 PR**：代理在分支上做完、开 PR、把任务标 `review`，你评审合并后任务才算 `done`。

### 7.1 派一个现成任务（最常用）

1. 打开看板「阻塞与依赖」视图，左栏是无未完成依赖、可以直接开始的任务；或 `python3 scripts/pm.py list --status todo --area RT`。
2. 把任务 id 交给一个代理或一个人。代理用下面的模板，一字不改地粘给 Codex 或 Claude Code：

```text
读 AGENTS.md 与 pm/README.md。认领任务 <ID>：
python3 scripts/pm.py set <ID> --status doing --owner <你的代理名> --note "开始"
按任务 refs 指向的文档实现，验收条目逐条满足并在任务文件里打勾 [x]。
在分支 feat/<ID>-<简短英文> 上工作，每次提交信息末尾带 [<ID>]。
完成后：python3 scripts/pm.py set <ID> --status review --note "一句话总结"，
再 python3 scripts/pm.py build 并把生成物一起提交，然后开 PR，描述里列出验收完成情况与未决问题。
遇到需要人拍板的问题：新建 DEC 任务（pm.py add --area DEC --type decision --wave 0），把本任务标 blocked，不要自行改设计。
```

3. 你只做两件事：评审 PR（看验收是否真的满足、有没有越过文档）；合并后 `python3 scripts/pm.py set <ID> --status done --note "已合并 PR #n"`（或让代理在合并后做）。

### 7.2 派一个方向（一波或一个领域）

适合让一个代理连续工作几天：

```text
读 AGENTS.md 与 pm/README.md。按依赖顺序完成 <stage> 第 <wave> 波 <AREA> 领域的全部待办任务
（python3 scripts/pm.py list --stage S1 --area KUN --status todo）。
一个任务一个分支一个 PR，流程同单任务；一个任务进入 review 后再开始下一个。
不要同时认领其他领域的任务。
```

并行时按领域分工：同一时间一个领域只给一个代理，`owner` 字段就是锁；两个代理不要同时碰同一个领域，避免冲突。

### 7.3 新需求（任务里还没有）

先问一句：这是改设计，还是在既有设计下加任务？

- **改设计**（新能力、改边界、推翻某条决策）：先改文档——需要决策的新建 DEC 任务并补 ADR，功能的改 PRD 对应章节；文档合并后再拆任务。任务文件里不写设计，只放 `refs` 链接。
- **既有设计下加任务**：一句话描述需求，直接让代理建任务：

```text
读 AGENTS.md。为下面的需求建任务并拆到可在 1–5 天完成的粒度，refs 指向 PRD 对应小节，depends 写清前置，
然后 python3 scripts/pm.py build 并提交，不要开始实现：
<需求描述>
```

你审一下生成的任务文件（标题、验收、依赖是否合理），合并后再按 7.1 派出去。

### 7.4 节奏

- 每天：看 `pm/STATUS.md` 或看板首页的进度条与「进行中」；评审待合并的 PR；处理 `blocked` 与 DEC 任务。
- 每周：`python3 scripts/pm.py stats` 看各波次与领域进度；把拆得太大的任务拆开、过时的标 `done` 并在日志里写「作废：原因」；重新排优先级只改 `priority` 字段。
- 不开第二套系统：不用 GitHub Issues、不用聊天记录当需求；要讨论就在 PR 或任务文件的进度日志里留痕。

## 8. 托管看板（可选）

`pm/index.html` 不依赖任何外部资源，可以：

- 本地双击打开；
- K3s（已部署）：WireGuard 侧打开 <http://10.78.1.161:30810/>，公司内网打开 <http://192.168.1.210/>；每次 `main` 有提交，一分钟内更新。清单与说明见 [deploy/pm-board/README.md](../deploy/pm-board/README.md)；
- 开启 GitHub Pages 指向 `main` 分支的 `/pm` 目录（私有仓库需要 GitHub 的付费方案）；
- 或者放到任何静态托管。
