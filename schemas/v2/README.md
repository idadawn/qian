# 声明式表面 Schema v2

依据 [组件模型规格](../../docs/prd/specs/component-model.md)。此目录是 .NET 与 TS 的共同结构契约，不修改 `contracts/v0` 的冻结定义。

- `plugin` 与 `permissions`：组件清单和反向权限。
- `skill`、`agent`、`command`：Markdown front matter。
- `hooks`、`mcp`：事件桥接与 MCP 声明。
- `tool`：引用冻结 capability-entry 的词汇，增加 binding；不携带数据库 id/plugin/kind。
- `patch`：按 scope 与 id 定位的条目数组。
- `surface-rules.json`：默认路径、固定事件与受限 shell 名称，供两端语义层共用。

所有 Schema 使用 2020-12。结构与静态语义测试见 [TCK](../../tck/surfaces/README.md)。路径检查、目录解析和权限策略不由 Schema 替代。
