> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Namespace Kun.Audit.EntityFrameworkCore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Audit.EntityFrameworkCore.DependencyInjection](/pr-preview/pr-122/sdk/api/kun/kun-audit-entityframeworkcore-dependencyinjection/index.md)

### Classes

[AuditLogConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-audit-entityframeworkcore/auditlogconfiguration.md)

EF Core mapping for [AuditLog](/pr-preview/pr-122/sdk/api/kun/kun-audit/auditlog.md): table name, column shapes,
query indexes, and the append-only convention (no concurrency token —
rows never update; no soft-delete — rows never delete).

[AuditSaveChangesInterceptor](/pr-preview/pr-122/sdk/api/kun/kun-audit-entityframeworkcore/auditsavechangesinterceptor.md)

EF Core [`SaveChangesInterceptor`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor) that emits one
[AuditLog](/pr-preview/pr-122/sdk/api/kun/kun-audit/auditlog.md) per [IAggregateRoot](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/iaggregateroot.md) (or
[IMultiTenant](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/imultitenant.md)) that was added / modified / deleted in
the current `SaveChangesAsync`.

[EfAuditReader\<TContext>](/pr-preview/pr-122/sdk/api/kun/kun-audit-entityframeworkcore/efauditreader-1.md)

在数据库内完成租户过滤与限量，禁止以查询参数切换当前租户。

[EfAuditWriter\<TDbContext>](/pr-preview/pr-122/sdk/api/kun/kun-audit-entityframeworkcore/efauditwriter-1.md)

[IAuditWriter](/pr-preview/pr-122/sdk/api/kun/kun-audit-abstractions/iauditwriter.md) that persists entries through a typed
`TDbContext`. The context may be the same
`KunDbContext` that holds the business aggregates (audit rows
land in the same transaction) or a dedicated audit database context
(decoupled lifecycle, different retention policy).

[PostgresAuditChain](/pr-preview/pr-122/sdk/api/kun/kun-audit-entityframeworkcore/postgresauditchain.md)

按租户串行追加的 PostgreSQL 审计链；记录与链头在同一事务中提交。
