---
id: S1-DATA-001
title: AddQianData 默认安全装配与 RLS 迁移模板
status: blocked
stage: S1
area: DATA
wave: 1
priority: P0
type: feature
owner: "cody"
estimate: 5d
depends: [S1-KUN-001, S1-KUN-005, S1-RT-001, S1-KUN-009]
refs:
  - docs/prd/07-s1-foundation.md#7.6
  - docs/adr/ADR-004-database-tenant-isolation.md
  - docs/adr/ADR-011-metadata-dynamic-data.md
updated: 2026-10-11
---

## 背景

组件 `qian.data`（`Qian.Data.Abstractions` / `ctx.Data` + `Qian.Data`，随 SDK 进每个 .NET profile）：`AddQianData<TContext>()` 强制 RLS 连接版 + 事务版拦截器、`KunMultiTenantModelCacheKeyFactory`、租户与软删过滤、审计字段填充、`xmin`、snake_case、Guid v7、Outbox 同 DbContext；`QianDbContext : KunDbContext` 构造期校验否则 `QianDataMisconfiguredException`；`UseKunSchema("plugin_<id>")`；系统列约定；迁移生成器为每个 `IMultiTenant` 表输出 `FORCE ROW LEVEL SECURITY` 策略；CI 检查「在线 DbContext 必须继承 `QianDbContext`」。全部组件表的前提。

Kun 2026.10.9 行为变更（S1-KUN-009，2026-10-10）：snake_case、Guid v7 默认主键、可空审计主体与 PostgreSQL `xmin` 并发令牌在 Kun 里是显式 opt-in（`AddKunDataConventions<TContext>()` / `ModelBuilder.ApplyKunDataConventions()`），不随 `AddKunEntityFrameworkCore()` 自动生效，由 `AddQianData<TContext>()` 统一装配并在构造期校验；`SaveChangesResultAsync()` 把并发冲突映射为 `Result` 的 Conflict；已有非空主键不变，旧非空数值主体需显式迁移。

## 验收

- [ ] UT：缺少 RLS 拦截器或模型缓存键的 DbContext 在启动时被 `AddQianData` 拒绝；代码中 `IgnoreQueryFilters()` 触发分析器错误
- [ ] IT：以数据库超级用户直连绕过 EF，在设置 `app.tenant_id = A` 后 `SELECT` 不到租户 B 的行，向 B 插入被 `WITH CHECK` 拒绝（RLS 兜底）
- [ ] E2E：参考应用的客户表在租户 A、B 各插入 1000 行，A 的列表查询只返回 A 的数据且 `EXPLAIN` 显示走租户索引
- [ ] CI：在线 DbContext 不继承 `QianDbContext` 时门禁失败
- [ ] RLS 连接版与事务版拦截器、`KunMultiTenantModelCacheKeyFactory` 的装配改为复用 Kun 2026.10.8 起提供的 `AddKunTenantRls<TContext>()`，`AddQianData` 只补 Qian 自己的约束与启动校验，S1-KUN-001 先行交付的最小装配随之收敛；若评估后不复用，在 `packages/Qian.Data/README.md` 写明原因（S1-KUN-008 的未决问题，产品负责人 2026-10-08 同意并入本任务）

## 进度日志

- 2026-10-06 planner：创建任务

- 2026-10-08 cody：S1-KUN-001 先行交付 Qian.Data.Abstractions / Qian.Data 最小 RLS 安全装配及启动拒绝测试，详见 packages/Qian.Data/README.md；本任务仍负责完整组件、迁移模板、数据约定与分析器，未认领或提前勾选本任务验收。
- 2026-10-08 cody：先行安全基础已合并 Qian PR #7（https://github.com/idadawn/qian/pull/7），后续实现复用 QianDbContext 与 AddQianData；完整验收与本任务状态保持待办。
- 2026-10-08 claude：验收加一条：AddQianData 的 RLS 拦截器与模型缓存键装配复用 Kun 2026.10.8 的 AddKunTenantRls<TContext>()（S1-KUN-008 未决问题，产品负责人同意并入本任务）
- 2026-10-10 claude：背景记入 Kun 2026.10.9 的数据约定变更（snake_case / Guid v7 / `xmin` 为显式 opt-in，`AddKunDataConventions<TContext>()` 由 `AddQianData` 装配），随 S1-KUN-009 升级可用
- 2026-10-11 someone：用户已批准补齐QA-002必要前置。开始DATA-001：复用Kun 2026.10.9安全装配和数据约定，补迁移RLS、编译期门禁与真实双租户验收。
- 2026-10-11 someone：验收中超级用户受RLS约束的要求与PostgreSQL16事实及仓库受限应用角色测试规范冲突；记录决策等待确认，继续独立的Kun装配、模型约定及分析器实现。
- 2026-10-11 someone：独立准备已实现：复用Kun双拦截器/审计/租户写入，强制插件schema与模型约定，保留业务过滤；编译正反例通过。PostgreSQL16容器8项数据测试无跳过通过，包括超级用户对照、Guid v7/审计/xmin/软删。自动迁移、完整组件与参考应用仍未完成，DEC-017待确认。
