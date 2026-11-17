> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# 组件清单 plugin.json

> 本页由构建脚本同步自 [组件模型规格 §3](/pr-preview/pr-50/design/prd/specs/component-model.md)（仓库 `docs/prd/specs/component-model.md`），不另写；请修改规格源文件。

清单文件名与格式已定稿：`.qian-plugin/plugin.json`（JSON，便于 JSON Schema 校验与源生成器合并）；profile 与 patch 继续用 YAML（人手编辑、需要注释）；`qian doctor --migrate-manifest` 负责从 v1 `qian-plugin.yaml` 迁移（ADR-020 §7 第 2 条，已拍板）。

## 3.1 示例 A：带代码的组件 `qian.files`

`ctx.Files` 的 S1 Provider（§6.2），`runtime.form: module`，由 `Qian.Files` 实现，存储后端经 Kun 新线 `Kun.Storage.*` 按 config 选择。

```json
{
  "$schema": "https://qian.dev/schemas/plugin/v2",
  "id": "qian.files",
  "version": "1.0.0",
  "description": "多租户文件服务：租户前缀、配额、上传策略、文件元数据与预览票据；存储后端可选本地盘 / S3 / OSS",
  "author": { "name": "Qian Platform Team", "url": "https://docs.example.internal/qian" },
  "license": "Apache-2.0",
  "engines": { "qian": ">=1.0 <2", "kun": ">=2026.9", "yue": "^1.0" },
  "interface": {
    "displayName": "文件存储",
    "category": "storage",
    "defaultPrompt": ["把这份合同扫描件上传到当前租户，并给我一个 24 小时有效的下载链接", "列出本租户本月上传量最大的 10 个文件"],
    "icon": "./assets/icon.svg",
    "brandColor": "#0F766E",
    "screenshots": ["./assets/shot-upload.png"]
  },
  "surfaces": { "skills": "./skills", "commands": "./commands", "hooks": "./hooks/hooks.json", "tools": "./tools",
                "ui": "./ui/manifest.json", "metadata": "./metadata", "schemas": "./schemas" },
  "runtime": {
    "form": "module",
    "nuget": [{ "id": "Qian.Files.Abstractions", "version": "1.0.0" }, { "id": "Qian.Files", "version": "1.0.0" }],
    "npm": [{ "name": "@qian/files-ui", "version": "1.0.0" }],
    "entry": "Qian.Files.FilesPlugin, Qian.Files",
    "configSchema": "./schemas/config.schema.json",
    "dbSchema": "plugin_qian_files"
  },
  "inject": ["Tenancy", "Config", "Authz", "Events", "Audit"],
  "provides": ["Files", "Files.Backends:local", "Files.Backends:s3", "Files.Backends:oss"],
  "policy": {
    "tier": "system", "sandbox": "none", "authentication": "ON_INSTALL", "profiles": ["platform", "standalone"],
    "egress": ["oss-cn-hangzhou.example.internal:443"],
    "permissions": { "platform": ["tenant:read", "audit:write"], "data": [{ "entity": "identity.member", "access": "r" }],
                     "secrets": [{ "name": "oss/*", "scope": "platform" }], "ui": { "connectSrc": [] } }
  },
  "scopes": ["global", "tenant"]
}
```

`schemas/config.schema.json` 节选——`x-qian-sensitive: true` 的属性只接受 `secretRef://…`（§11.3）：

```json
{ "$schema": "https://json-schema.org/draft/2020-12/schema", "type": "object", "additionalProperties": false, "required": ["storage"],
  "properties": {
    "storage": { "type": "object", "required": ["kind"], "properties": {
      "kind": { "enum": ["local", "s3", "oss"] }, "root": { "type": "string" }, "bucket": { "type": "string" }, "endpoint": { "type": "string" },
      "accessKey": { "type": "string", "x-qian-sensitive": true }, "secretKey": { "type": "string", "x-qian-sensitive": true } } },
    "quota": { "type": "object", "properties": { "perTenantBytes": { "type": "integer", "default": 10737418240 } } } } }
```

## 3.2 示例 B：无代码的 skill-only 组件

只教 Agent「如何做租户上线核对」的技能包：没有 `runtime`，安装只复制 `skills/` 与 `commands/`；`community` tier 也能发布。`inject[]` 仍有意义——声明技能正文会用到哪些服务键的工具，装载器在这些键未就绪的 scope 里不暴露该技能，`qian doctor` 据此检查目标 profile 是否满足依赖。

