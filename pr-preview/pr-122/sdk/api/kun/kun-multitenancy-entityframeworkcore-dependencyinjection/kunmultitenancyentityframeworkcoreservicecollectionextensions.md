> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunMultiTenancyEntityFrameworkCoreServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.MultiTenancy.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.MultiTenancy.EntityFrameworkCore.DependencyInjection](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy-entityframeworkcore-dependencyinjection/index.md)\
Assembly: Kun.MultiTenancy.EntityFrameworkCore.dll

Service-collection extensions for the EF Core side of Kun.MultiTenancy.

```csharp
public static class KunMultiTenancyEntityFrameworkCoreServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunMultiTenancyEntityFrameworkCoreServiceCollectionExtensions](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy-entityframeworkcore-dependencyinjection/kunmultitenancyentityframeworkcoreservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunMultiTenancyEntityFrameworkCore(IServiceCollection) \{#Kun_MultiTenancy_EntityFrameworkCore_DependencyInjection_KunMultiTenancyEntityFrameworkCoreServiceCollectionExtensions_AddKunMultiTenancyEntityFrameworkCore_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Registers [KunTenantSaveChangesInterceptor](/pr-preview/pr-122/sdk/api/kun/kun-multitenancy-entityframeworkcore/kuntenantsavechangesinterceptor.md) as a
scoped service so consumers can attach it to their
`DbContextOptionsBuilder.AddInterceptors(...)` call.
Pair with `modelBuilder.ApplyKunMultiTenantFilters(this)` on an IKunMultiTenantDbContext
in `OnModelCreating` for full read + write coverage.

```csharp
public static IServiceCollection AddKunMultiTenancyEntityFrameworkCore(this IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
