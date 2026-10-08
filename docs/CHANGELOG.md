# 变更日志

> 记录对读者可见的变化：新文档与新规格、新组件与命令、破坏性变更。按日期倒序，每条一个二级标题 `## YYYY-MM-DD 标题`，下面一两行说明并链接到详情。文档站首页「最新动态」取最近 4 条（[docs-site.md](prd/specs/docs-site.md) §2a.1）。

## 2026-10-09 yue 工程化基线、八项门禁与 Base UI 验证门（S1-YUE-003）

- 建立 yue 前端工作区：pnpm workspace + `catalog:` + Turborepo，`@yue/*` 八个 S1 包的骨架（tokens / contracts / core / sdk / shell / data / form / biz）、Rslib 构建、Changesets 与 `0.x` 版本策略；六层分层矩阵由 eslint-plugin-boundaries 强制，dependency-cruiser 查循环依赖。
- CI 工作流 `yue` 八项门禁在 PR 上独立显示：lint、typecheck、unit（Vitest 浏览器模式）、a11y（Playwright + axe）、lighthouse（首屏 script ≤ 250 KB）、boundaries、publint、circular。
- Base UI 验证门通过：Menu / Select / Combobox / Tabs / Tooltip 封装在 `@yue/core`，Tree 自建在 `@yue/data`，结论与五项发现记入 ADR-013 §9；靶场 `apps/yue-playground`。

## 2026-10-08 Kun 升级到 2026.10.8（S1-KUN-008）

- `KunVersion` 升到 2026.10.8，带入多租户后台链路修复（S1-KUN-001，上游 idadawn/kun#43）。文档站的「Kun 框架」小节与 Kun API 参考随之切到 `v2026.10.8`。

## 2026-10-08 文档站：Kun ADR 补收、API 页去掉侧栏、产物体积预算（S1-DEC-011）

- Kun 小节补收 ADR「P2-D SaaS 多租户 3-策略架构」。API 命名空间页与类型页不再显示侧栏，产物从约 57 MB 降到约 48 MB，并设 100 MB 预算。API 参考路径统一在 `/sdk/api/`。见 [docs-site.md](prd/specs/docs-site.md) §2b、§7，裁决 R54–R62。

## 2026-10-08 文档站「Kun 框架」小节与 Kun API 参考（S1-DOCS-005）

- SDK 部分新增「Kun 框架」：按 `Directory.Packages.props` 的 `KunVersion`（当前 2026.9.13）从 kun 仓库镜像上手、模块地图、指南、安全、设计说明与 ADR、白皮书与变更日志，另有手写的「与 Qian 的关系」；Kun API 参考由 DocFX 从同版本 `Kun.*` 包生成（[docs-site.md](prd/specs/docs-site.md) §2b、[ADR-021](adr/ADR-021-product-family-structure.md)）。

## 2026-10-08 文档站上线（S1-DOCS-001）

- Rspress 2.0 文档站：七个部分、中文主题、全文搜索、Mermaid，PR 预览与合并后自动发布；设计文档（本目录）在站内「设计与路线」镜像。

## 2026-10-07 产品族结构：Kun / Qian / yue（ADR-021）

- 三层口径「Qian 平台基于 Kun 框架构建，前端使用 yue 组件库」；不建大单仓，产品族仓库统一在 `idadawn`（Kun 已迁入）；Kun 文档将按 `KunVersion` 锁定版本镜像进文档站（[ADR-021](adr/ADR-021-product-family-structure.md)、[docs-site.md](prd/specs/docs-site.md) §2b）。

## 2026-10-07 任务看板上 K3s（S1-PM-002）

- 离线看板经 git-sync + nginx 部署到 K3s，由 Argo CD 托管。

## 2026-10-07 CI 基础：PublicAPI、依赖图与集成测试（S1-QA-001）

- .NET 构建基线、PublicAPI 门禁、依赖图门禁与 Testcontainers fixture（PostgreSQL 16 / Redis 7 / RabbitMQ / Apollo 替身），见仓库 `tests/README.md`。

## 2026-10-07 文档站选型与信息架构

- 选型 Rspress 2.0（[ADR-013](adr/ADR-013-frontend-yue.md) D13.18）；七个部分、组件页模板、frontmatter 与首页规范写入 [docs-site.md](prd/specs/docs-site.md)。

## 2026-10-07 组件模型 v2 与库优先定稿

- [ADR-020](adr/ADR-020-component-model-v2.md) 与 [ADR-019](adr/ADR-019-library-first-packages.md) 转为 Accepted，§7 待确认事项全部拍板。

## 2026-10-07 任务即文件的项目管理（S1-PM-001）

- 任务文件、离线看板、文档门禁 CI 与编码代理入口（`AGENTS.md`）。

## 2026-10-06 组件模型 v2 回填各章

- 一切皆插件、声明式表面、`qian add` 一键安装、seam 三角色、scope、profile / patch 的口径回填 PRD 各章与[组件模型规格](prd/specs/component-model.md)。

## 2026-10-06 设计文档首版

- ADR-001…018、PRD §01–§17、迁移方案 v1 草案，以及调研基线要点汇编与关键决策清单 v0。