```json
{
  "$schema": "https://qian.dev/schemas/plugin/v2",
  "id": "com.example.skills.tenant-onboarding",
  "version": "0.3.0",
  "description": "租户上线核对技能：按清单检查身份连接器、默认角色、文件配额与通知渠道是否就绪",
  "author": { "name": "Example Ops", "email": "ops@example.com" },
  "license": "MIT",
  "engines": { "qian": ">=1.0 <2" },
  "interface": { "displayName": "租户上线核对", "category": "operations", "brandColor": "#7C3AED",
                 "defaultPrompt": ["用 tenant-onboarding 技能检查租户 t-001 是否可以上线"] },
  "surfaces": { "skills": "./skills", "commands": "./commands" },
  "inject": ["Tenancy", "Identity", "Files"],
  "provides": [],
  "policy": { "tier": "community", "sandbox": "none", "authentication": "ON_INSTALL", "profiles": ["platform"] },
  "scopes": ["global", "tenant", "agent-session"]
}
```

## 3.3 示例 C：组合包 `qian.bundle.base`

`platform` profile 的 base 组合包（R31，§7.7）：无 `runtime.form`，`runtime.nuget[]` 带来全部条目的代码，`config/qian.patch.yml`（§7.3）插入条目，`skills/`、`commands/`、`hooks/` 携带平台运维表面。

```json
{
  "$schema": "https://qian.dev/schemas/plugin/v2",
  "id": "qian.bundle.base",
  "version": "1.0.0",
  "description": "base 组合包：原 18 个平台插件按 seam 拆包——注册表、生命周期、能力目录、钩子桥接、租户、身份客户端、鉴权、数据、文件、配置、事件、审计、可观测、通知、调度、实时、授权 v0",
  "author": { "name": "Qian Platform Team" },
  "license": "Apache-2.0",
  "engines": { "qian": ">=1.0 <2", "kun": ">=2026.9" },
  "interface": { "displayName": "平台 base 组合包", "category": "bundle", "icon": "./assets/icon.svg" },
  "surfaces": { "patch": "./config/qian.patch.yml", "skills": "./skills", "commands": "./commands", "hooks": "./hooks/hooks.json" },
  "runtime": {
    "nuget": [
      { "id": "Qian.Registry", "version": "1.0.0" }, { "id": "Qian.Lifecycle", "version": "1.0.0" },
      { "id": "Qian.Capabilities", "version": "1.0.0" }, { "id": "Qian.Hooks", "version": "1.0.0" },
      { "id": "Qian.Tenancy", "version": "1.0.0" }, { "id": "Qian.Identity.Client", "version": "1.0.0" },
      { "id": "Qian.Authz", "version": "1.0.0" }, { "id": "Qian.Data", "version": "1.0.0" }, { "id": "Qian.Data.Dialect.Pg", "version": "1.0.0" },
      { "id": "Qian.Files", "version": "1.0.0" }, { "id": "Qian.Config.Apollo", "version": "1.0.0" }, { "id": "Qian.Events", "version": "1.0.0" },
      { "id": "Qian.Audit", "version": "1.0.0" }, { "id": "Qian.Observability", "version": "1.0.0" }, { "id": "Qian.Notify", "version": "1.0.0" },
      { "id": "Qian.Notify.Channel.Inbox", "version": "1.0.0" }, { "id": "Qian.Notify.Channel.Email", "version": "1.0.0" },
      { "id": "Qian.Notify.Channel.Sms", "version": "1.0.0" }, { "id": "Qian.Scheduler", "version": "1.0.0" },
      { "id": "Qian.Realtime", "version": "1.0.0" }, { "id": "Qian.Entitlement", "version": "1.0.0" }
    ]
  },
  "inject": [], "provides": [],
  "policy": { "tier": "system", "sandbox": "none", "authentication": "ON_INSTALL", "profiles": ["platform"] },
  "scopes": ["global"]
}
```

## 3.4 字段逐项表

分级：S1 = S1 校验器与宿主都消费；S2 / S3 = 校验器接受结构、宿主到该阶段才消费语义（低阶段宿主输出 `warning: field reserved for S2+`，沿 v1 D10）。

