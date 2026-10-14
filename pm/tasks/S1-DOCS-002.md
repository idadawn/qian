---
id: S1-DOCS-002
title: 组件文档收集、活演示与 API 参考生成
status: todo
stage: S1
area: DOCS
wave: 2
priority: P0
type: feature
owner: ""
estimate: 5d
depends: [S1-DOCS-001, S1-YUE-005, S1-RT-010, S1-QA-010]
refs:
  - docs/prd/specs/docs-site.md#3
  - docs/adr/ADR-022-rspress-unified-component-docs.md
  - docs/adr/ADR-020-component-model-v2.md#D20.9
  - docs/prd/specs/component-model.md#2
  - docs/adr/ADR-013-frontend-yue.md#D13.18
  - docs/prd/07-s1-foundation.md#7.2.2
updated: 2026-10-15
---

## 背景

「介绍组件如何使用」是文档站的主体。按 ADR-020 D20.9，每个组件的文档源在它自己的文件夹里（`README.md` 含「模型体验」「已知限制」两节，`docs/*.mdx` 为使用指南），文档站在构建前汇总；yue 组件要能在 MDX 里活演示并自动出 API 表；.NET 包与 TypeScript 包的 API 参考由工具生成，不手写。

## 验收

- [ ] 收集脚本把各组件 `README.md` 与 `docs/*.mdx` 按 docs-site.md §3 的平台插件九节模板与 §3.1 的 yue 交互组件模板汇总进 `website/docs/components/`，保留组件 id、版本、所属 profile 等元数据到 frontmatter；缺「模型体验」或「已知限制」节的组件使构建失败（与 S1-QA-006 的 README 契约门禁同一规则）
- [ ] 复用 S1-YUE-005 的 Rspress core 文档、preview、离线 playground 与完整 props 生成接线，接入通用收集管线；不再引入 Storybook，不重复建设 core 展示；检查组件来源、版本与生成目录一致，并保留 Props 完整性、离线及 HTML/SSG-MD 双产物门禁（docs-site.md §3.1）
- [ ] `@rspress/plugin-typedoc` 生成 `@qian/runtime`、`@qian/plugin-sdk`、`@yue/sdk` 的 API 文档并挂在站点 `/sdk/api/frontend/`；DocFX 从 XML 注释生成 `Qian.*` .NET 包 API 参考并挂在 `/sdk/api/dotnet/`（复用 `website/scripts/build-kun-api.ts` 的流水线）；平台 OpenAPI 用 Scalar 渲染在 `/sdk/api/http/`；API 页不进侧栏、不渲染侧栏（R54、R56），并在 `/sdk/api/` 落地页更新状态
- [ ] `qian docs generate`（S1-RT-010）输出的配置目录、模块图、事件生产 / 消费图进入站点并随 CI 更新
- [ ] UT：收集脚本对缺节、重复 id、断链三类错误各有一个失败用例

## 进度日志

- 2026-10-07 claude：创建任务
- 2026-10-07 claude：信息架构（七个部分）经产品负责人确认，写入 specs/docs-site.md 并对齐验收
- 2026-10-08 claude：S1-DOCS-005 已搭好 DocFX 流水线（website/scripts/build-kun-api.ts：隔离目录构建、docfx metadata 输出 Markdown、xref 改写为站内路由与锚点），Qian.* 的 API 参考可复用；/sdk/api/index.md 落地页已为 dotnet/、frontend/ 留行。路径注意：本任务验收写 /api/dotnet/，docs-site.md §2b.2 与 §6 写 /sdk/api/dotnet/，落地页暂按后者
- 2026-10-08 claude：S1-DEC-011 裁决：API 参考统一挂 /sdk/api/{dotnet,frontend,http}/（R56），API 页不进侧栏、不渲染侧栏（R54），验收已按此改；产物总大小预算 100 MB（R55），加 Qian.* 与组件页后若超出，先查重复渲染再调预算
- 2026-10-14 cody：按用户要求与 ADR-022 对齐：复用 YUE-005 的 Rspress core 接入，不再引入 Storybook；保留通用收集和多语言 API 文档范围，补入 Props 完整性、离线及双产物验收。
- 2026-10-15 cody：按 S1-QA-010 验收要求加入其前置依赖，文档预算整改复核完成后才能开工
