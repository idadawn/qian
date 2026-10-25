> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunSagaDbContextExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Persistence.Ef](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence-ef/index.md)\
Assembly: Kun.Saga.dll

Extension methods for [`ModelBuilder`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder) to apply Kun Saga entity configurations.
Call [KunSagaDbContextExtensions.ApplyKunSagaConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence-ef/kunsagadbcontextextensions.md#Kun_Saga_Persistence_Ef_KunSagaDbContextExtensions_ApplyKunSagaConfiguration_Microsoft_EntityFrameworkCore_ModelBuilder_) from your `DbContext.OnModelCreating`
override to register `saga_instances` and `saga_step_queue` tables.

```csharp
public static class KunSagaDbContextExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunSagaDbContextExtensions](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence-ef/kunsagadbcontextextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### ApplyKunSagaConfiguration(ModelBuilder) \{#Kun_Saga_Persistence_Ef_KunSagaDbContextExtensions_ApplyKunSagaConfiguration_Microsoft_EntityFrameworkCore_ModelBuilder\_}

Applies the EF Core entity type configurations for [SagaInstance](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence/sagainstance.md) and
[SagaStepQueueEntry](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md) to the given `modelBuilder`.

```csharp
public static ModelBuilder ApplyKunSagaConfiguration(this ModelBuilder modelBuilder)
```

#### Parameters

`modelBuilder` [ModelBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder)

The model builder to configure.

#### Returns

[ModelBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder)

The same `modelBuilder` for fluent chaining.
