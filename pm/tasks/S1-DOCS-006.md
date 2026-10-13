---
id: S1-DOCS-006
title: 产品族地图页、版本矩阵页与全站 project 字段
status: done
stage: S1
area: DOCS
wave: 2
priority: P1
type: feature
owner: "claude"
estimate: 1.5d
depends: [S1-DOCS-001]
refs:
  - docs/adr/ADR-021-product-family-structure.md#D21.2
  - docs/adr/ADR-021-product-family-structure.md#D21.5
  - docs/prd/specs/docs-site.md#2b.4
  - docs/prd/specs/docs-site.md#4
updated: 2026-10-09
---

## 背景

产品族（Kun / Qian / yue 与周边仓库）不建大单仓，靠一个组织、一个文档站、一张版本矩阵、一个看板关联（ADR-021 D21.2）。文档站要让读者一眼看到三层关系和「哪个 Qian 配哪个 Kun」，知识库要能按项目过滤。

## 验收

- [x] `/start/family`（手写）：Kun / Qian / yue / `qian-plugins` / iClaw / 部署与模型服务的层、是什么、仓库、版本与许可、文档位置；与仓库 README「产品族」表同一内容（评审时核对）——`website/scripts/family.test.ts` 自动核对前五列逐行一致
- [x] `/start/versions`（生成）：`website/scripts/build-versions.ts` 从 `Directory.Packages.props`（`KunVersion`、`Qian.*`）、`global.json`（.NET SDK）、pnpm catalog（`@yue/*`、Node）与部署基线（K3s）产出每个文档版本的矩阵；手改即失败——pnpm 还没有 catalog，`@yue/*` 按工作区通配找包（现为「未建立」）；K3s 新增钉版文件 `deploy/baseline.json`（v1.35，S1-DEP-001 维护）；`versions.md` 进 `.gitignore`，进了 Git 文档门禁即失败
- [x] frontmatter 新增 `project`（`qian` / `kun` / `yue`，必填）与 `source`（镜像页必填）；校验并入 S1-DOCS-003 的 `check_docs.py` frontmatter 检查（若本任务先完成则先加枚举）；`llms.txt` 按 `project` 分节，`kb-index.json` 每条带 `project` / `version` / `source`——校验器与 kb-index 由 S1-DOCS-003（PR #12）完成，`llms.txt` 分节由本任务的 `website/scripts/llms.ts` 完成（Qian → Kun → yue，项目下再按部分）
- [x] 首页一句话定位点明「基于 Kun 框架构建、前端用 yue」；页脚列出产品族仓库与两种许可证（Qian Apache-2.0、Kun MIT）
- [x] 全站既有页面补 `project: qian`（第 7 部分镜像页另带 `source`）——手写页随 S1-DOCS-001，生成页（组件、CLI、设计镜像含 `source`）随 S1-DOCS-003

## 进度日志

- 2026-10-07 claude：创建任务（ADR-021 D21.2 / D21.5）
- 2026-10-08 claude：S1-DOCS-001（PR #3）已先做两处：首页一句话定位与页脚（产品族仓库、Qian Apache-2.0 / Kun MIT）；手写页全部补 project: qian。生成页（components / cli / design）的 project 与镜像页的 source 仍归本任务
- 2026-10-09 claude：开始（分支叠在 S1-DOCS-003 的 PR #12 之上，复用其 frontmatter 解析与 kb-index）
- 2026-10-09 claude：产品族页 `start/family.md`（与 README 表同内容，单测核对）；`build-versions.ts` 生成 `start/versions.md`（Kun 2026.10.8、Qian.* 5 个包未发布、@yue 未建立、.NET SDK 10.0.100、Node / pnpm、K3s v1.35）；`llms.ts` 让 llms.txt 先按 project 分节；check_docs 把 `start/versions.md` 当生成文件（默认跳过、进 Git 即失败、路由延后）。本地：616 页、产物 58.0 MB，网站单测 65、门禁自测 34、check_docs 218 + 生成页 588 通过。分支叠在 PR #12 之上
- 2026-10-09 claude：完成：产品族页、生成的版本矩阵（含 deploy/baseline.json 钉 K3s）、llms.txt 按 project 分节；五项验收满足，PR 叠在 S1-DOCS-003 的 PR #12 之上
- 2026-10-09 claude：已合并 PR #13（含版本矩阵识别 @idadawn/yue-* 的补充提交）
