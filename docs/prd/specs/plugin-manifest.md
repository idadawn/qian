# 专题 spec · 插件清单 v1（`qian-plugin.yaml`）

> **状态变更（2026-10-06，ADR-020）**：本文件为插件清单 **v1**（`qian-plugin.yaml`）规格，已被 [`component-model.md`](component-model.md)（组件模型 v2）取代为权威规格，本文件正文不再修订，保留作为**字段迁移对照**：`kind` 七值（`bundle` / `module` / `service` / `package` / `function` / `connector` / `ui`）→ `runtime.form`（`module` / `service` / `function` / `ui` / `package`；`bundle` 改为组合包——有 `config/qian.patch.yml`、可携带多个组件，应用 = 组合包 + marketplace 条目；`connector` 改为「为某个 seam 提供 Provider 并以 `policy.egress[]` 声明出网目标的组件」，`runtime.form` 按实现取 `module` / `service`；补充裁决 R28，见 component-model.md §3.6）；`qian-plugin.yaml` → `.qian-plugin/plugin.json`（component-model.md §3）。§3.2 字段表中仍有效的字段族在 v2 中的位置：`provides.capabilities[]` 的 Agent 字段（`sideEffect` / `policyHint` / `idempotent` / `requiredScopes` / `requiredPermission` / `requiredSecrets` / `dataClassification` / `schemaSha256` / `contractVersion` / `agent.expose`）→ `tools/<name>.json` 表面（§4.6）；反向权限 `permissions.platform / data / secrets / ui` → `plugin.json` 的 `policy.permissions`，`permissions.egress` 升为 `policy.egress[]`（§3.4）；`tenancy.alwaysEnabled` → `policy.required: true`，租户级 Enabled / Disabled 状态机 → `scope_patches` 行（R34，§7.6）；`provides.permissions / defaultRoles / events / messageTypes` → `metadata/` 表面（§2.1、§3.6）；`engines.qian / kun / yue` → `plugin.json` 的 `engines`（保留，§3.4）；OCI / 制品的 `@sha256:` digest 引用 → `runtime.image`、`runtime.wasm`、`runtime.nuget[] / npm[].digest`、市场条目 `source.digest` 与 `.qian/lock.json`（§3.4、§8.2、§9.3）。阶段分级（S1 / S2 / S3）与信任等级规则在 v2 中分别对应 component-model.md §3.4 的分级列与 §11.1。

> 状态：草案 v1（2026-10-06）。本 spec 是 §07 7.2 的配套专题，定义插件清单的字段、流程、职责映射与安全要求；阶段与形态口径以 ADR-002（插件模型）、ADR-003（内核边界）、ADR-014（市场与包格式）、ADR-018（阶段）为准。被借鉴的商业低代码产品一律称「参考平台」。

## 本版关键决策

| # | 决策 | 依据 |
|---|---|---|
| D1 | 清单文件名固定 `qian-plugin.yaml`，`schemaVersion: qian.dev/plugin/v1`，用 JSON Schema 2020-12 校验；签名**不在**清单里 | ADR-002；P 基线裁决 A-8 |
| D2 | 三个正交轴 `kind × role × distribution`，`kind` 顶层七值含复合包 `bundle`（其 `components` 下组件级 `kind` 取其余六值）；平台自带能力以 `tier: system`、`alwaysEnabled: true` 走同一清单 | ADR-002 |
| D3 | `id` 为反向域名、发布后不可改；插件 SemVer 与 Kun CalVer 独立；`engines.qian / kun / yue` 声明兼容范围 | ADR-001、ADR-002 |
| D4 | 制品只接受 OCI `@sha256:` digest 引用；代码制品 cosign 分离签名，元数据包与 `SKILL.md` 用「文件 URI + SHA-256 + 字节数」清单 + Ed25519 分离签名；未签名或签名不符直接安装失败 | ADR-002；P 基线 §4.5 |
| D5 | 清单从代码派生：源生成器与 CLI 生成片段，手写部分只限描述性字段；CI 比对清单与代码，漂移即失败 | P 基线原则 P4 |
| D6 | `provides.capabilities[]` 是 Agent 可发现的最小单元；副作用枚举采 iClaw 四值 `read \| write \| external \| destructive` + `policyHint`；`schemaSha256` 进入授权（schema 变即旧授权失效）；Agent 字段 S1 全部落库、S4 才派生 MCP 工具 | ADR-016；P 基线裁决 A-9 |
| D7 | 两个方向的权限分开：`provides.permissions`（本插件定义的权限点，供授予他人）与反向 `permissions.platform / data / egress / secrets / ui`（插件向平台申请，租户管理员开通时逐项批准；升级新增即重批） | ADR-006；P 基线 §3.1 |
| D8 | 租户字段永不进 `inputSchema`，契约 lint 强制；密钥只以 `secretRef` 名字出现，清单、事件、日志永不含明文 | ADR-018 防返工项 6；ADR-007 |
| D9 | 两层生命周期分开建模：平台级 `Draft → Submitted → Verified → Published → Installed → Active → Deprecated → Retired`；租户级 `Available → Entitled → Provisioning → Enabled ⇄ Disabled → Upgrading / Uninstalled → Purged`；`Entitled`（授权库）与 `Enabled`（Apollo 开关 + 租户状态）分开存 | ADR-002、ADR-014 |
| D10 | S1 字段集冻结为「S1 必做」一栏（见 §3.3）；S2–S4 字段只占位、校验器接受但宿主忽略 | ADR-018 |

## 1. 范围

本 spec 定义：

