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
| AUD | 审计与可观测（7.11） | META / FORM / LIST / WF / … | S2 及以后预留 |

## 4. 日常怎么用

所有命令在仓库根目录执行，只需要 Python 3。

| 想做什么 | 命令 |
|---|---|
| 看进度 | 打开 `pm/index.html`，或在 GitHub 上看 `pm/STATUS.md` |
| 找能开始的任务 | 看板「阻塞与依赖」视图左栏；或 `python3 scripts/pm.py list --status todo` |
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

## 7. 托管看板（可选）

`pm/index.html` 不依赖任何外部资源，可以：

- 本地双击打开；
- 开启 GitHub Pages 指向 `main` 分支的 `/pm` 目录（私有仓库需要 GitHub 的付费方案）；
- 或者放到任何静态托管。