**根与标识**

| 字段            | 类型                                            | 必填 | 说明                                                                     | 分级 |
| ------------- | --------------------------------------------- | -- | ---------------------------------------------------------------------- | -- |
| `$schema`     | `const "https://qian.dev/schemas/plugin/v2"`  | 否  | 编辑器提示；版本以文件路径 `.qian-plugin/plugin.json` 为准                            | S1 |
| `id`          | `^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$`，≤ 128 | 是  | 反向域名，发布后不可改；`qian.` 前缀保留给官方组件                                          | S1 |
| `version`     | SemVer 2.0                                    | 是  | 与 Kun CalVer 独立                                                        | S1 |
| `description` | `string`，10–500 字                             | 是  | 面向人；模型看 `skills` / `tools` 的描述                                         | S1 |
| `author`      | `{ name, email?, url? }`                      | 是  | 市场的 `publisher.verified` 在 S3 另行维护，不在清单                                | S1 |
| `license`     | SPDX 或 `LicenseRef-*`                         | 是  | Qian 包族许可证已定为 Apache-2.0（→ §13 第 6 条），官方组件写 `Apache-2.0`；S3 商业组件的许可证另定 | S1 |
| `engines`     | `{ qian?, kun?, yue? }` SemVer range          | 否  | 沿 v1 保留；有 `ui` 表面时 `yue` 必填，有 `runtime.nuget` 时 `kun` 必填               | S1 |

**展示（`interface`）**

| 字段                       | 类型                                             | 必填 | 说明                 | 分级 |
| ------------------------ | ---------------------------------------------- | -- | ------------------ | -- |
| `displayName`            | `string` ≤ 40 字                                | 是  | 市场与目录显示名           | S1 |
| `category`               | `^[a-z][a-z0-9-]*$`                            | 是  | 分类短名；官方分类目录由市场仓库维护 | S1 |
| `defaultPrompt[]`        | ≤ 5 条                                          | 否  | 安装后展示给用户的首批提示词     | S1 |
| `icon` / `screenshots[]` | `assets/` 下相对路径                                | 否  | 截图 ≤ 6 张           | S1 |
| `brandColor`             | `^#[0-9A-Fa-f]{6}$`                            | 否  | 品牌色                | S1 |
| `locales`                | `{ [locale]: { displayName?, description? } }` | 否  | 多语言；S1 只校验结构       | S3 |

**表面指针（`surfaces`）**——相对组件根；省略取默认；指向不存在的路径是 error。

| 字段                                                                  | 默认                                                                                      | 指向                                                |
| ------------------------------------------------------------------- | --------------------------------------------------------------------------------------- | ------------------------------------------------- |
| `skills` / `agents` / `commands` / `tools` / `metadata` / `schemas` | `./skills` 等同名目录                                                                        | 目录；`metadata/` 承载本组件定义的权限点、默认角色、事件、消息类型——已裁决（R37） |
| `hooks` / `mcpServers` / `ui` / `patch`                             | `./hooks/hooks.json` / `./.mcp.json` / `./ui/manifest.json` / `./config/qian.patch.yml` | 文件                                                |

**运行时（`runtime`）**——无代码组件省略整个对象。

| 字段                  | 类型                                               | 必填            | 说明                                                                                                                   | 分级                          |
| ------------------- | ------------------------------------------------ | ------------- | -------------------------------------------------------------------------------------------------------------------- | --------------------------- |
| `form`              | `module \| service \| function \| ui \| package` | 有 `src/` 时是   | 代码的物理形态（v1 `kind` 退化而来，§3.6）：进程内程序集 / 独立工作负载 / Wasm 函数 / 仅前端远程模块 / 低代码元数据包。组合包不写；连接器组件按实现取 `module` 或 `service`（R28） | S1（`function`、`package` S2） |
| `nuget[]` / `npm[]` | `{ id \| name, version, digest? }[]`             | 否             | 精确版本；`digest` 为包内容 `sha256:`，S1 可选、S3 第三方必填                                                                          | S1                          |
| `entry`             | `string`                                         | `module` 时是   | .NET `"<Type>, <Assembly>"`（实现 `IQianPlugin`）；TS 为 npm 包导出名（默认 `default`）                                            | S1                          |
| `image`             | `^oci://.+@sha256:[0-9a-f]{64}$`                 | `service` 时是  | 只接受 digest 引用（ADR-002 D2.7）；`service{ protocols, resources, health }` 沿 v1 `components.service`                      | S1                          |
| `wasm` / `limits`   | `…@sha256:…` / `{ fuel, memoryMiB, timeoutMs }`  | `function` 时是 | 预留                                                                                                                   | S2                          |
| `configSchema`      | 相对路径                                             | 有 config 时是   | JSON Schema 2020-12；装载器在 `Apply` 前校验                                                                                 | S1                          |
| `dbSchema`          | `^plugin_[a-z0-9_]+$`                            | 否             | 默认 `plugin_<id 以下划线连接>`                                                                                              | S1                          |