1. 清单文件 `qian-plugin.yaml` 的字段、类型、约束与默认值（§3）。
2. 清单在 `pack / sign / publish / install / tenant provision / upgrade / rollback` 七个流程中的使用方式（§4）。
3. 哪个系统插件或服务消费哪些字段（§5），以及它们据此生成的派生物。
4. S1 必做字段与 S2–S4 占位字段的分级（§3.3、§9）。

适用对象：全部 `tier: system | official` 插件（S1 预装 `module`、`service`、`ui`），以及 S2 起的 `package / function`、S3 起的 `partner / private / community` 插件。`qian.kernel` 的注册表（§07 7.2.1）以本 spec 为唯一输入；`qian plugin` CLI 与 TCK（§07 7.2.2）以本 spec 的 JSON Schema 为校验依据。

## 2. 非目标

- 不定义运行时协议本身（gRPC `.proto`、OpenAPI 文档、AsyncAPI 的内容），清单只引用它们。
- 不定义市场商品信息的完整模型（`marketplace.*` 在 S3 由 ADR-014 的市场章定稿，本版只占位）。
- 不定义 MCP 工具定义的生成规则（S4），本版只保证字段就位。
- 不定义 Wasm `function` 的 ABI（Extism 还是 WIT 组件，S2 中期拍板），只保留 `components.functions[]` 的结构。
- 不定义升级同意规则与三方合并算法（S2/S3），只保留 `lifecycle` 与 `permissions` 使之可判定「是否需重批」。
- 不以清单承载签名、许可证密钥、价格等需独立签发或变更频繁的信息。

## 3. 数据模型

### 3.1 完整示例

以下示例是 §07 7.0.1 参考应用 `qian.sample.crm` 在 S1 的清单（`module + ui`，`tier: official`），标 `# S2+` 的字段为占位。

```yaml
schemaVersion: qian.dev/plugin/v1
id: qian.sample.crm                     # 反向域名，发布后不可改
version: 1.4.0                          # 插件 SemVer；Kun 为 CalVer，二者独立
kind: bundle                            # bundle | module | service | package | function | connector | ui
role: app                               # provider | capability | extension | app | theme | agent
tier: official                          # system | official | partner | private | community
distribution: managed                   # S2+：template | managed（仅 package / bundle）
displayName: { zh-CN: 客户管理（参考应用）, en: Sample CRM }
description:  { zh-CN: 演示多租户应用如何只用平台能力完成登录、鉴权、存储、文件与审计, en: ... }
publisher: { id: qian.platform, verified: true }
icon: ./assets/icon.svg
license: Apache-2.0
links: { homepage: https://docs.example.internal/qian/sample-crm, docs: ..., support: ... }
categories: [sample, sales]

engines:
  qian: ">=1.0 <2"
  kun:  ">=2026.9"
  yue:  "^1.0"
dependencies:
  - { id: qian.files,  version: "^1", optional: false }
  - { id: qian.notify, version: "^1", optional: true }

provides:
  capabilities:
    - id: crm.customer.search
      version: 1
      contract: { openapi: ./contracts/openapi.yaml, operationId: searchCustomers }
      description: "按名称、电话或负责人查找客户；不用于批量导出"
      sideEffect: read                   # read | write | external | destructive
      policyHint: allow                  # allow | confirm | deny
      idempotent: true
      async: none                        # none | task
      requiredScopes: ["qian.sample.crm:read"]
      requiredPermission: "qian.sample.crm:customer:read"
      requiredSecrets: []
      dataClassification: internal       # public | internal | pii | finance | secret
      costTier: low
      contractVersion: crm-v1
      schemaSha256: "<pack 时计算>"
      audit: false
      agent: { expose: true, requiresConfirmation: false }
    - id: crm.customer.create
      version: 1
      contract: { openapi: ./contracts/openapi.yaml, operationId: createCustomer }
      description: "创建一个客户记录；需要客户名称与负责人"
      sideEffect: write
      policyHint: confirm
      idempotent: true                   # 写能力必须带 idempotencyKey
      requiredScopes: ["qian.sample.crm:write"]
      requiredPermission: "qian.sample.crm:customer:write"
      dataClassification: pii
      contractVersion: crm-v1
      schemaSha256: "<pack 时计算>"
      audit: true
      agent: { expose: true, requiresConfirmation: true }
  extensionPoints:                       # S2+：本插件开放给别人扩展的点
    - { id: crm.customer.detail.tab/v1, schema: ./contracts/detail-tab.schema.json }
  events:
    publish:   [{ id: crm.customer.created.v1, schema: ./contracts/events/customer-created.v1.json }]
    subscribe: [{ id: identity.member.disabled.v1 }]
  resources:                             # S2+：Agent 可读的元数据（MCP resources 来源）
    - { kind: entity, name: crm.customer, schemaRef: ./metadata/models/customer.json, sensitivity: [pii] }
  permissions:                           # 本插件定义的权限点
    - resource: customer                 # 登记为 qian.sample.crm:customer:<action>（§07 7.5）
      actions: [read, write, export]
      risk: { export: high }
      stepUp: [export]
  defaultRoles:                          # 安装即可用的默认角色模板
    - { role: tenant.owner, grant: ["qian.sample.crm:customer:*"] }
    - { role: tenant.user,  grant: ["qian.sample.crm:customer:read"] }
  messageTypes:
    - { code: crm.customer.assigned, paramsSchema: ./contracts/msg/assigned.json, deepLink: "qian://{tenant}/qian.sample.crm/customer/{id}" }

contributes:
  - to: qian.shell.nav/v1
    items: [{ id: crm.nav, title: { zh-CN: 客户 }, route: /crm, icon: users, permission: "qian.sample.crm:customer:read" }]
  - to: qian.files.upload-policy/v1
    items: [{ id: crm.attachment, allowedTypes: ["application/pdf", "image/*"], maxSize: 20Mi, maxCount: 10 }]

components:
  module:
    assembly: Qian.Sample.Crm            # NuGet 包 ID；源生成器产出 AddQianPlugin_QianSampleCrm()
    dbSchema: plugin_qian_sample_crm
  ui:
    federation: { manifest: ./ui/mf-manifest.json, exposes: [CrmApp, CustomerPicker] }
    sandbox: none                        # none(MF) | worker | iframe；system/official 只允许 none
  service: null                          # service 型插件填写：
  # service:
  #   image: oci://harbor.internal/qian-plugins/crm@sha256:...
  #   protocols: { grpc: ./contracts/crm.proto, http: ./contracts/openapi.yaml }
  #   resources: { cpu: 200m, memory: 256Mi }
  #   health: { liveness: /healthz, readiness: /readyz }
  packages: []                           # S2+
  functions: []                          # S2+
  connector: null                        # S3+

permissions:                             # 反向权限：插件向平台申请
  platform: [tenant:read, member:read, file:write, notify:send]
  data:     [{ entity: "crm.*", access: rw }, { entity: "identity.member", access: r }]
  egress:   []
  secrets:  []
  ui:       { connectSrc: [] }

tenancy:
  modes: [shared]                        # shared | dedicated
  dataIsolation: row                     # row | schema | database
  perTenantEnable: true
  alwaysEnabled: false                   # system 插件为 true

lifecycle:
  hooks:                                 # module：进程内 IPluginLifecycleHooks；service：gRPC qian.lifecycle.v1
    install:         inproc
    tenantProvision: inproc
    tenantUpgrade:   inproc
    tenantDisable:   inproc
    tenantUninstall: inproc
    tenantPurge:     inproc
  migrations:
    platform: ./migrations/platform      # K8s Job 先行
    tenant:   ./migrations/tenant        # 逐租户，幂等，按 fromVersion 条件
  dataRetentionDays: 30

config:
  schema: ./config.schema.json           # JSON Schema 2020-12；sensitive 字段只能 secretRef
  scopes: [platform, tenant]

agent:
  mcp: { expose: true }                  # S4 派生；S1 只落库
  a2a: null                              # 仅 role: agent
  workflows: []                          # S4+

marketplace: null                        # S3+

audit:
  emits: [crm.customer.created, crm.customer.exported]
```

