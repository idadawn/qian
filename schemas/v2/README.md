# 声明式表面 Schema v2

依据 [组件模型规格](../../docs/prd/specs/component-model.md)。此目录是 .NET 与 TS 的共同结构契约，不修改 `contracts/v0` 的冻结定义。

- `plugin` 与 `permissions`：组件清单和反向权限。
  `permissions.secrets[]` 为 name/scope 对象，和组件模型的密钥权限声明保持一致；不接受旧的字符串数组。
- `skill`、`agent`、`command`：Markdown front matter。
- `hooks`、`mcp`：事件桥接与 MCP 声明。
- `tool`：引用冻结 capability-entry 的词汇，增加 binding；不携带数据库 id/plugin/kind。
- `profile`：具名 profile 的组合包与启动策略。
- `marketplace`：市场名称、条目、来源、策略与分类；两端共用文档语义向量见 [市场 TCK](../../tck/marketplaces/README.md)。
- `patch`：按 scope 与 id 定位的条目数组。
- `authz-permissions`、`authz-roles`：Authz 拥有的权限声明与默认角色模板 Schema；通用表面层先解析 JSON，Authz 编译器再校验业务结构与引用。
- `files-upload-policies`：组件 `metadata/upload-policies.json` 的上传用途声明，由 qian.files 汇总；用途须以声明组件 ID 开头、同文件不重复，由 Provider 校验。
- `scheduler-jobs`：SDK 从 QianJob 能力请求派生的 metadata/jobs.json 目录；包含 local 类型、能力 URI 与输入/输出 Schema，运行时仍重查实际绑定和 scope。
- `surface-rules.json`：默认路径、固定事件与受限 shell 名称，供两端语义层共用。

所有 Schema 使用 2020-12。结构与静态语义测试见 [TCK](../../tck/surfaces/README.md)。路径检查、目录解析和权限策略不由 Schema 替代。
