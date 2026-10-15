# 专题 spec · 组件模型 v2（清单 · 表面 · 运行时 · profile · 市场 · CLI）

> 状态：草案 v2（2026-10-06；2026-10-07 §13 待确认项全部拍板）。依据 [ADR-020 组件模型 v2](../../adr/ADR-020-component-model-v2.md)（Accepted）为唯一权威决策；兼引 [ADR-019 库优先](../../adr/ADR-019-library-first-packages.md)（代码部分的打包规则）、[ADR-002](../../adr/ADR-002-plugin-model.md)（信任等级、从代码派生、digest 引用等被保留并映射到新字段的条目）、[ADR-016](../../adr/ADR-016-iclaw-boundary.md)（工具元数据、事件信封、MCP 版本）。本 spec 取代 [`plugin-manifest.md`](plugin-manifest.md)（v1）为权威规格；v1 保留为字段迁移对照（§3.6）。
>
> 读者：组件作者（.NET / TypeScript）、`Qian.Runtime` 与 `@qian/runtime` 的实现者、`qian` CLI 与市场仓库维护者、TCK 编写者。写法是「可直接照着实现」：每种文件格式给完整示例，每个运行时 API 给签名与语义表，每条规则给校验点。ADR-020 §7 的待确认项已于 2026-10-07 全部拍板，结论登记在 §13，正文标「→ §13」处以该节结论为准；本 spec 不自行新增决策。被借鉴的商业低代码产品一律称「参考平台」，被对标的 Agent 生态项目一律称「对标项目」。

---

## 1. 范围、非目标与术语

### 1.1 范围

本 spec 定义七件事，全部为 S1 交付（字段级的 S1 / S2 / S3 分级在各表中单独标注）：① 组件文件夹布局与每种表面的文件格式（§2、§4）；② 清单 `.qian-plugin/plugin.json` 的字段、约束与 JSON Schema 2020-12（§3）；③ `Qian.Runtime`（.NET 10）与 `@qian/runtime`（TS）的最小 API、五种分发模式、scope 与生命周期语义（§5）；④ seam 三角色的包命名与依赖规则，以及 Files / Identity / Authz 三个 S1 seam 的落位（§6）；⑤ profile、组合包、patch 的格式、叠加顺序、`dump-config` 与租户 scope patch 的存储形态（§7）；⑥ 目录市场与 CLI（§8、§9）；⑦ 校验器、TCK、CI 门禁与安全规则（§10、§11）。

适用对象：全部官方组件（原 ADR-003 的 18 个平台插件——旧称「系统插件」，现为 `platform` profile base 组合包的内容，R31）、S1 参考应用 `qian.sample.crm`、S2 起的低代码元数据组件、S3 起的第三方组件。

### 1.2 非目标

- 不定义具体能力的业务契约（各 `Qian.<X>.Abstractions` 的接口细节在 PRD §07 对应小节与包 README），只定义它们如何被声明、挂载、替换。
- 不定义低代码表面 `forms/ lists/ flows/ pages/` 的内容格式（S2，ADR-011 / ADR-012 配套 spec），只预留目录与清单指针。
- 不定义 Agent 运行时；skill / agent / command / hook 表面由 Agent 运行时（ADR-016：iClaw）执行，Qian 负责声明、校验、注册与 scope 过滤。
- 不定义 OCI 制品内部格式与 cosign 流程（沿 v1 §4.2 与 ADR-017），只规定 v2 中哪些东西仍走 OCI（仅代码部分）。
- 不定义 S3 商业市场的授权、计费、审核流程，只保证 `marketplace.json` 可被叠加；不定义代码插件热装卸的实现（S2 可选，→ §13）。

### 1.3 术语

| 术语 | 定义（与 ADR-020 一致） | 章节 |
|---|---|---|
| 组件 component | 一个文件夹 + 一份清单 + 若干声明式表面 + 可选代码；分发与安装的单位。无代码组件是常态 | §2、§3 |
| 插件 plugin | 组件的运行时形态：按一个条目挂进共享上下文、向 `ctx` 贡献服务 / 监听器 / 可逆副作用。谈文件用「组件」，谈运行时用「插件」 | §5 |
| 表面 surface | 组件向外暴露的一类声明式内容：`skills`、`agents`、`commands`、`hooks`、`mcpServers`、`tools`、`ui`、`metadata`（含 S2 低代码表面）、`schemas`、`patch`。v2 的顶层分类，取代 v1 `kind` | §2、§4 |
| 服务键 service key | `ctx.<Key>` 的 `<Key>`（`Files`、`Identity`、`Authz`…）；由 Service Definition 包拥有并定义类型，Provider 用 `Provide` 填充，Consumer 用 `inject` 声明依赖。注册表 seam 的键写成 `<Key>.<Registry>:<name>`（如 `Identity.Connectors:oidc`，R38） | §5.1、§6.1 |
| seam | 一项能力的缝合点 = Service Definition + 一个或多个 Provider + 一个或多个 Consumer；换 Provider 不改 Consumer | §6 |
| scope | 任一注册的归属：全局，或恰好一个 scope key。S1 种类：`tenant:<id>`、`app:<id>`、`agent-session:<id>`；不继承 | §5.5 |
| 条目 entry | 插件树节点 `{ id, plugin, config, scope?, disabled? }`；`id` 为条目 id（树内唯一），`plugin` 为组件 id；同一组件可以不同条目多次挂载（通常在不同 scope） | §7.4 |
| profile | 具名组装：`qian.profile.yml` 列出组合包、自己的 patch 与热重载开关。可部署单元 = profile；S1：`platform`、`gateway`、`identity`、`standalone`、`minimal` | §7.2、§7.7 |
| 组合包 bundle | 带 `config/qian.patch.yml` 的组件，可携带多个组件（经 `runtime.nuget[] / npm[]` 引用，或内联在 `components/<id>/`）；被 profile 引用时作为一层叠加，其 patch 插入一组条目；自身无 `runtime.form`（R28） | §7.3 |
| 连接器 connector | 为某个 seam 提供 Provider、且需要出网的组件：向该 seam 的注册表子服务按名称注册（`provides: ["Identity.Connectors:oidc"]` 这类「注册表键:名称」形式，R38），`policy.egress[]` 声明允许的出网目标（域名 / 端口）；`runtime.form` 按实现取 `module \| service`。**不是** `runtime.form` 的取值（R28） | §3.6、§6.1、§6.3 |
| 应用 app | 组合包 + marketplace 条目（R28；ADR-014 修订） | §7.3、§8 |
| 平台插件 | `policy.tier: system` 的组件，随发行版交付；旧称「系统插件」只作别名（R31） | §7.7、§11.1 |
| patch | 按 `(scope, id)` 定位条目并替换其整个 `config`、禁用它或插入新条目的一条记录；文件形态 `qian.patch.yml`，数据库形态为租户 scope patch 表，同一 JSON Schema | §7.4、§7.6 |
| 市场条目 | `.qian/marketplace.json` 的 `plugins[]` 一项：只有 `name`、`source`、`policy`、`category`、`interface` | §8 |
| 插件树 | 按 profile → 组合包 → profile patch → home patch → `--patch` overlay（运行中再叠租户 scope patch）层叠出的全部条目；`qian dump-config` 打印的就是它 | §7.1、§7.5 |
| 运行时 | `Qian.Runtime` / `@qian/runtime`：上下文、服务注册、类型化事件、可逆副作用、装载器；极小、无业务语义，是组件库唯一必需的 Qian 依赖 | §5 |

---

## 2. 组件文件夹布局

### 2.1 目录树（ADR-020 D20.6 展开）

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

### 2.2 表面一览

| 表面 | 路径 | 进入的注册表 | 受 scope 过滤 | S1 热重载 |
|---|---|---|---|---|
| `skills` | `skills/<name>/SKILL.md` | `ctx.Tools`（kind = skill） | 是 | 是 |
| `agents` | `agents/<name>.md` | `ctx.Agents` | 是 | 是 |
| `commands` | `commands/<name>.md` | `ctx.Commands` | 是 | 是 |
| `hooks` | `hooks/hooks.json` | `ctx.Hooks` | 是 | 是 |
| `mcpServers` | `.mcp.json` | `ctx.McpServers` | 是 | 是 |
| `tools` | `tools/<name>.json` | `ctx.Tools`（kind = tool） | 是 | 手写：是；代码派生：否 |
| `ui` | `ui/manifest.json` | `ctx.Ui`（yue 外壳） | 是 | 否 |
| `metadata` | `metadata/*.json` | `qian.authz` / `qian.events` / `qian.notify` 各自注册表 | 是 | 是 |
| `schemas` | `schemas/*.schema.json` | 契约仓库 | 否 | 是 |
| `patch` | `config/qian.patch.yml` | 插件树 | 条目自带 `scope` | 是 |
| 低代码表面 | `forms/ lists/ flows/ pages/` | S2 定义 | 是 | S2 定义 |

### 2.3 无代码组件与带代码组件

- **无代码组件**：无 `src/`，清单无 `runtime.form`。安装只做「复制表面 + 写 patch」，任何 tier 都可发布（§11.1）。典型：只带 `skills/` 的运维技能包、只带 `config/qian.patch.yml` 的环境配置包、只带 `metadata/roles.json` 的角色模板包。
- **带代码组件**：有 `src/`，`runtime.form ∈ { module, service, function, ui, package }`，`runtime.nuget[] / npm[]` 列出发布后的包。代码部分的清单字段（`runtime.entry`、`inject`、`provides`、代码派生的 `tools/*.json`、`configSchema`）由源生成器派生并**合并进同一份 `plugin.json`**（D20.6：不再有手写与派生两份文件）；CI 比对派生结果与仓库清单，漂移即失败（§10.3 G1）。

---

## 3. 清单 `.qian-plugin/plugin.json`

清单文件名与格式已定稿：`.qian-plugin/plugin.json`（JSON，便于 JSON Schema 校验与源生成器合并）；profile 与 patch 继续用 YAML（人手编辑、需要注释）；`qian doctor --migrate-manifest` 负责从 v1 `qian-plugin.yaml` 迁移（ADR-020 §7 第 2 条，已拍板）。

### 3.1 示例 A：带代码的组件 `qian.files`

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

### 3.2 示例 B：无代码的 skill-only 组件

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

### 3.3 示例 C：组合包 `qian.bundle.base`

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

### 3.4 字段逐项表

分级：S1 = S1 校验器与宿主都消费；S2 / S3 = 校验器接受结构、宿主到该阶段才消费语义（低阶段宿主输出 `warning: field reserved for S2+`，沿 v1 D10）。

**根与标识**

| 字段 | 类型 | 必填 | 说明 | 分级 |
|---|---|---|---|---|
| `$schema` | `const "https://qian.dev/schemas/plugin/v2"` | 否 | 编辑器提示；版本以文件路径 `.qian-plugin/plugin.json` 为准 | S1 |
| `id` | `^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$`，≤ 128 | 是 | 反向域名，发布后不可改；`qian.` 前缀保留给官方组件 | S1 |
| `version` | SemVer 2.0 | 是 | 与 Kun CalVer 独立 | S1 |
| `description` | `string`，10–500 字 | 是 | 面向人；模型看 `skills` / `tools` 的描述 | S1 |
| `author` | `{ name, email?, url? }` | 是 | 市场的 `publisher.verified` 在 S3 另行维护，不在清单 | S1 |
| `license` | SPDX 或 `LicenseRef-*` | 是 | Qian 包族许可证已定为 Apache-2.0（→ §13 第 6 条），官方组件写 `Apache-2.0`；S3 商业组件的许可证另定 | S1 |
| `engines` | `{ qian?, kun?, yue? }` SemVer range | 否 | 沿 v1 保留；有 `ui` 表面时 `yue` 必填，有 `runtime.nuget` 时 `kun` 必填 | S1 |

**展示（`interface`）**

| 字段 | 类型 | 必填 | 说明 | 分级 |
|---|---|---|---|---|
| `displayName` | `string` ≤ 40 字 | 是 | 市场与目录显示名 | S1 |
| `category` | `^[a-z][a-z0-9-]*$` | 是 | 分类短名；官方分类目录由市场仓库维护 | S1 |
| `defaultPrompt[]` | ≤ 5 条 | 否 | 安装后展示给用户的首批提示词 | S1 |
| `icon` / `screenshots[]` | `assets/` 下相对路径 | 否 | 截图 ≤ 6 张 | S1 |
| `brandColor` | `^#[0-9A-Fa-f]{6}$` | 否 | 品牌色 | S1 |
| `locales` | `{ [locale]: { displayName?, description? } }` | 否 | 多语言；S1 只校验结构 | S3 |

**表面指针（`surfaces`）**——相对组件根；省略取默认；指向不存在的路径是 error。

| 字段 | 默认 | 指向 |
|---|---|---|
| `skills` / `agents` / `commands` / `tools` / `metadata` / `schemas` | `./skills` 等同名目录 | 目录；`metadata/` 承载本组件定义的权限点、默认角色、事件、消息类型——已裁决（R37） |
| `hooks` / `mcpServers` / `ui` / `patch` | `./hooks/hooks.json` / `./.mcp.json` / `./ui/manifest.json` / `./config/qian.patch.yml` | 文件 |

**运行时（`runtime`）**——无代码组件省略整个对象。

| 字段 | 类型 | 必填 | 说明 | 分级 |
|---|---|---|---|---|
| `form` | `module \| service \| function \| ui \| package` | 有 `src/` 时是 | 代码的物理形态（v1 `kind` 退化而来，§3.6）：进程内程序集 / 独立工作负载 / Wasm 函数 / 仅前端远程模块 / 低代码元数据包。组合包不写；连接器组件按实现取 `module` 或 `service`（R28） | S1（`function`、`package` S2） |
| `nuget[]` / `npm[]` | `{ id \| name, version, digest? }[]` | 否 | 精确版本；`digest` 为包内容 `sha256:`，S1 可选、S3 第三方必填 | S1 |
| `entry` | `string` | `module` 时是 | .NET `"<Type>, <Assembly>"`（实现 `IQianPlugin`）；TS 为 npm 包导出名（默认 `default`） | S1 |
| `image` | `^oci://.+@sha256:[0-9a-f]{64}$` | `service` 时是 | 只接受 digest 引用（ADR-002 D2.7）；`service{ protocols, resources, health }` 沿 v1 `components.service` | S1 |
| `wasm` / `limits` | `…@sha256:…` / `{ fuel, memoryMiB, timeoutMs }` | `function` 时是 | 预留 | S2 |
| `configSchema` | 相对路径 | 有 config 时是 | JSON Schema 2020-12；装载器在 `Apply` 前校验 | S1 |
| `dbSchema` | `^plugin_[a-z0-9_]+$` | 否 | 默认 `plugin_<id 以下划线连接>` | S1 |

**依赖、提供、作用域**

| 字段 | 类型 | 必填 | 说明 | 分级 |
|---|---|---|---|---|
| `inject[]` | 服务键数组，每项 `^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*:[a-z][a-z0-9-]*)?$` | 是（可空） | 依赖的服务键：`<Key>`（单例 seam）或 `<Key>.<Registry>:<name>`（注册表 seam 的某个具名注册，R38）；全部就绪才 `Apply`；装载顺序由此推导 | S1 |
| `optionalInject[]` | 同上 | 否 | 可选依赖：缺失时仍 `Apply`，`TryInject` 为空 | S1 |
| `provides[]` | 同上 | 是（可空） | `Apply` 时会 `Provide` 的键，或向注册表子服务 `Register(name, …)` 的「注册表键:名称」（R38）；与 `inject` 不相交；代码组件由源生成器派生 | S1 |
| `scopes[]` | `global \| tenant \| app \| agent-session` 非空数组 | 是 | 允许挂载的 scope 种类；条目 `scope` 不在列表内即失败；`["global"]` = 只能作为全局条目挂载 | S1 |

**策略（`policy`）**