### 3.2 字段逐项说明

**根与标识**

| 字段 | 类型 / 约束 | 说明 |
|---|---|---|
| `schemaVersion` | 常量 `qian.dev/plugin/v1` | 决定校验用的 JSON Schema；破坏性变更开 `v2` |
| `id` | `^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$`，≤ 128 | 反向域名，全局唯一，发布后不可改；系统/官方插件以 `qian.` 开头，第三方不得使用该前缀 |
| `version` | SemVer 2.0 | 插件版本；与 `engines.kun` 的 CalVer 独立 |
| `kind` | 枚举，顶层七值 | `bundle`（复合包，`components` 下含多个组件，组件级 `kind` 取其余六值）、`module`（编译期并入宿主）、`service`（独立 Pod）、`package`（元数据，S2）、`function`（Wasm，S2 可选）、`connector`（S2–S3）、`ui`（浏览器） |
| `role` | 枚举 | `provider`（实现内核契约）、`capability`、`extension`（只扩展别人的扩展点）、`app`、`theme`、`agent`（S4） |
| `tier` | 枚举 | `system`（随内核发版、`alwaysEnabled`）、`official`、`partner`、`private`、`community`；决定允许的 `kind` 与 `components.ui.sandbox`（§7） |
| `distribution` | 枚举，S2+ | `template`（拷贝即拥有）、`managed`（托管可升级，默认，Q10）；仅 `package / bundle` 有意义 |
| `displayName / description` | 多语言映射，`zh-CN` 必填 | 面向人的展示文案；`description` 不用于 Agent（Agent 用 `capabilities[].description`） |
| `publisher` | `{ id, verified }` | `verified` 由市场在 S3 置位，清单内写入的值被忽略 |
| `icon / license / links / categories` | 展示性 | `license` 为 SPDX 标识或 `Commercial` |

**兼容与依赖**

| 字段 | 类型 / 约束 | 说明 |
|---|---|---|
| `engines.qian / kun / yue` | SemVer range | 宿主兼容范围；内核升级前据此预算不兼容插件；`ui` 组件必须声明 `yue` |
| `dependencies[]` | `{ id, version(range), optional }` | 依赖的其他插件；安装时拓扑排序；`optional: true` 的依赖缺失时相关贡献被跳过而非失败 |

**提供（`provides`）**

