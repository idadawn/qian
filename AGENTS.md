# AGENTS.md — 给编码代理的入口说明

这个仓库是 **Qian（乾）** 平台的设计与开发仓库。Qian 是基于 Kun（.NET 10 微服务框架）构建的、以「一切皆插件」组织的 PaaS：先做面向开发人员的技术底座（S1），再做低代码构建（S2）、面向客户的应用市场（S3），终态是 AI 原生 Agent 平台（S4）。

## 先读什么

1. `docs/README.md`：文档总索引。
2. `docs/adr/ADR-020-component-model-v2.md`：组件模型（一切皆插件、声明式表面、`qian add` 一键安装、目录市场）。它取代了 ADR-002/003 中被标记的条目。
3. `docs/prd/specs/component-model.md`：开发者规格，实现时以它为准。
4. `docs/prd/07-s1-foundation.md`：S1 每个组件的范围、技术落点与验收。
5. `pm/README.md`：任务怎么管。

## 任务流程（必须遵守）

- 任务在 `pm/tasks/<id>.md`，一个任务一个文件。开始前认领：
  `python3 scripts/pm.py set <id> --status doing --owner <你的代理名> --note "开始"`
- 结束时把「验收」里完成的项改为 `[x]`，改状态（`review` 或 `done`），并运行
  `python3 scripts/pm.py build`，把 `pm/index.html`、`pm/tasks.json`、`pm/STATUS.md` 一起提交。
- 提交信息末尾带任务 id，如 `feat(runtime): ctx.Effect 逆序释放 [S1-RT-001]`。
- 遇到需要人拍板的问题：不要自作主张改设计。用
  `python3 scripts/pm.py add --stage S1 --area DEC --type decision --wave 0 --title "..."`
  新建决策任务并把当前任务标 `blocked`。
- 不要修改 `pm/index.html`、`pm/tasks.json`、`pm/STATUS.md`（它们是生成物）。

## 写文档与代码的硬规则

- 文档一律中文。文档里不得出现参考平台的品牌名（统一写「参考平台」），也不要写对标项目的名字（写「对标项目」）。`python3 scripts/check_docs.py` 会检查禁用词、表格列数与链接，提交前跑一遍。
- Markdown 表格单元格里的反引号内若有竖线，写成 `\|`。
- 组件设计先回答四个问题：文件夹里有哪些表面、装进哪个 profile、属于哪个 seam 的哪个角色、能否 `qian add`。凡是把能力绑死在特权内核、DI 扩展方法或数据库开关上的写法都是倒退。
- 术语：平台插件（不是「系统插件」）、运行时 `Qian.Runtime`（不是「内核」）、profile（不是「可部署单元」）、`.qian-plugin/plugin.json`（不是 `qian-plugin.yaml`）、租户 scope patch（不是「租户开关」）。
- 代码部分遵循 ADR-019：每个有代码的组件发 `Qian.<X>.Abstractions` + Provider 包；组件库只依赖 `Qian.Runtime`，不依赖 `qian.registry` / `qian.lifecycle`。
- 已有决策不要重开。D1–D18 已获认可；ADR-019/020 为 Proposed，其待拍板项在 `pm/tasks/S1-DEC-*.md`。文档里标「已裁决（R{n}）」的是复核时的默认裁决，有异议时新建决策任务而不是直接改。

## 仓库布局

```text
docs/        设计文档：decisions（决策清单）、adr、prd（含 specs）、migration、research、ROADMAP.md
pm/          任务与看板（见 pm/README.md）
scripts/     pm.py、check_docs.py
.github/     CI：文档门禁 + 任务校验 + 看板新鲜度
```

代码目录（`packages/`、`plugins/`、`profiles/`、`samples/`）在 S1 第一波任务中建立，布局以 `docs/prd/specs/component-model.md` §2 与 §7 为准。

## 本地检查清单

```bash
python3 scripts/check_docs.py && python3 scripts/pm.py validate && python3 scripts/pm.py build
```
