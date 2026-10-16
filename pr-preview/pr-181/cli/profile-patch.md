> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# profile、组合包与 patch

> 本页由构建脚本同步自 [组件模型规格 §7](/pr-preview/pr-181/design/prd/specs/component-model.md)（仓库 `docs/prd/specs/component-model.md`），不另写；请修改规格源文件。

## 7.1 分层模型与叠加顺序

运行中的 Qian 是一棵插件树，由启动时**按序叠加**的层组成，后层覆盖前层（D20.7）：

| 层序  | 层              | 来源                                                       | 内容                                    |
| --- | -------------- | -------------------------------------------------------- | ------------------------------------- |
| 0…n | 组合包层           | profile `bundles[]` 按列出顺序，每个组合包一层                        | 组合包的 `config/qian.patch.yml`（以插入条目为主） |
| n+1 | profile patch  | `profiles/<name>/qian.patch.yml`                         | 本 profile 的替换 / 禁用 / 插入               |
| n+2 | home patch     | `$QIAN_HOME/qian.patch.yml`（默认 `~/.qian/qian.patch.yml`） | 开发者本机或节点级覆盖                           |
| n+3 | CLI overlay    | `--patch <file>`（可重复，按出现顺序）                              | 一次性覆盖（CI、故障演练）                        |
| 运行中 | 租户 scope patch | 数据库表（§7.6）                                               | 租户 scope 的条目（启用 / 禁用 / 定制）            |

叠加规则：条目身份 = `(scope, id)`；后层同身份记录**替换整个 `config`**（不深合并——改一个字段就把整个 `config` 写一遍，这保证 `dump-config` 的任何条目都能原样复制成一条 patch）；后层给出 `plugin` 时替换组件（Provider 替换），省略则沿用；`disabled: true` 使条目从树中移除，更后的层可再 `disabled: false`；插入新条目时 `plugin` 必填、`config` 可为 `{}`（由 `configSchema` 的 `default` 补齐）；层叠完成后统一 Resolve（§5.6），层序只影响覆盖关系不影响挂载顺序；每条记录带来源 `layer`，`dump-config` 原样打印。

## 7.2 profile 文件 `profiles/<name>/qian.profile.yml`

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

## 7.3 组合包的声明方式

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

## 7.4 patch 条目格式 `qian.patch.yml`

一份 patch 文件是**条目数组**，每条按 `https://qian.dev/schemas/patch/v2` 校验。

| 字段         | 类型                           | 必填            | 说明                                               |
| ---------- | ---------------------------- | ------------- | ------------------------------------------------ |
| `id`       | `^[a-z][a-z0-9.-]*$`         | 是             | 条目 id；与 `scope` 构成身份                             |
| `scope`    | `global` 或 `<kind>:<id>`     | 否，默认 `global` | 文件 patch 允许写租户 scope（开发与演练）；生产的租户 scope 条目以数据库为准 |
| `plugin`   | `<component-id>[@<version>]` | 插入时是          | 省略版本 = lock 中已解析版本                               |
| `config`   | `object`                     | 插入时是          | 整个配置对象（整体替换）                                     |
| `disabled` | `boolean`                    | 否             | 从树中移除                                            |
| `note`     | `string`                     | 否             | 说明，`dump-config` 原样打印                            |

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

## 7.5 `qian dump-config` 输出示例

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

## 7.6 租户 scope patch 的数据库表形态

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

## 7.7 S1 五个 profile 的组合包清单（R31 落位表）

组合包数量与边界由组合包定义而不是决策（D20.7）；下表是 R31 给出的落位。规范术语：「平台插件」= `policy.tier: system` 的组件，「系统插件」只作别名。