| 字段 | 类型 / 约束 | 说明 |
|---|---|---|
| `capabilities[].id` | 插件内短名 `<资源>.<动作>`（允许带 `<plugin 短名>.` 前缀的既有写法），插件内唯一 | 能力目录的主键；对应一个 Mediator 命令/查询或 gRPC 方法；全局 URN `<plugin-id>/<capability-id>` 由注册表拼接（见下「命名空间规则」） |
| `capabilities[].version` | 整数 | 能力契约版本；破坏性变更递增并保留旧版一个弃用窗口 |
| `capabilities[].contract` | `{ openapi, operationId }` 或 `{ grpc, method }` | 指向契约文件；pack 时据此抽取输入/输出 JSON Schema |
| `capabilities[].description` | 字符串，必填，≥ 10 字 | **意图式**描述（何时用、何时不用），面向 Agent；TCK 检查非空 |
| `capabilities[].sideEffect` | `read \| write \| external \| destructive` | 对齐 iClaw `ToolSpec.side_effect`；`destructive` 默认 `policyHint: confirm` |
| `capabilities[].policyHint` | `allow \| confirm \| deny` | iClaw ToolPolicy 的默认值来源；运行时权威仍是调用方策略 |
| `capabilities[].idempotent` | 布尔 | `sideEffect ≠ read` 时必须为 `true` 且输入 Schema 含 `idempotencyKey`（分析器与 TCK 强制） |
| `capabilities[].async` | `none \| task` | `task` 表示返回 Operation 资源；契约由 `qian.kernel` 在 S1 的 SDK 契约包中预留，`qian.operations` 实现可到 S2，S1 的导入导出等长操作即以此声明（§07 7.2.1，R8） |
| `capabilities[].requiredScopes[]` | OAuth scope 串 | 调用所需 scope（`<plugin id>:<read\|write\|admin>` 约定） |
| `capabilities[].requiredPermission` | 权限点编码 | `provides.permissions` 登记后的全名 `<plugin-id>:<resource>:<action>`（§07 7.5），供 PEP 使用 |
| `capabilities[].requiredSecrets[]` | 名字 | 只写 secretRef 名，永不写值 |
| `capabilities[].dataClassification` | `public \| internal \| pii \| finance \| secret` | 数据分级，供脱敏与导出策略 |
| `capabilities[].costTier` | `low \| medium \| high` | 成本档，S3 配额与预算用 |
| `capabilities[].contractVersion` | 字符串 | 契约族版本标签，便于按族弃用 |
| `capabilities[].schemaSha256` | 十六进制 | pack 时对「名称 + 描述 + 规范化 inputSchema」计算；进入授权快照，变即旧授权失效 |
| `capabilities[].audit` | 布尔 | 为 `true` 时命令级审计强制记录（`sideEffect ≠ read` 默认 `true`） |
| `capabilities[].agent` | `{ expose, requiresConfirmation }` | S4 是否派生工具、是否需人工确认；S1 落库 |
| `extensionPoints[]` | `{ id (带 /vN), schema }`，S2+ | 本插件开放的扩展点，输入数据 Schema 必填 |
| `events.publish[] / subscribe[]` | `{ id (<资源>.<事件>.v<N>，允许带 <domain>. 前缀), schema }` | 稳定事件 ID（插件内短名）；`publish` 必须有 Schema；用于主题 ACL 与事件目录；全局 URN `<plugin-id>/<event-id>` 由注册表拼接 |
| `resources[]` | `{ kind, name, schemaRef, sensitivity[] }`，S2+ | Agent 可读元数据（MCP resources 来源） |
| `permissions[]` | `{ resource, actions[], risk{}, stepUp[] }` | 本插件定义的权限点；`resource` 不带插件前缀，由 `qian.authz` 登记为 `<plugin-id>:<resource>:<action>`（如 `qian.workflow:flow:launch`、`qian.sample.crm:customer:read`），同时生成 scope 目录 |
| `defaultRoles[]` | `{ role, grant[] }` | 安装即生效的默认角色模板（策略包） |
| `messageTypes[]` | `{ code, paramsSchema, deepLink }` | 向 `qian.notify` 注册的消息类型 |
| `skills` | 目录路径，S3+ | agentskills.io 格式的 `SKILL.md` |

**命名空间规则（R23）**：`capabilities[].id` 与 `events.*[].id` 保持插件内短名约定——能力 `<资源>.<动作>`、事件 `<资源>.<事件>.v<N>`（允许带 `<plugin 短名>.` 前缀的既有写法，如 `crm.customer.search`、`crm.customer.created.v1`），只需插件内唯一；全局唯一形式由 `qian.kernel` 注册表拼接为 URN `<plugin-id>/<capability-id>`、`<plugin-id>/<event-id>`（如 `qian.sample.crm/crm.customer.search`），清单不要求手写。只有权限点必须手写完整插件 ID（`<plugin-id>:<resource>:<action>`，R9）。

**贡献（`contributes[]`）**：`{ to: <扩展点 ID 带 /vN>, items[] }`，`items` 的结构由扩展点 Schema 决定；S1 可用扩展点：`qian.shell.nav/v1`、`qian.shell.settings/v1`、`qian.shell.user-menu/v1`、`qian.shell.command/v1`、`qian.shell.preview/v1`、`qian.shell.client-action/v1`、`qian.files.upload-policy/v1`、`qian.notify.message-type/v1`、`qian.authz.resource-type/v1`、`qian.scheduler.job-type/v1`、`qian.data.dialect/v1`。

**组件（`components`）**

| 字段 | 说明 |
|---|---|
| `module.assembly / dbSchema` | NuGet 包 ID 与 PG schema 名（`plugin_<id 以下划线连接>`）；仅 `system / official` |
| `service.image` | 只接受 `oci://…@sha256:`；tag 引用校验失败 |
| `service.protocols` | `{ grpc: .proto, http: openapi }`，生成 YARP 路由与契约探测 |
| `service.resources / health` | 生成 Deployment 资源与探针；`health.readiness` 必填 |
| `ui.federation` | `mf-manifest.json` 路径与 `exposes[]`；外壳按需 `loadRemote` |
| `ui.sandbox` | `none \| worker \| iframe`；由 `tier` 决定允许值（§7） |
| `packages[] / functions[] / connector` | S2 / S2 / S3 占位；`functions[].limits { fuel, memoryMiB, timeoutMs }` 预留 |

**反向权限（`permissions`）**：`platform[]`（平台能力 scope，如 `tenant:read`、`notify:send`）、`data[]`（`{ entity, access: r|rw }`，跨插件实体访问必须声明）、`egress[]`（外发域名 → NetworkPolicy/出口代理）、`secrets[]`（`{ name, scope: platform|tenant }`）、`ui.connectSrc[]`（CSP）。升级时新增任一项 → 租户管理员重新批准（S2 规则，S1 记录差异）。

