---
id: S1-DATA-001
title: AddQianData 默认安全装配与 RLS 迁移模板
status: done
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
updated: 2026-10-12
---

## 背景

组件 `qian.data`（`Qian.Data.Abstractions` / `ctx.Data` + `Qian.Data`，随 SDK 进每个 .NET profile）：`AddQianData<TContext>()` 强制 RLS 连接版 + 事务版拦截器、`KunMultiTenantModelCacheKeyFactory`、租户与软删过滤、审计字段填充、`xmin`、snake_case、Guid v7、Outbox 同 DbContext；`QianDbContext : KunDbContext` 构造期校验否则 `QianDataMisconfiguredException`；`UseKunSchema("plugin_<id>")`；系统列约定；迁移生成器为每个 `IMultiTenant` 表输出 `FORCE ROW LEVEL SECURITY` 策略；CI 检查「在线 DbContext 必须继承 `QianDbContext`」。全部组件表的前提。

Kun 2026.10.9 行为变更（S1-KUN-009，2026-10-10）：snake_case、Guid v7 默认主键、可空审计主体与 PostgreSQL `xmin` 并发令牌在 Kun 里是显式 opt-in（`AddKunDataConventions<TContext>()` / `ModelBuilder.ApplyKunDataConventions()`），不随 `AddKunEntityFrameworkCore()` 自动生效，由 `AddQianData<TContext>()` 统一装配并在构造期校验；`SaveChangesResultAsync()` 把并发冲突映射为 `Result` 的 Conflict；已有非空主键不变，旧非空数值主体需显式迁移。

## 验收

- [x] UT：缺少 RLS 拦截器或模型缓存键的 DbContext 在启动时被 `AddQianData` 拒绝；代码中 `IgnoreQueryFilters()` 触发分析器错误
- [x] IT：以非超级用户、无 BYPASSRLS 的应用角色直连绕过 EF，在设置 `app.current_tenant_id = A` 后 `SELECT` 不到租户 B 的行，向 B 插入被 `WITH CHECK` 拒绝（RLS 兜底）；管理员仅建表造数，保留超级用户可绕过的对照，在线应用拒用高权限角色（S1-DEC-017）
- [x] E2E：参考应用的客户表在租户 A、B 各插入 1000 行，A 的列表查询只返回 A 的数据且 `EXPLAIN` 显示走租户索引
- [x] CI：在线 DbContext 不继承 `QianDbContext` 时门禁失败
- [x] RLS 连接版与事务版拦截器、`KunMultiTenantModelCacheKeyFactory` 的装配改为复用 Kun 2026.10.8 起提供的 `AddKunTenantRls<TContext>()`，`AddQianData` 只补 Qian 自己的约束与启动校验，S1-KUN-001 先行交付的最小装配随之收敛；若评估后不复用，在 `packages/Qian.Data/README.md` 写明原因（S1-KUN-008 的未决问题，产品负责人 2026-10-08 同意并入本任务）

## 分阶段验收边界

按已批准的 [S1-DEC-018](S1-DEC-018.md)，以上五项为数据本体验收，本任务在本体通过 CI 并合并后完成，解除 Registry 的实现前置。完整组件交付仍须由 [S1-RT-014](S1-RT-014.md) 完成 SDK 字段源生成、默认装配与私有 NuGet 锁步发布，并由 [S1-RT-011](S1-RT-011.md) 回验真实 `qian add data` 安装。相关安装门通过前，`plugins/qian.data` 保留骨架标记；QA-002 总目标继续未完成。

## 进度日志

- 2026-10-06 planner：创建任务

