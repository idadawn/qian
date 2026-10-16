> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunEventBusServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.DependencyInjection](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-dependencyinjection/index.md)\
Assembly: Kun.EventBus.dll

Registers Kun event bus services.

```csharp
public static class KunEventBusServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunEventBusServiceCollectionExtensions](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-dependencyinjection/kuneventbusservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunEventBus(IServiceCollection, Action\<KunEventBusBuilder>?) \{#Kun_EventBus_DependencyInjection_KunEventBusServiceCollectionExtensions_AddKunEventBus_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_EventBus_DependencyInjection_KunEventBusBuilder\_\_}

Adds event bus abstractions and an in-process outbox buffer.

```csharp
public static IServiceCollection AddKunEventBus(this IServiceCollection services, Action<KunEventBusBuilder>? configure = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunEventBusBuilder](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-dependencyinjection/kuneventbusbuilder.md)>?

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
