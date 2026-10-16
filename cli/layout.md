> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# 组件文件夹布局

> 本页由构建脚本同步自 [组件模型规格 §2](/design/prd/specs/component-model.md)（仓库 `docs/prd/specs/component-model.md`），不另写；请修改规格源文件。

## 2.1 目录树（ADR-020 D20.6 展开）

```text
<plugin>/
  .qian-plugin/plugin.json      清单（§3）。唯一必需文件
  skills/<name>/SKILL.md        技能：front matter + 正文（§4.1）；同目录可放 references/、scripts/
  agents/<name>.md              子 Agent：front matter + 系统提示（§4.2）
  commands/<name>.md            人类斜杠命令：front matter + 命令体（§4.3）
  hooks/hooks.json              钩子：Agent 宿主事件与 Qian 事件的桥接（§4.4）
  .mcp.json                     MCP 服务器声明（§4.5）
  tools/<name>.json             面向模型的工具描述：代码派生或手写（§4.6）
  ui/manifest.json  ui/dist/    yue 远程模块：exposes、页面、控件、设置卡（§4.7）
  metadata/                     permissions.json、roles.json、events.json、message-types.json（v1 provides.* 的落点）
  forms/ lists/ flows/ pages/   低代码元数据（S2 起）
  schemas/                      config.schema.json（有 config 的组件必需）与其它 JSON Schema
  config/qian.patch.yml         对其他条目的 patch；组合包用它插入条目（§4.8、§7.4）
  components/<id>/              组合包内联携带的组件（可选；也可经 runtime.nuget / npm 引用，R28）
  assets/                       图标、截图
  README.md  AGENTS.md          README 含「模型体验」「已知限制与延期项」两节（D20.9）
  src/                          可选：.NET 或 TypeScript 代码 → NuGet / npm
```

- 只有 `.qian-plugin/plugin.json` 必需。清单的表面指针（§3.4 `surfaces`）默认即上述路径，使用默认布局时可不写。
- 目录与文件名全部小写短横线（`<name>` 匹配 `^[a-z][a-z0-9-]*$`）；`SKILL.md`、`README.md`、`AGENTS.md` 例外。
- 组件文件夹可以是 git 子目录、npm 包或 NuGet 包内的 `qian/` 内容目录（§7.3），三种载体布局相同；CLI 与装载器以 `.qian-plugin/plugin.json` 的位置为组件根。
- 组件文件夹是**分发形态**，运行态的真相源是注册表（`platform` profile 下为 `qian.registry`，§7.6）；两者用同一校验器（§10.1）；git 修订就是分发版本（R35）。

## 2.2 表面一览

| 表面           | 路径                            | 进入的注册表                                             | 受 scope 过滤   | S1 热重载      |
| ------------ | ----------------------------- | -------------------------------------------------- | ------------ | ----------- |
| `skills`     | `skills/<name>/SKILL.md`      | `ctx.Tools`（kind = skill）                          | 是            | 是           |
| `agents`     | `agents/<name>.md`            | `ctx.Agents`                                       | 是            | 是           |
| `commands`   | `commands/<name>.md`          | `ctx.Commands`                                     | 是            | 是           |
| `hooks`      | `hooks/hooks.json`            | `ctx.Hooks`                                        | 是            | 是           |
| `mcpServers` | `.mcp.json`                   | `ctx.McpServers`                                   | 是            | 是           |
| `tools`      | `tools/<name>.json`           | `ctx.Tools`（kind = tool）                           | 是            | 手写：是；代码派生：否 |
| `ui`         | `ui/manifest.json`            | `ctx.Ui`（yue 外壳）                                   | 是            | 否           |
| `metadata`   | `metadata/*.json`             | `qian.authz` / `qian.events` / `qian.notify` 各自注册表 | 是            | 是           |
| `schemas`    | `schemas/*.schema.json`       | 契约仓库                                               | 否            | 是           |
| `patch`      | `config/qian.patch.yml`       | 插件树                                                | 条目自带 `scope` | 是           |
| 低代码表面        | `forms/ lists/ flows/ pages/` | S2 定义                                              | 是            | S2 定义       |

## 2.3 无代码组件与带代码组件

- **无代码组件**：无 `src/`，清单无 `runtime.form`。安装只做「复制表面 + 写 patch」，任何 tier 都可发布（§11.1）。典型：只带 `skills/` 的运维技能包、只带 `config/qian.patch.yml` 的环境配置包、只带 `metadata/roles.json` 的角色模板包。
- **带代码组件**：有 `src/`，`runtime.form ∈ { module, service, function, ui, package }`，`runtime.nuget[] / npm[]` 列出发布后的包。代码部分的清单字段（`runtime.entry`、`inject`、`provides`、代码派生的 `tools/*.json`、`configSchema`）由源生成器派生并**合并进同一份 `plugin.json`**（D20.6：不再有手写与派生两份文件）；CI 比对派生结果与仓库清单，漂移即失败（§10.3 G1）。