| 字段 | 类型 | 必填 | 说明 | 分级 |
|---|---|---|---|---|
| `tier` | `system \| official \| partner \| private \| community` | 是 | 信任等级（ADR-002 D2.4）；决定允许的 `form`、`sandbox`、hooks 类型与进程边界（§11.1） | S1 |
| `sandbox` | `none \| worker \| iframe \| wasm \| workload` | 是 | `none` 共享进程 / 外壳直接加载；`worker` / `iframe` 不可信 UI；`wasm` 函数；`workload` 独立工作负载 | S1 |
| `authentication` | `ON_INSTALL \| ON_USE` | 是 | 安装时一次性批准反向权限与密钥，或首次使用时由当前用户授权（S3 连接器组件） | S1（`ON_USE` S3） |
| `profiles[]` | `string[]` | 否 | 推荐 / 允许的 profile；不在列表的 profile 安装需 `--force-profile` | S1 |
| `required` | `boolean`，默认 `false` | 否 | `true` = 该组件的条目不可被任何 scope patch 禁用或遮蔽，`qian remove` 拒绝卸载（取代 v1 `alwaysEnabled`，R34）；只允许 `tier: system` | S1 |
| `egress[]` | `string[]`，每项 `<host>[:<port>]`（host 可含 `*.` 前缀） | 否；连接器组件必填 | 允许的出网目标（R28）；为空 = 默认拒绝出网（NetworkPolicy / 出口代理）；`hooks.json`、`.mcp.json` 的 `url` 域名必须在其中 | S1 |
| `permissions` | `{ platform[], data[], secrets[], ui{ connectSrc[] } }` | 否 | 反向权限，沿 v1 `permissions.*` 保留（含义见 `plugin-manifest.md` §3.2；v1 `permissions.egress` 升为 `policy.egress`）；安装时列出并批准，升级新增即重批。落点已裁决（R37） | S1 |

根对象 `additionalProperties: false`，接受 `^x-` 自定义字段。

### 3.5 JSON Schema 2020-12 骨架

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

### 3.6 v1 → v2 字段迁移对照

`qian-plugin.yaml` → `.qian-plugin/plugin.json`；CLI `qian doctor --migrate-manifest <qian-plugin.yaml>` 输出 v2 草稿（文件名与格式 → §13 第 2 条）。

| v1 字段 | v2 落点 | 备注 |
|---|---|---|
| `schemaVersion` | `$schema`（可选） | 版本由文件位置决定 |
| `id` / `version` / `license` | 同名 | 不变 |
| `kind: module \| service \| package \| function \| ui` | `runtime.form` 同名值（`service` 加 `runtime.image`，`function` 加 `runtime.wasm`，`ui` 加 `surfaces.ui`） | 顶层分类改为表面 |
| `kind: connector` | **不是** `runtime.form` 取值。连接器 = 为某个 seam 提供 Provider 的组件：向该 seam 的注册表子服务按名称注册，`provides` 写 `["Identity.Connectors:oidc"]` 这类「注册表键:名称」（R38）+ `policy.egress[]` 声明出网目标（域名 / 端口）；`runtime.form` 按实现取 `module \| service`（R28） | S3 `policy.authentication: ON_USE` 的连接器同此；例见 §6.3 身份连接器 |
| `kind: bundle` + `components{}` | 组合包：有 `config/qian.patch.yml`，可携带多个组件（经 `runtime.nuget[] / npm[]` 引用或内联 `components/<id>/`），无 `runtime.form`；v1 `components` 下各组件各自成为独立组件。应用 = 组合包 + marketplace 条目（R28） | — |
| `role` | 删除 | 由 `provides[]`（Provider）、`inject[]`（Consumer）与表面组合（App）体现 |
| `tier` | `policy.tier` | 枚举不变 |
| `distribution` | S2 低代码表面的属性，不在清单根 | ADR-014 修订后由 `qian.metadata` spec 定义 |
| `displayName` / `description`（多语言映射） | `interface.displayName`、根 `description`；多语言放 `interface.locales` | — |
| `publisher` / `icon` / `links` / `categories` | 删除（S3 市场维护）/ `interface.icon` / `author.url` / `interface.category` | — |
| `engines.*` | `engines.*` | 保留 |
| `dependencies[]` | `inject[]` | 依赖服务键而非具体组件（D20.4） |
| `provides.capabilities[]`（含 Agent 字段） | `tools/<name>.json`（§4.6） | `sideEffect`、`policyHint`、`idempotent`、`requiredScopes`、`requiredPermission`、`requiredSecrets`、`dataClassification`、`schemaSha256`、`contractVersion`、`agent{ expose, requiresConfirmation }` 字段名不变 |
| `provides.events` / `permissions` / `defaultRoles` / `messageTypes` | `metadata/events.json` / `permissions.json` / `roles.json` / `message-types.json` | 已裁决（R37） |
| `provides.extensionPoints` / `resources` | 扩展点 = 事件定义（waterfall / bail，D20.3）；`resources` 进 `metadata/`（S2） | — |
| `provides.skills`（S3） | `surfaces.skills`（S1） | 前移 |
| `contributes[]` | `Apply` 中对目标服务键做可逆注册（如 `ctx.Shell().Nav.Add(...)`）；声明式贡献放 `ui/manifest.json`（`pages[].nav`）或 `metadata/` | 插槽 = 服务键上的注册 |
| `components.module.assembly / dbSchema` | `runtime.nuget[]` / `runtime.dbSchema` | — |
| `components.ui.federation / sandbox` | `ui/manifest.json` / `policy.sandbox` | — |
| `permissions.platform / data / secrets / ui` | `policy.permissions.*` | 原样保留；已裁决（R37） |
| `permissions.egress[]` | `policy.egress[]`（`<host>[:<port>]`） | 升为 `policy` 顶层字段（R28） |
| `tenancy.modes / dataIsolation` | 条目 `config`（Provider 自己的配置） | 不再是清单字段 |
| `tenancy.perTenantEnable` / `alwaysEnabled` | `scopes[]` 含 `tenant` / `policy.required: true` | 租户开关 = scope patch（D20.5）；`required` 见 R34 |
| 租户级状态 `Available → Entitled → … Enabled ⇄ Disabled …` | 挂载状态只存 `scope_patches` 行（§7.6）；「买没买」留在 `qian.entitlement` | 不再有 Enabled / Disabled 状态机（R34） |
| `lifecycle.hooks.*` | `Apply` + `ctx.Effect` + 监听 `tenant/*` 事件 | 两层生命周期取消 |
| `lifecycle.migrations.*` | 随 NuGet 包的 EF 迁移（ADR-019 D19.2）；逐租户迁移由 `tenant/provisioned` 监听器执行 | — |
| `config.schema / scopes` | `runtime.configSchema` / `scopes[]` | — |
| `agent.mcp.expose` | `tools/*.json` 的 `agent.expose` 与 `.mcp.json` | — |
| `marketplace.*` | 市场条目（§8），不在清单 | — |
| `audit.emits[]` | `metadata/events.json` 中 `audit: true` 的事件 | — |

---

## 4. 声明式表面的文件格式

通用规则：Markdown 表面 = YAML front matter + 正文（front matter 按各自 Schema 校验，正文原样交给消费者）；JSON 表面按 JSON Schema 2020-12 校验、`additionalProperties: false`、接受 `^x-`。表面内的名字在组件内唯一，全局形式由注册表拼接为 `<component-id>/<name>`（如 `qian.files/upload`），模型看到的是全局形式经 scope 过滤后的集合。所有注册带条目的 `scope`，scope 内同名遮蔽全局（§5.5）。密钥只以 `secretRef://…` 出现（§11.3）。

### 4.1 `skills/<name>/SKILL.md`

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `name` | `^[a-z][a-z0-9-]*$` ≤ 64 | 是 | 与目录名一致 |
| `description` | 20–1024 字 | 是 | **意图式**：何时用、何时不用；Agent 运行时据此决定是否加载正文 |
| `allowed-tools` | `string[]` | 否 | 正文可调用的工具全局名或通配（`qian.files/*`）；省略 = 继承会话默认；仍受 scope 过滤与权限判定 |
| `license` | `string` | 否 | 省略 = 继承组件 `license` |
| `metadata.version` | SemVer | 否 | 技能自身版本，用于签名载荷与变更追踪 |
| `metadata.visibility` | `public \| internal`，默认 `public` | 否 | `internal` 只能被同组件的 `agents/`、`commands/` 引用，不进目录与市场详情 |
| `metadata.surface` | `const skill` | 否 | 表面类型标记 |

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

### 4.2 `agents/<name>.md`

文件名（去 `.md`）= `name`；正文是系统提示。

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `name` | `^[a-z][a-z0-9-]*$` | 是 | 与文件名一致 |
| `description` | 20–1024 字 | 是 | 意图式：什么任务应委派给它 |
| `tools` | `string[]` | 否 | 允许的工具全局名 / 通配；省略 = 继承父会话 |
| `model` | `string` | 否 | 模型别名（`default`、`fast`、`reasoning`…），由 `qian.model-gateway` 的 `model-capabilities` 解析（ADR-015）；S1 只校验结构 |
| `skills` | `string[]` | 否 | 预加载技能的全局名（可含本组件 `internal` 技能） |
| `metadata.surface` | `const agent` | 否 | 表面类型标记 |

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

### 4.3 `commands/<name>.md`

人类斜杠命令：以 `/<component 短名>:<name>`（无冲突时可省前缀）调用。命令定义**不进入模型消息**（D20.6）：宿主把展开后的命令体作为一条用户消息发送。

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `description` | ≤ 200 字 | 是 | 命令列表里的一行说明 |
| `allowed-tools` | `string[]` | 否 | 命令体执行期间允许的工具；内联 shell 需列出 `shell(<argv 前缀>)` |
| `argument-hint` | `string` | 否 | 如 `<tenant-id> [--strict]` |
| `metadata.surface` | `const command` | 否 | 表面类型标记 |

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

### 4.4 `hooks/hooks.json`

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

| 事件 | 来源 | 分发模式（§5.4） | `matcher` 对象 | 载荷要点 | 典型用途 |
|---|---|---|---|---|---|
| `SessionStart` / `SessionEnd` | Agent 宿主 | `emit` | 会话来源 | `sessionId`、`tenantId`、`scope` | 注入租户说明；清理 agent-session scope |
| `UserPromptSubmit` | Agent 宿主 | `waterfall` | — | `prompt` | 敏感词拦截、补充上下文 |
| `PreToolUse` | Agent 宿主 | `waterfall` | 工具全局名 | `tool`、`input` | 阻断 / 改写输入；二次确认 |
| `PostToolUse` | Agent 宿主 | `parallel` | 工具全局名 | `tool`、`input`、`output` | 审计、派生副作用 |
| `SubagentStop` / `Stop` | Agent 宿主 | `emit` / `bail` | 子 Agent 名 | `agent`、`result` | 汇总；决定是否继续 |
| `tenant/provisioned` | Qian `Tenancy` | `serial` | 租户 id | `tenantId`、`plan` | 逐租户迁移、默认角色实例化 |
| `registry/entry-mounted` / `registry/entry-disabled` | Qian `Registry`（`platform`） | `serial` | 条目 id | `scope`、`entryId`、`plugin` | 条目在某 scope 挂载 / 禁用后的收尾（取代 v1 Enabled / Disabled 状态变更，R34） |
| `tenant/purging` → `tenant/purged` | Qian `Tenancy` | `waterfall` → `serial` | 租户 id | `tenantId`、`retentionDays` | 阻止 / 延后清除；物理删除本插件数据 |
| `identity/signing-in` → `identity/signed-in` / `identity/signed-out` | Qian `Identity` | `waterfall` → `emit` | 连接器名 | `personId`、`membershipId`、`sessionId`、`connector` | 风控、强制 MFA；登录通知 |
| `identity/member-disabled` | Qian `Identity` | `parallel` | 租户 id | `membershipId` | 吊销会话、回收授权 |
| `files/uploading` → `files/uploaded` | Qian `Files` | `waterfall` → `emit` | `purpose` | `descriptor`、`policy` / `fileId`、`sizeBytes` | 改写上传策略、扫描前置；索引、计量 |
| `files/deleting` → `files/deleted` | Qian `Files` | `bail` → `emit` | `purpose` | `fileId` | 返回非空即否决删除；索引清理 |

Agent 宿主事件（大驼峰）由 Agent 运行时定义并透传：校验器只校验命名形式，未知名报 warning；Qian 事件（`<domain>/<event>`）必须在 hooks Schema 固定的矩阵或文档生成器产出的事件目录中，未知名报 error（R39）。完整 Qian 事件目录由代码生成（D20.9 事件图）。

执行方式：

| `type` | 字段 | 语义 | 允许的 tier（§11.4） |
|---|---|---|---|
| `command` | `command: string[]`（argv，不经 shell）、`timeout`（默认 30s，上限 600s）、`env?`（白名单键，值可为 `secretRef://`） | 受限执行器运行；stdin 收事件 JSON 信封；`waterfall` / `bail` 读 stdout JSON 作返回值；退出码非 0 = 失败 | `system`、`official` |
| `http` | `url`、`method?`（默认 `POST`）、`headers?`、`timeout`（默认 10s）、`retries?`（≤ 3） | POST 事件信封（ADR-016：`version / event_id / subject / occurred_at / tenant_id / payload`）；2xx 成功；`waterfall` / `bail` 读响应体 | 全部；域名须在 `policy.egress` |
| `plugin` | `plugin`（组件 id）、`handler`（该组件在 `Apply` 中以 `ctx.Hooks().Expose(name, fn)` 暴露） | 进程内调用 | 由被调用组件的 tier 决定 |

### 4.5 `.mcp.json`

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

| 字段 | 说明 |
|---|---|
| `type` | `http`（无状态 HTTP，MCP 2026-07-28 目标版本，ADR-016 D16.4）或 `stdio` |
| `url` / `headers` | `http` 用；`url` 可含 `{tenant}` 占位由门面按 scope 替换；敏感头必须 `secretRef://` |
| `command` / `args` / `env` | `stdio` 用；`env` 明文密钥校验失败 |
| `scopes` / `profiles` | 可见的 scope 种类（默认取组件 `scopes[]`）/ 只在这些 profile 注册（默认不限） |

### 4.6 `tools/<name>.json`

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

### 4.7 `ui/manifest.json`

格式以 [`yue.md`](yue.md) §3.6 的组件 manifest 与 MF2 外壳契约为准；本 spec 只规定位置与字段族。`pages[].nav` 取代 v1 的 `contributes: qian.shell.nav/v1`，`widgets[]` 对应 `registerWidget`，全部是外壳侧带 scope 的可逆注册。

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

### 4.8 `config/qian.patch.yml`（简述）

格式与 §7.4 完全相同。组合包用它插入一组条目（§7.3）；普通组件用它调整**其它**条目（例如主题组件改外壳的 `brand`）。组件自身的默认配置写在 `schemas/config.schema.json` 的 `default`，不写在这里。

```yaml
- id: shell
  config:
    brand: { primary: "#7C3AED", logo: "qian://com.example.theme.violet/assets/logo.svg" }
```

---

## 5. 运行时模型

### 5.1 基本概念

- **上下文 `ctx`** 是插件与世界交互的唯一入口：服务注册表、事件总线、效果栈、当前 scope。`ctx.<Key>` 在 .NET 是 Definition 包提供的扩展方法（`ctx.Files()`），在 TS 是代理属性（`ctx.files`），都等价于 `ctx.Inject<T>(key)`。
- **注册即副作用，副作用皆可逆**：`Provide`、`On` 与任何对服务键做的注册都返回释放函数并自动压入当前插件的效果栈；`ctx.Effect(disposer)` 把第三方资源也纳入同一栈。卸载时按注册**逆序**释放。
- **注册可原位替换**：`Provide` 返回的注册句柄还能替换实例（.NET `Replace`、TS `replace`），撤销旧实例与登记新实例在同一临界区完成：解析方只看到旧实例或新实例，不会看到缺失；scope、注册顺序、独占性与效果栈位置不变。注册身份随之改变，以 `inject` 依赖该键的条目与撤销后再登记一样回到 `Pending` 并重新 `Apply`。句柄已撤销或上下文已卸载时替换失败。
- **依赖由 `inject` 表达**：装载器按 `inject → provides` 有向图决定顺序；插件在其 `inject` 全部就绪前 `Pending`；键被撤销时它被卸载并回到 `Pending`，键再就绪时重新 `Apply`。
- **注册表 seam 的注册也是副作用**（R38）：Definition 的注册表子服务 `Register(name, impl)` 返回释放函数并压入效果栈；源生成器把它派生为 `provides: ["<Key>.<Registry>:<name>"]`，装载器把该键当作就绪键（可被其它条目 `inject`）；同名第二次注册即挂载失败。
- **运行时无业务语义**：只认服务键字符串、事件定义与 scope key。租户、鉴权、审计等强制执行点是 base 组合包里的 `waterfall` 监听器（ADR-020 §6 对 ADR-003 的修订）。

