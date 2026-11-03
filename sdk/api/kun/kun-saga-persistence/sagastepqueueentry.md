> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class SagaStepQueueEntry

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Saga)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Persistence](/sdk/api/kun/kun-saga-persistence/index.md)\
Assembly: Kun.Saga.dll

A queued work item representing a single step execution or compensation for a saga.
The background `SagaStepExecutor` polls this table and processes entries in order.
ORM mapping lives in `SagaStepQueueEntryConfiguration` — no ORM attributes here.

```csharp
public sealed class SagaStepQueueEntry
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SagaStepQueueEntry](/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SagaStepQueueEntry(Guid, Guid, int, StepDirection, DateTimeOffset) \{#Kun_Saga_Persistence_SagaStepQueueEntry__ctor_System_Guid_System_Guid_System_Int32_Kun_Saga_Persistence_StepDirection_System_DateTimeOffset\_}

Creates a new [SagaStepQueueEntry](/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md) with zero attempts and no processed timestamp.

```csharp
public SagaStepQueueEntry(Guid id, Guid sagaId, int stepIndex, StepDirection direction, DateTimeOffset enqueuedAt)
```

#### Parameters

`id` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

Unique identifier for this entry.

`sagaId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

The owning saga instance identifier.

`stepIndex` [int](https://learn.microsoft.com/dotnet/api/system.int32)

Zero-based step index.

`direction` [StepDirection](/sdk/api/kun/kun-saga-persistence/stepdirection.md)

Forward execution or compensation.

`enqueuedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp of enqueue.

## Properties

### AttemptCount \{#Kun_Saga_Persistence_SagaStepQueueEntry_AttemptCount}

Gets the number of execution attempts made for this entry.

```csharp
public int AttemptCount { get; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### Direction \{#Kun_Saga_Persistence_SagaStepQueueEntry_Direction}

Gets the direction: forward [StepDirection.Execute](/sdk/api/kun/kun-saga-persistence/stepdirection.md#Kun_Saga_Persistence_StepDirection_Execute) or reverse [StepDirection.Compensate](/sdk/api/kun/kun-saga-persistence/stepdirection.md#Kun_Saga_Persistence_StepDirection_Compensate).

```csharp
public StepDirection Direction { get; }
```

#### Property Value

[StepDirection](/sdk/api/kun/kun-saga-persistence/stepdirection.md)

### EnqueuedAt \{#Kun_Saga_Persistence_SagaStepQueueEntry_EnqueuedAt}

Gets the UTC timestamp when this entry was added to the queue.

```csharp
public DateTimeOffset EnqueuedAt { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Id \{#Kun_Saga_Persistence_SagaStepQueueEntry_Id}

Gets the unique identifier for this queue entry.

```csharp
public Guid Id { get; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)

### NextAttemptAt \{#Kun_Saga_Persistence_SagaStepQueueEntry_NextAttemptAt}

Earliest UTC time for the next attempt; null means immediately eligible.

```csharp
public DateTimeOffset? NextAttemptAt { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

### ProcessedAt \{#Kun_Saga_Persistence_SagaStepQueueEntry_ProcessedAt}

Gets the UTC timestamp when this entry was successfully processed; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) while pending.

```csharp
public DateTimeOffset? ProcessedAt { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

### SagaId \{#Kun_Saga_Persistence_SagaStepQueueEntry_SagaId}

Gets the saga instance this entry belongs to.

```csharp
public Guid SagaId { get; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)

### StepIndex \{#Kun_Saga_Persistence_SagaStepQueueEntry_StepIndex}

Gets the zero-based index of the step to execute or compensate.

```csharp
public int StepIndex { get; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

## Methods

### IncrementAttempt() \{#Kun_Saga_Persistence_SagaStepQueueEntry_IncrementAttempt}

Increments the attempt counter by one. Called before each execution attempt.

```csharp
public void IncrementAttempt()
```

### MarkProcessed(DateTimeOffset) \{#Kun_Saga_Persistence_SagaStepQueueEntry_MarkProcessed_System_DateTimeOffset\_}

Records that this entry has been successfully processed at `processedAt`.

```csharp
public void MarkProcessed(DateTimeOffset processedAt)
```

#### Parameters

`processedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp of successful processing.
