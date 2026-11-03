> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# qian 命令

> 本页由构建脚本同步自 [组件模型规格 §9](/pr-preview/pr-122/design/prd/specs/component-model.md)（仓库 `docs/prd/specs/component-model.md`），不另写；请修改规格源文件。

## 9.1 命令与参数

全局参数：`--profile <name>`（默认 `.qian/config.yml` 的 `defaultProfile`）、`--cwd <dir>`、`--json`、`--verbose`、`--yes`。实现：单一 Node / TypeScript 实现、发布到 npm，前置 Node ≥ 22；`dotnet qian` 薄包装（只负责定位并调用 Node 版 CLI、缺 Node 时给出安装提示）为 S1 可选 P2（ADR-020 §7 第 3 条，→ §13）；命令、参数与行为不依赖实现语言。遥测默认不采集，S2 提供 opt-in `qian telemetry on`（ADR-020 §7 第 7 条，→ §13）。

| 命令                             | 参数                                                                                                                                                                                                                                                                    | 作用                                                                                                                   |
| ------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `qian init --template <stack>` | `<stack>`：`dotnet-webapi`（standalone profile 的 ASP.NET Core 项目）、`dotnet-platform`（platform 宿主）、`ts-shell`（yue 外壳）、`skill-only`（只含 `skills/` 的组件仓库）、`component-dotnet` / `component-ts`（带代码组件骨架）；`--id`、`--name`                                                       | 生成 `.qian/config.yml`、`profiles/<p>/qian.profile.yml`、`.qian-plugin/plugin.json`、`README.md` / `AGENTS.md` 骨架、TCK 配置 |
| `qian add <ref>…`              | `<ref>` = `<name>[@<version>]` / `<market>/<name>[@<version>]` / 本地路径 / URL；`--from <market>`、`--scope <scope>`（写租户 scope patch，需平台连接）、`--surfaces skills,commands`（只装部分表面）、`--no-code`、`--force-profile`、`--allow-unverified`、`--force`、`--reset-config`、`--dry-run` | 安装（§9.3）                                                                                                             |
| `qian remove <ref>…`           | `--purge`、`--scope <scope>`                                                                                                                                                                                                                                           | 卸载（§9.4）                                                                                                             |
| `qian list`                    | `--outdated`、`--markets`、`--surfaces`、`--scope <scope>`                                                                                                                                                                                                               | 列出已安装组件（来源、版本、digest、表面、条目）；`--outdated` 对比市场最新版                                                                     |
| `qian doctor`                  | `--fix`（可自动修复：缺失默认 config、过期 lock、表面指针）、`--manifest <path>`（只校验一个组件）、`--migrate-manifest <qian-plugin.yaml>`                                                                                                                                                          | 健康检查：§10.1 全部校验 + 依赖等待 / 环路 / 缺失 Provider / digest 不符 / `secretRef` 不可解析 / 文档新鲜度                                     |
| `qian dump-config`             | §7.5                                                                                                                                                                                                                                                                  | 打印插件树                                                                                                                |
| `qian dev`                     | `--profile`、`--watch`（声明式表面热重载）、`--aspire`（拉起 PG / Redis / RabbitMQ / Apollo 容器）                                                                                                                                                                                      | 代码部分：本机运行当前 profile 的宿主，取代 v1 `qian plugin dev`（R33）                                                                 |
| `qian pack`                    | `--out <dir>`                                                                                                                                                                                                                                                         | 代码部分：构建 NuGet / npm 包（组件文件夹进包的 `qian/`）、`service` 的 OCI 制品与文件 digest 清单、SBOM；运行 §10.1 校验（R33）                        |
| `qian sign`                    | `--key <ref>`                                                                                                                                                                                                                                                         | 代码部分：OCI 制品 cosign 分离签名；组件文件夹 digest 清单 Ed25519 分离签名（沿 v1 §4.2）                                                      |
| `qian publish`                 | `--market <name>`、`--channel stable\|preview`                                                                                                                                                                                                                         | 代码部分：推送包 / 制品，并向市场仓库提交或更新 marketplace 条目（PR 或直接提交，按市场仓库策略）                                                           |

CLI 动词定稿为以上十个，没有 `plugin` 名词（R33）：`add \| remove \| list \| doctor \| dump-config \| init` 面向安装与运维，`dev \| pack \| sign \| publish` 面向代码部分。

## 9.2 `npx qian@latest` 一键形式

```text
npx qian@latest [--skill <name>]… [--agent <name>]… [--command <name>]… [--mcp <name>]… [--plugin <name>]… [--profile <p>] [--from <market>] [--yes]
```

| 参数                                       | 等价                                                                                       |
| ---------------------------------------- | ---------------------------------------------------------------------------------------- |
| `--plugin <name>`                        | `qian add <name>`                                                                        |
| `--skill <name>`                         | `qian add <name> --surfaces skills --no-code`；`<name>` 为 `<component>/<skill>` 时只复制该技能目录 |
| `--agent` / `--command` / `--mcp <name>` | 同上，分别只装 `agents` / `commands` / `mcpServers` 表面                                          |
| `--yes`                                  | 跳过 `installation: confirm` 的确认                                                           |

各 flag 可重复、可并列；没有构建步骤，组合就是多个 flag 并列（ADR-020 §1 对标项目做法一）。

## 9.3 安装过程（`qian add`）

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

## 9.4 幂等与回滚