**租户（`tenancy`）**：`modes[]`（`shared | dedicated` 部署拓扑）、`dataIsolation`（`row | schema | database`，对齐 Kun 三策略，S1 只接受 `row`）、`perTenantEnable`、`alwaysEnabled`（`system` 必为 `true`）。

**生命周期（`lifecycle`）**：`hooks.*` 取 `inproc`（module）或 `grpc://Lifecycle/<Method>`（service），全部必须幂等；`migrations.platform / tenant` 目录按版本组织，支持 `pre / post` 阶段与 `appVersion` 条件；`dataRetentionDays` 卸载后数据保留天数（默认 30）。

**配置（`config`）**：`schema`（JSON Schema 2020-12，`sensitive: true` 的属性只能填 `secretRef://…`）、`scopes[]`（`platform` → Apollo namespace `qian.plugin.<id>`；`tenant` → `qian.tenancy` 租户配置服务）。

**Agent（`agent`）**：`mcp.expose`、`a2a.card`（仅 `role: agent`）、`workflows[]`（Arazzo）——S1 只校验结构。

**市场（`marketplace`）**：S3 占位（`plans / manageability / screenshots / pricingRef`）。

**审计（`audit.emits[]`）**：本插件会产生的操作审计 action 列表，用于审计查询筛选与文案。

### 3.3 字段分级

- **S1 必做**：`schemaVersion / id / version / kind / role / tier`、`displayName / description / publisher / license`、`engines / dependencies`、`provides.capabilities`（含全部 Agent 标注字段）、`provides.events / permissions / defaultRoles / messageTypes`、`contributes`（S1 扩展点）、`components.module | service | ui`、`permissions.platform / data / egress / secrets / ui`、`tenancy`、`lifecycle`、`config`、`audit`。
- **S2 必做**：`distribution`、`components.packages / functions`、`provides.extensionPoints`（业务插槽）、`provides.resources`、`capabilities[].async: task`。
- **S3 必做**：`marketplace.*`、`components.connector`、`publisher.verified`、`provides.skills`。
- **S4**：`agent.a2a / workflows`、`agent.mcp.expose` 的实际派生。

校验器对高阶段字段的策略：结构校验通过即可（D10），宿主在当前阶段忽略其语义；`qian plugin pack` 对占位字段输出 `warning: field reserved for S2+`。

## 4. 流程

每个流程写「输入 → 动作 → 产出 → 失败处理」。执行者：`qian plugin` CLI（开发者侧）、`qian.kernel` 生命周期控制器（平台侧）、`IDeploymentExecutor`（部署侧）。

### 4.1 pack

- 输入：源码树、`qian-plugin.yaml` 手写部分、源生成器产出的 `qian-plugin.generated.yaml` 片段、契约文件。
- 动作：合并手写与生成片段（生成片段优先，冲突即失败）→ JSON Schema 校验 → 语义校验（`id` 正则、OCI digest、`inputSchema` 无租户字段、写能力有 `idempotencyKey`、`description` 非空、扩展点 ID 带 `/vN`）→ 抽取每个能力的输入/输出 JSON Schema 并计算 `schemaSha256` → 生成派生物（OpenAPI 聚合片段、事件 Schema、AsyncAPI、YARP 路由片段、Kustomize/Helm 片段、NetworkPolicy、Apollo namespace 骨架、权限点与默认角色模板 JSON、TCK 配置）→ 运行 TCK 本地子集 → 打成 OCI 制品（`application/vnd.qian.plugin.v1`），内含清单最终版、文件摘要清单、SBOM、派生物。
- 产出：制品目录 + `manifest.lock.json`（最终清单 + 摘要）。
- 失败：任何校验错误以 `{ code, pointer, message, allowed? }` 列表返回，退出码非零；不产出制品。

### 4.2 sign

- 输入：pack 产物、签名身份（平台密钥 / 伙伴 cosign 身份 / 租户密钥，按 `tier`）。
- 动作：代码制品 → cosign 分离签名 + 证明（SLSA provenance）；元数据包与 `SKILL.md` → 对「文件 URI + SHA-256 + 字节数」清单做 Ed25519 分离签名（与 iClaw Skill 签名载荷 `{ name, version, body_sha256 }` 同形）。
- 产出：签名与证明对象，与制品一同推送；签名**不写回清单**。
- 失败：签名身份与 `tier` 不匹配（如 `partner` 用平台密钥）即拒绝。

### 4.3 publish

- 输入：已签名制品。
- 动作：推 Harbor → 调 `qian.kernel` 注册表 API 以 digest 登记 `plugin_artifacts`（状态 `Submitted`）→ 平台侧验签 + 扫描 + TCK 全量（`system / official` 由 CI 自动完成，`partner` 以上 S3 走审核台）→ `Verified` → `Published`（目录可见，通道 `stable | preview`）。
- 产出：注册表记录；`provides.permissions / defaultRoles / messageTypes / events` 在此刻写入各自系统插件的注册表（权限点、角色模板、消息类型、事件目录），尚不对任何租户生效。
- 失败：验签失败直接拒绝（不是警告）；TCK 失败附报告；重复 digest 幂等返回已存在记录。

### 4.4 install（平台级）

- 输入：`Published` 制品、目标修订号。
- 动作：解析 `dependencies` 拓扑并检查 `engines`；`module` 的安装 = 确认该修订已随宿主镜像发布（注册表核对宿主上报的程序集版本）；`service` 的安装 = `IDeploymentExecutor.GitOps` 生成 Kustomize 片段提交部署仓库 → Argo 收敛 → 健康双条件（K8s Ready + 插件 `/readyz`）回写；`ui` 的安装 = 把 `mf-manifest.json` 发布到静态站并登记插槽贡献；执行 `lifecycle.hooks.install`（平台级一次）与 `migrations.platform`（K8s Job 先行）。
- 产出：`plugin_revisions` 一行 `Installed`；`Activate` 后为 `Active`，旧修订保留 N 个（默认 3）可回切。
- 失败：依赖缺失或 `engines` 不满足列出清单；部署失败不改变 `Active` 修订。

