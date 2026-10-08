---
id: S1-DATA-001
title: AddQianData 默认安全装配与 RLS 迁移模板
status: todo
stage: S1
area: DATA
wave: 1
priority: P0
type: feature
owner: ""
estimate: 5d
depends: [S1-KUN-001, S1-KUN-005, S1-RT-001]
refs:
  - docs/prd/07-s1-foundation.md#7.6
  - docs/adr/ADR-004-database-tenant-isolation.md
  - docs/adr/ADR-011-metadata-dynamic-data.md
updated: 2026-10-08
---

## 背景

组件 `qian.data`（`Qian.Data.Abstractions` / `ctx.Data` + `Qian.Data`，随 SDK 进每个 .NET profile）：`AddQianData<TContext>()` 强制 RLS 连接版 + 事务版拦截器、`KunMultiTenantModelCacheKeyFactory`、租户与软删过滤、审计字段填充、`xmin`、snake_case、Guid v7、Outbox 同 DbContext；`QianDbContext : KunDbContext` 构造期校验否则 `QianDataMisconfiguredException`；`UseKunSchema("plugin_<id>")`；系统列约定；迁移生成器为每个 `IMultiTenant` 表输出 `FORCE ROW LEVEL SECURITY` 策略；CI 检查「在线 DbContext 必须继承 `QianDbContext`」。全部组件表的前提。

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
