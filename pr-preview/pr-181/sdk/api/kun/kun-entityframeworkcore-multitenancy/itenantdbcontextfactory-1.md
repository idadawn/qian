> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface ITenantDbContextFactory\<TContext>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.MultiTenancy](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Builds a `TContext` instance bound to a specific tenant's schema
or database. Used by [SchemaPerTenantProvisioner\<TContext>](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/schemapertenantprovisioner-1.md) at tenant
provisioning time, when the ambient [ITenantContext](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/itenantcontext.md)
may not yet reference the new tenant.

```csharp
public interface ITenantDbContextFactory<TContext> where TContext : DbContext
```

#### Type Parameters

`TContext`

The concrete [`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext) subclass.

## Remarks

Consumers implement this interface once per DbContext type. A minimal implementation
wraps `DbContextOptionsBuilder.UseNpgsql(...).UseKunSchema(tenantId)` and returns
a freshly-constructed `TContext`.

For **runtime query access** (inside a request), prefer the standard
DI-injected `TContext` registered via
`AddDbContext<TContext>((sp, opts) => opts.UseKunSchema(sp.GetRequiredService<ITenantContext>().TenantId!))`
combined with [TenantAwareModelCacheKeyFactory](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/tenantawaremodelcachekeyfactory.md). The factory is the explicit
escape hatch for administrative / provisioning flows that must target a specific tenant
regardless of the ambient context.

## Methods

### CreateForTenant(string) \{#Kun_EntityFrameworkCore_MultiTenancy_ITenantDbContextFactory_1_CreateForTenant_System_String\_}

Builds a `TContext` bound to the specified tenant's schema.

```csharp
TContext CreateForTenant(string tenantId)
```

#### Parameters

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

Non-empty tenant ID.

#### Returns

TContext

A new, caller-owned `TContext`; caller must dispose.