### 4.5 tenant provision（租户级）

- 输入：租户 id、插件 id、目标版本；前置 `Entitled`（`qian.entitlement` 授权存在；`system` 插件恒视为已授权）。
- 动作：租户管理员批准反向权限 `permissions.*`（S1：平台运营代批 + 记录；S2：UI）→ 状态 `Provisioning` → 调 Kun `ITenantProvisioner.ProvisionAsync` 原语 → 执行 `migrations.tenant`（按 `fromVersion = null`）→ 调 `lifecycle.hooks.tenantProvision`（幂等键 `(tenant, plugin, provision, version)`，结果入 `lifecycle_runs`）→ 实例化 `defaultRoles` 到该租户、注册 `contributes` 的租户可见项、写 Apollo 开关默认值 → `Enabled`。
- 产出：`tenant_plugin_installs` 一行 `Enabled`；事件 `kernel.tenant-plugin.enabled.v1`；网关、SDK 中间件、外壳三处缓存失效后放行。
- 失败：任一步失败停留 `Provisioning` 并可重试，重试从失败步继续；超过重试上限转人工；`Disable` 清临时数据但保留业务数据，`Uninstall` 后按 `dataRetentionDays` 保留，`Purge` 才物理删除。

### 4.6 upgrade

- 输入：租户当前版本 `from`、目标修订 `to`。
- 动作：比较两版清单 → 若 `permissions.*` 新增任一项或 `provides.permissions` 风险升级 → 需租户管理员重新批准（S1 记录差异并由运营确认；S2 规则化）→ 状态 `Upgrading` → `migrations.tenant` 按 `fromVersion` 条件执行（扩展 → 迁移数据 → 收缩，S1 只做扩展与迁移，收缩在旧修订退役后另行执行）→ `lifecycle.hooks.tenantUpgrade(from, to)` → `Enabled`。
- 产出：`tenant_plugin_installs.version = to`；事件 `kernel.tenant-plugin.upgraded.v1`。
- 失败：停留 `Upgrading`，可重试或回切（§4.7）；S1 对少量租户串行执行，批次/并发/人工介入点在 S2 的批量迁移中实现。

### 4.7 rollback

- 输入：目标修订 `n−1`（必须在保留窗口内）。
- 动作：平台级 `Rollback(toRevision)`：`module` 回切宿主镜像修订，`service` 回切部署片段，`ui` 回切 `mf-manifest.json`；租户级：因迁移采用「扩展 → 迁移 → 收缩」且收缩延后，数据对旧修订前向兼容，租户 `version` 回写并执行可选的 `tenantUpgrade(from=to, to=n−1)` 兼容钩子。
- 产出：`plugin_revisions.Active = n−1`；事件与审计记录回切原因。
- 失败：若目标修订已 `Retired` 或超出保留窗口则拒绝；不承诺自动数据回滚（与各平台一致）。

## 5. 职责映射

| 字段 | 消费者（插件 / 服务） | 派生物或行为 | 阶段 |
|---|---|---|---|
| `schemaVersion / id / version / kind / role / tier / engines / dependencies` | `qian.kernel` 注册表、`qian plugin` CLI | 制品登记、依赖拓扑、兼容预算、`tier` → 允许的 `kind` 与沙箱 | S1 |
| `provides.capabilities[]`（全部字段） | `qian.kernel` 能力目录；`qian.authz`（`requiredPermission`）；`qian.audit`（`audit`）；`qian-gateway`（`requiredScopes`） | 能力目录条目 + OpenAPI 聚合；PEP 所需权限；审计策略；网关 scope 校验；S4 MCP 工具 | S1（S4 派生） |
| `provides.events` | `qian.events` | 事件目录、JSON Schema、主题 ACL（RabbitMQ 权限） | S1 |
| `provides.permissions / defaultRoles` | `qian.authz` | 权限点注册、scope 目录、默认角色模板（策略包） | S1 |
| `provides.messageTypes` | `qian.notify` | 消息类型注册、参数 Schema、落地深链 | S1 |
| `provides.extensionPoints / resources` | `qian.kernel` 扩展点注册表；S4 MCP resources | 业务插槽、Agent 可读元数据 | S2 |
| `contributes[]` | 扩展点所有者：`qian.shell`（导航/设置/命令/预览/客户端动作）、`qian.files`（上传策略）、`qian.notify`、`qian.authz`、`qian.scheduler`、`qian.data` | 外壳插槽注册、上传策略对象、资源类型、任务类型、方言 | S1 |
| `components.module` | `qian.kernel`（核对宿主程序集）、`qian.data`（schema 名） | 预装校验、PG schema | S1 |
| `components.service` | `IDeploymentExecutor`、`qian-gateway` | Kustomize/Helm 片段、Deployment 资源与探针、YARP 路由与集群、契约一致性探测 | S1 |
| `components.ui` | `qian.shell` | `loadRemote` 目标、`engines.yue` 校验、沙箱模式 | S1 |
| `components.packages / functions / connector` | `qian.metadata`、`qian.wasm-runtime`、`qian.connector-runtime` | 元数据实例化计划、Wasm 限额、连接器映射 | S2 / S2 / S3 |
| `permissions.platform / data` | `qian.authz`、`qian.lifecycle`（批准记录，租户表 `permission_approvals`；`qian.entitlement` 只管授予，R120 ②） | 反向权限审批、跨插件数据访问白名单 | S1 |
| `permissions.egress / ui.connectSrc` | `IDeploymentExecutor`、`qian.shell` | NetworkPolicy / 出口代理规则；CSP | S1 |
| `permissions.secrets` | `qian.config` `ISecretResolver` | 允许解析的 secretRef 范围 | S1 |
| `tenancy` | `qian.kernel`、`qian.tenancy`、`qian.data` | 租户级状态机是否启用、隔离策略装配 | S1 |
| `lifecycle` | `qian.kernel` 生命周期控制器 | 钩子调用、迁移 Job、保留期 | S1 |
| `config` | `qian.config`（platform）、`qian.tenancy`（tenant） | Apollo namespace 骨架与校验器；租户配置 Schema | S1 |
| `agent` | `qian.mcp-facade`（S4） | 工具暴露策略、A2A Card | S4 |
| `marketplace` | `qian.marketplace` | 详情页、计划、可定制性 | S3 |
| `audit.emits` | `qian.audit` | 审计 action 目录与文案 | S1 |
| 全部 | `qian plugin test`（TCK） | 清单校验、契约兼容、钩子幂等、越权探测、租户字段 lint | S1 |

