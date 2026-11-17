> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# 声明式表面的文件格式

> 本页由构建脚本同步自 [组件模型规格 §4](/pr-preview/pr-202/design/prd/specs/component-model.md)（仓库 `docs/prd/specs/component-model.md`），不另写；请修改规格源文件。

通用规则：Markdown 表面 = YAML front matter + 正文（front matter 按各自 Schema 校验，正文原样交给消费者）；JSON 表面按 JSON Schema 2020-12 校验、`additionalProperties: false`、接受 `^x-`。表面内的名字在组件内唯一，全局形式由注册表拼接为 `<component-id>/<name>`（如 `qian.files/upload`），模型看到的是全局形式经 scope 过滤后的集合。所有注册带条目的 `scope`，scope 内同名遮蔽全局（§5.5）。密钥只以 `secretRef://…` 出现（§11.3）。

## 4.1 `skills/<name>/SKILL.md`

| 字段                    | 类型                               | 必填 | 说明                                                          |
| --------------------- | -------------------------------- | -- | ----------------------------------------------------------- |
| `name`                | `^[a-z][a-z0-9-]*$` ≤ 64         | 是  | 与目录名一致                                                      |
| `description`         | 20–1024 字                        | 是  | **意图式**：何时用、何时不用；Agent 运行时据此决定是否加载正文                        |
| `allowed-tools`       | `string[]`                       | 否  | 正文可调用的工具全局名或通配（`qian.files/*`）；省略 = 继承会话默认；仍受 scope 过滤与权限判定 |
| `license`             | `string`                         | 否  | 省略 = 继承组件 `license`                                         |
| `metadata.version`    | SemVer                           | 否  | 技能自身版本，用于签名载荷与变更追踪                                          |
| `metadata.visibility` | `public \| internal`，默认 `public` | 否  | `internal` 只能被同组件的 `agents/`、`commands/` 引用，不进目录与市场详情       |
| `metadata.surface`    | `const skill`                    | 否  | 表面类型标记                                                      |

```markdown
---
name: upload-with-policy
description: 当用户要把文件存进 Qian 并需要受上传策略（类型、大小、配额）约束时使用；不用于批量迁移历史文件（请用 files-migration 技能），也不用于生成下载链接以外的分享方式。
allowed-tools: [qian.files/upload, qian.files/get-download-url, qian.files/get-quota]
license: Apache-2.0
metadata: { version: 1.0.0, visibility: public, surface: skill }
---

# 受策略约束的上传

## 步骤
1. 先调用 `qian.files/get-quota` 确认剩余配额；不足时停止并告知用户剩余字节数。
2. 用 `qian.files/upload` 上传，`purpose` 取 `attachment | avatar | export` 之一；MIME 类型交给服务端嗅探。
3. 成功后用 `qian.files/get-download-url` 生成链接，默认 24 小时有效；用户要求更长时说明最长 7 天。

## 不要做
- 不要把文件内容写进对话；上传失败不要重试超过 2 次，第 3 次把错误码原样给用户。

参考：`references/upload-policy.md`（上传策略字段说明）
```

## 4.2 `agents/<name>.md`

文件名（去 `.md`）= `name`；正文是系统提示。

| 字段                 | 类型                  | 必填 | 说明                                                                                                     |
| ------------------ | ------------------- | -- | ------------------------------------------------------------------------------------------------------ |
| `name`             | `^[a-z][a-z0-9-]*$` | 是  | 与文件名一致                                                                                                 |
| `description`      | 20–1024 字           | 是  | 意图式：什么任务应委派给它                                                                                          |
| `tools`            | `string[]`          | 否  | 允许的工具全局名 / 通配；省略 = 继承父会话                                                                               |
| `model`            | `string`            | 否  | 模型别名（`default`、`fast`、`reasoning`…），由 `qian.model-gateway` 的 `model-capabilities` 解析（ADR-015）；S1 只校验结构 |
| `skills`           | `string[]`          | 否  | 预加载技能的全局名（可含本组件 `internal` 技能）                                                                         |
| `metadata.surface` | `const agent`       | 否  | 表面类型标记                                                                                                 |

```markdown
---
name: onboarding-checker
description: 当需要对一个租户做上线前的只读核对（身份连接器、默认角色、文件配额、通知渠道）并产出核对报告时委派给它；它不做任何修改。
tools: [qian.identity/list-connectors, qian.authz/list-roles, qian.files/get-quota, qian.notify/list-channels]
model: fast
skills: [com.example.skills.tenant-onboarding/tenant-onboarding]
metadata: { surface: agent }
---

你是租户上线核对员。你只读不写：任何一步看起来需要修改配置时停下来，把需要修改的项列出来交给人。
核对顺序固定为：身份连接器 → 默认角色 → 文件配额 → 通知渠道；每项输出「通过 / 不通过 / 无法判断」与一句话依据。
最终输出一份 Markdown 表格，末尾给出「可以上线 / 不可以上线」的结论，不给结论以外的建议。
```

