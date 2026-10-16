> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.MultiTenancy.EntityFrameworkCore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.MultiTenancy.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.MultiTenancy.EntityFrameworkCore.DependencyInjection](/sdk/api/kun/kun-multitenancy-entityframeworkcore-dependencyinjection/index.md)

### Classes

[KunMultiTenancyModelBuilderExtensions](/sdk/api/kun/kun-multitenancy-entityframeworkcore/kunmultitenancymodelbuilderextensions.md)

[`ModelBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder) extensions that attach the Kun multi-tenant
global query filter to every [IMultiTenant](/sdk/api/kun/kun-core-domain/imultitenant.md) entity.

[KunMultiTenantModelCacheKeyFactory](/sdk/api/kun/kun-multitenancy-entityframeworkcore/kunmultitenantmodelcachekeyfactory.md)

[`IModelCacheKeyFactory`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.infrastructure.imodelcachekeyfactory) that emits a distinct compiled
model per tenant when the [`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext) implements
[IKunMultiTenantDbContext](/sdk/api/kun/kun-multitenancy-entityframeworkcore/ikunmultitenantdbcontext.md). Falls back to EF Core's
default key shape (context type + design-time flag) otherwise, so
consumers who forget to opt in get the same behaviour they had
before.

[KunTenantSaveChangesInterceptor](/sdk/api/kun/kun-multitenancy-entityframeworkcore/kuntenantsavechangesinterceptor.md)

EF Core [`SaveChangesInterceptor`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.diagnostics.savechangesinterceptor) that walks newly added
[IMultiTenant](/sdk/api/kun/kun-core-domain/imultitenant.md) entities and, when their
[IMultiTenant.TenantId](/sdk/api/kun/kun-core-domain/imultitenant.md#Kun_Core_Domain_IMultiTenant_TenantId) is [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) or
empty, injects the ambient [ITenantContext.TenantId](/sdk/api/kun/kun-core-multitenancy/itenantcontext.md#Kun_Core_MultiTenancy_ITenantContext_TenantId).
Existing non-empty values are preserved so administrative tooling
that explicitly sets a tenant id retains control.

### Interfaces

[IKunMultiTenantDbContext](/sdk/api/kun/kun-multitenancy-entityframeworkcore/ikunmultitenantdbcontext.md)

Marker interface for `DbContext`s that want the
[KunMultiTenantModelCacheKeyFactory](/sdk/api/kun/kun-multitenancy-entityframeworkcore/kunmultitenantmodelcachekeyfactory.md) to partition EF
Core's compiled-model cache per tenant. Implementers expose the
ambient tenant id so the factory can emit a distinct cache key for
each tenant, preventing EF from folding the query-filter constant
across tenants on the first model build.
