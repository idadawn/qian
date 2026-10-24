> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Enum TenantIsolationStrategy

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.MultiTenancy](/pr-preview/pr-202/sdk/api/kun/kun-core-multitenancy/index.md)\
Assembly: Kun.Core.dll

SaaS 多租户数据隔离策略。由 `Kun.EntityFrameworkCore.MultiTenancy` 层在运行时解析成
具体的 DbContext 构造、迁移调度与连接解析行为。

```csharp
public enum TenantIsolationStrategy
```

## Fields

`DatabasePerTenant = 2`

每租户独立数据库实例（通常也是独立连接字符串、独立备份、独立 RPO/RTO）。
适用：合规要求强（医疗/金融 GDPR 数据主权）、租户数量 \<= 数百但单租户数据量大。

`SchemaPerTenant = 1`

同库不同 schema，每个租户有独立 schema。逻辑隔离更强、备份粒度到租户；需要运行时
按 `ITenantContext.TenantId` 切 `DefaultSchema` 与 `MigrationsHistoryTable`。
适用：合规要求中等（如数据可"单租户导出"）、租户数量 \<= 数千。

`Shared = 0`

所有租户同库同 schema，用 `IMultiTenant` 实体的 `TenantId` 列 + 全局查询
过滤器做应用层隔离，叠加 PostgreSQL RLS（`AddKunTenantRls()`）做 defence-in-depth。
适用：租户数量大但单租户数据量小、合规要求普通的 B2B SaaS。

## Remarks

Part 1（2026-04-23）只落地枚举本身 + 配套契约类型；[TenantIsolationStrategy.SchemaPerTenant](/pr-preview/pr-202/sdk/api/kun/kun-core-multitenancy/tenantisolationstrategy.md#Kun_Core_MultiTenancy_TenantIsolationStrategy_SchemaPerTenant) 与
[TenantIsolationStrategy.DatabasePerTenant](/pr-preview/pr-202/sdk/api/kun/kun-core-multitenancy/tenantisolationstrategy.md#Kun_Core_MultiTenancy_TenantIsolationStrategy_DatabasePerTenant) 的运行时实现分别延后到 P2-D Part 2 / Part 3。
详细取舍见 `docs/architecture/adr-p2d-multitenancy.md`。
