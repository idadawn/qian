> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# AI SDK

「AI SDK」是对外统一的名称，覆盖三件事：调模型、让 Agent 用上你的组件、与 Agent 运行时对接。Qian **不写 Agent loop**、沙箱、记忆与技能引擎，这些由 iClaw 提供（[ADR-016](/pr-preview/pr-181/design/adr/adr-016-iclaw-boundary.md)）。

## 调模型与 Embedding

经[模型网关](/pr-preview/pr-181/sdk/ai/gateway.md)的 OpenAI 兼容端点调用，用逻辑模型名（如 `chat-default`）而不是供应商的模型名；按租户的配额、限流与计量在平台侧完成，调用方不自己记账。S1 先接 LLM 与 Embedding。

## 让 Agent 用上你的组件

- **声明式表面**：skills、agents、commands、hooks、tools 写在组件文件夹里，装组件即生效，S1 就是一等公民（见[表面](/pr-preview/pr-181/start/concepts/surface.md)、[文件格式](/pr-preview/pr-181/cli/surfaces.md)）。
- **工具元数据**：每个工具声明 `sideEffect`、`policyHint`、`requiredScopes`、`requiredSecrets`（只写名字）、`schemaSha256` 与 `contractVersion`，与 iClaw 契约对齐；写类工具必须幂等。
- **能力目录**：工具定义由[能力目录](/pr-preview/pr-181/sdk/dotnet/capabilities.md)机械生成，暴露哪些、怎么描述单独策划。
- **MCP**：组件可以附带 `.mcp.json`；每租户的 MCP 能力门面在 S4，见 [MCP](/pr-preview/pr-181/sdk/ai/mcp.md)。

## 与 Agent 运行时对接

见 [Agent 运行时对接](/pr-preview/pr-181/sdk/ai/agent-runtime.md)；隔离执行 Agent 生成的代码见[沙箱](/pr-preview/pr-181/sdk/ai/sandbox.md)（S4）。
