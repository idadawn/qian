> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Agent 运行时对接

Qian 不是 Agent 框架。分工（[ADR-016](/pr-preview/pr-202/design/adr/adr-016-iclaw-boundary.md)）：

| 角色      | 负责                                         |
| ------- | ------------------------------------------ |
| Qian    | 能力提供方 + 业务主数据权威：组件、能力目录、权限、审计              |
| iClaw   | Agent 运行时 + AI 资产控制面：Agent loop、沙箱、记忆、技能引擎 |
| Higress | 共用数据面：模型与 MCP 流量                           |
| Kun     | 共用的身份、审计、Outbox 基座                         |

## 接入要点

- **身份**：服务间调用用 S2S JWT，claim 形状与 iClaw 对齐（`iss / aud[] / sub / tenant_id / project_id? / roles[] / scope / session_id`，有效期不超过 300 秒，「加 scope 不加 claim」）。
- **调用路径**（S4）：Agent 不直连 Qian 服务的裸地址，一律经 Higress 的 MCP 路由；令牌绑定受众、禁止透传、最小 scope + 逐步提权；「知道 ID 不构成权限」。
- **工具**：写类工具必须幂等；工具元数据（副作用、策略提示、所需 scope 与密钥名）见 [AI SDK](/pr-preview/pr-202/sdk/ai/index.md)。
- **生成**：Agent 生成应用 = 生成组件包，经 schema 校验、TCK、差异审阅与人工确认；生成的代码只落沙箱，并记录生成来源与审核人。

对接以 iClaw 冻结的契约与代码为准，不依赖其设计态能力。