- 2026-10-08 cody：S1-KUN-001 先行交付 Qian.Data.Abstractions / Qian.Data 最小 RLS 安全装配及启动拒绝测试，详见 packages/Qian.Data/README.md；本任务仍负责完整组件、迁移模板、数据约定与分析器，未认领或提前勾选本任务验收。
- 2026-10-08 cody：先行安全基础已合并 Qian PR #7（https://github.com/idadawn/qian/pull/7），后续实现复用 QianDbContext 与 AddQianData；完整验收与本任务状态保持待办。
- 2026-10-08 claude：验收加一条：AddQianData 的 RLS 拦截器与模型缓存键装配复用 Kun 2026.10.8 的 AddKunTenantRls<TContext>()（S1-KUN-008 未决问题，产品负责人同意并入本任务）
- 2026-10-10 claude：背景记入 Kun 2026.10.9 的数据约定变更（snake_case / Guid v7 / `xmin` 为显式 opt-in，`AddKunDataConventions<TContext>()` 由 `AddQianData` 装配），随 S1-KUN-009 升级可用
- 2026-10-11 cody：用户已批准补齐QA-002必要前置。开始DATA-001：复用Kun 2026.10.9安全装配和数据约定，补迁移RLS、编译期门禁与真实双租户验收。
- 2026-10-11 cody：验收中超级用户受RLS约束的要求与PostgreSQL16事实及仓库受限应用角色测试规范冲突；记录决策等待确认，继续独立的Kun装配、模型约定及分析器实现。
- 2026-10-11 cody：独立准备已实现：复用Kun双拦截器/审计/租户写入，强制插件schema与模型约定，保留业务过滤；编译正反例通过。PostgreSQL16容器8项数据测试无跳过通过，包括超级用户对照、Guid v7/审计/xmin/软删。自动迁移、完整组件与参考应用仍未完成，DEC-017待确认。
- 2026-10-11 cody：DEC-017已获批，验收口径阻塞解除。草稿PR #44保留，待当前AZ-001契约收尾后按既定串行约束恢复完整实现；尚未完成的功能与验收不变。
- 2026-10-11 cody：Kun权限修复等待发布期间串行恢复：落实已批准DEC-017，先同步真实角色边界及app.current_tenant_id，并补在线高权限角色拒绝。
- 2026-10-11 cody：DEC-017已实施：会话契约与受限角色验收已同步；应用启动和同步/异步开连接拒绝superuser/BYPASSRLS，预打开连接及管理员SET ROLE降权对照同样拒绝。13项真库数据测试通过；全仓266项测试零跳过，36门禁及12包依赖检查通过。自动迁移、完整seam/Outbox与1000行索引仍未完成。
- 2026-10-12 cody：新增PostgreSQL迁移SQL生成器：从目标模型的租户标记自动为新建表输出ENABLE/FORCE RLS，限制策略抵御附加宽松策略，空租户拒绝访问。真实执行迁移差异器生成的SQL验证跨租户写和空租户写均42501；数据16项通过。历史表改造仍须显式迁移，参考应用/seam/Outbox尚未完成。
- 2026-10-12 cody：当前增量全仓269项测试无跳过、36门禁与12包依赖检查通过；保留草稿#44，串行转回已取得正式Kun修复包的AZ-001收尾。DATA待恢复项为完整Data seam/Outbox、deleted_by和每租户1000行参考应用索引验收，既有代码与验收证据保留。
- 2026-10-12 cody：AZ-001已通过最终CI并随PR #45合并；恢复DATA完整实现。同步主线正式Kun2026.10.11与QIAN0005，合并保留数据QIAN0003/0004和全部角色/RLS策略保护。
- 2026-10-12 cody：与AZ-001主线合并后验证通过：正式Kun2026.10.11，13个包依赖检查、37项门禁、293项测试均成功且无跳过；保留QIAN0003/0004/0005全部规则。接着补齐Data seam、同上下文Outbox、deleted_by及真实参考应用索引验收。
- 2026-10-12 cody：新增真实DataPlugin、DataDefinition/IDataService/IDataSession：每次会话独立scope，复用Kun EfUnitOfWork并绑定同一上下文/事件缓冲；验证通知同scope、保存失败整体回滚、多上下文互不串写、租户切换拒绝及卸载释放。补齐deleted_by：直接Remove转软删除，删除主体/时间自动填充且原审计不能改写。全仓299测试零跳过；数据22项含严格ValidateOnBuild/ValidateScopes通过；13包依赖、37门禁覆盖通过（一次本地还原进程异常的委托编译样例独立重跑通过）。旧组件配置骨架与正式入口尚待对齐，千行参考应用/索引验收未完成，保持草稿。
- 2026-10-12 cody：五项本体验收完成：实际Kestrel+RS256身份+PostgreSQL的客户列表验证A/B各1000行；捕获真实列表SQL执行EXPLAIN ANALYZE命中租户复合索引。连接引用选择、独立库模式和租户挂载边界通过；全仓308测试零跳过、37门禁及13包依赖通过。SDK字段生成RT-014依赖RT-004，RT-004又依赖DATA-001，完整代码安装另属RT-011；按DEC-018请求本体与集成分期，未获批前保留草稿及骨架标记。
- 2026-10-12 cody：独立前置CFG-001已随PR #46合并；同步最新主线并保留本草稿。组合验证316项测试零跳过、37门禁、15包依赖检查通过，包含真实CRM/PG索引与真实Apollo。DEC-018仍待产品负责人答复，本次同步不代表DATA合并或安装门通过。
- 2026-10-12 cody：同步已合并的 CFG-002 与 Kun 2026.10.12；组合全仓构建零警告、358 项测试零失败零跳过、38 项门禁及 16 包依赖检查通过。真实 CRM HTTP/RLS 与实际列表 SQL 索引计划、真实 Apollo 拒绝/恢复同时通过。PR #44 继续保持草稿和 blocked，DEC-018 未答复，不改验收分期或 SDK/CLI 安装边界。
- 2026-10-12 cody：产品负责人已批准 DEC-018 分阶段验收；五项数据本体验收完成，并随 PR #44 在最终 CI 全绿后合并交付。复核 Linux 证据为 358/358 测试零跳过，真实列表查询命中租户复合索引。SDK 源生成、默认装配、私有 NuGet 锁步发布和 qian add data 已回写 RT-014/011 为未完成必过项；保留骨架标记，QA-002 未完成。
