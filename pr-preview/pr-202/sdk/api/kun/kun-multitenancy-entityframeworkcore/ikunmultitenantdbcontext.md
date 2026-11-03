> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface IKunMultiTenantDbContext

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.MultiTenancy.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.MultiTenancy.EntityFrameworkCore](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-entityframeworkcore/index.md)\
Assembly: Kun.MultiTenancy.EntityFrameworkCore.dll

Marker interface for `DbContext`s that want the
[KunMultiTenantModelCacheKeyFactory](/pr-preview/pr-202/sdk/api/kun/kun-multitenancy-entityframeworkcore/kunmultitenantmodelcachekeyfactory.md) to partition EF
Core's compiled-model cache per tenant. Implementers expose the
ambient tenant id so the factory can emit a distinct cache key for
each tenant, preventing EF from folding the query-filter constant
across tenants on the first model build.

```csharp
public interface IKunMultiTenantDbContext
```

## Remarks

`Kun.EntityFrameworkCore.KunDbContext` already exposes an
equivalent `TenantIdForModelCacheKey` property; custom
contexts that inherit from plain [`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext)
should implement this interface to opt into per-tenant isolation.

## Properties

### TenantIdForModelCacheKey \{#Kun_MultiTenancy_EntityFrameworkCore_IKunMultiTenantDbContext_TenantIdForModelCacheKey}

Ambient tenant id used by the model-cache key and the DbContext-bound query-filter overload.

```csharp
string? TenantIdForModelCacheKey { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