## 6. 选型与待决策项

**已选**

- 清单格式 YAML（人读）+ JSON Schema 2020-12 校验；锁定产物 `manifest.lock.json` 为 JSON。理由：与契约格式（JSON Schema 2020-12、OpenAPI 3.1）同一套工具链。
- 制品格式 OCI artifact（`application/vnd.qian.plugin.v1`）推 Harbor；签名 cosign（代码）+ Ed25519 分离签名（元数据包）。理由：复用现网供应链证据链路，与 iClaw Skill 签名同形。
- 副作用枚举采 iClaw 四值 + `policyHint`，不押注 MCP 工具注解字段名，保留自有扩展 `x-qian-agent`。
- 事件 ID 形如 `<domain>.<entity>.<verb>.vN`，与 iClaw envelope `subject` 约定一致。
- 扩展点与契约 ID 带 `/vN`，破坏性变更开新版并给弃用窗口（借鉴 MCP 12 个月弃用政策）。

**待决策（需在 S1 内拍板）**

| # | 问题 | 默认倾向 | 影响 |
|---|---|---|---|
| T1 | 手写与生成片段的合并策略：生成优先（本版）还是手写优先 + 漂移告警 | 生成优先 | 决定 CI 比对的严格度 |
| T2 | `requiredScopes` 的命名：`<plugin id>:<read\|write\|admin>` 三档还是按资源细分 | 三档起步，S3 市场按需细分 | 影响 OAuth scope 目录规模与同意页 |
| T3 | `module` 修订与宿主镜像的对应关系：一个宿主镜像携带全部 system/official module 的同一修订号，还是允许各 module 独立修订号 | 宿主镜像为发布单元、各 module 保留独立 SemVer 仅作元数据；回切以宿主镜像为粒度 | 决定 7.2.1「回切」的实际粒度 |
| T4 | `function` ABI：Extism 还是 WIT 组件 | S2 中期拍板（ADR-002 遗留） | `components.functions[].limits` 字段形状 |
| T5 | 反向权限批准在 S1 的形态：运营代批 API 还是直接要求租户管理员 UI | 已裁决（R120 ⑤）：租户管理员 API（RBAC，`qian.lifecycle:install:approve` 默认授予 tenant.owner / tenant.admin）与运营代批（Authz 超管旁路）并存，两类主体都要求路径租户等于宿主已建立的租户上下文；每次批准写入该租户审计链（S1-AGT-006）；UI 在 S2 | S2 升级同意规则的起点 |
| T6 | 保留修订数 N 与 `dataRetentionDays` 默认值 | N = 3，30 天 | 存储成本 |
| T7 | 清单是否允许 `dependencies` 指向 `tier: private` 插件 | 不允许（S3 再议） | 市场可组合性 |

## 7. 安全考量

1. **信任等级决定形态与沙箱**（R22）：顶层 `kind: bundle` 对所有 tier 开放，按 tier 收紧的是 bundle 内（或单组件插件）的组件级 `kind`——`system / official` 允许全部六种组件 `module / service / package / function / connector / ui`，UI 沙箱 `none`；`partner` 允许 `package / function / connector / ui`，UI `none`（需审核）或 `worker`；`private` 允许 `package / function / connector / ui`，UI `worker | iframe`；`community` 允许 `package / function / connector / ui`，UI `worker | iframe`；`partner / private / community` 不允许 `module`、`service` 进入共享进程（`service` 仅在 S3 沙箱策略另行开放）。校验器据 `tier` 拒绝不允许的组合。第三方代码永不进共享进程。
2. **强制验签**：未签名或签名不符直接安装失败；信任根经 Apollo `qian.common.trust` 引用 K8s Secret，支持多把密钥与重叠轮换；发布者撤销与黑名单（S3）触发自动下线。
3. **按 digest 引用**：`components.service.image`、`components.functions[].wasm` 只接受 `@sha256:`，杜绝 tag 漂移（TOCTOU）。
4. **租户字段不进输入**：契约 lint 拒绝 `inputSchema` 中出现 `tenantId / tenant_id / tenant` 等字段；租户只来自令牌与平台注入。
5. **密钥只以名字出现**：`requiredSecrets[]`、`permissions.secrets[]`、`config.schema` 的 `sensitive` 属性只能是 `secretRef://…`；pack 时扫描清单与契约文件中疑似密钥的键名与高熵值并拒绝。
6. **反向权限最小化**：`permissions.data` 跨插件实体访问必须显式声明并经批准；未声明的跨 schema 访问在 `qian.data` 层被拒（每插件独立 PG schema 与数据库角色，S3 对第三方生效）；`permissions.egress` 为空时 NetworkPolicy 默认拒绝出网。
7. **工具描述防投毒**：`schemaSha256` 覆盖「名称 + 描述 + 规范化 inputSchema」，变更即旧授权失效；S3 注册表钉死已批准哈希，变更即下线复审。
8. **幂等与副作用分级**：写能力必须幂等并带 `idempotencyKey`；`destructive` 默认需确认；生命周期操作全部 `destructive` + `humanOnly`。
9. **CSP 与前端边界**：`permissions.ui.connectSrc` 生成 CSP；MF 远程只给可信 `tier`；桥接 API 校验来源远程与声明的插槽作用域，来自插件 UI 的消息视为不可信输入。
10. **供应链证据**：制品附 SBOM 与 provenance；CI 对镜像与制品做漏洞扫描；清单中的 `license` 用于许可证合规扫描（禁止引入许可证不兼容的依赖）。