## 4.3 `commands/<name>.md`

人类斜杠命令：以 `/<component 短名>:<name>`（无冲突时可省前缀）调用。命令定义**不进入模型消息**（D20.6）：宿主把展开后的命令体作为一条用户消息发送。

| 字段                 | 类型              | 必填 | 说明                                           |
| ------------------ | --------------- | -- | -------------------------------------------- |
| `description`      | ≤ 200 字         | 是  | 命令列表里的一行说明                                   |
| `allowed-tools`    | `string[]`      | 否  | 命令体执行期间允许的工具；内联 shell 需列出 `shell(<argv 前缀>)` |
| `argument-hint`    | `string`        | 否  | 如 `<tenant-id> [--strict]`                   |
| `metadata.surface` | `const command` | 否  | 表面类型标记                                       |

命令体替换：`$ARGUMENTS`（命令名后的全部文本）、`$1`、`$2`…（按空白切分）、`` !`<argv>` ``（内联 shell：发送前执行并用 stdout 替换；必须被 `allowed-tools` 的 `shell(...)` 覆盖，执行规则 §11.4）、`@<相对路径>`（内联组件内文件）。

```markdown
---
description: 显示某租户的文件配额使用情况，并给出是否需要扩容的判断
allowed-tools: [qian.files/get-quota, "shell(qian dump-config)"]
argument-hint: <tenant-id>
metadata: { surface: command }
---

请检查租户 `$1` 的文件配额：
1. 调用 `qian.files/get-quota`，租户为 `$1`。
2. 当前 profile 中 `files` 条目的配置如下，用于核对配额上限是否与配置一致：

!`qian dump-config --entry files --format json`

3. 若已用量超过上限的 80%，给出扩容建议并说明需要修改的 patch 条目；否则回答「配额充足」。
```

## 4.4 `hooks/hooks.json`

钩子把事件桥接到三种执行方式。两类事件共用一份文件：**Agent 宿主事件**（大驼峰，由 Agent 运行时触发）与 **Qian 事件**（`<domain>/<event>`，由运行时事件总线触发）。

```json
{
  "$schema": "https://qian.dev/schemas/hooks/v2",
  "hooks": {
    "PreToolUse": [
      { "matcher": "qian\\.files/(upload|delete)",
        "hooks": [{ "type": "plugin", "plugin": "qian.files", "handler": "enforceUploadPolicy" }] }
    ],
    "PostToolUse": [
      { "matcher": "qian\\.files/.*",
        "hooks": [{ "type": "plugin", "plugin": "qian.audit", "handler": "recordToolUse" }] }
    ],
    "tenant/purged": [
      { "hooks": [
          { "type": "plugin", "plugin": "qian.files", "handler": "purgeTenantFiles" },
          { "type": "http", "url": "https://hooks.example.internal/qian/files-purged",
            "headers": { "Authorization": "secretRef://platform/hooks/files-webhook" }, "timeout": 10, "retries": 2 } ] }
    ],
    "files/uploaded": [
      { "matcher": "export",
        "hooks": [{ "type": "command", "command": ["qian-files-indexer", "--file", "{{payload.fileId}}"], "timeout": 60 }] }
    ]
  }
}
```

`matcher` 是正则，匹配事件的主体（工具全局名 / 事件 subject）；省略 = 全部。`{{payload.<path>}}` 是 `command` / `http` 的参数模板，只能引用载荷中的标量。

事件名矩阵（S1）。已裁决（R39）：Qian 事件行由 hooks Schema 固定（含分发模式）；Agent 宿主事件行是 S1 对接 Agent 运行时（iClaw）的透传约定，其定义与分发模式的权威在 Agent 运行时，Qian 只校验大驼峰命名：