**依赖、提供、作用域**

| 字段                 | 类型                                                                   | 必填    | 说明                                                                                              | 分级 |
| ------------------ | -------------------------------------------------------------------- | ----- | ----------------------------------------------------------------------------------------------- | -- |
| `inject[]`         | 服务键数组，每项 `^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*:[a-z][a-z0-9-]*)?$` | 是（可空） | 依赖的服务键：`<Key>`（单例 seam）或 `<Key>.<Registry>:<name>`（注册表 seam 的某个具名注册，R38）；全部就绪才 `Apply`；装载顺序由此推导 | S1 |
| `optionalInject[]` | 同上                                                                   | 否     | 可选依赖：缺失时仍 `Apply`，`TryInject` 为空                                                                | S1 |
| `provides[]`       | 同上                                                                   | 是（可空） | `Apply` 时会 `Provide` 的键，或向注册表子服务 `Register(name, …)` 的「注册表键:名称」（R38）；与 `inject` 不相交；代码组件由源生成器派生 | S1 |
| `scopes[]`         | `global \| tenant \| app \| agent-session` 非空数组                      | 是     | 允许挂载的 scope 种类；条目 `scope` 不在列表内即失败；`["global"]` = 只能作为全局条目挂载                                    | S1 |

**策略（`policy`）**

| 字段               | 类型                                                      | 必填        | 说明                                                                                                                                 | 分级              |
| ---------------- | ------------------------------------------------------- | --------- | ---------------------------------------------------------------------------------------------------------------------------------- | --------------- |
| `tier`           | `system \| official \| partner \| private \| community` | 是         | 信任等级（ADR-002 D2.4）；决定允许的 `form`、`sandbox`、hooks 类型与进程边界（§11.1）                                                                     | S1              |
| `sandbox`        | `none \| worker \| iframe \| wasm \| workload`          | 是         | `none` 共享进程 / 外壳直接加载；`worker` / `iframe` 不可信 UI；`wasm` 函数；`workload` 独立工作负载                                                        | S1              |
| `authentication` | `ON_INSTALL \| ON_USE`                                  | 是         | 安装时一次性批准反向权限与密钥，或首次使用时由当前用户授权（S3 连接器组件）                                                                                            | S1（`ON_USE` S3） |
| `profiles[]`     | `string[]`                                              | 否         | 推荐 / 允许的 profile；不在列表的 profile 安装需 `--force-profile`                                                                               | S1              |
| `required`       | `boolean`，默认 `false`                                    | 否         | `true` = 该组件的条目不可被任何 scope patch 禁用或遮蔽，`qian remove` 拒绝卸载（取代 v1 `alwaysEnabled`，R34）；只允许 `tier: system`                            | S1              |
| `egress[]`       | `string[]`，每项 `<host>[:<port>]`（host 可含 `*.` 前缀）        | 否；连接器组件必填 | 允许的出网目标（R28）；为空 = 默认拒绝出网（NetworkPolicy / 出口代理）；`hooks.json`、`.mcp.json` 的 `url` 域名必须在其中                                            | S1              |
| `permissions`    | `{ platform[], data[], secrets[], ui{ connectSrc[] } }` | 否         | 反向权限，沿 v1 `permissions.*` 保留（含义见 `plugin-manifest.md` §3.2；v1 `permissions.egress` 升为 `policy.egress`）；安装时列出并批准，升级新增即重批。落点已裁决（R37） | S1              |

根对象 `additionalProperties: false`，接受 `^x-` 自定义字段。

## 3.5 JSON Schema 2020-12 骨架

