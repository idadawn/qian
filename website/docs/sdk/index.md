---
title: SDK
description: 写组件的编程模型与 API：后端 .NET、前端 TypeScript 与 AI SDK；组件怎么用见组件目录。
part: sdk
audience: [developer, agent]
updated: 2026-10-07
---

# SDK

本部分只写**编程模型与 API**：怎样写一个组件、怎样让 Agent 用上它。某个组件怎么装、怎么配，写在[组件目录](/components/)，两边互相链接、不重复。每节按「编程模型 → 示例 → API 参考」组织。

| 部分 | 包 | 内容 |
|---|---|---|
| [后端 SDK（.NET）](/sdk/dotnet/) | `Qian.Runtime`、`Qian.Plugin.Sdk`、各组件的 `Qian.*` 包 | 上下文 `ctx`、服务注册、类型化事件、可逆副作用、[能力目录](/sdk/dotnet/capabilities)、[事件分发模式](/sdk/dotnet/events) |
| [前端 SDK](/sdk/frontend/) | `@qian/runtime`、`@qian/plugin-sdk`、`@yue/sdk` | 外壳插件树、MF2 远程模块、插槽 |
| [AI SDK](/sdk/ai/) | — | 在插件与前端里调模型与 Embedding、写 Agent 原生表面、MCP、Agent 运行时对接 |

API 参考由工具生成，不手写：.NET 包用 DocFX，TypeScript 包用 TypeDoc（S1-DOCS-002）。Kun 的 API 以 Kun 文档为准，本站不重写。