| 事件                                                                   | 来源                          | 分发模式（§5.4）             | `matcher` 对象 | 载荷要点                                              | 典型用途                                                      |
| -------------------------------------------------------------------- | --------------------------- | ---------------------- | ------------ | ------------------------------------------------- | --------------------------------------------------------- |
| `SessionStart` / `SessionEnd`                                        | Agent 宿主                    | `emit`                 | 会话来源         | `sessionId`、`tenantId`、`scope`                    | 注入租户说明；清理 agent-session scope                             |
| `UserPromptSubmit`                                                   | Agent 宿主                    | `waterfall`            | —            | `prompt`                                          | 敏感词拦截、补充上下文                                               |
| `PreToolUse`                                                         | Agent 宿主                    | `waterfall`            | 工具全局名        | `tool`、`input`                                    | 阻断 / 改写输入；二次确认                                            |
| `PostToolUse`                                                        | Agent 宿主                    | `parallel`             | 工具全局名        | `tool`、`input`、`output`                           | 审计、派生副作用                                                  |
| `SubagentStop` / `Stop`                                              | Agent 宿主                    | `emit` / `bail`        | 子 Agent 名    | `agent`、`result`                                  | 汇总；决定是否继续                                                 |
| `tenant/provisioned`                                                 | Qian `Tenancy`              | `serial`               | 租户 id        | `tenantId`、`plan`                                 | 逐租户迁移、默认角色实例化                                             |
| `registry/entry-mounted` / `registry/entry-disabled`                 | Qian `Registry`（`platform`） | `serial`               | 条目 id        | `scope`、`entryId`、`plugin`                        | 条目在某 scope 挂载 / 禁用后的收尾（取代 v1 Enabled / Disabled 状态变更，R34） |
| `tenant/purging` → `tenant/purged`                                   | Qian `Tenancy`              | `waterfall` → `serial` | 租户 id        | `tenantId`、`retentionDays`                        | 阻止 / 延后清除；物理删除本插件数据                                       |
| `identity/signing-in` → `identity/signed-in` / `identity/signed-out` | Qian `Identity`             | `waterfall` → `emit`   | 连接器名         | `personId`、`membershipId`、`sessionId`、`connector` | 风控、强制 MFA；登录通知                                            |
| `identity/member-disabled`                                           | Qian `Identity`             | `parallel`             | 租户 id        | `membershipId`                                    | 吊销会话、回收授权                                                 |
| `files/uploading` → `files/uploaded`                                 | Qian `Files`                | `waterfall` → `emit`   | `purpose`    | `descriptor`、`policy` / `fileId`、`sizeBytes`      | 改写上传策略、扫描前置；索引、计量                                         |
| `files/deleting` → `files/deleted`                                   | Qian `Files`                | `bail` → `emit`        | `purpose`    | `fileId`                                          | 返回非空即否决删除；索引清理                                            |

Agent 宿主事件（大驼峰）由 Agent 运行时定义并透传：校验器只校验命名形式，未知名报 warning；Qian 事件（`<domain>/<event>`）必须在 hooks Schema 固定的矩阵或文档生成器产出的事件目录中，未知名报 error（R39）。完整 Qian 事件目录由代码生成（D20.9 事件图）。

执行方式：

| `type`    | 字段                                                                                           | 语义                                                                                                                     | 允许的 tier（§11.4）         |
| --------- | -------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- | ----------------------- |
| `command` | `command: string[]`（argv，不经 shell）、`timeout`（默认 30s，上限 600s）、`env?`（白名单键，值可为 `secretRef://`） | 受限执行器运行；stdin 收事件 JSON 信封；`waterfall` / `bail` 读 stdout JSON 作返回值；退出码非 0 = 失败                                          | `system`、`official`     |
| `http`    | `url`、`method?`（默认 `POST`）、`headers?`、`timeout`（默认 10s）、`retries?`（≤ 3）                      | POST 事件信封（ADR-016：`version / event_id / subject / occurred_at / tenant_id / payload`）；2xx 成功；`waterfall` / `bail` 读响应体 | 全部；域名须在 `policy.egress` |
| `plugin`  | `plugin`（组件 id）、`handler`（该组件在 `Apply` 中以 `ctx.Hooks().Expose(name, fn)` 暴露）                 | 进程内调用                                                                                                                  | 由被调用组件的 tier 决定         |

`tenant/provisioned` 的默认角色监听（qian.authz）使用 priority=1000，组件 hook 桥接使用默认 priority=0；运行时按优先级降序执行，同优先级才按登记顺序。默认角色先创建，组件 hook 随后执行，初始主体授予仍在整个派发之后。

## 4.5 `.mcp.json`

平台形态按 ADR-016 D16.10：MCP 服务器按租户逻辑门面托管在 Higress，不是每插件一个进程；平台宿主只接受 `http`，`stdio` 只在开发者本机与 `standalone` / `minimal` profile 生效。

