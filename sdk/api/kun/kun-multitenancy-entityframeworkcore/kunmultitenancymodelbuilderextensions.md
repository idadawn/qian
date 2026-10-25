> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunMultiTenancyModelBuilderExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy.EntityFrameworkCore` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.MultiTenancy.EntityFrameworkCore)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.MultiTenancy.EntityFrameworkCore](/sdk/api/kun/kun-multitenancy-entityframeworkcore/index.md)\
Assembly: Kun.MultiTenancy.EntityFrameworkCore.dll

[`ModelBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder) extensions that attach the Kun multi-tenant
global query filter to every [IMultiTenant](/sdk/api/kun/kun-core-domain/imultitenant.md) entity.

```csharp
public static class KunMultiTenancyModelBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunMultiTenancyModelBuilderExtensions](/sdk/api/kun/kun-multitenancy-entityframeworkcore/kunmultitenancymodelbuilderextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### ApplyKunMultiTenantFilters(ModelBuilder, ITenantContext) \{#Kun_MultiTenancy_EntityFrameworkCore_KunMultiTenancyModelBuilderExtensions_ApplyKunMultiTenantFilters_Microsoft_EntityFrameworkCore_ModelBuilder_Kun_Core_MultiTenancy_ITenantContext\_}

Retained binary entry point for the unsafe accessor-only overload; rejects model creation.

```csharp
[Obsolete("Use ApplyKunMultiTenantFilters(this) with a DbContext implementing IKunMultiTenantDbContext. Accessor-only filters cannot safely bind a shared model.", DiagnosticId = "KUNTENANT001")]
public static ModelBuilder ApplyKunMultiTenantFilters(this ModelBuilder modelBuilder, ITenantContext tenantContext)
```

#### Parameters

`modelBuilder` [ModelBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder)

`tenantContext` ITenantContext

#### Returns

[ModelBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder)

#### Remarks

An accessor alone cannot bind a reused model to the executing DbContext. Capturing its value
would also be unsafe when the host uses EF's default shared model cache. Migrate to the
DbContext overload; KunDbContext consumers already receive the safe filter automatically.

### ApplyKunMultiTenantFilters\<TContext>(ModelBuilder, TContext) \{#Kun_MultiTenancy_EntityFrameworkCore_KunMultiTenancyModelBuilderExtensions_ApplyKunMultiTenantFilters__1_Microsoft_EntityFrameworkCore_ModelBuilder___0\_}

Attaches the tenant predicate to the executing DbContext so a shared model never reads
a previous context's tenant accessor. Call with `modelBuilder.ApplyKunMultiTenantFilters(this)`.

```csharp
public static ModelBuilder ApplyKunMultiTenantFilters<TContext>(this ModelBuilder modelBuilder, TContext context) where TContext : DbContext, IKunMultiTenantDbContext
```

#### Parameters

`modelBuilder` [ModelBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder)

`context` TContext

#### Returns

[ModelBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder)

#### Type Parameters

`TContext`