- **幂等**：同一 `<name>@<version>` 重复 `add`：比对 lock 中 digest 与文件哈希，一致则无操作返回 0；文件被本地修改过则列出差异并拒绝覆盖（`--force` 覆盖）。不同版本 = 升级：替换表面文件、更新依赖版本、**保留** patch 中既有 `config`（按新 `configSchema` 重新校验，不兼容时打印差异并失败，`--reset-config` 用新默认值重写）。
- **事务与自动回滚**：步骤 4 任一子步骤失败时按 journal 逆序撤销已完成的子步骤（删除已复制文件、移除已加依赖、还原 patch 文件），返回非零；journal 保留最近 20 次。
- **`qian remove`**：按 lock 逆向执行——删除 `.qian/plugins/<id>/`、移除依赖（仅当无其它已安装组件引用同一包）、patch 条目改 `disabled: true`（默认，保留 `config` 便于重装）或整条删除（`--purge`）。
- **git 友好**：全部产出都是工作区文件，任何一步都能用 git 还原。
- **租户 scope**（`--scope tenant:<id>`）：不动工作区，调用管理面 API 写 `scope_patches`（§7.6），`revision` 乐观并发，失败不留半成品。

## 9.5 管理面端点（R33 / R32）

| 前缀                                              | 提供者                 | 内容                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| ----------------------------------------------- | ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `/api/registry/*`                               | `qian.registry`     | 条目与插件树（`GET /api/registry/entries?scope=`，等价 `dump-config`）、scope patch（`PUT /api/registry/tenants/{tenant}/patches/{scope}/{entry}`）、lock 与来源；对未挂载 / 被过滤的组件，持有 `qian.registry:scope:read` 的调用者得到 403 原因码（`PLUGIN_NOT_MOUNTED` 等四个，R32 / R48），其他调用者 404                                                                                                                                                                                                                                                                                                                                     |
| `/api/lifecycle/*`                              | `qian.lifecycle`    | `mount` / `unmount` / `reload`（声明式表面）、滚动重启请求、`/api/lifecycle/diagnostics`（Pending / Failed / 环路）                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `/api/capabilities/*`                           | `qian.capabilities` | 能力目录（工具、事件、实体），鉴权后按 scope 与权限过滤，响应为信封 `contracts/v0/capability-catalog`（`items`、`ttlMs`、`cacheScope: private`）；`/api/capabilities/openapi.json` 聚合                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `POST /api/capabilities/{plugin}/{name}/invoke` | `qian.capabilities` | 通用执行入口（R75）：每次重做与目录同款的判定，不可见与不存在一样 404；请求体先按条目 `inputSchema`（JSON Schema 2020-12）校验，不符返回 400 `INPUT_INVALID`，`errors[]` 带输入 JSON Pointer；请求体读不成确定的 JSON 值（成员重名、孤立的 UTF-16 代理项、绝对值超过 decimal 范围约 7.9e28 的数）返回 400 `BAD_REQUEST`；写类能力可带 `Idempotency-Key`（UUID），同一主体、同一能力、同一键 24 小时内不再执行：回执保存了响应体时返回首次成功响应（`Idempotent-Replayed: true`），按回执策略（见表下）没有保存时返回 409 `CAPABILITY_REPLAY_UNAVAILABLE`；键相同而输入不同、或同键请求仍在执行，返回 409 `CONFLICT`，失败不留回执，只读能力（`sideEffect: read`）忽略该键；Agent 主体只见 `agent.expose: true` 的能力，S1 不开放 Agent 执行与 Actor 委托；ApiExplorer 不列此通用模板，`openapi.json` 为每个可见能力列出具体路径与 Schema |
| `/api/kernel/*`                                 | 别名                  | v1 路径在 S1 内保留为别名，响应带 `Deprecation` 头与 `Link: rel="successor-version"`，S2 移除                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |

全部管理面端点只在 `platform` profile 存在，受 `qian.authz` 的 PEP 保护；业务与模型表面（路由、`ctx.Tools`、能力目录、菜单）对未挂载 / 被过滤的组件一律 404（R32）。

**执行入口的回执策略与登记期检查**（S1-RT-020）：幂等回执只在条目 `dataClassification` 为 `public` / `internal`、且响应体不超过组件配置 `receiptMaxBytes`（UTF-8 字节，默认 65536，取值 0–1048576）时保存响应体；`pii` / `finance` / `secret`（及未知分级）与超限的响应只记「已执行」和原因（`data_classification` / `response_too_large`），回放窗口内同键请求返回 409 `CAPABILITY_REPLAY_UNAVAILABLE` 且不再执行。组件作者要按响应内容标注能力的 `dataClassification`：SDK 默认 `internal`，即默认保存响应体；响应含个人信息、财务或机密数据的能力标为 `pii` / `finance` / `secret`（参考应用响应含客户名称的 `customer.*` 能力标为 `pii`）。回执表另以检查约束限定响应体不超过 1 MiB，迁移以 `NOT VALID` 添加、只约束新写入：存量超限行会让带校验的约束失败，而迁移属主在 FORCE RLS 下删不掉这些行；DDL 校验本身不经 RLS。回执过期后不再回放，物理删除靠本租户后续带键写调用登记时的惰性清理（每次最多 100 行）；长期没有带键写调用的租户由运维清理（运维手册给出 SQL）。条目在能力登记（`ctx.ProvideCapability`）时生成并预编译输入校验器：输入含租户字段、Schema 无法可靠校验（`patternProperties` / `dependentSchemas` 键含 `/` 或 `~`、`$ref` 找不到目标、`enum` / `const` 取值含超出 decimal 范围的数、无法编译）的契约在登记阶段以带组件 ID、能力名与 JSON Pointer 的异常拒绝，组件挂载失败，执行入口不会因此返回 500。登记期检查由宿主调用 `AddQianCapabilities()` 装配，platform profile 宿主必须调用，否则退化为首次读目录或调用时才编译。