### 5.2 `Qian.Runtime`（.NET 10）最小 API 草图

```csharp
namespace Qian.Runtime;

public readonly record struct ServiceKey(string Name);                                   // "Files"
public readonly record struct ScopeKey(string Kind, string Id)                           // ("tenant", "t-001")
{ public static readonly ScopeKey Global = new("global", ""); }
public enum DispatchMode { Emit, Waterfall, Parallel, Serial, Bail }
public sealed record QianEvent<TPayload, TResult>(string Name, DispatchMode Mode);      // Definition 包里 static readonly 定义
public static class QianEvent
{ public static QianEvent<TPayload, TResult> Define<TPayload, TResult>(string name, DispatchMode mode) => new(name, mode); }

public interface IQianPlugin
{
    IReadOnlyList<ServiceKey> Inject { get; }                                           // 与 plugin.json inject[] 一致（源生成器校验）
    ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct);
}
public interface IQianPlugin<TConfig> : IQianPlugin                                     // 运行时先按 configSchema 校验再反序列化
{ ValueTask ApplyAsync(IPluginContext ctx, TConfig config, CancellationToken ct); }

public interface IPluginContext
{
    PluginEntry Entry { get; }                                                          // 当前条目：id、plugin、version、scope、layer、config
    ScopeKey Scope { get; }                                                             // 全局为 ScopeKey.Global
    IServiceProvider Services { get; }                                                  // 宿主 DI（keyed services），只读
    ILogger Logger { get; }

    IRegistrationHandle<TService> Provide<TService>(ServiceKey key, TService instance) where TService : class;
                                                                                        // Dispose 撤销；Replace 原位替换实例（§5.1）
    TService Inject<TService>(ServiceKey key) where TService : class;                   // 未就绪抛 ServiceNotReadyException（Apply 内不会发生）
    TService? TryInject<TService>(ServiceKey key) where TService : class;               // optionalInject 用
    ValueTask<TService> WhenReadyAsync<TService>(ServiceKey key, CancellationToken ct) where TService : class;

    void Effect(IDisposable disposer);  void Effect(Action dispose);  void Effect(Func<ValueTask> disposeAsync);

    IDisposable On<TPayload, TResult>(QianEvent<TPayload, TResult> evt, DispatchMode mode, Delegate handler, int priority = 0);
                                                                                        // 委托类型按 mode（§5.4）；mode ≠ evt.Mode 即抛
    ValueTask<TResult> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, TResult> evt, TPayload payload, CancellationToken ct);

    IPluginContext Scoped(ScopeKey scope);                                              // 子上下文：之后的注册归属该 scope
    IDisposable Restrict(ScopeKey scope, Func<Registration, bool> keep);                // 为 scope 过滤全局集合；多个取交集
    IReadOnlyList<Registration> List(ServiceKey key);                                   // 当前 scope 可见的注册（已应用遮蔽与 restrict）
}
public sealed record PluginEntry(string Id, string Plugin, string Version, ScopeKey Scope, int Layer, JsonElement Config);
public sealed record Registration(ServiceKey Key, string Name, ScopeKey Scope, string OwnerEntryId, object Value);
public interface IRegistrationHandle<TService> : IDisposable where TService : class
{ void Replace(TService instance); }                                                    // 已撤销或上下文已卸载时抛 ObjectDisposedException
```

与 Kun 的衔接：`Provide` 底层是 .NET DI keyed services 的可撤销封装；进程内事件直接分发，需跨进程的集成事件由 `qian.events` 插件桥接到 `Kun.Mediator` + Outbox（运行时本身不依赖 RabbitMQ）。实现类用 `[QianPlugin("<component-id>")]` 标注，源生成器据此派生 `runtime.entry`、`inject[]`、`provides[]`（静态分析 `Provide` 调用）。

### 5.3 `@qian/runtime`（TypeScript）最小 API 草图

S1-DEC-014 已于 2026-10-11 批准：本节的 `@qian/runtime` 是逻辑导入名；GitHub Packages 私有源中的物理包名为 `@idadawn/qian-runtime`，消费者用 npm alias `"@qian/runtime": "npm:@idadawn/qian-runtime@<固定版本>"` 安装。包所有者、源码关联与真实消费验收见 ADR-021 D21.6，运行时语义不变。

```ts
export type ServiceKey<T = unknown> = string & { readonly __type?: T };                 // "files"
export type ScopeKey = "global" | `${"tenant" | "app" | "agent-session"}:${string}`;
export type DispatchMode = "emit" | "waterfall" | "parallel" | "serial" | "bail";
export interface QianEvent<P, R = void> { readonly name: string; readonly mode: DispatchMode; readonly __p?: P; readonly __r?: R }
export function defineEvent<P, R = void>(name: string, mode: DispatchMode): QianEvent<P, R>;
export type Disposer = () => void | Promise<void>;
export type RegistrationHandle<T = unknown> = Disposer & { readonly replace: (value: T) => void };  // 调用即撤销；replace 原位替换值（§5.1）

export interface QianPlugin<C = unknown> {
  readonly name: string;                                   // 组件 id
  readonly inject?: readonly ServiceKey[];
  readonly optionalInject?: readonly ServiceKey[];
  apply(ctx: Context, config: C): void | Promise<void>;
}
export interface Context {
  readonly entry: { id: string; plugin: string; version: string; scope: ScopeKey; layer: number };
  readonly scope: ScopeKey;
  provide<T>(key: ServiceKey<T>, value: T): RegistrationHandle<T>;
  inject<T>(key: ServiceKey<T>): T;                        // 未就绪抛 ServiceNotReadyError
  tryInject<T>(key: ServiceKey<T>): T | undefined;
  whenReady<T>(key: ServiceKey<T>, signal?: AbortSignal): Promise<T>;
  effect(disposer: Disposer): void;
  on<P, R>(evt: QianEvent<P, R>, mode: DispatchMode, handler: HandlerFor<P, R>, priority?: number): Disposer;
  dispatch<P, R>(evt: QianEvent<P, R>, payload: P): Promise<R>;
  scoped(scope: ScopeKey): Context;
  restrict(scope: ScopeKey, keep: (reg: Registration) => boolean): Disposer;
  list(key: ServiceKey): readonly Registration[];
  readonly [key: `${Lowercase<string>}`]: unknown;        // 代理属性：ctx.files === ctx.inject("files")
}
```

TS 侧服务键小驼峰（`files`），.NET 侧大驼峰（`Files`）；两侧 Definition 包各自声明常量，文档生成器把它们对应到同一 seam。S1 内部以固定版本 vendor 的 Cordis（MIT）作为实现，公共 API 为 `@qian/runtime`，薄适配层对齐本节与 ADR-020 的语义（ADR-020 §7 第 1 条，→ §13）；本节 API 语义、§5.4–§5.6 与 §10.2 TCK 以我们的语义为准，不随实现变化，若 TCK 对不上可换为自研实现而不改任何组件代码。

### 5.4 五种分发模式

每个事件在 Definition 包**定义处**标注模式；`ctx.On` 调用点必须写同一模式（重复是为了调用点可读并让 CI 做一致性校验，D20.3）。

| 模式 | 监听器签名（.NET / TS） | 调用顺序 | 返回值 | 异常 | 典型用途 |
|---|---|---|---|---|---|
| `emit` 观察 | `Func<TPayload, ValueTask>` / `(p) => void \| Promise<void>` | priority 降序、同级按挂载顺序，逐个 await | 无 | 记录日志、不传播到发起方，其余监听器继续 | 通知、审计、索引 |
| `waterfall` 环绕 | `Func<TPayload, Func<ValueTask<TResult>>, ValueTask<TResult>>` / `(p, next) => Promise<R>` | 按 priority 组成洋葱；调用 `next()` 才委托下一层 | 最外层返回值 | 向发起方传播；未捕获即中断 | 鉴权 PEP、租户校验、输入改写、阻断 |
| `parallel` 扇出 | `Func<TPayload, ValueTask<TResult>>` / `(p) => Promise<R>` | 全部并发 | `IReadOnlyList<TResult>` / `R[]`，顺序 = 监听器顺序 | 等全部完成后抛聚合异常，`Partial` 含成功结果 | 多渠道通知、并行校验 |
| `serial` 按序 | 同 `parallel` | 逐个 await | `IReadOnlyList<TResult>` | 首个异常处中断，后续不执行 | 逐步迁移、有序收尾 |
| `bail` 首值 | `Func<TPayload, ValueTask<TResult?>>` / `(p) => Promise<R \| undefined>` | 逐个 await | 首个非空返回值；全空为 `default` | 向发起方传播 | 否决、解析器链、首个匹配 Provider |

补充：`waterfall` 的链尾由发起方 `DispatchAsync(evt, payload, terminal)` 或 `Define` 时给出；分发的 scope = 发起方 `ctx.Scope`，全局监听器对所有 scope 的分发可见，scope 内监听器只对该 scope 可见；事件名全局唯一，两个 Definition 定义同名事件即挂载失败；`hooks.json` 的 `command` / `http` 监听器由 `qian.hooks` 插件转成同模式进程内监听器，priority 默认 `-100`（在代码监听器之后）。

### 5.5 scope：两层扁平

- **形状**：`global` 或 `<kind>:<id>`，`kind ∈ { tenant, app, agent-session }`。没有父指针；`app:crm-7` 属于哪个租户是 lineage 数据（由 `Tenancy` 回答），不是 scope 结构。
- **遮蔽**：`ctx.Scoped(scope)` 返回的子上下文做的注册归属该 scope；在该 scope 解析 `(key, name)` 时先找 scope 内（命中即遮蔽全局项），未命中再找全局；scope 之外看不到 scope 内注册。
- **`restrict`**：`ctx.Restrict(scope, keep)` 为该 scope 过滤**全局**集合；多个 restriction 取交集；被过滤项在该 scope 内不出现在 `List()`、目录与提示词中，执行时返回与「不存在」完全相同的 `RegistrationNotFound`。restriction 也是副作用，随所属插件卸载而解除。
- **不可区分原则（R32）**：被 scope 过滤或未挂载的组件，对业务与模型表面（路由、`ctx.Tools`、能力目录、菜单）一律 404，与不存在无法区分；只有管理面 `/api/registry/*` 对持有 `qian.registry:scope:read` 的调用者返回 403，错误码即原因（`PLUGIN_NOT_MOUNTED` 被某层 patch 禁用 / `PLUGIN_RESTRICTED` 被某条 restriction 过滤 / `PLUGIN_DEPENDENCY_PENDING` 依赖未就绪 / `PLUGIN_NOT_ENTITLED` 未授权），`message` 带层级或 restriction id、`pointer` 指向该条目（R48）。
- **无继承**：`agent-session:s-9` 不会自动看到 `tenant:t-001` 的注册；需要「会话继承租户」时由 Agent 门面插件在创建会话 scope 时显式复制所需注册或在 `keep` 里查询 lineage——这是数据，不是 scope 机制。
- **租户开关**（D20.5）：启用 = 在 `tenant:<id>` scope 挂载条目（租户 scope patch，§7.6）；停用 = `disabled: true`；定制 = 替换该条目的 `config`，或在租户 scope 内对同名键做遮蔽注册。
- **校验**：条目 `scope.kind` 必须在组件 `scopes[]` 中。

### 5.6 生命周期

```text
条目进入插件树
  → Resolve   定位组件文件夹 / 包、读清单、校验 Schema 与语义、按 configSchema 校验 config、检测 inject 环路
  → Load      module：定位 runtime.entry 并实例化（程序集随宿主加载）；声明式表面：解析文件
  → Pending   等待 inject[] 全部就绪（默认 30s 后在日志与 qian doctor 报「等待 X 的条目」）
  → Apply     ApplyAsync(ctx, config)；期间的 Provide / On / Effect 压入该条目的效果栈
  → Ready     provides[] 标记就绪 → 解锁依赖它们的 Pending 条目
  ← Unmount   逆序弹出效果栈（Provide 撤销 → 依赖者先被 Unmount 回到 Pending）→ 条目移出
  ↻ Reload    声明式表面：重新解析 → 与现有注册 diff → 先新增、再经注册句柄原位替换、最后撤销变化的注册（新旧都有的表面
              对读取方不出现缺失）；代码条目：config 变更 = Unmount + Apply，代码变更 = 进程重启（S1；热装卸 S2 可选 → §13 第 5 条）
```

- `Apply` 只描述「我提供什么、监听什么」，不做一次性业务动作；逐租户初始化放在 `tenant/provisioned` 监听器。
- `Apply` 抛异常 = 条目 `Failed`：效果栈立即逆序释放，依赖者保持 `Pending`，宿主继续启动其它条目；`policy.tier: system` 的条目 `Failed` 使 `/readyz` 失败（profile `health.readyRequiresTiers`）。
- `Apply` 尚未结束时，所 `inject` 的键被其它条目撤销、替换或经 restriction 隐藏（例如热重载的文件监视线程、Registry 刷新租户 scope），本次 `Apply` 无论成败都作废：效果栈逆序释放、条目回到 `Pending`，键再就绪后重新 `Apply`，判据与 `Ready` 后失效相同，不记为 `Failed`。`Apply` 自己的注册或 restriction 改变了所注入键的可见性时仍为 `Failed`，否则条目 `Ready` 后会立即自我失效。
- 整树 teardown 按挂载顺序逆序 Unmount；同一 `(scope, id)` 只能有一个条目；`service` 形态的条目在其 `/readyz` 通过后由装载器把 `provides[]` 标记就绪（Provider 为远程代理；`platform` 下其状态由 `qian.registry` 维护）。
- 装载器属于 `Qian.Runtime`（D20.1），任何 profile 都能从文件 patch 直接装树；`qian.registry` / `qian.lifecycle` / `qian.capabilities` 只是 `platform` profile 里叠加的数据库注册表、生命周期管理 API 与能力目录（R34）。

### 5.7 示例：C# 插件——把 `Kun.Storage` 包装为 `ctx.Files` Provider

```csharp
using Kun.Storage.Abstractions;                 // IFileStorage（Kun 新线）
using Qian.Files.Abstractions;                  // FilesKeys.Files、IFileService、FilesConfig
using Qian.Runtime;

[QianPlugin("qian.files")]
public sealed class FilesPlugin : IQianPlugin<FilesConfig>
{
    public IReadOnlyList<ServiceKey> Inject => [TenancyKeys.Tenancy, ConfigKeys.Config, AuthzKeys.Authz, EventsKeys.Events, AuditKeys.Audit];
    public async ValueTask ApplyAsync(IPluginContext ctx, FilesConfig config, CancellationToken ct)
    {
        var files = new FileService(ctx.Tenancy(), ctx.Authz(), config);                  // 租户前缀、配额、上传策略、元数据表；含 Backends 注册表
        foreach (var (name, factory) in KunStorageBackends.BuiltIn)                       // local / s3 / oss（Kun.Storage.Local / .S3 / .Oss）
            files.Backends.Register(name, factory);                                       // => provides "Files.Backends:<name>"（R38 注册表 seam）
        files.UseBackend(config.Storage.Kind);                                            // 按名称选用
        ctx.Provide(FilesKeys.Files, (IFileService)files);                                // => ctx.Files()
        ctx.On(TenancyEvents.Provisioned, DispatchMode.Serial, (TenantProvisioned e) => files.EnsureBucketAsync(e.TenantId, ct));
        ctx.On(TenancyEvents.Purged, DispatchMode.Serial, (TenantPurged e) => files.PurgeAsync(e.TenantId, ct));
        ctx.Effect(files.DisposeAsync);
        await files.WarmupAsync(ct);
    }
}
```

