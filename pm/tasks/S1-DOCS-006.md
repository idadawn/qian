---
id: S1-DOCS-006
title: 产品族地图页、版本矩阵页与全站 project 字段
status: todo
stage: S1
area: DOCS
wave: 2
priority: P1
type: feature
owner: ""
estimate: 1.5d
depends: [S1-DOCS-001]
refs:
  - docs/adr/ADR-021-product-family-structure.md#D21.2
  - docs/adr/ADR-021-product-family-structure.md#D21.5
  - docs/prd/specs/docs-site.md#2b.4
  - docs/prd/specs/docs-site.md#4
updated: 2026-10-08
---

## 背景

产品族（Kun / Qian / yue 与周边仓库）不建大单仓，靠一个组织、一个文档站、一张版本矩阵、一个看板关联（ADR-021 D21.2）。文档站要让读者一眼看到三层关系和「哪个 Qian 配哪个 Kun」，知识库要能按项目过滤。

## 验收

- [ ] `/start/family`（手写）：Kun / Qian / yue / `qian-plugins` / iClaw / 部署与模型服务的层、是什么、仓库、版本与许可、文档位置；与仓库 README「产品族」表同一内容（评审时核对）
- [ ] `/start/versions`（生成）：`website/scripts/build-versions.ts` 从 `Directory.Packages.props`（`KunVersion`、`Qian.*`）、`global.json`（.NET SDK）、pnpm catalog（`@yue/*`、Node）与部署基线（K3s）产出每个文档版本的矩阵；手改即失败
- [ ] frontmatter 新增 `project`（`qian` / `kun` / `yue`，必填）与 `source`（镜像页必填）；校验并入 S1-DOCS-003 的 `check_docs.py` frontmatter 检查（若本任务先完成则先加枚举）；`llms.txt` 按 `project` 分节，`kb-index.json` 每条带 `project` / `version` / `source`
- [ ] 首页一句话定位点明「基于 Kun 框架构建、前端用 yue」；页脚列出产品族仓库与两种许可证（Qian Apache-2.0、Kun MIT）
- [ ] 全站既有页面补 `project: qian`（第 7 部分镜像页另带 `source`）

## 进度日志

- 2026-10-07 claude：创建任务（ADR-021 D21.2 / D21.5）
- 2026-10-08 claude：S1-DOCS-001（PR #3）已先做两处：首页一句话定位与页脚（产品族仓库、Qian Apache-2.0 / Kun MIT）；手写页全部补 project: qian。生成页（components / cli / design）的 project 与镜像页的 source 仍归本任务
