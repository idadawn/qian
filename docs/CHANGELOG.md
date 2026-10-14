# 变更日志

> 记录对读者可见的变化：新文档与新规格、新组件与命令、破坏性变更。按日期倒序，每条一个二级标题 `## YYYY-MM-DD 标题`，下面一两行说明并链接到详情。文档站首页「最新动态」取最近 4 条（[docs-site.md](prd/specs/docs-site.md) §2a.1）。

## 2026-10-15 数据契约冻结、组件元数据与文档预算整改（S1-QA-010）

[data/v1 契约](../contracts/data/v1/README.md) 按 R80 明确 frozen 状态、修订规则和已知例外，统一 `$id` 前缀并补结构测试；[yue manifest](../packages/yue/core/README.md) 的属性元数据（含嵌套）递归取自 TS 注释，不存在的属性拒绝导出；[文档站预算](prd/specs/docs-site.md#7-门禁) 按 R55 调为 120 MB，main 实测 96.2 MB。

## 2026-10-15 数据连接连通测试的出网约束与凭据错误（S1-DATA-005）

- 连通测试先解析目标主机并逐个检查地址：缺省拒绝回环、链路本地（含云元数据）、100.100.100.200、未指定、组播与广播地址。运维可经 `QianDataConnectionOptions.Egress` 追加拒绝网段或限定放行网段；被拒返回 DATA_CONNECTION_TARGET_DENIED，不连接也不读取密钥，放行时只连检查过的地址。
- 租户密钥不是 UTF-8 文本时，PG 方言返回 DATA_CONNECTION_CREDENTIAL_INVALID，不再是 500。见 [Qian.Data](../packages/Qian.Data/README.md)。

## 2026-10-14 能力目录信封契约、别名 Link 头与通用执行入口规范化（S1-RT-019）

`GET /api/capabilities` 的响应冻结为 [`contracts/v0/capability-catalog`](../contracts/v0/capability-catalog.schema.json)（`items`、`ttlMs`、`cacheScope`），`ttlMs` 来自组件配置 `catalogTtlMs`；`/api/kernel/capabilities` 别名同时返回 `Deprecation` 与 `Link: <后继路径>; rel="successor-version"`；`POST /api/capabilities/{plugin}/{name}/invoke` 先按条目 `inputSchema` 校验（400 `INPUT_INVALID`），写类能力支持 `Idempotency-Key`（24 小时内重放返回首次响应并带 `Idempotent-Replayed: true`）；`Qian.Capabilities` 随包提供 EF 迁移（偏好表、回执表，经 `Qian.Data` 生成器带 RLS）。详见[组件规格 §9.5](prd/specs/component-model.md) 与 [PRD 7.2.1](prd/07-s1-foundation.md)。

## 2026-10-14 yue 组件文档迁入 Rspress（S1-YUE-005）

51 个组件入口、141 个公开组件/部件的 Props、令牌页与可编辑的真实演示统一进入现有 Rspress。共用示例及 126 个状态场景迁移到独立 Playwright，离线编辑器资源随站点发布；移除旧文档应用与依赖，保留原有八项门禁。使用说明见 [core 主文档](../packages/yue/core/README.md)。
## 2026-10-14 身份事件载荷不再重复信封字段（S1-DEC-020、S1-IDP-011）

破坏性变更：五类身份事件的载荷去掉 `event_id`、`occurred_at`、`tenant_id`，构造参数随之减少，`IdentityIntegrationEvent` 不再实现 Kun 的 `IIntegrationEvent`；三项只由 Qian.Events 写入端生成在信封上。v1 尚无生产方、消费方与已发布包，按 S1-DEC-020 就地修正，此后的破坏性变化才升 v2。见 [身份事件契约](../contracts/identity/v1/README.md) 与 [ADR-008](adr/ADR-008-events-messaging.md) D8.7。

## 2026-10-14 记录变更事件改为登记载荷（S1-DATA-004）

`qian.record.changed.v1` 按 Qian.Events 的模型发布：`[IntegrationEventId]` 移到 `RecordChangedPayload`，`RegisterRecordEvents` 登记载荷，信封只由写入端生成一层；`RecordChangedEvent` 改为 after Hook 视图，由 `RecordChangedEvent.FromEnvelope` 从收到的信封派生。破坏性变更：删除 `DataContractJson.RecordChangedEventTypeInfo`，`EnsureConsistentContext` 移到载荷，改为核对当前租户与事件来源。线上信封、payload Schema 与 fixture 不变，见 [数据契约规格 §6](prd/specs/data-contracts.md#6-record-hook-与变更事件)。

## 2026-10-14 统一 Rspress 组件文档与验收口径（S1-YUE-005、S1-DOCS-002）

产品负责人决定不再使用 Storybook；[ADR-022](adr/ADR-022-rspress-unified-component-docs.md) 记录真实组件试构建结果、Props 默认过滤问题及离线编辑器要求。YUE-005 重新打开承接替换，DOCS-002 复用接线；本次为评估与规格修订，迁移实现尚未完成。

## 2026-10-14 基准类型、PG 方言组件与数据连接档案（S1-DATA-003）

- 10 个系统基准类型作为种子数据随 `Qian.Data.Abstractions` 发布；新增方言 SPI qian.data.dialect/v1 与 PostgreSQL 方言包 `Qian.Data.Dialect.Pg`，`DialectTypeMapping` 拒绝超出长度、精度、小数位或取值区间的字段定义。
- `IDataConnectionRegistry`：按租户隔离的连接档案，口令只存租户 secretRef，连通测试在使用点解析，详情不回传密钥。新增 `Qian.Data.AspNetCore` 提供 `GET /api/data/meta/types` 与 `/api/data/connections`（含 `POST /connections/{id}:test`）。见 [Qian.Data](../packages/Qian.Data/README.md)。

## 2026-10-14 事件抽象拆包与 Definition 依赖门禁（S1-KUN-010）

KunVersion 升为 2026.10.14，身份契约改用 `Kun.EventBus.Abstractions`。Definition 的 Kun 直接依赖新增门禁，既有数据基类/RLS 依赖按具体边记录例外，见 [测试规范](../tests/README.md#definition-的-kun-直接依赖s1-kun-010)；框架关系与 API 参考随同版本更新。

## 2026-10-14 多副本迁移协调、雪花 ID 与序号服务（S1-DATA-002）

- `IDataMigrator`：按 schema 取 PostgreSQL advisory lock 后执行 EF 迁移，同时启动的副本只有一个执行 DDL；K8s 迁移 Job 用 `--migrate-only`，在线副本启动时发现未应用的迁移即拒绝启动。
- `ISnowflakeIdGenerator`（WorkerId 经 Redis 租约 `qian:snowflake:worker:{n}` 分配，带高水位，编号回收复用不重 ID）与 `ISequenceService`（规则 `'SO' + yyyyMMdd + seq(6)`，单条 upsert 原子取号，规则修改后从 1 开始）。见 [Qian.Data](../packages/Qian.Data/README.md)。

## 2026-10-14 yue 基础组件与 Storybook（S1-YUE-005）

[core 组件库](../packages/yue/core/README.md) 补齐 51 个组件入口、统一令牌、可寻址部件和机器清单；Storybook 10 覆盖明暗与交互状态，附令牌页、Schema 生成类型和真实构建页面的键盘/无障碍门禁。

## 2026-10-14 集成事件组件 Qian.Events（S1-EVT-001）

- 新增 `Qian.Events.Abstractions` / `Qian.Events`：入队即生成 v1 事件信封（与 iClaw 对齐），Outbox 按租约领取、publisher confirm 后逐条标记，停机或 broker 变慢时交还未发的行，多副本不重复发布；RabbitMQ topic exchange `qian.events` 与 quorum / 广播队列命名。见 [Qian.Events](../packages/Qian.Events/README.md)。
- 新编译门禁 QIAN0006：集成事件只能在写命令 handler 或领域事件 handler 内发布。参考应用的客户创建事件改为 `crm.customer.created.v1`，配置 `ConnectionStrings__Events` 后投递到 RabbitMQ。

## 2026-10-14 数据过滤与记录扩展契约（S1-DATA-004）

新增 [数据契约规格](prd/specs/data-contracts.md)：统一 Filter AST、13 运算符与 10 类字段矩阵、15 个系统变量、数据规则，以及 Record Hook / qian.record.changed.v1 信封；JSON 与源生成 C# 契约随 Qian.Data.Abstractions 分发，供 S2 五处条件消费方复用。

## 2026-10-13 验收复核跟进与 Kun 2026.10.13（S1-DEC-019、S1-KUN-014、S1-DOCS-004）

- 复核 10-10 之后完成的 21 项任务：12 项真完成、8 项有口径偏差、1 项证据不足。修正与补强记为裁决 R65–R74：Kun 同日修复版的 NuGet 语义与精确钉定、受保护请求的运行期兜底、仓库 profile 生效配置的明文密钥扫描进 CI、T23 与 DEC-018 的口径、DEC-014–018 的批准留痕、任务日志署名校验。
- `KunVersion` 升到 2026.10.13：首个包含 hotfix.2 / hotfix.3 修复的无后缀正式版，带入 Mediator 对受保护请求的构建期兜底（上游 idadawn/kun#57）。
- 文档站改用 `@idadawn/yue-tokens` 的令牌，删除占位令牌与 data-theme 桥接脚本。

## 2026-10-10 Kun 升级到 2026.10.9（S1-KUN-009）

- `KunVersion` 升到 2026.10.9，带入 Outbox 与消费可靠性（S1-KUN-002，上游 idadawn/kun#46）、审计原子性与数据约定（S1-KUN-005，idadawn/kun#45）、平台基础端口与废弃 API 标记（S1-KUN-006，idadawn/kun#47）三组修复；`Kun.Authorization` 与 `Kun.Configuration.Apollo` 首次随 Kun 发包。文档站的「Kun 框架」小节与 Kun API 参考随之切到 `v2026.10.9`，[与 Qian 的关系](../website/docs/sdk/kun/with-qian.mdx) 的修复状态表与模块表同步更新。
- 下游要跟的行为变更已记入 S1-EVT-001（Outbox `NextAttemptAt` 迁移、非事务存储默认拒绝无锁）、S1-AZ-001（`PermissionBehavior` 逐命令注册、缺引擎即拒绝）、S1-DATA-001（snake_case / Guid v7 / `xmin` 为显式 opt-in）。

## 2026-10-09 yue 工程化基线、八项门禁与 Base UI 验证门（S1-YUE-003）

- 建立 yue 前端工作区：pnpm workspace + `catalog:` + Turborepo，`@yue/*` 八个 S1 包的骨架（tokens / contracts / core / sdk / shell / data / form / biz）、Rslib 构建、Changesets 与 `0.x` 版本策略；六层分层矩阵由 eslint-plugin-boundaries 强制，dependency-cruiser 查循环依赖。
- CI 工作流 `yue` 八项门禁在 PR 上独立显示：lint、typecheck、unit（Vitest 浏览器模式）、a11y（Playwright + axe）、lighthouse（首屏 script ≤ 250 KB）、boundaries、publint、circular。
- Base UI 验证门通过：Menu / Select / Combobox / Tabs / Tooltip 封装在 `@yue/core`，Tree 自建在 `@yue/data`，结论与五项发现记入 ADR-013 §9；靶场 `apps/yue-playground`。
- 发包渠道拍板：GitHub Packages，包名 `@idadawn/yue-<包>`（文档中 `@yue/<包>` 是简称，规格 §3.1）。

## 2026-10-08 Kun 升级到 2026.10.8（S1-KUN-008）

- `KunVersion` 升到 2026.10.8，带入多租户后台链路修复（S1-KUN-001，上游 idadawn/kun#43）。文档站的「Kun 框架」小节与 Kun API 参考随之切到 `v2026.10.8`。

## 2026-10-09 文档站产品族地图与版本矩阵（S1-DOCS-006）

- 「开始」部分新增产品族页 `/start/family`（Kun / Qian / yue 与周边仓库，与仓库 README 同一张表）与由钉版文件生成的版本矩阵 `/start/versions`（哪个 Qian 配哪个 Kun、.NET SDK、Node、K3s 基线）；`llms.txt` 先按项目（Qian → Kun → yue）再按部分分节。见 [docs-site.md](prd/specs/docs-site.md) §2b.4。

## 2026-10-09 文档站机器可读：每页 Markdown、llms.txt、知识库索引与 frontmatter 校验（S1-DOCS-003）

- 每页多一份构建期渲染的 `.md`（标题下「复制为 Markdown」），全站 `llms.txt` / `llms-full.txt`，加 `kb-index.json`（页面清单、元数据与内容 sha256）与 `sitemap.xml` 作为知识库导入契约；页面 frontmatter 按规范校验，生成页也补齐 `project` / `audience` / `updated` / `source`。见 [docs-site.md](prd/specs/docs-site.md) §4、§5.1。

## 2026-10-08 文档站：Kun ADR 补收、API 页去掉侧栏、产物体积预算（S1-DEC-011）

- Kun 小节补收 ADR「P2-D SaaS 多租户 3-策略架构」。API 命名空间页与类型页不再显示侧栏，产物从约 57 MB 降到约 48 MB，并设 100 MB 预算。API 参考路径统一在 `/sdk/api/`。见 [docs-site.md](prd/specs/docs-site.md) §2b、§7，裁决 R54–R62。

## 2026-10-08 文档站「Kun 框架」小节与 Kun API 参考（S1-DOCS-005）

- SDK 部分新增「Kun 框架」：按 `Directory.Packages.props` 的 `KunVersion`（当前 2026.9.13）从 kun 仓库镜像上手、模块地图、指南、安全、设计说明与 ADR、白皮书与变更日志，另有手写的「与 Qian 的关系」；Kun API 参考由 DocFX 从同版本 `Kun.*` 包生成（[docs-site.md](prd/specs/docs-site.md) §2b、[ADR-021](adr/ADR-021-product-family-structure.md)）。

## 2026-10-08 文档站上线（S1-DOCS-001）

- Rspress 2.0 文档站：七个部分、中文主题、全文搜索、Mermaid，PR 预览与合并后自动发布；设计文档（本目录）在站内「设计与路线」镜像。

## 2026-10-07 产品族结构：Kun / Qian / yue（ADR-021）

- 三层口径「Qian 平台基于 Kun 框架构建，前端使用 yue 组件库」；不建大单仓，产品族仓库统一在 `idadawn`（Kun 已迁入）；Kun 文档将按 `KunVersion` 锁定版本镜像进文档站（[ADR-021](adr/ADR-021-product-family-structure.md)、[docs-site.md](prd/specs/docs-site.md) §2b）。

## 2026-10-07 任务看板上 K3s（S1-PM-002）

- 离线看板经 git-sync + nginx 部署到 K3s，由 Argo CD 托管。

## 2026-10-07 CI 基础：PublicAPI、依赖图与集成测试（S1-QA-001）

- .NET 构建基线、PublicAPI 门禁、依赖图门禁与 Testcontainers fixture（PostgreSQL 16 / Redis 7 / RabbitMQ / Apollo 替身），见仓库 `tests/README.md`。

## 2026-10-07 文档站选型与信息架构

- 选型 Rspress 2.0（[ADR-013](adr/ADR-013-frontend-yue.md) D13.18）；七个部分、组件页模板、frontmatter 与首页规范写入 [docs-site.md](prd/specs/docs-site.md)。

## 2026-10-07 组件模型 v2 与库优先定稿

- [ADR-020](adr/ADR-020-component-model-v2.md) 与 [ADR-019](adr/ADR-019-library-first-packages.md) 转为 Accepted，§7 待确认事项全部拍板。

## 2026-10-07 任务即文件的项目管理（S1-PM-001）

- 任务文件、离线看板、文档门禁 CI 与编码代理入口（`AGENTS.md`）。

## 2026-10-06 组件模型 v2 回填各章

- 一切皆插件、声明式表面、`qian add` 一键安装、seam 三角色、scope、profile / patch 的口径回填 PRD 各章与[组件模型规格](prd/specs/component-model.md)。

## 2026-10-06 设计文档首版

- ADR-001…018、PRD §01–§17、迁移方案 v1 草案，以及调研基线要点汇编与关键决策清单 v0。
