---
title: AI SDK
description: 在插件与前端里调模型与 Embedding，写 skills / agents / commands / hooks / tools 表面，声明工具副作用与策略；Qian 不写 Agent loop。
part: sdk
project: qian
audience: [developer, agent]
updated: 2026-10-07
---

# AI SDK

「AI SDK」是对外统一的名称，覆盖三件事：调模型、让 Agent 用上你的组件、与 Agent 运行时对接。Qian **不写 Agent loop**、沙箱、记忆与技能引擎，这些由 iClaw 提供（[ADR-016](/design/adr/adr-016-iclaw-boundary)）。

## 调模型与 Embedding

经[模型网关](/sdk/ai/gateway)的 OpenAI 兼容端点调用，用逻辑模型名（如 `chat-default`）而不是供应商的模型名；按租户的配额、限流与计量在平台侧完成，调用方不自己记账。S1 先接 LLM 与 Embedding。

## 让 Agent 用上你的组件

- **声明式表面**：skills、agents、commands、hooks、tools 写在组件文件夹里，装组件即生效，S1 就是一等公民（见[表面](/start/concepts/surface)、[文件格式](/cli/surfaces)）。
- **工具元数据**：每个工具声明 `sideEffect`、`policyHint`、`requiredScopes`、`requiredSecrets`（只写名字）、`schemaSha256` 与 `contractVersion`，与 iClaw 契约对齐；写类工具必须幂等。
- **能力目录**：工具定义由[能力目录](/sdk/dotnet/capabilities)机械生成，暴露哪些、怎么描述单独策划。
- **MCP**：组件可以附带 `.mcp.json`；每租户的 MCP 能力门面在 S4，见 [MCP](/sdk/ai/mcp)。

## 与 Agent 运行时对接

见 [Agent 运行时对接](/sdk/ai/agent-runtime)；隔离执行 Agent 生成的代码见[沙箱](/sdk/ai/sandbox)（S4）。