完整 Schema 随 CLI 发布并托管在 `https://qian.dev/schemas/plugin/v2`；骨架如下（省略 `description`）。`tier × form × sandbox` 联合约束、`inject ∩ provides = ∅`、指针路径存在、`secretRef` 只出现在 `x-qian-sensitive` 属性等属于语义校验（§10.1），不写进 Schema。

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://qian.dev/schemas/plugin/v2",
  "type": "object", "additionalProperties": false, "patternProperties": { "^x-": {} },
  "required": ["id", "version", "description", "author", "license", "interface", "inject", "provides", "policy", "scopes"],
  "properties": {
    "$schema": { "const": "https://qian.dev/schemas/plugin/v2" },
    "id": { "type": "string", "pattern": "^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$", "maxLength": 128 },
    "version": { "$ref": "#/$defs/semver" },
    "description": { "type": "string", "minLength": 10, "maxLength": 500 },
    "author": { "type": "object", "required": ["name"], "additionalProperties": false,
                "properties": { "name": { "type": "string" }, "email": { "type": "string", "format": "email" }, "url": { "type": "string", "format": "uri" } } },
    "license": { "type": "string" },
    "engines": { "type": "object", "additionalProperties": false,
                 "properties": { "qian": { "type": "string" }, "kun": { "type": "string" }, "yue": { "type": "string" } } },
    "interface": { "type": "object", "required": ["displayName", "category"], "additionalProperties": false,
      "properties": { "displayName": { "type": "string", "maxLength": 40 }, "category": { "type": "string", "pattern": "^[a-z][a-z0-9-]*$" },
        "defaultPrompt": { "type": "array", "maxItems": 5, "items": { "type": "string" } }, "icon": { "$ref": "#/$defs/relPath" },
        "brandColor": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" }, "screenshots": { "type": "array", "maxItems": 6, "items": { "$ref": "#/$defs/relPath" } },
        "locales": { "type": "object" } } },
    "surfaces": { "type": "object", "additionalProperties": false,
      "properties": { "skills": { "$ref": "#/$defs/relPath" }, "agents": { "$ref": "#/$defs/relPath" }, "commands": { "$ref": "#/$defs/relPath" },
        "hooks": { "$ref": "#/$defs/relPath" }, "mcpServers": { "$ref": "#/$defs/relPath" }, "tools": { "$ref": "#/$defs/relPath" },
        "ui": { "$ref": "#/$defs/relPath" }, "metadata": { "$ref": "#/$defs/relPath" }, "schemas": { "$ref": "#/$defs/relPath" }, "patch": { "$ref": "#/$defs/relPath" } } },
    "runtime": { "type": "object", "additionalProperties": false,
      "properties": {
        "form": { "enum": ["module", "service", "function", "ui", "package"] },
        "nuget": { "type": "array", "items": { "$ref": "#/$defs/pkgRef" } }, "npm": { "type": "array", "items": { "$ref": "#/$defs/pkgRef" } },
        "entry": { "type": "string" }, "image": { "type": "string", "pattern": "^oci://.+@sha256:[0-9a-f]{64}$" }, "service": { "type": "object" },
        "wasm": { "type": "string", "pattern": "@sha256:[0-9a-f]{64}$" }, "limits": { "type": "object" },
        "configSchema": { "$ref": "#/$defs/relPath" }, "dbSchema": { "type": "string", "pattern": "^plugin_[a-z0-9_]+$" } },
      "allOf": [
        { "if": { "properties": { "form": { "const": "module" } }, "required": ["form"] }, "then": { "required": ["entry"] } },
        { "if": { "properties": { "form": { "const": "service" } }, "required": ["form"] }, "then": { "required": ["image"] } },
        { "if": { "properties": { "form": { "const": "function" } }, "required": ["form"] }, "then": { "required": ["wasm"] } } ] },
    "inject": { "$ref": "#/$defs/serviceKeys" }, "optionalInject": { "$ref": "#/$defs/serviceKeys" }, "provides": { "$ref": "#/$defs/serviceKeys" },
    "policy": { "type": "object", "required": ["tier", "sandbox", "authentication"], "additionalProperties": false,
      "properties": { "tier": { "enum": ["system", "official", "partner", "private", "community"] },
        "sandbox": { "enum": ["none", "worker", "iframe", "wasm", "workload"] }, "authentication": { "enum": ["ON_INSTALL", "ON_USE"] },
        "required": { "type": "boolean", "default": false },
        "egress": { "type": "array", "items": { "type": "string", "pattern": "^(\\*\\.)?[a-z0-9.-]+(:[0-9]{1,5})?$" } },
        "profiles": { "type": "array", "items": { "type": "string" } }, "permissions": { "$ref": "https://qian.dev/schemas/plugin/v2/permissions" } } },
    "scopes": { "type": "array", "minItems": 1, "uniqueItems": true, "items": { "enum": ["global", "tenant", "app", "agent-session"] } }
  },
  "$defs": {
    "semver": { "type": "string", "pattern": "^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(-[0-9A-Za-z.-]+)?(\\+[0-9A-Za-z.-]+)?$" },
    "relPath": { "type": "string", "pattern": "^\\./" },
    "serviceKeys": { "type": "array", "uniqueItems": true,
                     "items": { "type": "string", "pattern": "^[A-Z][A-Za-z0-9]*(\\.[A-Z][A-Za-z0-9]*:[a-z][a-z0-9-]*)?$" } },
    "pkgRef": { "type": "object", "required": ["version"], "additionalProperties": false,
                "properties": { "id": { "type": "string" }, "name": { "type": "string" }, "version": { "$ref": "#/$defs/semver" },
                                "digest": { "type": "string", "pattern": "^sha256:[0-9a-f]{64}$" } } }
  }
}
```

## 3.6 v1 → v2 字段迁移对照

`qian-plugin.yaml` → `.qian-plugin/plugin.json`；CLI `qian doctor --migrate-manifest <qian-plugin.yaml>` 输出 v2 草稿（文件名与格式 → §13 第 2 条）。

| v1 字段                                                               | v2 落点                                                                                                                                                                                                                     | 备注                                                                                                                                                                                                 |
| ------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `schemaVersion`                                                     | `$schema`（可选）                                                                                                                                                                                                             | 版本由文件位置决定                                                                                                                                                                                          |
| `id` / `version` / `license`                                        | 同名                                                                                                                                                                                                                        | 不变                                                                                                                                                                                                 |
| `kind: module \| service \| package \| function \| ui`              | `runtime.form` 同名值（`service` 加 `runtime.image`，`function` 加 `runtime.wasm`，`ui` 加 `surfaces.ui`）                                                                                                                          | 顶层分类改为表面                                                                                                                                                                                           |
| `kind: connector`                                                   | **不是** `runtime.form` 取值。连接器 = 为某个 seam 提供 Provider 的组件：向该 seam 的注册表子服务按名称注册，`provides` 写 `["Identity.Connectors:oidc"]` 这类「注册表键:名称」（R38）+ `policy.egress[]` 声明出网目标（域名 / 端口）；`runtime.form` 按实现取 `module \| service`（R28） | S3 `policy.authentication: ON_USE` 的连接器同此；例见 §6.3 身份连接器                                                                                                                                            |
| `kind: bundle` + `components{}`                                     | 组合包：有 `config/qian.patch.yml`，可携带多个组件（经 `runtime.nuget[] / npm[]` 引用或内联 `components/<id>/`），无 `runtime.form`；v1 `components` 下各组件各自成为独立组件。应用 = 组合包 + marketplace 条目（R28）                                                  | —                                                                                                                                                                                                  |
| `role`                                                              | 删除                                                                                                                                                                                                                        | 由 `provides[]`（Provider）、`inject[]`（Consumer）与表面组合（App）体现                                                                                                                                          |
| `tier`                                                              | `policy.tier`                                                                                                                                                                                                             | 枚举不变                                                                                                                                                                                               |
| `distribution`                                                      | S2 低代码表面的属性，不在清单根                                                                                                                                                                                                         | ADR-014 修订后由 `qian.metadata` spec 定义                                                                                                                                                               |
| `displayName` / `description`（多语言映射）                                | `interface.displayName`、根 `description`；多语言放 `interface.locales`                                                                                                                                                          | —                                                                                                                                                                                                  |
| `publisher` / `icon` / `links` / `categories`                       | 删除（S3 市场维护）/ `interface.icon` / `author.url` / `interface.category`                                                                                                                                                       | —                                                                                                                                                                                                  |
| `engines.*`                                                         | `engines.*`                                                                                                                                                                                                               | 保留                                                                                                                                                                                                 |
| `dependencies[]`                                                    | `inject[]`                                                                                                                                                                                                                | 依赖服务键而非具体组件（D20.4）                                                                                                                                                                                 |
| `provides.capabilities[]`（含 Agent 字段）                               | `tools/<name>.json`（§4.6）                                                                                                                                                                                                 | `sideEffect`、`policyHint`、`idempotent`、`requiredScopes`、`requiredPermission`、`requiredSecrets`、`dataClassification`、`schemaSha256`、`contractVersion`、`agent{ expose, requiresConfirmation }` 字段名不变 |
| `provides.events` / `permissions` / `defaultRoles` / `messageTypes` | `metadata/events.json` / `permissions.json` / `roles.json` / `message-types.json`                                                                                                                                         | 已裁决（R37）                                                                                                                                                                                           |
| `provides.extensionPoints` / `resources`                            | 扩展点 = 事件定义（waterfall / bail，D20.3）；`resources` 进 `metadata/`（S2）                                                                                                                                                          | —                                                                                                                                                                                                  |
| `provides.skills`（S3）                                               | `surfaces.skills`（S1）                                                                                                                                                                                                     | 前移                                                                                                                                                                                                 |
| `contributes[]`                                                     | `Apply` 中对目标服务键做可逆注册（如 `ctx.Shell().Nav.Add(...)`）；声明式贡献放 `ui/manifest.json`（`pages[].nav`）或 `metadata/`                                                                                                                  | 插槽 = 服务键上的注册                                                                                                                                                                                       |
| `components.module.assembly / dbSchema`                             | `runtime.nuget[]` / `runtime.dbSchema`                                                                                                                                                                                    | —                                                                                                                                                                                                  |
| `components.ui.federation / sandbox`                                | `ui/manifest.json` / `policy.sandbox`                                                                                                                                                                                     | —                                                                                                                                                                                                  |
| `permissions.platform / data / secrets / ui`                        | `policy.permissions.*`                                                                                                                                                                                                    | 原样保留；已裁决（R37）                                                                                                                                                                                      |
| `permissions.egress[]`                                              | `policy.egress[]`（`<host>[:<port>]`）                                                                                                                                                                                      | 升为 `policy` 顶层字段（R28）                                                                                                                                                                              |
| `tenancy.modes / dataIsolation`                                     | 条目 `config`（Provider 自己的配置）                                                                                                                                                                                               | 不再是清单字段                                                                                                                                                                                            |
| `tenancy.perTenantEnable` / `alwaysEnabled`                         | `scopes[]` 含 `tenant` / `policy.required: true`                                                                                                                                                                           | 租户开关 = scope patch（D20.5）；`required` 见 R34                                                                                                                                                         |
| 租户级状态 `Available → Entitled → … Enabled ⇄ Disabled …`               | 挂载状态只存 `scope_patches` 行（§7.6）；「买没买」留在 `qian.entitlement`                                                                                                                                                                 | 不再有 Enabled / Disabled 状态机（R34）                                                                                                                                                                    |
| `lifecycle.hooks.*`                                                 | `Apply` + `ctx.Effect` + 监听 `tenant/*` 事件                                                                                                                                                                                 | 两层生命周期取消                                                                                                                                                                                           |
| `lifecycle.migrations.*`                                            | 随 NuGet 包的 EF 迁移（ADR-019 D19.2）；逐租户迁移由 `tenant/provisioned` 监听器执行                                                                                                                                                         | —                                                                                                                                                                                                  |
| `config.schema / scopes`                                            | `runtime.configSchema` / `scopes[]`                                                                                                                                                                                       | —                                                                                                                                                                                                  |
| `agent.mcp.expose`                                                  | `tools/*.json` 的 `agent.expose` 与 `.mcp.json`                                                                                                                                                                             | —                                                                                                                                                                                                  |
| `marketplace.*`                                                     | 市场条目（§8），不在清单                                                                                                                                                                                                             | —                                                                                                                                                                                                  |
| `audit.emits[]`                                                     | `metadata/events.json` 中 `audit: true` 的事件                                                                                                                                                                                | —                                                                                                                                                                                                  |
