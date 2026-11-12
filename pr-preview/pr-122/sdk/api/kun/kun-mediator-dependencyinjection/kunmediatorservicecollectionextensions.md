> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunMediatorServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Mediator)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator.DependencyInjection](/pr-preview/pr-122/sdk/api/kun/kun-mediator-dependencyinjection/index.md)\
Assembly: Kun.Mediator.dll

Extension methods for registering [KunMediator](/pr-preview/pr-122/sdk/api/kun/kun-mediator/kunmediator.md) with an
[`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection).

```csharp
public static class KunMediatorServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunMediatorServiceCollectionExtensions](/pr-preview/pr-122/sdk/api/kun/kun-mediator-dependencyinjection/kunmediatorservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunMediator(IServiceCollection, Action\<KunMediatorBuilder>) \{#Kun_Mediator_DependencyInjection_KunMediatorServiceCollectionExtensions_AddKunMediator_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Mediator_KunMediatorBuilder\_\_}

Adds [KunMediator](/pr-preview/pr-122/sdk/api/kun/kun-mediator/kunmediator.md) and all handlers/behaviors configured via
`configure` to the service collection.

```csharp
public static IServiceCollection AddKunMediator(this IServiceCollection services, Action<KunMediatorBuilder> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunMediatorBuilder](/pr-preview/pr-122/sdk/api/kun/kun-mediator/kunmediatorbuilder.md)>

A delegate that configures the [KunMediatorBuilder](/pr-preview/pr-122/sdk/api/kun/kun-mediator/kunmediatorbuilder.md) with request handlers,
pipeline behaviors, and notification handlers.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection) for chaining.