| profile      | 组合包                                                                       | 组合包内容（条目 → 组件）                                                                                                                                                                                                                                                                                                                                                                                                      | 备注                                                                |
| ------------ | ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| `platform`   | `qian.bundle.base` + `qian.bundle.shell` + 可选 `qian.bundle.model-gateway` | base：原 18 个平台插件按 seam 拆包——`registry`、`lifecycle`、`capabilities`、`hooks`、`tenancy`、`identity → qian.identity.client`、`authz`、`data`、`files`、`config → qian.config.apollo`、`events`、`audit`、`observability`、`notify` + 三个渠道、`scheduler`、`realtime`、`entitlement`（§7.3），并携带平台运维技能 / 命令 / 钩子表面；shell：`shell → qian.shell`（yue 外壳静态制品与外壳 API）；model-gateway：`model-gateway → qian.model-gateway`（P1，默认 `disabled: true`） | 不承诺 AOT                                                           |
| `gateway`    | `qian.bundle.gateway`                                                     | `gateway → qian.gateway`、`identity → qian.identity.client`（Cookie 换 S2S、JWKS）、`config → qian.config.apollo`、`observability`、`audit`                                                                                                                                                                                                                                                                                 | 可 AOT：`build.aot: true`                                           |
| `identity`   | `qian.bundle.identity-server`                                             | `identity → qian.identity.server`（可嵌入 OpenIddict，唯一 issuer）、`identity.connector.oidc`、`identity.connector.wecom`（连接器组件，向 `ctx.Identity.Connectors` 注册 `oidc` / `wecom`，R28 / R38）、`tenancy`、`data`、`config → qian.config.apollo`、`events`、`audit`、`files`（头像，可选）                                                                                                                                                    | 独立安全边界                                                            |
| `standalone` | `qian.bundle.standalone`                                                  | 供消费者宿主安装的最小组合包：`Qian.Runtime` + `config → qian.config`（标准 `IConfiguration`）、`tenancy`（条目配置只含空 `initializationCapabilities`；是否单租户由宿主显式提供 `ITenantContext` / `SingleTenant` 决定，不经配置字符串切换，ADR-019 D19.4，S1-TEN-007 复核对齐）+ 所选 seam 的 Definition / Provider（由 `qian add identity authz files …` 带入）                                                                                                                      | ADR-019 D19.8 的 30 分钟验收宿主；`qian init --template dotnet-webapi` 默认 |
| `minimal`    | `qian.bundle.minimal`                                                     | 测试与 SDK 基准宿主，仅 `Qian.Runtime` 与声明式表面装载器；组合包初始 patch 为空，无默认业务组件（S1-DEC-012，2026-10-09 确认）                                                                                                                                                                                                                                                                                                                            | TCK 宿主、CLI 自身运行时                                                  |

`minimal` 的 `config`、`observability`、`hooks`、`docs` 均为按需添加的组件，不进入默认组合包；表面装载器不因解析 hook 声明而依赖业务 hook Provider。该口径与 PRD §7.0.4 及 S1-RT-002 验收一致，裁决记录见 [ADR-020 §7.1](/pr-preview/pr-181/design/adr/adr-020-component-model-v2.md#71-minimal-默认清单澄清2026-10-09)。

平台插件三件套 `qian.registry` / `qian.lifecycle` / `qian.capabilities` 的包名与服务键为 `Qian.Registry`（`ctx.Registry`）、`Qian.Lifecycle`（`ctx.Lifecycle`）、`Qian.Capabilities`（`ctx.Capabilities`），只进 `platform` profile（base 组合包），不供消费者独立安装（R34）；其它 profile 由 `Qian.Runtime` 的装载器直接从文件 patch 装树，没有数据库注册表与租户 scope patch。

S2 新增 profile `lowcode`（含 `qian.bundle.lowcode` 与 `qian.bundle.file-preview`）与 `workflow`（`qian.bundle.workflow`）（R31；ADR-020 §6）。前端外壳自身也是一棵由 `@qian/runtime` 装载的插件树（D20.2），其 profile 随 `qian.bundle.shell` 内置。
