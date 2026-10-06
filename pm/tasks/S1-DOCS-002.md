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
depends: [S1-DOCS-001, S1-YUE-005, S1-RT-010]
refs:
  - docs/adr/ADR-020-component-model-v2.md#D20.9
  - docs/prd/specs/component-model.md#2
  - docs/adr/ADR-013-frontend-yue.md#D13.18
  - docs/prd/07-s1-foundation.md#7.2.2
updated: 2026-10-07
---

## 背景

「介绍组件如何使用」是文档站的主体。按 ADR-020 D20.9，每个组件的文档源在它自己的文件夹里（`README.md` 含「模型体验」「已知限制」两节，`docs/*.mdx` 为使用指南），文档站在构建前汇总；yue 组件要能在 MDX 里活演示并自动出 API 表；.NET 包与 TypeScript 包的 API 参考由工具生成，不手写。

## 验收

- [ ] 收集脚本把各组件 `README.md` 与 `docs/*.mdx` 汇总进 `website/docs/components/<id>/`，保留组件 id、版本、所属 profile 等元数据到 frontmatter；缺「模型体验」或「已知限制」节的组件使构建失败（与 S1-QA-006 的 README 契约门禁同一规则）
- [ ] `@rspress/plugin-preview` 与 `@rspress/plugin-playground` 接入：yue 组件在 MDX 代码块内活渲染并可编辑；`@rspress/plugin-api-docgen` 为 `@yue/core` 全部组件生成 props 表
- [ ] `@rspress/plugin-typedoc` 生成 `@qian/runtime`、`@qian/plugin-sdk`、`@yue/sdk` 的 API 文档；DocFX 从 XML 注释生成 `Qian.*` .NET 包 API 参考并挂在站点 `/api/dotnet/`；平台 OpenAPI 用 Scalar 渲染在 `/api/http/`
- [ ] `qian docs generate`（S1-RT-010）输出的配置目录、模块图、事件生产 / 消费图进入站点并随 CI 更新
- [ ] UT：收集脚本对缺节、重复 id、断链三类错误各有一个失败用例

## 进度日志

- 2026-10-07 claude：创建任务