### 5.8 示例：TS 插件——把平台文件 API 包装为外壳的 `ctx.files`

```ts
import type { QianPlugin } from "@qian/runtime";
import { filesKey, type FileClient } from "@qian/files-abstractions";
import { httpKey } from "@qian/http-abstractions";
import { identityEvents } from "@qian/identity-abstractions";

export const filesHttp: QianPlugin<{ baseUrl: string }> = {
  name: "qian.files.client",
  inject: [httpKey],
  apply(ctx, config) {
    const http = ctx.inject(httpKey);
    const client: FileClient = {
      upload: (blob, opts) => http.postMultipart(`${config.baseUrl}/files`, blob, opts),
      downloadUrl: (id, ttl = 86_400) => http.get(`${config.baseUrl}/files/${id}/url?ttl=${ttl}`),
      quota: (tenantId) => http.get(`${config.baseUrl}/tenants/${tenantId}/files/quota`),
    };
    ctx.provide(filesKey, client);                                            // => ctx.files
    ctx.on(identityEvents.signedOut, "emit", () => http.abortAll(config.baseUrl));
    ctx.effect(() => http.abortAll(config.baseUrl));
  },
};
```

---

## 6. seam 三角色：包命名与依赖规则

### 6.1 规则

| 角色 | 包名 | 内容 | 允许依赖 | 禁止依赖 |
|---|---|---|---|---|
| Service Definition | `Qian.<X>.Abstractions` / `@qian/<x>-abstractions` | 服务键常量、服务接口、DTO / 词汇类型、事件定义（含模式）、`ctx.<X>()` 扩展、错误码；**无实现** | `Qian.Runtime`、`Kun.*.Abstractions`、其它 `Qian.*.Abstractions` | 任何 Provider 包；`qian.registry` / `qian.lifecycle` / `qian.capabilities` |
| Service Provider | `Qian.<X>.<Provider>`（如 `Qian.Files`、`Qian.Identity.Server`、`Qian.Identity.Client`、`Qian.Authz`）/ `@qian/<x>-<provider>` | `IQianPlugin` 实现、`Provide` 该键、EF 迁移、组件文件夹（包内 `qian/`） | 自己与其它 seam 的 Definition、`Kun.*` 实现包、`Qian.Runtime` | 其它 seam 的 Provider；`qian.registry` / `qian.lifecycle` |
| Consumer | 任何组件（含工具、UI、技能） | `inject` 该键，只经接口调用 | 所需 seam 的 Definition、`Qian.Runtime` | 任何 Provider 包 |