## 8. 验收标准

- [ ] JSON Schema：`qian.dev/plugin/v1` 的 Schema 文件入库，§3.1 示例校验通过；去掉任一 S1 必填字段、把 `image` 改为 tag 引用、在 `inputSchema` 加 `tenantId`、把写能力 `idempotent` 设为 `false`，四个变体各自校验失败并返回带 JSON Pointer 的错误。
- [ ] TCK：参考应用 `pack` 产出的制品含最终清单、文件摘要、SBOM、OpenAPI 聚合片段、事件 Schema、YARP 路由片段、Kustomize 片段、Apollo namespace 骨架、权限点与默认角色 JSON，共九类派生物。
- [ ] IT：未签名制品与错误密钥签名的制品 `publish` 被拒绝；正确签名的制品登记为 `Submitted → Verified → Published`，`provides.permissions / messageTypes / events` 出现在 `qian.authz / qian.notify / qian.events` 的注册表中。
- [ ] E2E：参考应用对租户 A `Provision` 后 `Enabled`，默认角色模板生效、导航出现、上传策略可用；对 A 重复 `Provision` 幂等；对租户 B 未授权时 `Provision` 被拒（`NOT_ENTITLED`）。
- [ ] E2E：发布修订 n+1（新增一条 `permissions.platform`），租户 A 升级前要求重新批准；批准后升级成功；回切到 n 后 A 的请求由修订 n 处理且数据可读。
- [ ] UT：手写清单与源生成片段冲突（同一能力 `sideEffect` 不一致）时 `pack` 失败并指出冲突路径。
- [ ] UT：`schemaSha256` 在修改能力 `description` 后变化；仅修改 `displayName` 不变。
- [ ] 代码审查：清单与契约文件中 grep 不到任何 `secret / token / password` 形态的明文值。

## 9. 后续扩展（S2–S4 字段）

| 阶段 | 字段 | 用途 | 依赖 |
|---|---|---|---|
| S2 | `distribution: template \| managed` | 元数据包分发模式（默认 `managed`，Q10） | `qian.metadata` 分层与同源导入升级 |
| S2 | `components.packages[]` | 元数据文件集合（模型/表单/列表/流程/菜单/角色/种子数据），安装 = 写租户元数据 | 元数据注册表、Schema Migrator |
| S2 | `components.functions[] { id, target, wasm@sha256, limits }` | Wasm 函数插件；`target` 指向函数型扩展点（如 `qian.form.compute/v1`） | ABI 拍板、`qian.wasm-runtime` |
| S2 | `provides.extensionPoints[]`（业务插槽）、`contributes` 到 `qian.form.field-type/v1`、`qian.record.detail.tab/v1`、`qian.list.layout/v1`、`qian.action.type/v1` 等 | 控件、详情页签、列表布局、动作类型注册 | yue 控件注册表、设计器 |
| S2 | `provides.resources[] { kind, name, schemaRef, sensitivity }` | Agent 可读元数据与字段敏感度标签 | 元数据注册表 |
| S2 | `capabilities[].async: task` | 长任务返回 Operation 资源 | `qian.operations` |
| S2 | `lifecycle.migrations.tenant` 的批次参数 `{ batchSize, concurrency, pauseOnFailure }` | 逐租户批量迁移 | `qian.kernel` 批量迁移器 |
| S2 | `upgradePolicy { autoWhenNoNewPermissions, channel }` | 升级同意规则 | 清单 diff |
| S3 | `marketplace { plans[], manageability, screenshots, pricingRef }` | 市场详情、可定制性、套餐 | `qian.marketplace`、`qian.entitlement` 完整模型 |
| S3 | `components.connector { openapi, auth, mapping }` | 通用连接器运行时 | `qian.connector-runtime`、出网基线 |
| S3 | `publisher.verified`、`tier: partner \| private \| community` 的审核字段 | ISV 入驻与审核台 | 市场 |
| S3 | `provides.skills` 目录 | `SKILL.md` 随包分发 | Skills 签名 |
| S3 | `components.ui.sandbox: worker \| iframe` 的实际执行 | 不可信 UI 沙箱 | `qian.shell` 远程渲染白名单 |
| S4 | `agent.mcp.expose` 派生、`agent.a2a.card`、`agent.workflows[]`（Arazzo） | 每租户 MCP 门面、A2A 互通、确定性编排 | `qian.mcp-facade`、Higress MCP 路由 |
| S4 | `generatedBy / promptHash / reviewedBy`（制品元数据，不在清单） | Agent 生成 `package` 的来源标识与合规标识 | 元数据生成闭环 |

扩展原则：新增字段只能 additive；破坏性变更开 `schemaVersion: qian.dev/plugin/v2` 并提供 `qian plugin migrate-manifest`；S1 校验器对未知字段报 warning 而非 error，以便高阶段清单在低阶段宿主上安装其 S1 子集。
