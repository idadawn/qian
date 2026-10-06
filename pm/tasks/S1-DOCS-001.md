---
id: S1-DOCS-001
title: 文档站脚手架：Rspress 2.0 + MDX + 中文主题 + 搜索 + 托管
status: todo
stage: S1
area: DOCS
wave: 2
priority: P0
type: infra
owner: ""
estimate: 3d
depends: [S1-QA-001]
refs:
  - docs/prd/specs/docs-site.md
  - docs/adr/ADR-013-frontend-yue.md#D13.18
  - docs/adr/ADR-020-component-model-v2.md#D20.9
  - docs/prd/14-quality.md
updated: 2026-10-07
---

## 背景

产品负责人 2026-10-07 确认文档站选型为 Rspress 2.0（ADR-013 D13.18）。本任务只搭骨架：站点工程、主题、搜索、构建与托管，让后续的组件文档收集（S1-DOCS-002）与 llms 输出（S1-DOCS-003）有落点。站点源码放在仓库 `website/`，文档内容本身仍以各组件文件夹与 `docs/` 为源。

## 验收

- [ ] `website/` 为 Rspress 2.0 工程（`@rspress/core`，MDX，Node ≥ 20，pnpm），`pnpm --filter website build` 产出纯静态站点，可离线打开
- [ ] 按 `docs/prd/specs/docs-site.md` §2 建立七个部分（开始 / 组件目录 / SDK / CLI 与规格 / 运维 / 市场与 ISV / 设计与路线），路径与版本策略一致；`website/README.md` 落 §6 目录骨架；首页中文，主题色接入 `@yue/tokens` 的 CSS 变量，明暗两态
- [ ] 首页按 docs-site.md §2a 实现：三条起步命令、「配置你的编码代理」区块、最新动态、五栏产品能力（用 AI 构建 / 构建应用 / 多租户平台 / 保护应用 / 部署与运维）、指南；无公司信息，规划中能力带阶段标记
- [ ] 内置全文搜索可用；Shiki 高亮覆盖 C# / TypeScript / JSON / YAML / Bash；Mermaid 图可渲染
- [ ] CI：PR 构建站点并产出预览链接（Cloudflare Pages 或 K3s 静态托管二选一，记录在 `website/README.md`），`main` 合并后自动发布
- [ ] `scripts/check_docs.py` 的检查范围扩展到 `website/docs`，禁用词与断链在站点内容上同样生效

## 进度日志

- 2026-10-07 claude：创建任务（选型确认见 ADR-013 D13.18）
- 2026-10-07 claude：信息架构（七个部分）经产品负责人确认，写入 specs/docs-site.md 并对齐验收
- 2026-10-07 claude：首页与产品导航按产品负责人提供的对标产品文档首页借鉴，写入 specs/docs-site.md §2a