```json
{
  "$schema": "https://qian.dev/schemas/mcp/v2",
  "mcpServers": {
    "qian-files": {
      "type": "http",
      "url": "https://mcp.example.internal/qian/{tenant}/files",
      "headers": { "Authorization": "Bearer secretRef://tenant/qian.files/mcp-token" },
      "scopes": ["tenant", "agent-session"]
    },
    "files-local-dev": {
      "type": "stdio",
      "command": "npx", "args": ["-y", "@qian/mcp-files@1"],
      "env": { "QIAN_BASE_URL": "http://localhost:5000", "QIAN_TOKEN": "secretRef://dev/qian/token" },
      "profiles": ["standalone", "minimal"]
    }
  }
}
```

| 字段                         | 说明                                                                |
| -------------------------- | ----------------------------------------------------------------- |
| `type`                     | `http`（无状态 HTTP，MCP 2026-07-28 目标版本，ADR-016 D16.4）或 `stdio`       |
| `url` / `headers`          | `http` 用；`url` 可含 `{tenant}` 占位由门面按 scope 替换；敏感头必须 `secretRef://` |
| `command` / `args` / `env` | `stdio` 用；`env` 明文密钥校验失败                                          |
| `scopes` / `profiles`      | 可见的 scope 种类（默认取组件 `scopes[]`）/ 只在这些 profile 注册（默认不限）             |

## 4.6 `tools/<name>.json`

面向模型的工具描述：带代码组件由源生成器从 `[Capability]` 派生，无代码组件手写。字段沿 v1 `capabilities[]` 的 Agent 字段（§3.6），加 `binding`。约束沿 v1：`sideEffect ≠ read ⇒ idempotent = true ∧ inputSchema 含 idempotencyKey`；`inputSchema` 不得含租户字段；`description` ≥ 10 字且意图式。

```json
{
  "$schema": "https://qian.dev/schemas/tool/v2",
  "name": "upload",
  "description": "把一个文件上传到当前租户的文件库并返回文件 id；用于附件、头像、导出件。不用于批量迁移历史文件。",
  "inputSchema": { "type": "object", "additionalProperties": false, "required": ["content", "fileName", "purpose", "idempotencyKey"],
    "properties": { "content": { "type": "string", "contentEncoding": "base64" }, "fileName": { "type": "string", "maxLength": 255 },
                    "purpose": { "enum": ["attachment", "avatar", "export"] }, "idempotencyKey": { "type": "string", "format": "uuid" } } },
  "outputSchema": { "type": "object", "required": ["fileId"], "properties": { "fileId": { "type": "string" }, "sizeBytes": { "type": "integer" } } },
  "sideEffect": "write", "policyHint": "confirm", "idempotent": true, "async": "none",
  "requiredScopes": ["qian.files:write"], "requiredPermission": "qian.files:file:write", "requiredSecrets": [],
  "dataClassification": "internal", "contractVersion": "files-v1",
  "schemaSha256": "<校验器计算并回写：名称 + 描述 + 规范化 inputSchema>",
  "agent": { "expose": true, "requiresConfirmation": true },
  "binding": { "kind": "service", "key": "Files", "method": "UploadAsync" }
}
```

`binding.kind`：`service`（服务键上的方法）、`http`（`{ url, method }`，无代码组件用）、`command`（`Kun.Mediator` 命令 / 查询 id）。

## 4.7 `ui/manifest.json`

格式以 [`yue.md`](/pr-preview/pr-202/design/prd/specs/yue.md) §3.6 的组件 manifest 与 MF2 外壳契约为准；本 spec 只规定位置与字段族。`pages[].nav` 取代 v1 的 `contributes: qian.shell.nav/v1`，`widgets[]` 对应 `registerWidget`，全部是外壳侧带 scope 的可逆注册。

```json
{
  "$schema": "https://qian.dev/schemas/ui/v2",
  "federation": { "manifest": "./dist/mf-manifest.json", "exposes": ["FilesPage", "FilesSettingsCard", "FilePicker", "FileFieldWidget"] },
  "pages": [{ "route": "/files", "expose": "FilesPage", "permission": "qian.files:file:read", "nav": { "title": "文件", "icon": "folder", "order": 40 } }],
  "widgets": [{ "type": "qian.file", "expose": "FileFieldWidget" }],
  "settingsCards": [{ "expose": "FilesSettingsCard", "scope": "tenant", "permission": "qian.files:settings:write" }],
  "engines": { "yue": "^1.0" }
}
```

## 4.8 `config/qian.patch.yml`（简述）

格式与 §7.4 完全相同。组合包用它插入一组条目（§7.3）；普通组件用它调整**其它**条目（例如主题组件改外壳的 `brand`）。组件自身的默认配置写在 `schemas/config.schema.json` 的 `default`，不写在这里。

```yaml
- id: shell
  config:
    brand: { primary: "#7C3AED", logo: "qian://com.example.theme.violet/assets/logo.svg" }
```
