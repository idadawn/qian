> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# SDK

本部分只写**编程模型与 API**：怎样写一个组件、怎样让 Agent 用上它。某个组件怎么装、怎么配，写在[组件目录](/pr-preview/pr-50/components/index.md)，两边互相链接、不重复。每节按「编程模型 → 示例 → API 参考」组织。

| 部分                                                    | 包                                                | 内容                                                                                                                               |
| ----------------------------------------------------- | ------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------- |
| [Kun 框架](/pr-preview/pr-50/sdk/kun/index.md)          | `Kun.*`                                          | 底座框架：按锁定版本从 kun 仓库镜像的上手、模块地图、指南、安全、设计说明与变更日志；Qian 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)                    |
| [后端 SDK（.NET）](/pr-preview/pr-50/sdk/dotnet/index.md) | `Qian.Runtime`、`Qian.Plugin.Sdk`、各组件的 `Qian.*` 包 | 上下文 `ctx`、服务注册、类型化事件、可逆副作用、[能力目录](/pr-preview/pr-50/sdk/dotnet/capabilities.md)、[事件分发模式](/pr-preview/pr-50/sdk/dotnet/events.md) |
| [前端 SDK](/pr-preview/pr-50/sdk/frontend/index.md)     | `@qian/runtime`、`@qian/plugin-sdk`、`@yue/sdk`    | 外壳插件树、MF2 远程模块、插槽                                                                                                                |
| [AI SDK](/pr-preview/pr-50/sdk/ai/index.md)           | —                                                | 在插件与前端里调模型与 Embedding、写 Agent 原生表面、MCP、Agent 运行时对接                                                                               |

[API 参考](/pr-preview/pr-50/sdk/api/index.md)由工具生成，不手写：.NET 包用 DocFX，TypeScript 包用 TypeDoc。[Kun 的 API 参考](/pr-preview/pr-50/sdk/api/kun/index.md)已生成，`Qian.*` 与前端包的随 S1-DOCS-002 加入。

「Kun 框架」小节的内容按仓库 `Directory.Packages.props` 里 `KunVersion` 锁定的版本从 kun 仓库镜像，API 参考由 DocFX 从同一版本的 `Kun.*` 包生成，与 Qian 实际依赖的 Kun 始终是同一版本（[ADR-021](/pr-preview/pr-50/design/adr/adr-021-product-family-structure.md)）。
