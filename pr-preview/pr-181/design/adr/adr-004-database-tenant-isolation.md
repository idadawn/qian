> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# ADR-004 数据库与租户隔离

> 本页由构建脚本镜像自仓库 [`docs/adr/ADR-004-database-tenant-isolation.md`](https://github.com/idadawn/qian/blob/main/docs/adr/ADR-004-database-tenant-isolation.md)，请修改源文件，不要改本页。

## 1 背景与问题（Context）

Kun 的多租户数据层由 EF 全局过滤（`IMultiTenant` 自动过滤、新增行自动填 `TenantId`、租户模型缓存键）与 PostgreSQL RLS 双拦截器（连接版 + 事务版，PgBouncer 事务池可用）组成，支持 Shared / SchemaPerTenant / DatabasePerTenant 三种策略与幂等的 `ITenantProvisioner`（基线 K §2.1）。测试成熟度不均：只有共享库 + RLS 有真库集成测试，Schema / Database 策略只有单元测试；`ITenantDbContextFactory` 无框架实现；`DatabasePerTenantProvisioner` 不建库、`DeprovisionAsync` 为 no-op（基线 K 附录 B-1；基线 P §4.2）。

MySQL 只是纸面支持：Pomelo 只钉版本、无引用无测试，RLS 无等价物；Kun 的 schema 供给与 PgBouncer 方案都是 PostgreSQL 专属（基线 K §3.2 数据存储行、§7.2 第 5 条）。现网每环境已有专属 PostgreSQL 16 + pgvector（基线 K §6.1）。

已确认的隔离缺口：两套 `ITenantContext` 互斥、claim 名 `tid` 与 `tenant_id` 不一致、请求头兜底与 `FallbackTenantId="default"` 会 fail-open、忘记 `ReplaceService<IModelCacheKeyFactory>` 即跨租户泄漏、RLS 事务版在事务外读返回空、缓存与存储键无租户维度、显式写入他租户 id 只靠 RLS `WITH CHECK` 拦截（基线 K §2.1 差距①–⑩）。参考平台缓存未命中时静默落默认库、租户解析 fail-open（基线 S §6.2 第 2 条）。隔离策略创建后不可改、不提供在线迁移是参考平台与基线的一致做法（基线 S §3.1）。

## 2 决策（Decision）

- **D4.1 PostgreSQL 16 是唯一一等数据库**。平台自身表与低代码业务表都只在 PostgreSQL 上提供生产支持。
- **D4.2 本期不支持 MySQL 与国产数据库作为业务库**（Q5 裁决）。数据接口的「外部数据连接」可按方言插件扩展，但不等于业务库支持。
- **D4.3 默认隔离策略 = 共享库 + 行级 `tenant_id` 过滤 + PostgreSQL RLS 双保险**（`FORCE ROW LEVEL SECURITY` + `WITH CHECK`）。这是唯一有真库集成测试的策略（Q6 裁决）。
- **D4.4 租户上下文只来自已校验令牌**：claim 统一 `tenant_id`；生产禁用 `X-Tenant-Id` 头兜底与 `default` 回退，解析失败即 fail-closed；非 HTTP 入口（消费端、作业、Saga、Agent 调用）显式传播并在每一跳校验。
- **D4.5 平台自身表按 schema-per-service**；每个插件独立 PG schema 与数据库角色，不得直连其他插件的表；低代码动态表见 ADR-011。
- **D4.6 隔离策略在租户创建时一次性决定、创建后不可改**；不提供在线迁移。
- **D4.7 阶段**：S1 可选做 SchemaPerTenant 真库验证；S3 提供独立库（DatabasePerTenant）高隔离档作为付费选项，并补齐建库、注销与集成测试。
- **D4.8 `qian.data` 的默认安全装配**：强制挂 RLS 拦截器与租户模型缓存键；禁止 `IgnoreQueryFilters`；读操作约定包事务（事务版 RLS 只在显式事务内生效）；租户感知缓存与存储键前缀装饰器；CI 检查「在线 DbContext 必须继承多租户基类」。
- **D4.9 数据层约定**：审计字段自动填充拦截器、`CreatedBy` 可空、并发令牌（`xmin`）、snake_case 命名、`Guid.CreateVersion7`、迁移 Job 先行且多副本迁移有协调；分布式 ID 的 WorkerId 在多副本下集中分配。
- **D4.10 跨租户渗透测试样例** 进入 TCK：显式写入他租户 id、无令牌调用、消费端跨租户消息都必须被拦截。

## 3 理由（Rationale）

- 共享库 + RLS 是应用市场自助开通与跨租户运营报表的自然选择，也是 Kun 唯一经真库验证的策略（基线 K §7.2 第 5 条）。
- 双保险的意义：EF 过滤防「忘了写条件」，RLS 防「绕过 ORM 的 SQL」（动态数据层正是绕过 EF 模型的 SQL 生成，见 ADR-011）。
- 不支持 MySQL 不是技术偏好而是事实：RLS、schema 供给、PgBouncer 方案都没有 MySQL 等价物，承诺多库会使每个安全机制都要双份实现。

## 4 备选方案与放弃原因（Alternatives）

- **默认 SchemaPerTenant**：Kun 只有单元测试、无 DbContext 工厂实现；大量租户下每租户一套 schema 的迁移与连接池成本高。保留为 S1 可选验证与高隔离档的中间选项。
- **默认 DatabasePerTenant**：不建库、无集成测试、跨租户报表困难。只作 S3 付费高隔离档。
- **多数据库一等支持（MySQL / 国产库）**：安全机制无等价物，测试矩阵翻倍。本期放弃。

## 5 后果与代价（Consequences）

- 正面：一套安全机制、一套测试；RLS 兜底让动态 SQL 生成层的失误不会直接变成跨租户泄漏。
- 负面：失去「客户指定数据库」的销售灵活性；RLS 的 GUC 设置要求每个连接 / 事务都正确设上下文，连接池配置错误会导致全量读空或泄漏；私有化交付客户若只有国产库，本期无法满足。
- 配套：PgBouncer 事务池参数与 RLS 事务版的兼容测试；共享库下大租户的分区与索引策略；S3 高隔离档的 ApplicationSet 模板（见 ADR-017）。

## 6 对各阶段的影响

- **S1**：`qian.data` 默认安全装配、租户目录与 Shared/RLS 开通、Kun 租户缺口修复、跨租户渗透测试样例。
- **S2**：动态表沿用系统列约定并带 RLS 策略；Schema Migrator 在 SchemaPerTenant 下循环所有租户 schema。
- **S3**：DatabasePerTenant 高隔离档与自助开通；隔离策略作为套餐属性对外。
- **S4**：Agent 调用的租户上下文同样只来自令牌，`inputSchema` 禁止租户字段。

## 7 待确认事项

- **是否允许业务库由租户自带连接**（决策清单 D4 / D11 需拍板项，Q1–Q18 未给默认）。本 ADR 暂按「本期不支持，S3 独立库档由平台托管」写入，需复核。
- 是否承诺隔离策略付费迁移服务（基线 S §11 第 6 条）。
- SchemaPerTenant / DatabasePerTenant 的真库成熟度需 Qian 自补集成测试后才能对外承诺。

## 8 参考

- 相关 ADR：ADR-001、ADR-003、ADR-006、ADR-011、ADR-017。
- PRD：`PRD §07`、`PRD §09`（S3 面向客户）、`PRD §12`、`PRD §13`。
- 基线：K §2.1、§3.2、§5.1 第 1/9 条、§7.2 第 5 条、附录 B-1/B-4；P §4.2、§10.2 第 8 条；S §3.1、§6.2 第 2 条、§9.3 L0、§11 第 6 条。

## S1-DEC-017：真实数据库安全边界（2026-10-11 已批准）

RLS 验收用 NOSUPERUSER / NOBYPASSRLS 应用角色直接执行原生 SQL；管理员连接仅用于 DDL 与造数。FORCE RLS 约束表所有者，不能约束超级用户及 BYPASSRLS 角色，必须保留其可绕过的对照。在线应用启动及新打开数据库连接时校验登录角色和当前角色，拒绝高权限凭据；迁移使用独立管理员连接。

会话变量统一复用 Kun 已发布契约 `app.current_tenant_id`，值为字符串租户标识；策略 `USING` 与 `WITH CHECK` 使用 `NULLIF(current_setting('app.current_tenant_id', true), '')`，不增加第二变量，也不强转 UUID。缺失或空租户不匹配任何业务行。应用角色自身还须由部署侧限制角色切换、DDL 与其他额外授权，RLS 不用于隔离恶意的数据库管理员。
