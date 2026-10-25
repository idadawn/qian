> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class KunSagaBuilder

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Saga)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.DependencyInjection](/pr-preview/pr-50/sdk/api/kun/kun-saga-dependencyinjection/index.md)\
Assembly: Kun.Saga.dll

Builder for registering saga types and their steps with the Kun Saga infrastructure.
Obtained via [KunSagaServiceCollectionExtensions.AddKunSaga](/pr-preview/pr-50/sdk/api/kun/kun-saga-dependencyinjection/kunsagaservicecollectionextensions.md#Kun_Saga_DependencyInjection_KunSagaServiceCollectionExtensions_AddKunSaga_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Saga_DependencyInjection_KunSagaBuilder__).

```csharp
public sealed class KunSagaBuilder
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunSagaBuilder](/pr-preview/pr-50/sdk/api/kun/kun-saga-dependencyinjection/kunsagabuilder.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddSaga\<TSaga, TContext>(JsonTypeInfo\<TContext>) \{#Kun_Saga_DependencyInjection_KunSagaBuilder_AddSaga__2_System_Text_Json_Serialization_Metadata_JsonTypeInfo___1\_\_}

Registers a saga type and its associated context. The saga is resolved from DI as a
scoped service; registration never constructs a temporary container.

```csharp
public KunSagaBuilder AddSaga<TSaga, TContext>(JsonTypeInfo<TContext> typeInfo) where TSaga : class, ISaga<TContext> where TContext : ISagaContext
```

#### Parameters

`typeInfo` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)\<TContext>

The [`JsonTypeInfo`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1) for `TContext` used for AOT-safe
JSON serialization. Obtain from a source-generated `JsonSerializerContext`.

#### Returns

[KunSagaBuilder](/pr-preview/pr-50/sdk/api/kun/kun-saga-dependencyinjection/kunsagabuilder.md)

The same builder for fluent chaining.

#### Type Parameters

`TSaga`

The saga type implementing [ISaga\<TContext>](/pr-preview/pr-50/sdk/api/kun/kun-saga-abstractions/isaga-1.md).

`TContext`

The saga context type implementing [ISagaContext](/pr-preview/pr-50/sdk/api/kun/kun-saga-abstractions/isagacontext.md).
