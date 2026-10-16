> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunSagaServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.DependencyInjection](/pr-preview/pr-181/sdk/api/kun/kun-saga-dependencyinjection/index.md)\
Assembly: Kun.Saga.dll

Extension methods for [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection) to register Kun Saga services.

```csharp
public static class KunSagaServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunSagaServiceCollectionExtensions](/pr-preview/pr-181/sdk/api/kun/kun-saga-dependencyinjection/kunsagaservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunSaga(IServiceCollection, Action\<KunSagaBuilder>) \{#Kun_Saga_DependencyInjection_KunSagaServiceCollectionExtensions_AddKunSaga_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Saga_DependencyInjection_KunSagaBuilder\_\_}

Adds Kun Saga orchestration services to the dependency injection container.

```csharp
public static IServiceCollection AddKunSaga(this IServiceCollection services, Action<KunSagaBuilder> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection to configure.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunSagaBuilder](/pr-preview/pr-181/sdk/api/kun/kun-saga-dependencyinjection/kunsagabuilder.md)>

A delegate to configure the [KunSagaBuilder](/pr-preview/pr-181/sdk/api/kun/kun-saga-dependencyinjection/kunsagabuilder.md), used to register saga types
and their step implementations.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same `services` for fluent chaining.
