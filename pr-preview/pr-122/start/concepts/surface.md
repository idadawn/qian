> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# 表面

**表面**（surface）是组件向外暴露的一类声明式内容，也是组件模型的顶层分类：

| 表面           | 路径                                  | 进入的注册表                             |
| ------------ | ----------------------------------- | ---------------------------------- |
| `skills`     | `skills/<name>/SKILL.md`            | `ctx.Tools`（kind = skill）          |
| `agents`     | `agents/<name>.md`                  | `ctx.Agents`                       |
| `commands`   | `commands/<name>.md`                | `ctx.Commands`                     |
| `hooks`      | `hooks/hooks.json`                  | `ctx.Hooks`                        |
| `mcpServers` | `.mcp.json`                         | `ctx.McpServers`                   |
| `tools`      | `tools/<name>.json`                 | `ctx.Tools`（kind = tool）           |
| `ui`         | `ui/manifest.json`                  | `ctx.Ui`（yue 外壳）                   |
| `metadata`   | `metadata/*.json`                   | 权限点、事件类型、消息类型等，进鉴权 / 事件 / 通知各自的注册表 |
| `schemas`    | `schemas/*.schema.json`             | 契约仓库                               |
| `patch`      | `config/qian.patch.yml`             | 插件树（组件自带的默认条目）                     |
| 低代码表面        | `forms/` `lists/` `flows/` `pages/` | S2 定义                              |

除 `schemas` 外，表面内容都受 [scope](/pr-preview/pr-122/start/concepts/scope.md) 过滤；多数声明式表面在 S1 支持热重载。

skill、agent、command、hook、MCP、tool 这些 Agent 原生表面在 S1 就是一等公民，不是 S4 才有（[ADR-020 D20.9](/pr-preview/pr-122/design/adr/adr-020-component-model-v2.md)）。所有模型可见的内容都注册在 `ctx.Tools` 等注册表里。

`qian add <组件> --surfaces skills,commands` 只装一部分表面；`npx qian@latest --skill <组件>` 只把技能装进编码代理。文件格式见 [声明式表面的文件格式](/pr-preview/pr-122/cli/surfaces.md)。
