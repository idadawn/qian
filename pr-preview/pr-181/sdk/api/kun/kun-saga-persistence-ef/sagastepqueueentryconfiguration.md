> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class SagaStepQueueEntryConfiguration

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Persistence.Ef](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence-ef/index.md)\
Assembly: Kun.Saga.dll

EF Core entity type configuration for [SagaStepQueueEntry](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md).
Table: `saga_step_queue`.
Indexes: `(processed_at, enqueued_at)` for executor polling.

```csharp
public sealed class SagaStepQueueEntryConfiguration : IEntityTypeConfiguration<SagaStepQueueEntry>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SagaStepQueueEntryConfiguration](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence-ef/sagastepqueueentryconfiguration.md)

#### Implements

[IEntityTypeConfiguration\<SagaStepQueueEntry>](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.ientitytypeconfiguration-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SagaStepQueueEntryConfiguration() \{#Kun_Saga_Persistence_Ef_SagaStepQueueEntryConfiguration__ctor}

```csharp
public SagaStepQueueEntryConfiguration()
```

## Methods

### Configure(EntityTypeBuilder\<SagaStepQueueEntry>) \{#Kun_Saga_Persistence_Ef_SagaStepQueueEntryConfiguration_Configure_Microsoft_EntityFrameworkCore_Metadata_Builders_EntityTypeBuilder_Kun_Saga_Persistence_SagaStepQueueEntry\_\_}

Configures the entity of type [SagaStepQueueEntry](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md).

```csharp
public void Configure(EntityTypeBuilder<SagaStepQueueEntry> builder)
```

#### Parameters

`builder` [EntityTypeBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.entitytypebuilder-1)\<[SagaStepQueueEntry](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md)>

The builder to be used to configure the entity type.
