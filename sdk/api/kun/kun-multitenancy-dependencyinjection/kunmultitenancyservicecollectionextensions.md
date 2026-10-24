> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunMultiTenancyServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.MultiTenancy)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.MultiTenancy.DependencyInjection](/sdk/api/kun/kun-multitenancy-dependencyinjection/index.md)\
Assembly: Kun.MultiTenancy.dll

Service-collection extensions that wire up Kun multi-tenancy.

```csharp
public static class KunMultiTenancyServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunMultiTenancyServiceCollectionExtensions](/sdk/api/kun/kun-multitenancy-dependencyinjection/kunmultitenancyservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunMultiTenancy(IServiceCollection, Action\<KunMultiTenancyOptions>) \{#Kun_MultiTenancy_DependencyInjection_KunMultiTenancyServiceCollectionExtensions_AddKunMultiTenancy_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_MultiTenancy_Configuration_KunMultiTenancyOptions\_\_}

Registers [ITenantContext](/sdk/api/kun/kun-core-multitenancy/itenantcontext.md) as an
[AsyncLocalTenantContext](/sdk/api/kun/kun-multitenancy/asynclocaltenantcontext.md) singleton,
[ITenantResolver](/sdk/api/kun/kun-multitenancy-abstractions/itenantresolver.md) as
[DefaultTenantResolver](/sdk/api/kun/kun-multitenancy/defaulttenantresolver.md), and the
[KunMultiTenancyOptions](/sdk/api/kun/kun-multitenancy-configuration/kunmultitenancyoptions.md) configured by
`configure`. Pair with
`app.UseKunMultiTenancy()` to scope the AsyncLocal context
for each request.

```csharp
public static IServiceCollection AddKunMultiTenancy(this IServiceCollection services, Action<KunMultiTenancyOptions> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunMultiTenancyOptions](/sdk/api/kun/kun-multitenancy-configuration/kunmultitenancyoptions.md)>

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
