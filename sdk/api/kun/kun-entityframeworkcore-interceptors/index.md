> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.EntityFrameworkCore.Interceptors

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[AuditFieldsSaveChangesInterceptor](/sdk/api/kun/kun-entityframeworkcore-interceptors/auditfieldssavechangesinterceptor.md)

在业务保存前填充审计字段，保留创建字段，拒绝无法转换的已认证主体。

[SchemaConsistencyInterceptor](/sdk/api/kun/kun-entityframeworkcore-interceptors/schemaconsistencyinterceptor.md)

Fails fast when a [KunDbContext](/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md)'s `DefaultSchema` diverges from the EF Core
`MigrationsHistoryTable` schema. Throws at the first `SaveChangesAsync` so developers
catch mis-wired `UseKunSchema` calls before any business write lands in the wrong schema.

[TenantRlsConnectionInterceptor](/sdk/api/kun/kun-entityframeworkcore-interceptors/tenantrlsconnectioninterceptor.md)

Defence-in-depth interceptor that pushes the current [ITenantContext.TenantId](/sdk/api/kun/kun-core-multitenancy/itenantcontext.md#Kun_Core_MultiTenancy_ITenantContext_TenantId)
into the PostgreSQL session variable `app.current_tenant_id` on every
connection open, and clears it on connection close. Used in combination with
Row-Level Security policies on the database side — EF Core's global query
filter remains the first line of defence; this interceptor is the second.

[TenantRlsTransactionInterceptor](/sdk/api/kun/kun-entityframeworkcore-interceptors/tenantrlstransactioninterceptor.md)

Emits `set_config('app.current_tenant_id', @tenant, true)` at the start of every
EF Core transaction so PostgreSQL Row-Level Security policies can use
`current_setting('app.current_tenant_id')` to enforce tenant isolation even when a
caller bypasses the global query filter (e.g. `IgnoreQueryFilters()`, admin scenarios).
