---
id: S2-DOCS-001
title: 站内「问 AI」挂件：自有模型网关 + 文档检索 + 引用来源
status: todo
stage: S2
area: DOCS
wave: 1
priority: P1
type: feature
owner: ""
estimate: 5d
depends: [S1-DOCS-003, S1-MGW-001, S1-YUE-005]
refs:
  - docs/adr/ADR-013-frontend-yue.md#D13.18
  - docs/adr/ADR-015-model-gateway.md
  - docs/adr/ADR-016-iclaw-boundary.md
  - docs/prd/10-s4-agent.md
updated: 2026-10-07
---

## 背景

Rspress 自身不带站内 AI 聊天，这一块按 ADR-013 D13.18 用自己的栈实现：检索基于 S1-DOCS-003 产出的 `.md` 与 `kb-index.json`（由产品负责人规划的知识库承载），模型调用经模型网关（Higress + GPUStack，ADR-015），Agent 运行时侧按 ADR-016 复用 iClaw（经 MCP / HTTP），挂件本身是一个 yue 组件。

## 验收

- [ ] yue 组件 `<AskDocs>` 可嵌入文档站右下角与搜索框，支持流式回答，回答末尾列出来源页链接（至少 1 条、最多 5 条）
- [ ] 检索走知识库接口，命中内容来自 S1-DOCS-003 的 `.md` 产物；知识库不可用时自动降级为站内搜索并明示
- [ ] 模型调用只经模型网关，不直连任何第三方模型服务；网关侧有租户与配额计量（ADR-015）
- [ ] 默认不保存对话内容；是否记录可在站点配置开关，并在界面明示
- [ ] IT：对 20 条「组件如何使用」类问题的回答全部给出正确来源页（人工评审集，纳入发布前检查）

## 进度日志

- 2026-10-07 claude：创建任务