S1-KUN-010 对 Definition 的 Kun 直接依赖设置门禁；因既有公开契约暂留的逐边例外及理由见 [测试规范](../../../tests/README.md#definition-的-kun-直接依赖s1-kun-010)，新增实现包不能自动沿用这些例外。

- **seam 分两种风格**（已裁决 R38）：
  - (a) **单例 seam**：同 scope 内同键只能有一个 `Provide`，第二个即挂载失败；多实现靠 patch 替换条目或租户 scope 遮蔽。S1：`Tenancy`、`Identity`、`Authz`、`Files`、`Config`、`Events`、`Audit`、`Notify`、`Scheduler`…
  - (b) **注册表 seam**：Definition 本身（或其子服务）是一个注册表 `IRegistry<T> { IDisposable Register(string name, T impl); T? Get(string name); IReadOnlyList<Registration> List(); }`；Provider 按名称独占注册（同名第二个失败），消费者按名称或策略选用；清单 `provides` 写 `<Key>.<Registry>:<name>`。S1 的注册表子服务：`Identity.Connectors`（`ctx.Identity().Connectors`）、`Notify.Channels`、`Files.Backends`、`Scheduler.JobTypes`。注册表注册同样带 scope：租户 scope 内注册同名项遮蔽全局项。
  - **连接器一律走 (b)**：身份连接器注册到 `ctx.Identity.Connectors`，通知渠道注册到 `ctx.Notify.Channels`，存储后端注册到 `ctx.Files.Backends`。
- 组件 id 与包名映射：Provider 组件 id = 去掉 `Qian.` 前缀、小写、加 `qian.`（`Qian.Files` → `qian.files`、`Qian.Identity.Server` → `qian.identity.server`、`Qian.Data.Dialect.Pg` → `qian.data.dialect.pg`）；不同名处在 `runtime.nuget[]` 显式列出即可，CLI 不要求同名（ADR-019 §7 的映射遗留项由此消解）。
- `AddQian<X>()` 保留在 Provider 包内，只是 `Apply` 内部的装配细节（D20.10）；独立模式（ADR-019 D19.4）= `minimal` / `standalone` profile 上的同一份代码。
- CI（§10.3 G3）用 `dotnet list package --include-transitive` 与 npm lockfile 断言上表的禁止项。

### 6.2 Files seam

| 角色 | 包 / 组件 | 内容 |
|---|---|---|
| Definition | `Qian.Files.Abstractions` / `@qian/files-abstractions` | 键 `Files`（TS `files`）→ `IFileService { UploadAsync, GetAsync, GetDownloadUrlAsync, DeleteAsync, GetQuotaAsync, ListAsync, Backends }`；`Backends` 为注册表子服务（R38）：`Register(name, IStorageBackendFactory)` 返回释放函数、同名独占，`config.storage.kind` 按名称选用；DTO `FileDescriptor`、`UploadPolicy`、`Quota`、`DownloadTicket`；SPI `IFilePreviewProvider`（可替换预览提供者，ADR-003 D3.9）；事件 `files/uploading`（waterfall）、`files/uploaded`（emit）、`files/deleting`（bail）、`files/deleted`（emit）；错误码 `files.quota_exceeded`、`files.type_not_allowed`、`files.not_found` |
| S1 Provider | `Qian.Files` → 组件 `qian.files`（§3.1） | 基于 Kun 新线 `IFileStorage`：租户前缀、配额、上传策略、元数据表 `plugin_qian_files`、`ContentDispositionPolicy` 不可关闭、预览票据；内置后端 `local` / `s3` / `oss`（`Kun.Storage.Local / .S3 / .Oss`）注册进 `Files.Backends`，故 `provides: ["Files", "Files.Backends:local", "Files.Backends:s3", "Files.Backends:oss"]`（§3.1）；第三方后端组件（如 `com.example.files.sharepoint`）`inject: ["Files"]`、`provides: ["Files.Backends:sharepoint"]`、`policy.egress` 声明出网目标 |
| S1 Consumer | `qian.sample.crm`（附件）、`qian.identity.server`（头像）、`qian.notify`（邮件附件）、`qian.shell` 的 Upload 控件（经 `@qian/files-abstractions`）、工具 `qian.files/upload` 等、技能 `upload-with-policy` | 全部只依赖 Definition；换 `Files` 的 Provider 或新增一个后端都不改一行 |

### 6.3 Identity seam

| 角色 | 包 / 组件 | 内容 |
|---|---|---|
| Definition | `Qian.Identity.Abstractions` / `@qian/identity-abstractions` | 键 `Identity`（TS `identity`）→ `IIdentityService { CurrentPrincipal, GetPersonAsync, GetMembershipAsync, ListSessionsAsync, RevokeSessionsAsync, Connectors }`；`Connectors` 为注册表子服务（R38）：`Register(name, IIdentityConnector)` 返回释放函数、同名独占，`IIdentityConnector { BeginSignInAsync, CompleteSignInAsync, SyncDirectoryAsync? }`；DTO `Principal`（S2S claim 形状，ADR-016 D16.3）、`Person`、`Membership`、`Session`；事件 `identity/signing-in`（waterfall）、`identity/signed-in`（emit）、`identity/signed-out`（emit）、`identity/member-disabled`（parallel） |
| S1 Provider | `Qian.Identity.Server` → `qian.identity.server`（`identity` profile：嵌入式 OpenIddict 唯一 issuer、Person / Membership、会话中心、组织目录最小集）；`Qian.Identity.Client` → `qian.identity.client`（`platform` / `standalone` / `gateway` profile：RP 接入、Cookie 会话、S2S 校验，远程访问 Server）；**连接器组件**（R28 / R38）`Qian.Identity.Connector.Oidc` → `qian.identity.connector.oidc`（`inject: ["Identity"]`，`provides: ["Identity.Connectors:oidc"]`，`policy.egress: ["<idp-host>:443"]`）与 `Qian.Identity.Connector.WeCom` → `qian.identity.connector.wecom`（`provides: ["Identity.Connectors:wecom"]`，`policy.egress: ["qyapi.weixin.qq.com:443"]`），在 `Apply` 中 `ctx.Identity().Connectors.Register("oidc" \| "wecom", …)`，`runtime.form: module` | `Identity` 的两个 Provider 分别在不同 profile 的树里；`qian.identity.server` 的登录页按 `Connectors.List()` 渲染可用连接器；S3 租户自助 IdP = 在租户 scope 注册同名 `oidc` 连接器遮蔽全局项 |
| S1 Consumer | `qian.authz`（主体）、`qian.gateway`（Cookie 换 S2S）、`qian.audit`（actor）、`qian.shell` 认证页族、连接器组件、工具 `qian.identity/list-connectors` 等 | 把 `Qian.Identity.Server` 换成外部 IdP 连接器方案时登录页与会话中心不变（D20.4 例） |

### 6.4 Authz seam

| 角色 | 包 / 组件 | 内容 |
|---|---|---|
| Definition | `Qian.Authz.Abstractions` / `@qian/authz-abstractions` | 键 `Authz`（TS `authz`）→ `IPermissionChecker { Check, BatchCheck, ListAllowedActions }`（端口归属沿 ADR-019 §7：建议只依赖并再导出 `Kun.Authorization` 端口）、`IDataPermissionFilter`、`IFieldPolicy`、`IPermissionRegistry.Register(PermissionPoint[])` 返回释放函数；DTO `PermissionPoint`（`<component-id>:<resource>:<action>`）、`RoleTemplate`、`Decision { allow, reason, obligations }`；事件 `authz/checking`（waterfall，可包裹判定做 step-up）、`authz/decided`（emit，供审计与可解释性）；`metadata/permissions.json`、`metadata/roles.json` 的 Schema |
| S1 Provider | `Qian.Authz` → `qian.authz` | 租户域 RBAC、数据权限编译为谓词叠加 RLS、字段权限、管理 API；**PEP** 作为 `Kun.Mediator` 管道行为与 `command/dispatching` 的 `waterfall` 监听器在 `Apply` 中注册（强制执行点）；读取全树 `metadata/permissions.json` 登记权限点，监听 `tenant/provisioned` 实例化 `metadata/roles.json` 的默认角色 |
| S1 Consumer | 每个声明 `requiredPermission` 的工具、`qian.gateway`（scope 校验）、`qian.shell`（权限码目录做 UI 裁剪）、`qian.files`（上传策略里的权限检查）、`qian.audit`（记录 `authz/decided`） | 高级权限模式（S3 可选）= 另一个 Provider 或 `authz/checking` 的 waterfall 监听器，Consumer 不变 |

.NET 契约落地（S1-AZ-001）：权限点类型使用 `PermissionDefinition`，`IAuthzService` 继承已发布 Kun 的 `IPermissionChecker` 五个方法，并通过 DataPermissions / Fields 暴露原数据与字段端口；判定使用原 `PermissionDecision`，不复制同名接口。受保护 Mediator 命令采用 `AddAuthorizedHandler` 逐命令将 Kun 权限行为与 handler 登记在同一 builder，QIAN0005 拦截直接注册和未知请求类型的泛型包装器。清单注册表、角色模板及其 Schema 属 S1-AZ-002，完整 PEP、扩展事件与审计集成属 S1-AZ-004；契约包存在不代表这些 Provider 已实现。

---

## 7. profile、组合包与 patch

### 7.1 分层模型与叠加顺序

运行中的 Qian 是一棵插件树，由启动时**按序叠加**的层组成，后层覆盖前层（D20.7）：

| 层序 | 层 | 来源 | 内容 |
|---|---|---|---|
| 0…n | 组合包层 | profile `bundles[]` 按列出顺序，每个组合包一层 | 组合包的 `config/qian.patch.yml`（以插入条目为主） |
| n+1 | profile patch | `profiles/<name>/qian.patch.yml` | 本 profile 的替换 / 禁用 / 插入 |
| n+2 | home patch | `$QIAN_HOME/qian.patch.yml`（默认 `~/.qian/qian.patch.yml`） | 开发者本机或节点级覆盖 |
| n+3 | CLI overlay | `--patch <file>`（可重复，按出现顺序） | 一次性覆盖（CI、故障演练） |
| 运行中 | 租户 scope patch | 数据库表（§7.6） | 租户 scope 的条目（启用 / 禁用 / 定制） |

叠加规则：条目身份 = `(scope, id)`；后层同身份记录**替换整个 `config`**（不深合并——改一个字段就把整个 `config` 写一遍，这保证 `dump-config` 的任何条目都能原样复制成一条 patch）；后层给出 `plugin` 时替换组件（Provider 替换），省略则沿用；`disabled: true` 使条目从树中移除，更后的层可再 `disabled: false`；插入新条目时 `plugin` 必填、`config` 可为 `{}`（由 `configSchema` 的 `default` 补齐）；层叠完成后统一 Resolve（§5.6），层序只影响覆盖关系不影响挂载顺序；每条记录带来源 `layer`，`dump-config` 原样打印。

### 7.2 profile 文件 `profiles/<name>/qian.profile.yml`

```yaml
name: platform
description: 平台宿主：base 组合包 + shell 组合包 + 可选 model-gateway 组合包（R31）；不承诺 AOT
bundles:                                   # 按序叠加；可带 range，解析结果写进 .qian/lock.json
  - qian.bundle.base@^1
  - qian.bundle.shell@^1
  - qian.bundle.model-gateway@^1           # 可选；其条目默认 disabled，需在 profile patch 里启用
patch: ./qian.patch.yml                    # profile 自己的 patch（可省略）
hmr:
  surfaces: true                           # 声明式表面运行中重载（S1 支持）
  code: false                              # 代码热装卸（S2 可选；S1 校验器拒绝 true）
build:
  aot: false                               # gateway profile 为 true（D20.10）；为 true 时树中不得出现 hmr.code、function 形态与非 AOT 组件
hooks:
  allowCommand: true                       # hooks.json 的 command 类型是否执行（默认 false，§11.4）
health:
  readyRequiresTiers: [system]             # 这些 tier 的条目 Failed 时 /readyz 失败
```

`name` 与目录名一致，也是 `qian --profile <name>` 的参数；profile 之间**不继承**（与 scope 同一哲学），共用内容放组合包。`build.aot` / `hmr` / `health` / `hooks.allowCommand` 四个字段已裁决保留（R41）。

### 7.3 组合包的声明方式

组合包是组件，随 NuGet / npm 包分发，包内放在 `qian/` 内容目录；CLI 与装载器扫描引用的包找到 `qian/.qian-plugin/plugin.json`。

```xml
<!-- Qian.Bundle.Base.csproj（NuGet 载体） -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PackageId>Qian.Bundle.Base</PackageId>
    <Version>1.0.0</Version>
    <QianComponentRoot>.</QianComponentRoot>          <!-- Qian.Runtime.Sdk 目标：把组件文件夹打进包的 qian/ 并校验清单 -->
    <IncludeBuildOutput>false</IncludeBuildOutput>    <!-- 组合包无代码 -->
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Qian.Runtime" Version="1.0.0" />
    <PackageReference Include="Qian.Tenancy" Version="1.0.0" />
    <PackageReference Include="Qian.Files" Version="1.0.0" />
    <!-- …与 plugin.json runtime.nuget[] 一致；源生成器比对，漂移即构建失败 -->
  </ItemGroup>
  <ItemGroup>
    <QianComponentFile Include=".qian-plugin/**;config/**;skills/**;assets/**;README.md;AGENTS.md" />
  </ItemGroup>
</Project>
```

```json
// package.json（npm 载体：前端组合包或声明式组件）
{ "name": "@qian/bundle-shell-base", "version": "1.0.0",
  "files": [".qian-plugin", "config", "skills", "ui", "assets", "README.md", "AGENTS.md"],
  "qian": { "component": ".qian-plugin/plugin.json" },
  "peerDependencies": { "@qian/runtime": "^1" },
  "dependencies": { "@qian/shell": "^1.0.0", "@qian/files-client": "^1.0.0" } }
```

`qian.bundle.base` 的 `config/qian.patch.yml`（插入条目；条目 id 用短名，不带 `qian.` 前缀）：

```yaml
- { id: registry,      plugin: qian.registry,        config: { store: pg } }
- { id: lifecycle,     plugin: qian.lifecycle,       config: {} }
- { id: capabilities,  plugin: qian.capabilities,    config: {} }
- { id: hooks,         plugin: qian.hooks,           config: {} }
- { id: tenancy,       plugin: qian.tenancy,         config: { resolver: { mode: production, allowHeaderFallback: false } } }
- { id: identity,      plugin: qian.identity.client, config: { issuer: "https://id.example.internal", clientId: qian-platform, clientSecret: "secretRef://platform/identity/client-secret" } }
- { id: authz,         plugin: qian.authz,           config: { advancedMode: false } }
- { id: data,          plugin: qian.data,            config: { dialect: pg, connection: "secretRef://platform/pg/qian-platform", rls: { enforce: true } } }
- { id: files,         plugin: qian.files,           config: { storage: { kind: local, root: /var/lib/qian/files }, quota: { perTenantBytes: 10737418240 } } }
- { id: config,        plugin: qian.config.apollo,   config: { meta: "http://apollo-config.apollo:8080", appId: qian-platform, namespaces: [application, qian.common.trust] } }
- { id: events,        plugin: qian.events,          config: { broker: "secretRef://platform/rabbitmq/url", outbox: { lock: redis } } }
- { id: audit,         plugin: qian.audit,           config: { chain: { store: pg } } }
- { id: observability, plugin: qian.observability,   config: { otlp: "http://otel-collector.observability:4317" } }
- { id: notify,        plugin: qian.notify,          config: { channels: [inbox, email, sms] } }
- { id: notify.inbox,  plugin: qian.notify.channel.inbox, config: {} }
- { id: notify.email,  plugin: qian.notify.channel.email, config: { smtp: "secretRef://platform/smtp/url" } }
- { id: notify.sms,    plugin: qian.notify.channel.sms,   config: { provider: "secretRef://platform/sms/credential" } }
- { id: scheduler,     plugin: qian.scheduler,       config: { store: pg } }
- { id: realtime,      plugin: qian.realtime,        config: { transport: sse } }
- { id: entitlement,   plugin: qian.entitlement,     config: {} }
```

`qian.bundle.shell` 的 patch 插入 `shell → qian.shell`（yue 外壳静态制品与外壳 API）；可选的 `qian.bundle.model-gateway` 的 patch 只有一条 `- { id: model-gateway, plugin: qian.model-gateway, config: { higress: "http://higress-gateway.higress:8080" }, disabled: true }`（P1，默认不挂载，需要时在 profile patch 里 `disabled: false`）。

### 7.4 patch 条目格式 `qian.patch.yml`

一份 patch 文件是**条目数组**，每条按 `https://qian.dev/schemas/patch/v2` 校验。

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `id` | `^[a-z][a-z0-9.-]*$` | 是 | 条目 id；与 `scope` 构成身份 |
| `scope` | `global` 或 `<kind>:<id>` | 否，默认 `global` | 文件 patch 允许写租户 scope（开发与演练）；生产的租户 scope 条目以数据库为准 |
| `plugin` | `<component-id>[@<version>]` | 插入时是 | 省略版本 = lock 中已解析版本 |
| `config` | `object` | 插入时是 | 整个配置对象（整体替换） |
| `disabled` | `boolean` | 否 | 从树中移除 |
| `note` | `string` | 否 | 说明，`dump-config` 原样打印 |

示例（`profiles/platform/qian.patch.yml`：生产换 OSS、启用模型网关、给某租户独立的 S3 桶）：

```yaml
- id: files
  config:
    storage: { kind: oss, bucket: qian-prod, endpoint: "https://oss-cn-hangzhou.example.internal",
               accessKey: "secretRef://platform/oss/access-key", secretKey: "secretRef://platform/oss/secret-key" }
    quota: { perTenantBytes: 53687091200 }
  note: 生产用 OSS；配额 50 GiB
- id: model-gateway
  disabled: false
- id: files
  scope: tenant:t-001
  plugin: qian.files
  config:
    storage: { kind: s3, bucket: t-001-files, endpoint: "https://s3.example.internal",
               accessKey: "secretRef://tenant/t-001/s3/access-key", secretKey: "secretRef://tenant/t-001/s3/secret-key" }
  note: 租户 t-001 要求数据落自己的桶（租户 scope 遮蔽全局 files）
```

### 7.5 `qian dump-config` 输出示例

```yaml
# $ qian --profile platform dump-config --format yaml --resolve
profile: platform
resolvedAt: "2026-10-06T08:12:31Z"
layers:
  - { index: 0, kind: bundle, ref: qian.bundle.base@1.0.0 }
  - { index: 1, kind: bundle, ref: qian.bundle.shell@1.0.0 }
  - { index: 2, kind: bundle, ref: qian.bundle.model-gateway@1.0.0 }
  - { index: 3, kind: profile-patch, path: profiles/platform/qian.patch.yml }
  - { index: 4, kind: home-patch, path: ~/.qian/qian.patch.yml, present: false }
  - { index: 5, kind: cli, path: ./ops/prod.patch.yml }
entries:
  - { id: registry, scope: global, plugin: qian.registry@1.0.0, layer: 0, inject: [], provides: [Registry], config: {} }
  - id: files
    scope: global
    plugin: qian.files@1.0.0
    layer: 3                                  # 最后一次改写它的层：profile patch
    inject: [Tenancy, Config, Authz, Events, Audit]
    provides: [Files]
    config:
      storage: { kind: oss, bucket: qian-prod, endpoint: "https://oss-cn-hangzhou.example.internal",
                 accessKey: "secretRef://platform/oss/access-key", secretKey: "secretRef://platform/oss/secret-key" }
      quota: { perTenantBytes: 53687091200 }
    note: 生产用 OSS；配额 50 GiB
  - id: files
    scope: tenant:t-001
    plugin: qian.files@1.0.0
    layer: 3
    inject: [Tenancy, Config, Authz, Events, Audit]
    provides: [Files]
    config: { storage: { kind: s3, bucket: t-001-files, endpoint: "https://s3.example.internal",
                         accessKey: "secretRef://tenant/t-001/s3/access-key", secretKey: "secretRef://tenant/t-001/s3/secret-key" } }
  - { id: model-gateway, scope: global, plugin: qian.model-gateway@1.0.0, layer: 3, disabled: false, inject: [Config, Tenancy], provides: [Models],
      config: { higress: "http://higress-gateway.higress:8080" } }
mountOrder: [registry, lifecycle, capabilities, config, observability, tenancy, data, identity, authz, events, audit, files, …]
diagnostics: []
```

参数：`--format yaml|json`、`--entry <id>`、`--scope <scope>`（叠加运行中的租户 scope patch，需平台连接）、`--resolve`（附 `mountOrder` 与 `diagnostics`：等待中 / 失败 / 环路）、`--patch <file>`、`--redact`（默认开：`secretRef://` 原样打印，已解析的密钥值绝不出现）。任一 `entries[]` 元素去掉 `layer / inject / provides` 后即是一条合法 patch 记录。

### 7.6 租户 scope patch 的数据库表形态

租户层存放在 `qian.registry` 拥有的 PG schema `plugin_qian_registry`，一行 = 一条 patch 记录，按**同一份** `patch/v2` Schema 校验（应用层 `IPatchValidator`，与文件 patch 共用实现）：

```sql
CREATE TABLE plugin_qian_registry.scope_patches (
  tenant_id       text        NOT NULL,            -- RLS 键；app / agent-session scope 也归属某租户（lineage）
  scope           text        NOT NULL,            -- 'tenant:t-001' | 'app:crm-7' | 'agent-session:s-9'
  entry_id        text        NOT NULL,
  plugin          text        NULL,                -- 组件 id（插入 / 替换 Provider 时非空）
  plugin_version  text        NULL,
  config          jsonb       NULL,                -- 整个 config；NULL = 沿用全局条目 config
  disabled        boolean     NOT NULL DEFAULT false,
  note            text        NULL,
  revision        integer     NOT NULL DEFAULT 1,  -- 乐观并发
  updated_by      text        NOT NULL,
  updated_at      timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (tenant_id, scope, entry_id)
);
-- RLS：tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')（Kun RLS 拦截器装配，ADR-004）
```

`qian.registry` 在租户上下文建立时把该租户的行叠为运行中的最后一层，并发出 `registry/entry-mounted` / `registry/entry-disabled`。**租户挂载状态只存这张表**，不再有 v1 的 Enabled / Disabled 状态机（R34）；「买没买、能不能」留在 `qian.entitlement`：写入前调用 `ctx.Entitlement().CheckAsync(tenant, plugin)`，未授权返回 `NOT_ENTITLED`；`policy.required: true` 的组件拒绝写入 `disabled: true`。写入路径：管理面 `PUT /api/registry/tenants/{tenant}/patches/{scope}/{entry}`（幂等，带 `revision`，§9.5）与 CLI `qian add --scope tenant:<id>`，两者由 S1-RT-029 交付（R86）。管理面 403 的 `pointer` 以逻辑文档 `registry/scope-patches.json` 为前缀：`#/n` 是该 scope patch 行按条目标识排序后的序号，无该行时指向文档根 `#`，被 profile 某层禁用时指向该层文件位置（R87）。注册表是运行态的真相源，组件文件夹是分发形态，二者共用校验器（R35）。

### 7.7 S1 五个 profile 的组合包清单（R31 落位表）

组合包数量与边界由组合包定义而不是决策（D20.7）；下表是 R31 给出的落位。规范术语：「平台插件」= `policy.tier: system` 的组件，「系统插件」只作别名。

| profile | 组合包 | 组合包内容（条目 → 组件） | 备注 |
|---|---|---|---|
| `platform` | `qian.bundle.base` + `qian.bundle.shell` + 可选 `qian.bundle.model-gateway` | base：原 18 个平台插件按 seam 拆包——`registry`、`lifecycle`、`capabilities`、`hooks`、`tenancy`、`identity → qian.identity.client`、`authz`、`data`、`files`、`config → qian.config.apollo`、`events`、`audit`、`observability`、`notify` + 三个渠道、`scheduler`、`realtime`、`entitlement`（§7.3），并携带平台运维技能 / 命令 / 钩子表面；shell：`shell → qian.shell`（yue 外壳静态制品与外壳 API）；model-gateway：`model-gateway → qian.model-gateway`（P1，默认 `disabled: true`） | 不承诺 AOT |
| `gateway` | `qian.bundle.gateway` | `gateway → qian.gateway`、`identity → qian.identity.client`（Cookie 换 S2S、JWKS）、`config → qian.config.apollo`、`observability`、`audit` | 可 AOT：`build.aot: true` |
| `identity` | `qian.bundle.identity-server` | `identity → qian.identity.server`（可嵌入 OpenIddict，唯一 issuer）、`identity.connector.oidc`、`identity.connector.wecom`（连接器组件，向 `ctx.Identity.Connectors` 注册 `oidc` / `wecom`，R28 / R38）、`tenancy`、`data`、`config → qian.config.apollo`、`events`、`audit`、`files`（头像，可选） | 独立安全边界 |
| `standalone` | `qian.bundle.standalone` | 供消费者宿主安装的最小组合包：`Qian.Runtime` + `config → qian.config`（标准 `IConfiguration`）、`tenancy`（默认 `SingleTenant`）+ 所选 seam 的 Definition / Provider（由 `qian add identity authz files …` 带入） | ADR-019 D19.8 的 30 分钟验收宿主；`qian init --template dotnet-webapi` 默认 |
| `minimal` | `qian.bundle.minimal` | 测试与 SDK 基准宿主，仅 `Qian.Runtime` 与声明式表面装载器；组合包初始 patch 为空，无默认业务组件（S1-DEC-012，2026-10-09 确认） | TCK 宿主、CLI 自身运行时 |

`minimal` 的 `config`、`observability`、`hooks`、`docs` 均为按需添加的组件，不进入默认组合包；表面装载器不因解析 hook 声明而依赖业务 hook Provider。该口径与 PRD §7.0.4 及 S1-RT-002 验收一致，裁决记录见 [ADR-020 §7.1](../../adr/ADR-020-component-model-v2.md#71-minimal-默认清单澄清2026-10-09)。

平台插件三件套 `qian.registry` / `qian.lifecycle` / `qian.capabilities` 的包名与服务键为 `Qian.Registry`（`ctx.Registry`）、`Qian.Lifecycle`（`ctx.Lifecycle`）、`Qian.Capabilities`（`ctx.Capabilities`），只进 `platform` profile（base 组合包），不供消费者独立安装（R34）；其它 profile 由 `Qian.Runtime` 的装载器直接从文件 patch 装树，没有数据库注册表与租户 scope patch。

S2 新增 profile `lowcode`（含 `qian.bundle.lowcode` 与 `qian.bundle.file-preview`）与 `workflow`（`qian.bundle.workflow`）（R31；ADR-020 §6）。前端外壳自身也是一棵由 `@qian/runtime` 装载的插件树（D20.2），其 profile 随 `qian.bundle.shell` 内置。

---

## 8. 目录市场

### 8.1 `.qian/marketplace.json` 完整示例

```json
{
  "$schema": "https://qian.dev/schemas/marketplace/v2",
  "name": "qian-official",
  "version": "2026.10.6",
  "interface": { "displayName": "Qian 官方市场", "description": "官方组件：平台基础能力、组合包、运维技能与参考应用",
                 "homepage": "https://plugins.example.internal/qian", "icon": "./assets/market.svg" },
  "owner": { "name": "Qian Platform Team", "url": "https://docs.example.internal/qian", "publicKeys": ["./keys/qian-official-2026.pub"] },
  "categories": ["bundle", "storage", "identity", "authz", "operations", "sample"],
  "plugins": [
    { "name": "files",
      "source": { "type": "local", "path": "./plugins/qian.files" },
      "policy": { "installation": "auto", "authentication": "ON_INSTALL", "profiles": ["platform", "standalone"] },
      "category": "storage",
      "interface": { "displayName": "文件存储", "description": "多租户文件服务", "brandColor": "#0F766E", "defaultPrompt": ["把这份合同扫描件上传到当前租户"] } },
    { "name": "identity",
      "source": { "type": "git-subdir", "repo": "https://github.com/idadawn/qian-plugins.git", "ref": "v1.0.0", "path": "plugins/qian.identity.client", "digest": "sha256:9f2c…" },
      "policy": { "installation": "confirm", "authentication": "ON_INSTALL", "profiles": ["platform", "standalone", "gateway"] },
      "category": "identity",
      "interface": { "displayName": "身份客户端", "description": "RP 接入、Cookie 会话、S2S 校验", "brandColor": "#1D4ED8" } },
    { "name": "wecom-connector",
      "source": { "type": "url", "url": "https://plugins.example.internal/qian/qian.identity.connector.wecom-1.0.0.tgz", "digest": "sha256:0c41…" },
      "policy": { "installation": "confirm", "authentication": "ON_USE", "profiles": ["identity"] },
      "category": "identity",
      "interface": { "displayName": "企业微信连接器", "description": "扫码登录与通讯录同步", "brandColor": "#07C160" } },
    { "name": "tenant-onboarding",
      "source": { "type": "git-subdir", "repo": "https://github.com/example-ops/qian-skills.git", "ref": "main", "path": "tenant-onboarding", "digest": "sha256:77ab…" },
      "policy": { "installation": "auto", "authentication": "ON_INSTALL", "profiles": ["platform"] },
      "category": "operations",
      "interface": { "displayName": "租户上线核对", "description": "只读核对技能", "brandColor": "#7C3AED" } }
  ]
}
```

### 8.2 字段表

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `name` | `^[a-z][a-z0-9-]*$` | 是 | 市场短名；`qian add <market>/<name>` 的前缀 |
| `version` | `string` | 否 | 市场文件版本（CalVer 建议） |
| `interface.displayName` / `description` / `homepage` / `icon` | 展示 | `displayName` 必填 | 目录网站首页 |
| `owner.name` / `url` / `publicKeys[]` | — | `name` 必填 | Ed25519 公钥（PEM 路径或内联）；无公钥 = 未签名市场（§11.5） |
| `categories[]` | `string[]` | 否 | 条目 `category` 必须在其中（省略则不限） |
| `plugins[].name` | `^[a-z][a-z0-9-]*$` | 是 | 市场内唯一的安装名（不是组件 id） |
| `plugins[].source.type` | `local \| url \| git-subdir` | 是 | 来源类型 |
| `plugins[].source.path` / `url` / `repo` + `ref` + `path` | — | 按类型 | `local`：相对市场仓库根；`url`：tgz / zip，解压根含 `.qian-plugin/plugin.json`；`git-subdir`：仓库、提交 / 标签 / 分支、子目录 |
| `plugins[].source.digest` | `sha256:` | `url` 必填；`git-subdir` 的 `ref` 为分支时必填 | 归档或子目录内容哈希（目录 digest = 文件 `路径 + 字节数 + SHA-256` 清单的哈希，沿 v1 D4） |
| `plugins[].policy.installation` | `auto \| confirm \| restricted` | 是 | `auto` 直接安装；`confirm` 先展示反向权限、密钥需求与表面清单并要求确认（`--yes` 跳过）；`restricted` 只允许平台运营经审核台安装（S3）。值域已裁决（R40） |
| `plugins[].policy.authentication` | `ON_INSTALL \| ON_USE` | 是 | 与组件清单一致；不一致以市场为准并告警 |
| `plugins[].policy.profiles[]` | `string[]` | 否 | 推荐 profile |
| `plugins[].category` | `string` | 是 | 分类 |
| `plugins[].interface` | 同清单 `interface` 的子集 | 否 | 目录网站用；**不复制**清单其它字段，详情页读组件自身的 `plugin.json` 与 README |

### 8.3 多市场与优先级

项目级 `.qian/config.yml`：

```yaml
defaultProfile: platform
marketplaces:
  - { name: acme-private,  source: "git+https://git.example.internal/platform/qian-market.git#main", priority: 10 }
  - { name: qian-official, source: "git+https://github.com/idadawn/qian-plugins.git#stable", priority: 20 }      # 官方市场仓库 qian-plugins，S1 私有（§13 第 4 条）
  - { name: community,     source: "github-topic:qian-plugin", priority: 90, trust: unverified }
trust:
  keys: ["./.qian/keys/acme.pub"]            # 本项目额外信任的市场公钥
```

`qian add <name>` 按 `priority` 升序逐个市场查找 `plugins[].name`，首个命中即用；多市场同名时告警并列出来源，用 `--from <market>` 或 `<market>/<name>` 指定；`trust: unverified` 的市场只有加 `--allow-unverified` 才参与解析，且只允许安装无代码组件、tier 最高 `community`（§11.5；已裁决 R40）。`qian list --markets` 打印解析顺序。

### 8.4 社区发现：GitHub topic `qian-plugin`

官方市场仓库名为 `qian-plugins`（与 Qian 同组织下的独立仓库，根目录 `.qian/marketplace.json`），S1 私有、S3 随许可证评估公开（ADR-020 §7 第 4 条，已拍板）；社区发现走以下 topic 机制。

公开仓库打上 topic `qian-plugin` 且根目录含 `.qian/marketplace.json`（多组件）或 `.qian-plugin/plugin.json`（单组件仓库）即可被发现。官方目录网站的生成器每日抓取 topic 结果生成只读索引 `community/index.json`（仓库、最近提交、是否签名、组件数、分类）；`source: github-topic:qian-plugin` 读的就是这份索引（CLI 不直接调用 GitHub 搜索 API）。社区条目默认 `trust: unverified`，`tier` 最高只接受 `community`，更高者被索引器降级并标记。

### 8.5 目录网站生成约定

- 输入：一个或多个 `marketplace.json` + 各条目的 `plugin.json`、README、`SKILL.md` front matter、`tools/*.json` 的 `name / description / sideEffect`、`policy.permissions`。
- 输出：静态站点——首页（市场 `interface` + 分类）、分类页、每条目一页（展示信息、`defaultPrompt`、表面清单、工具与副作用等级、反向权限与密钥需求、版本与 digest、安装片段 `qian add <name>` / `npx qian@latest --plugin <name> --yes`、健康徽章）、机器可读 `index.json`。
- 健康徽章来自市场仓库 CI 对每个条目运行 `qian doctor --manifest <path>`；不通过的条目仍展示但标红并禁用安装片段。
- 生成器随官方市场仓库交付（D20.8）；S3 商业市场在 `index.json` 之上叠加价格、授权、审核状态字段，不改既有字段（ADR-014 修订）。

---

## 9. CLI

### 9.1 命令与参数

全局参数：`--profile <name>`（默认 `.qian/config.yml` 的 `defaultProfile`）、`--cwd <dir>`、`--json`、`--verbose`、`--yes`。实现：单一 Node / TypeScript 实现、发布到 npm，前置 Node ≥ 22；`dotnet qian` 薄包装（只负责定位并调用 Node 版 CLI、缺 Node 时给出安装提示）为 S1 可选 P2（ADR-020 §7 第 3 条，→ §13）；命令、参数与行为不依赖实现语言。遥测默认不采集，S2 提供 opt-in `qian telemetry on`（ADR-020 §7 第 7 条，→ §13）。

| 命令 | 参数 | 作用 |
|---|---|---|
| `qian init --template <stack>` | `<stack>`：`dotnet-webapi`（standalone profile 的 ASP.NET Core 项目）、`dotnet-platform`（platform 宿主）、`ts-shell`（yue 外壳）、`skill-only`（只含 `skills/` 的组件仓库）、`component-dotnet` / `component-ts`（带代码组件骨架）；`--id`、`--name` | 生成 `.qian/config.yml`、`profiles/<p>/qian.profile.yml`、`.qian-plugin/plugin.json`、`README.md` / `AGENTS.md` 骨架、TCK 配置 |
| `qian add <ref>…` | `<ref>` = `<name>[@<version>]` / `<market>/<name>[@<version>]` / 本地路径 / URL；`--from <market>`、`--scope <scope>`（写租户 scope patch，需平台连接）、`--surfaces skills,commands`（只装部分表面）、`--no-code`、`--force-profile`、`--allow-unverified`、`--force`、`--reset-config`、`--dry-run` | 安装（§9.3） |
| `qian remove <ref>…` | `--purge`、`--scope <scope>` | 卸载（§9.4） |
| `qian list` | `--outdated`、`--markets`、`--surfaces`、`--scope <scope>` | 列出已安装组件（来源、版本、digest、表面、条目）；`--outdated` 对比市场最新版 |
| `qian doctor` | `--fix`（可自动修复：缺失默认 config、过期 lock、表面指针）、`--manifest <path>`（只校验一个组件）、`--migrate-manifest <qian-plugin.yaml>` | 健康检查：§10.1 全部校验 + 依赖等待 / 环路 / 缺失 Provider / digest 不符 / `secretRef` 不可解析 / 文档新鲜度 |
| `qian dump-config` | §7.5 | 打印插件树 |
| `qian dev` | `--profile`、`--watch`（声明式表面热重载）、`--aspire`（拉起 PG / Redis / RabbitMQ / Apollo 容器） | 代码部分：本机运行当前 profile 的宿主，取代 v1 `qian plugin dev`（R33） |
| `qian pack` | `--out <dir>` | 代码部分：构建 NuGet / npm 包（组件文件夹进包的 `qian/`）、`service` 的 OCI 制品与文件 digest 清单、SBOM；运行 §10.1 校验（R33） |
| `qian sign` | `--key <ref>` | 代码部分：OCI 制品 cosign 分离签名；组件文件夹 digest 清单 Ed25519 分离签名（沿 v1 §4.2） |
| `qian publish` | `--market <name>`、`--channel stable\|preview` | 代码部分：推送包 / 制品，并向市场仓库提交或更新 marketplace 条目（PR 或直接提交，按市场仓库策略） |

CLI 动词定稿为以上十个，没有 `plugin` 名词（R33）：`add \| remove \| list \| doctor \| dump-config \| init` 面向安装与运维，`dev \| pack \| sign \| publish` 面向代码部分。

### 9.2 `npx qian@latest` 一键形式

```text
npx qian@latest [--skill <name>]… [--agent <name>]… [--command <name>]… [--mcp <name>]… [--plugin <name>]… [--profile <p>] [--from <market>] [--yes]
```

| 参数 | 等价 |
|---|---|
| `--plugin <name>` | `qian add <name>` |
| `--skill <name>` | `qian add <name> --surfaces skills --no-code`；`<name>` 为 `<component>/<skill>` 时只复制该技能目录 |
| `--agent` / `--command` / `--mcp <name>` | 同上，分别只装 `agents` / `commands` / `mcpServers` 表面 |
| `--yes` | 跳过 `installation: confirm` 的确认 |

各 flag 可重复、可并列；没有构建步骤，组合就是多个 flag 并列（ADR-020 §1 对标项目做法一）。

### 9.3 安装过程（`qian add`）

```text
1 Resolve  按 §8.3 在市场定位条目 → 按 source 取得组件文件夹（local 直接读；url 下载归档；git-subdir 浅克隆到缓存）
2 Verify   市场签名（§11.5）→ source.digest 与实际内容 → 读 plugin.json → Schema + 语义校验（§10.1）
           → tier × form × sandbox（§11.1）→ engines 与当前 profile 运行时版本比对
3 Plan     计算并打印变更集：
           · 复制到 .qian/plugins/<component-id>/ 的表面文件（skills、agents、commands、hooks、.mcp.json、tools、metadata、schemas、assets、README、AGENTS）
           · 加入的 NuGet（dotnet add package）/ npm（npm i）依赖 = runtime.nuget[] / npm[]（--no-code 跳过）
           · 写入 profiles/<p>/qian.patch.yml 的条目：id = 组件短名（qian.files → files），config = configSchema 的 default 合成
           · policy.permissions 与 secretRef 需求清单（installation: confirm 在此等待确认）
4 Apply    写 .qian/journal/<timestamp>.json（变更集 + 回滚指令）→ 复制 / 加依赖 / 写 patch → 更新 .qian/lock.json
5 Verify   运行 qian doctor 的组件子集 → 打印 defaultPrompt 与「下一步」（待填写的 secretRef、推荐 profile）
```

`.qian/lock.json` 一行记录：

```json
{ "id": "qian.files", "version": "1.0.0", "market": "qian-official", "source": { "type": "local", "path": "./plugins/qian.files" },
  "digest": "sha256:9f2c…", "installedAt": "2026-10-06T08:20:11Z", "surfaces": ["skills", "commands", "hooks", "tools", "metadata", "schemas"],
  "files": [{ "path": "skills/upload-with-policy/SKILL.md", "sha256": "…", "bytes": 2048 }],
  "packages": { "nuget": ["Qian.Files.Abstractions@1.0.0", "Qian.Files@1.0.0"], "npm": ["@qian/files-ui@1.0.0"] },
  "entries": [{ "profile": "platform", "scope": "global", "id": "files" }] }
```

工作区布局：`.qian/config.yml`（市场与默认 profile）、`.qian/lock.json`（必须提交）、`.qian/plugins/<id>/`（复制进来的表面）、`.qian/journal/`（加入 `.gitignore`）、`profiles/<name>/qian.profile.yml` 与 `qian.patch.yml`。

### 9.4 幂等与回滚

- **幂等**：同一 `<name>@<version>` 重复 `add`：比对 lock 中 digest 与文件哈希，一致则无操作返回 0；文件被本地修改过则列出差异并拒绝覆盖（`--force` 覆盖）。不同版本 = 升级：替换表面文件、更新依赖版本、**保留** patch 中既有 `config`（按新 `configSchema` 重新校验，不兼容时打印差异并失败，`--reset-config` 用新默认值重写）。
- **事务与自动回滚**：步骤 4 任一子步骤失败时按 journal 逆序撤销已完成的子步骤（删除已复制文件、移除已加依赖、还原 patch 文件），返回非零；journal 保留最近 20 次。
- **`qian remove`**：按 lock 逆向执行——删除 `.qian/plugins/<id>/`、移除依赖（仅当无其它已安装组件引用同一包）、patch 条目改 `disabled: true`（默认，保留 `config` 便于重装）或整条删除（`--purge`）。
- **git 友好**：全部产出都是工作区文件，任何一步都能用 git 还原。
- **租户 scope**（`--scope tenant:<id>`）：不动工作区，调用管理面 API 写 `scope_patches`（§7.6），`revision` 乐观并发，失败不留半成品。

### 9.5 管理面端点（R33 / R32）

| 前缀 | 提供者 | 内容 |
|---|---|---|
| `/api/registry/*` | `qian.registry` | scope 视图 `GET /api/registry/scopes/{scopeKey}/entries[/{entryId}]`（R86；全树视图由 CLI `qian dump-config` 承担，S1 不设 HTTP 全树端点）、scope patch 写入（`PUT /api/registry/tenants/{tenant}/patches/{scope}/{entry}`，S1-RT-029）、lock 与来源；对未挂载 / 被过滤的组件，持有 `qian.registry:scope:read` 的调用者得到 403 原因码（`PLUGIN_NOT_MOUNTED` 等四个，R32 / R48），其他调用者 404 |
| `/api/lifecycle/*` | `qian.lifecycle` | `mount` / `unmount` / `reload`（声明式表面）、滚动重启请求、`/api/lifecycle/diagnostics`（Pending / Failed / 环路） |
| `/api/capabilities/*` | `qian.capabilities` | 能力目录（工具、事件、实体），鉴权后按 scope 与权限过滤，响应为信封 `contracts/v0/capability-catalog`（`items`、`ttlMs`、`cacheScope: private`）；`/api/capabilities/openapi.json` 聚合 |
| `POST /api/capabilities/{plugin}/{name}/invoke` | `qian.capabilities` | 通用执行入口（R75）：每次重做与目录同款的判定，不可见与不存在一样 404；请求体先按条目 `inputSchema`（JSON Schema 2020-12）校验，不符返回 400 `INPUT_INVALID`，`errors[]` 带输入 JSON Pointer；请求体读不成确定的 JSON 值（成员重名、孤立的 UTF-16 代理项、绝对值超过 decimal 范围约 7.9e28 的数）返回 400 `BAD_REQUEST`；写类能力可带 `Idempotency-Key`（UUID），同一主体、同一能力、同一键 24 小时内不再执行：回执保存了响应体时返回首次成功响应（`Idempotent-Replayed: true`），按回执策略（见表下）没有保存时返回 409 `CAPABILITY_REPLAY_UNAVAILABLE`；键相同而输入不同、或同键请求仍在执行，返回 409 `CONFLICT`，失败不留回执，只读能力（`sideEffect: read`）忽略该键；Agent 主体只见 `agent.expose: true` 的能力，S1 不开放 Agent 执行与 Actor 委托；ApiExplorer 不列此通用模板，`openapi.json` 为每个可见能力列出具体路径与 Schema |
| `/api/kernel/*` | 别名 | v1 路径在 S1 内保留为别名，响应带 `Deprecation` 头与 `Link: rel="successor-version"`，S2 移除 |

全部管理面端点只在 `platform` profile 存在，受 `qian.authz` 的 PEP 保护；业务与模型表面（路由、`ctx.Tools`、能力目录、菜单）对未挂载 / 被过滤的组件一律 404（R32）。

**执行入口的回执策略与登记期检查**（S1-RT-020）：幂等回执只在条目 `dataClassification` 为 `public` / `internal`、且响应体不超过组件配置 `receiptMaxBytes`（UTF-8 字节，默认 65536，取值 0–1048576）时保存响应体；`pii` / `finance` / `secret`（及未知分级）与超限的响应只记「已执行」和原因（`data_classification` / `response_too_large`），回放窗口内同键请求返回 409 `CAPABILITY_REPLAY_UNAVAILABLE` 且不再执行。组件作者要按响应内容标注能力的 `dataClassification`：SDK 默认 `internal`，即默认保存响应体；响应含个人信息、财务或机密数据的能力标为 `pii` / `finance` / `secret`（参考应用响应含客户名称的 `customer.*` 能力标为 `pii`）。回执表另以检查约束限定响应体不超过 1 MiB，迁移以 `NOT VALID` 添加、只约束新写入：存量超限行会让带校验的约束失败，而迁移属主在 FORCE RLS 下删不掉这些行；DDL 校验本身不经 RLS。回执过期后不再回放，物理删除靠本租户后续带键写调用登记时的惰性清理（每次最多 100 行）；长期没有带键写调用的租户由运维清理（运维手册给出 SQL）。条目在能力登记（`ctx.ProvideCapability`）时生成并预编译输入校验器：输入含租户字段、Schema 无法可靠校验（`patternProperties` / `dependentSchemas` 键含 `/` 或 `~`、`$ref` 找不到目标、`enum` / `const` 取值含超出 decimal 范围的数、无法编译）的契约在登记阶段以带组件 ID、能力名与 JSON Pointer 的异常拒绝，组件挂载失败，执行入口不会因此返回 500。登记期检查由宿主调用 `AddQianCapabilities()` 装配，platform profile 宿主必须调用，否则退化为首次读目录或调用时才编译。

---

## 10. 校验与 TCK

### 10.1 清单与表面校验器

校验器是一个库（`Qian.Runtime.Validation` / `@qian/validation`），被 `qian doctor` / `qian pack`、装载器（Resolve 阶段）、`qian.registry`（写入 `scope_patches` 前）、市场仓库 CI 与 Agent 门面（S4 校验 Agent 生成的草稿组件）共用——组件文件夹（分发形态）与注册表（运行态真相源）用同一校验器（R35）。错误以 `{ code, pointer, message, allowed? }` 列表返回（沿 v1 §4.1），`pointer` = 文件路径 + JSON Pointer（Markdown 表面指向 front matter 键）。

| 对象 | 结构校验 | 语义校验 |
|---|---|---|
| `plugin.json` | `plugin/v2` | `id` 前缀规则；`inject ∩ provides = ∅`；`surfaces.*` 指向存在；`tier × form × sandbox`（§11.1）；`runtime.entry` 在包内可定位；`configSchema` 合法且 `x-qian-sensitive` 属性为 `string`；`engines` 可解析；`inject / provides` 中 `<Key>.<Registry>:<name>` 的 `<Key>.<Registry>` 必须是某 Definition 声明的注册表子服务（R38） |
| `SKILL.md` / `agents/*.md` / `commands/*.md` | front matter Schema | `name` = 目录名 / 文件名；正文非空；`allowed-tools` / `tools` / `skills` 引用可解析（跨组件引用 warning）；内联 shell 必须被 `shell(...)` 覆盖；`@` 内联只允许组件内路径；相对链接指向组件内文件 |
| `hooks.json` | `hooks/v2` | Qian 事件名在 hooks Schema 固定的矩阵或事件目录中（未知 `a/b` error）；Agent 宿主事件只校验大驼峰命名（未知名 warning 并透传，R39）；`matcher` 可编译；`command` 仅 `system / official`；`http.url` 域名在 `policy.egress`；`plugin.handler` 可解析 |
| `.mcp.json` | `mcp/v2` | `stdio` 仅非平台 profile；敏感头 / env 必须 `secretRef://`；`url` 域名在 `policy.egress` |
| `tools/*.json` | `tool/v2` | `inputSchema` 合法且 `additionalProperties: false`；无租户字段；`sideEffect ≠ read ⇒ idempotent ∧ idempotencyKey`；`description ≥ 10 字`；`schemaSha256` 回写一致；`binding` 可解析 |
| `metadata/*.json` | 各自 Schema | 权限点 `<component-id>:<resource>:<action>` 且前缀 = 本组件 id；事件 id 全局唯一；角色模板只引用本组件或已 `inject` seam 的权限点 |
| `ui/manifest.json` | `ui/v2` | `engines.yue` 必填；`exposes` 与 `federation.manifest` 一致 |
| `qian.patch.yml` / `qian.profile.yml` | `patch/v2` / `profile/v2` | 条目 `scope.kind ∈ scopes[]`；`plugin` 可解析；`config` 过 `configSchema`；S1 拒绝 `hmr.code: true`；`build.aot: true` 时树中无非 AOT 组件 |
| `marketplace.json` | `marketplace/v2` | `name` 唯一；`source` 按类型必填项；`category ∈ categories[]`；条目 `policy` 与组件清单不一致 warning |
| 全部文件 | — | 密钥扫描：疑似密钥键名（`secret / token / password / key`）的值必须 `secretRef://`；高熵字符串拒绝；任何输入 Schema 禁止租户字段 |

### 10.2 运行时语义 TCK

TCK 是**语言无关的场景文件**（`tck/runtime/*.scenario.yml`：给定组件 / 条目 / patch 层 → 期望的挂载顺序、注册可见性、事件结果、释放顺序），由 .NET 与 TS 各自的 harness 执行，两侧必须全部通过（D20.2；ADR-020 §5「语义以本 spec 为准」）。S1 用例：

| 编号 | 用例 | 期望 |
|---|---|---|
| T01 | 依赖等待 | B `inject: [X]` 先于 A（`provides: [X]`）进入树；B `Pending`，A `Ready` 后 B 才 `Apply`；顺序 A → B |
| T02 | 环路 | A ↔ B 互相 `inject`；Resolve 失败，诊断列出 `A → B → A` |
| T03 | 逆序释放 | 依次注册 e1、e2、e3；Unmount 释放顺序 e3、e2、e1 |
| T04 | 级联卸载与恢复 | Unmount A（提供 X）后 B 被 Unmount 回到 `Pending`；重新 Mount A 后 B 重新 `Apply`，效果栈先清空再填充 |
| T05 | waterfall 短路 | 三个监听器，中间者不调 `next()`；第三个未被调用，结果为中间者返回值 |
| T06 | waterfall 顺序 | priority 高者在外层；同级按挂载顺序 |
| T07 | parallel 聚合 | 一个抛异常，其余完成；发起方收到聚合异常且 `Partial` 含两个结果 |
| T08 | serial 中断 | 第二个抛异常；第三个不执行 |
| T09 | bail 首值 | 第一个返回空、第二个返回值；第三个不被调用，结果为第二个的值 |
| T10 | emit 隔离 | 监听器异常不传播到发起方；其余监听器仍执行 |
| T11 | 模式不一致 | 事件定义 `emit`，`On` 传 `waterfall`；挂载失败 |
| T12 | scope 遮蔽 | 全局 `(Files, upload)` 与 `tenant:t-001` 内同名注册；在 t-001 得 scope 内项，在 `tenant:t-002` 与全局得全局项 |
| T13 | restrict 交集 | 对 t-001 施加两个 restriction（保留 {a, b} 与 {b, c}）；`List` 只返回 b；执行 a 的错误与执行不存在的 z 完全相同 |
| T14 | 无继承 | `agent-session:s-9` 看不到 `tenant:t-001` 的 scope 内注册 |
| T15 | patch 叠加顺序 | 组合包 `{a}`、profile patch `{b}`、CLI overlay `{c}`；结果 `{c}`（整体替换无残留），`layer` 为 CLI 层 |
| T16 | patch 禁用与再启用、替换 Provider | profile `disabled: true` + overlay `disabled: false` ⇒ 条目存在；后层不同 `plugin` ⇒ 新 Provider 提供同一键，Consumer 不变 |
| T17 | config 校验先于 Apply | 不合法 config 的条目 `Failed`，`Apply` 未被调用 |
| T18 | 同 scope 重复 Provide | 第二个条目挂载失败并报出第一个条目 id |
| T19 | 声明式表面热重载 | 修改 `SKILL.md` 的 `description`；`ctx.Tools` 更新，替换期间读取方只看到旧内容或新内容，代码条目效果栈未变 |
| T20 | 整树 teardown | 按挂载顺序逆序 Unmount；每个条目满足 T03 |
| T21 | 跨 scope 分发可见性 | 全局监听器收到 `tenant:t-001` 的分发；`tenant:t-002` 的监听器收不到 |
| T22 | optionalInject | 键缺失时 `Apply` 执行且 `TryInject` 为空；键稍后出现不触发重新 `Apply` |
| T23 | 独立模式等价 | 同一组 Provider 在 `minimal` 与 `platform` 下对同一场景产出相同结果（ADR-019 D19.8 第二条的运行时侧）；S1 由 QA-002 用 TCK 组合包 `qian.bundle.tck-platform` 与 CRM 样例 Provider 组验证，D19.8 点名的 Files / Authz / Identity.Client 对照见 S1-QA-003 / S1-QA-005（S1-DEC-019 R68） |
| T24 | 注册表 seam 同名独占与遮蔽 | 两个全局条目向 `Identity.Connectors` 注册同名 `oidc`，第二个挂载失败并报出第一个条目 id；`tenant:t-001` 内注册同名 `oidc` 后，t-001 的 `Connectors.Get("oidc")` 得 scope 内项、t-002 得全局项（R38） |
| T25 | 注册原位替换 | 经 `Provide` 句柄替换实例后解析得新值，具名注册的 `List` 顺序与 scope 内注册的可见范围不变；以 `inject` 依赖该键的条目释放后重新 `Apply`；条目卸载后句柄拒绝替换，注册无残留 |

### 10.3 CI 门禁

| 门禁 | 检查 | 失败条件 |
|---|---|---|
| G1 清单派生一致 | 源生成器重新派生 `runtime.entry`、`inject[]`、`provides[]`、代码派生的 `tools/*.json`、`configSchema`，与仓库比对 | 任一差异 |
| G2 文档新鲜度 | `qian.docs` 重新产出配置目录（全部 `configSchema` 汇编）、模块图（条目 × `inject / provides`）、事件图（事件 × 生产方 × 消费方 × 模式）、seam 图（Definition × Provider × Consumer），与 `docs/generated/` 比对 | 任一差异（D20.9） |
| G3 依赖图 | `dotnet list package --include-transitive` + npm lockfile：Abstractions 不含 Provider；Provider 不含其它 Provider；任何 `Qian.*` / `@qian/*` 库不含 `qian.registry` / `qian.lifecycle` / `qian.capabilities` 的包；组件库对 Qian 的唯一依赖是 `Qian.Runtime` / `@qian/runtime` | 任一违反（D20.10、ADR-019 D19.8） |
| G4 公共 API | `Microsoft.CodeAnalysis.PublicApiAnalyzers` 覆盖全部 `Qian.*`；`api-extractor` 覆盖全部 `@qian/*` | 未登记的公共 API 变更 |
| G5 事件模式一致 | 全部 `ctx.On` 调用点的模式字面量 vs 事件定义处 | 不一致（D20.3） |
| G6 校验器全绿 | 对仓库内全部组件、profile、patch、marketplace 运行 §10.1 | 任一 error |
| G7 TCK 双侧 | §10.2 全部场景在 .NET 与 TS harness 通过 | 任一失败 |
| G8 密钥、租户字段、链接、禁用词 | §10.1 末行扫描；文档链接指向真实文件；参考平台品牌名零命中 | 任一命中 |

---

## 11. 安全

### 11.1 信任等级 → 允许的 `runtime.form` 与 `sandbox`

沿 ADR-002 D2.4 的五级信任与「第三方代码永不进共享进程」，映射到 v2 字段（D20.11）。无代码组件对所有 tier 开放。校验器据此表拒绝不允许的组合；装载器在 Resolve 阶段**再次**检查并拒绝为非 `system / official` 组件加载任何程序集，即使包已在宿主——两道检查互为冗余（防止绕过 CLI 直接改文件）。

| `policy.tier` | 允许的 `runtime.form` | 允许的 `policy.sandbox` | hooks `type` | 代码运行位置 |
|---|---|---|---|---|
| `system` / `official` | `module`、`service`、`ui`、`package`、`function` | `none`（`service` 为 `workload`） | `command`、`http`、`plugin` | 共享进程（profile 宿主） |
| `partner` | `package`、`function`、`ui`；`service`（S3 沙箱策略另行开放） | `ui`：`worker`（审核后可 `none`）；`function`：`wasm`；`service`：`workload` | `http`、`plugin` | 独立工作负载 / Wasm / 浏览器隔离 |
| `private` | `package`、`function`、`ui` | `ui`：`worker \| iframe`；`function`：`wasm` | `http` | 同上 |
| `community` | 同 `private`；**S1 只接受无代码组件**（`runtime` 必须为空） | 同 `private` | `http` | 同上 |

### 11.2 第三方代码不进共享进程

- `module` 只对 `system / official` 开放；`partner` 的 `service` 以独立工作负载身份运行（独立 PG schema 与数据库角色、事件主题 ACL、出口 NetworkPolicy，ADR-002 D2.4），以远程代理方式 `Provide`（§5.6）。
- `function` 的 Wasm 运行时（S2 可选）在 `qian.wasm-runtime` 的隔离实例中执行，`runtime.limits` 强制。
- 声明式表面不是代码，但 `commands/*.md` 的内联 shell 与 `skills/*/scripts/` 是：只在 CLI / 开发者本机或 `system / official` 组件的受限执行器中运行（§11.4）；`community / private` 组件出现内联 shell 即校验失败。
- Agent 生成的组件（S4）落为草稿组件文件夹，tier 固定 `private`，经同一校验器与 TCK，不得含 `module`。

### 11.3 `secretRef`

- 形状 `secretRef://<scope>/<path>`，`scope ∈ { platform, tenant, tenant/<id>, dev }`。清单、表面、patch、事件、日志、`dump-config` 输出中只允许这个引用形式，永不出现明文。
- 由 `Config` seam 的 `ISecretResolver` 在**使用时**解析（Apollo 引用 K8s Secret，ADR-007）；`policy.permissions.secrets[]` 声明允许解析的范围，越界被拒并审计；`secretRef://tenant/<id>/…` 只能被同租户上下文解析（RLS + resolver 双重检查）。
- `secrets[]` 条目使用 `{ "name": "oss/*", "scope": "platform" }`，与既定权限对象口径一致。name 为精确路径或末尾 `/*` 前缀，scope 为 platform、tenant、dev；声明与宿主审批记录取交集。tenant 路径匹配后仍检查真实租户身份，dev 仅在开发宿主显式开启。
- 只有 `configSchema` 中 `x-qian-sensitive: true` 的属性、`hooks.json` 与 `.mcp.json` 的 `headers / env` 允许 `secretRef://`；其它位置出现是 warning；疑似密钥键名的明文值与高熵值是 error（§10.1）。
- 判定配置字段是否敏感时，属性名与 `patternProperties` 的键都不区分大小写（配置键与 Web 默认反序列化都如此）；模式按 ECMA-262 可移植子集解释，超出子集或规模上限即判 Schema 无效；键名含各正则方言判断不同的字符时按敏感处理，只接受 `secretRef://`。本地 CLI 与 .NET 的装配、配置扫描按同一规则判定，由共享 TCK 约束。装配时两端的 Schema 验证器也按这一子集执行 `pattern` 与 `patternProperties`（ECMA-262 不带标志、逐码元），超出子集的写法在 Schema 的对应位置报错；验证诊断归一为出错实例位置并按序数排序，两端逐字一致。`configSchema` 只接受 JSON Schema 2020-12 中两端解释相同的写法：不支持 `$id` 等标识与动态引用、`unevaluatedItems` 与 `unevaluatedProperties`、`nullable` 等，`$ref` 只指向本文档内的子 Schema 位置且不能构成循环；数值按双精度、文本长度按码点判断。逐条规则见 [Qian.Profiles](../../../packages/Qian.Profiles/README.md#配置-schema-验证)。

### 11.4 hooks `command` 类型与内联 shell 的限制（已裁决 R41）

- 只允许 `system / official`；`command` 是 argv 数组，不经 shell 解释（无管道、重定向、通配）。
- 执行器：工作目录 = `.qian/plugins/<id>/`；环境变量只传白名单（`QIAN_PROFILE`、`QIAN_SCOPE`、`QIAN_EVENT`、`PATH`）加 `env` 显式声明的键；默认超时 30s、上限 600s；stdout / stderr 各上限 1 MiB；无网络访问，除非 `policy.egress` 非空（由执行器所在工作负载的 NetworkPolicy 保证）。
- 平台宿主默认关闭 `command` 执行（profile `hooks.allowCommand: false`），`platform` profile 需显式开启；`gateway` / `identity` profile 校验器拒绝开启。`commands/*.md` 的 `` !`…` `` 遵守同一规则并须被 `allowed-tools` 的 `shell(<argv 前缀>)` 逐条覆盖。

### 11.5 市场条目验签

- 市场文件签名：`.qian/marketplace.json.sig`（Ed25519 分离签名，载荷 = 文件字节的 SHA-256）；公钥在 `owner.publicKeys[]` 且须命中客户端信任根之一（项目 `.qian/config.yml` 的 `trust.keys[]`、`$QIAN_HOME` 信任根、平台 Apollo `qian.common.trust`）。未命中 = 未签名市场。
- 内容校验：`source.digest` 与实际内容一致；`url` 来源无 `digest` 即拒绝；`git-subdir` 的 `ref` 为分支时必须有 `digest`，为标签 / 提交时可省略但安装后写入 lock。代码包由 NuGet 签名 / npm provenance 校验，`runtime.nuget[] / npm[].digest`（S3 第三方必填）与 lock 比对。
- 未签名市场或未命中信任根的条目：需 `--allow-unverified`，只允许无代码组件，tier 最高 `community`；`qian list` 持续标记 `unverified`（已裁决 R40）。
- 撤销：信任根支持多把公钥与重叠轮换；`owner.publicKeys[]` 移除某钥后，用它签名的旧市场快照在 CLI 缓存中失效；发布者撤销与黑名单（S3）触发自动下线。

---

## 12. 验收标准

- [ ] **Schema**：`plugin/v2`、`hooks/v2`、`mcp/v2`、`tool/v2`、`ui/v2`、`patch/v2`、`profile/v2`、`marketplace/v2` 八份 Schema 入库并托管；§3.1–§3.3 三个示例校验通过；去掉 `id`、把 `runtime.image` 改为 tag 引用、让 `inject` 与 `provides` 相交、给 `community` 组件写 `runtime.form: module`，四个变体各自失败并返回带 pointer 的错误（UT）。
- [ ] **表面校验**：§4.1–§4.7 示例全部通过；`SKILL.md` 的 `name` 与目录名不一致、`community` 组件命令含内联 shell、`.mcp.json` 的 `env` 写明文 token、`tools/upload.json` 的 `inputSchema` 加 `tenantId`，四个变体各自失败（UT）。
- [ ] **运行时 TCK**：§10.2 T01–T24 在 `Qian.Runtime` 与 `@qian/runtime` 两个 harness 全部通过（TCK）。
- [ ] **示例插件**：§5.7 与 §5.8 原样编译通过并在 `minimal` profile 下 `Ready`；把 `files` 条目的 Provider 换成内存实现后，`qian.sample.crm` 的附件上传用例不改代码仍通过（IT）。
- [ ] **seam 依赖图**：G3 对全部 `Qian.*` / `@qian/*` 包通过；故意让 `Qian.Files` 引用 `Qian.Authz` 时 G3 失败（CI）。
- [ ] **profile 与 patch**：五个 profile 与组合包入库；`qian --profile platform dump-config` 输出通过 `patch/v2` 校验，任一条目复制为 patch 记录后再次 `dump-config` 结果不变；T15 在真实文件上复现（E2E）。
- [ ] **租户 scope patch**：对租户 A 写 `files` 的 scope patch 后 A 的上传落到 A 的桶、B 不受影响；对 A 写 `disabled: true` 后 A 调用 `qian.files/upload`、访问其路由与菜单均得到与「不存在」相同的 404，而持有 `qian.registry:scope:read` 的运维调用 `GET /api/registry/scopes/tenant:<A>/entries/<entry>`（R86）得到 403 `PLUGIN_NOT_MOUNTED`，`message` 为「被租户 scope patch 禁用」、`pointer` 指向该条目（R32 / R48）；对 `policy.required: true` 的条目写 `disabled: true` 被拒；未授权租户写入返回 `NOT_ENTITLED`（E2E）。
- [ ] **管理面别名**：`GET /api/kernel/capabilities` 与 `GET /api/capabilities` 返回相同内容，前者带 `Deprecation` 头与 `Link: </api/capabilities>; rel="successor-version"`（保留原查询串），`openapi.json` 别名同理（IT，R33）。
- [ ] **市场与 CLI**：官方市场仓库含 `marketplace.json` 与签名；空白项目 `qian init --template dotnet-webapi` 后 `qian add identity authz files`，照各包 README 在 30 分钟内跑通登录、权限检查与上传（ADR-020 §6 S1 退出标准）；`npx qian@latest --skill tenant-onboarding --yes` 只复制技能、不改依赖（E2E）。
- [ ] **幂等与回滚**：重复 `qian add files@1.0.0` 返回 0 且工作区无变化；在加依赖步骤注入失败后工作区与 lock 恢复到安装前（`git status` 干净）；`qian remove files` 后条目 `disabled: true`、文件与依赖移除（IT）。
- [ ] **热重载**：`platform` profile 运行中修改已安装技能的 `description`，5 秒内 `ctx.Tools` 与 Agent 门面返回新描述，代码条目未重启（E2E）。
- [ ] **安全**：手工把 `community` 组件放入 `.qian/plugins/` 并写上 `runtime.form: module`，装载器拒绝加载并在 `/readyz` 诊断列出；未签名市场条目不加 `--allow-unverified` 即被拒；`dump-config` 输出 grep 不到任何已解析密钥值（IT + 代码审查）。
- [ ] **文档门禁**：改动任一组件 `configSchema` 而不重新生成配置目录时 G2 失败；全部组件 README 含「模型体验」「已知限制与延期项」两节（CI）。

---

## 13. 已拍板事项（原「待确认事项」，2026-10-07 全部拍板）

本节登记 [ADR-020 §7](../../adr/ADR-020-component-model-v2.md) 八条待确认项的拍板结论及其在本 spec 的落位；本 spec 不新增决策，正文按下列结论书写，理由与可回退条件见 ADR-020 §7。

1. 已拍板（ADR-020 §7 第 1 条）：TypeScript 侧 `@qian/runtime` 为公共 API 包，S1 内部以固定版本 vendor 的 Cordis（MIT）作为实现，薄适配层对齐 §5.3–§5.6 语义，TCK（§10.2）以我们的语义为准；.NET 侧 `Qian.Runtime` 自研不变。
2. 已拍板（ADR-020 §7 第 2 条）：清单为 `.qian-plugin/plugin.json`（JSON，§3），profile 与 patch 继续用 YAML（§7）；`qian doctor --migrate-manifest` 负责从 v1 YAML 迁移（§3.6）。
3. 已拍板（ADR-020 §7 第 3 条）：`qian` CLI 为单一 Node / TypeScript 实现、发布到 npm，`npx qian@latest` 为一键入口，Node ≥ 22 为前置；`dotnet qian` 薄包装为 S1 可选 P2（§9.1）。
4. 已拍板（ADR-020 §7 第 4 条）：官方市场仓库为与 Qian 同组织下的独立仓库 `qian-plugins`，根目录 `.qian/marketplace.json`，S1 私有、S3 随许可证评估公开；社区发现用 GitHub topic `qian-plugin`（§8.3、§8.4）。组织名已定：产品族统一在 `idadawn`（ADR-021，2026-10-07 拍板），示例地址据此写定。
5. 已拍板（ADR-020 §7 第 5 条）：代码插件热装卸不进 S2 核心交付，S2 内由 RT 领域做 5 天时间盒 spike 验证 collectible `AssemblyLoadContext` 与 Kun 约束，结论写回 ADR-020；生产默认滚动重启，S1 校验器继续拒绝 `hmr.code: true`（§5.6、§7.2），声明式表面热重载不变。
6. 已拍板（ADR-020 §7 第 6 条）：Qian 包族、`qian` CLI、yue 一律 Apache-2.0，官方组件 `license` 写 `Apache-2.0`（§3.4）；S3 商业市场与计费组件的许可证到 S3 再定；公开 feed S1 为 GitHub Packages 私有，S2 末或首个外部宿主试用时公开到 nuget.org 与 npm，`runtime.nuget[]` / `runtime.npm[]` 的解析源随之切换（§11.5 验签要求不变）。
7. 已拍板（ADR-020 §7 第 7 条）：CLI 遥测默认不采集；S2 提供 opt-in `qian telemetry on`，只采集命令名、CLI 版本、操作系统与匿名安装 id，不含路径、租户与代码；私有化部署可在 profile 中禁用（§9.1）。
8. 已拍板（ADR-020 §7 第 8 条）：`qian init --template` 模板名统一为 §9.1 的集合（R42），后续新增模板在本 spec 登记。

补充裁决 R28（连接器与组合包的定义、`policy.egress[]`）、R31（五个 profile 的组合包落位、「平台插件」术语）、R32（未挂载 / 被过滤一律 404，管理面 403 原因码 `PLUGIN_NOT_MOUNTED` 等，R48）、R33（CLI 十个动词、管理面端点前缀与 `/api/kernel/*` 别名）、R34（`Qian.Registry` / `Qian.Lifecycle` / `Qian.Capabilities` 只进 `platform`、`policy.required`、挂载状态只存 `scope_patches`）、R35（文件夹 = 分发形态、注册表 = 运行态真相源、同一校验器、git 修订 = 分发版本）、R37（反向权限落 `policy.permissions`，权限点 / 默认角色 / 事件 / 消息类型落 `metadata/`）、R38（单例 seam 与注册表 seam 两种风格；连接器一律向注册表子服务按名称注册，`provides` 写「注册表键:名称」）、R39（hooks Schema 固定 Qian 事件矩阵；Agent 宿主事件由 Agent 运行时定义并透传）、R40（`policy.installation: auto \| confirm \| restricted`；未验签条目只装无代码组件、tier 最高 `community`）、R41（hooks `command` 与内联 shell 边界；profile 的 `build.aot` / `hmr` / `health` 字段保留）已在正文落实（§1.3、§2.1、§3.1、§3.4、§3.5、§3.6、§4.4、§5.1、§5.5、§5.7、§6.1–§6.3、§7.2、§7.6、§7.7、§8.2、§8.3、§9.1、§9.5、§10.1、§10.2、§11.4、§11.5）。本 spec 上报的口径空白已全部裁决，当前没有未裁决的草案口径。

---

## 参考

- 决策：[ADR-020 组件模型 v2](../../adr/ADR-020-component-model-v2.md)（权威）、[ADR-019 库优先](../../adr/ADR-019-library-first-packages.md)、[ADR-002 插件模型](../../adr/ADR-002-plugin-model.md)、[ADR-003 内核边界](../../adr/ADR-003-kernel-boundary-s1-services.md)、[ADR-014 应用市场](../../adr/ADR-014-marketplace.md)、[ADR-016 与 iClaw 的边界](../../adr/ADR-016-iclaw-boundary.md)。
- PRD：[§03 核心概念模型](../03-concepts.md)、[§07 S1 技术底座](../07-s1-foundation.md)、[§13 权限与安全](../13-security.md)、[§14 质量与评估](../14-quality.md)。
- 专题 spec：[`plugin-manifest.md`](plugin-manifest.md)（v1，字段迁移对照）、[`yue.md`](yue.md)（`ui/manifest.json` 与外壳契约）。

## 修订历史

| 日期 | 版本 | 变更 |
|---|---|---|
| 2026-10-06 | 草案 v2 | 首版：按 ADR-020 重写组件模型；取代 `plugin-manifest.md` v1 为权威规格 |
| 2026-10-07 | 草案 v2（拍板传播） | ADR-020 §7 八条全部拍板：§13 改为已拍板清单；§3 清单 JSON 定稿；§3.4 `license` 改 Apache-2.0；§5.3 TS 运行时 S1 vendor Cordis、公共 API `@qian/runtime`；§8.3 / §8.4 官方市场仓库 `qian-plugins`（S1 私有）；§9.1 Node ≥ 22 前置、`dotnet qian` 薄包装 P2、遥测默认不采集 |
| 2026-10-14 | 草案 v2（S1-RT-018） | §5.1 / §5.2 / §5.3 `Provide` 返回可原位替换实例的注册句柄（.NET `IRegistrationHandle<T>.Replace`、TS `RegistrationHandle<T>.replace`）；§5.6 声明式表面重载改为先新增、再原位替换、最后撤销；§10.2 T19 补替换期间不缺失，新增 T25。产品负责人在任务中要求修复热重载读取方瞬时看到注册缺失的竞态 |
| 2026-10-14 | 草案 v2（S1-RT-019） | §9.5 增加通用执行入口 `POST /api/capabilities/{plugin}/{name}/invoke` 一行（R75），`/api/capabilities/*` 注明目录信封契约；§12 管理面别名验收补 `Link: rel="successor-version"`。S1-RT-006 复核发现执行入口未进规格、别名缺 Link 头 |
| 2026-10-15 | 草案 v2（S1-RT-020） | §9.5 执行入口加回执策略：按数据分级与 `receiptMaxBytes` 决定是否保存响应体，不保存时同键重放返回 409 `CAPABILITY_REPLAY_UNAVAILABLE`；能力契约在登记阶段生成条目并预编译输入校验器，不合格即拒绝。S1-RT-019 复核发现回执明文无上限、输入 Schema 首次调用才编译 |
| 2026-10-15 | 草案 v2（S1-RT-021） | §9.5 回执策略段更正两处说法：过期回执的物理删除靠本租户后续带键写调用登记时的惰性清理（每次最多 100 行），`NOT VALID` 的理由是存量超限行会让带校验的约束失败、迁移属主在 FORCE RLS 下删不掉；补作者按 `dataClassification` 标注能力（SDK 默认 `internal` 即默认保存响应体）与 platform 宿主必须调用 `AddQianCapabilities()`。S1-RT-020 复核发现两处说法不准、默认分级会让含个人信息的响应明文入回执 |
| 2026-10-15 | 草案 v2（S1-RT-024） | §9.5 执行入口补请求体读不成确定值时返回 400 `BAD_REQUEST`（含绝对值超过 decimal 范围的数）；登记期检查拒绝 `enum` / `const` 取值含这类数的输入 Schema。JsonSchema.Net 9.4 按 decimal 比较取值，这类数曾让执行入口返回 500 |
| 2026-10-15 | 草案 v2（S1-RT-026） | §5.6 写明 `Apply` 尚未结束时所注入的键被其它条目撤销、替换或隐藏的处理：本次 `Apply` 作废、逆序释放后回到 `Pending`，不记为 `Failed`；`Apply` 自身造成的可见性变化仍为 `Failed`。S1-RT-023 调查时发现两侧装载器只让 `Ready` 条目失效，`Apply` 中途依赖变化的条目被误判 `Failed` 且不再重试 |
