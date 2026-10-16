> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class EfSagaInstanceStore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Persistence.Ef](/sdk/api/kun/kun-saga-persistence-ef/index.md)\
Assembly: Kun.Saga.dll

EF Core-backed implementation of [ISagaInstanceStore](/sdk/api/kun/kun-saga-persistence/isagainstancestore.md).
Operates against `saga_instances` and `saga_step_queue` tables
via `DbContext.Set<T>`.

```csharp
public sealed class EfSagaInstanceStore : ISagaInstanceStore
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[EfSagaInstanceStore](/sdk/api/kun/kun-saga-persistence-ef/efsagainstancestore.md)

#### Implements

[ISagaInstanceStore](/sdk/api/kun/kun-saga-persistence/isagainstancestore.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### EfSagaInstanceStore(KunDbContext) \{#Kun_Saga_Persistence_Ef_EfSagaInstanceStore__ctor_Kun_EntityFrameworkCore_KunDbContext\_}

Creates a new [EfSagaInstanceStore](/sdk/api/kun/kun-saga-persistence-ef/efsagainstancestore.md).

```csharp
public EfSagaInstanceStore(KunDbContext dbContext)
```

#### Parameters

`dbContext` KunDbContext

The [KunDbContext](/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md) to use for persistence.

## Methods

### AddAsync(SagaInstance, CancellationToken) \{#Kun_Saga_Persistence_Ef_EfSagaInstanceStore_AddAsync_Kun_Saga_Persistence_SagaInstance_System_Threading_CancellationToken\_}

Persists a newly created saga instance.

```csharp
public Task AddAsync(SagaInstance instance, CancellationToken ct = default)
```

#### Parameters

`instance` [SagaInstance](/sdk/api/kun/kun-saga-persistence/sagainstance.md)

The saga instance to add.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### EnqueueStepAsync(SagaStepQueueEntry, CancellationToken) \{#Kun_Saga_Persistence_Ef_EfSagaInstanceStore_EnqueueStepAsync_Kun_Saga_Persistence_SagaStepQueueEntry_System_Threading_CancellationToken\_}

Persists a newly enqueued step entry.

```csharp
public Task EnqueueStepAsync(SagaStepQueueEntry entry, CancellationToken ct = default)
```

#### Parameters

`entry` [SagaStepQueueEntry](/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md)

The step queue entry to add.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### FetchPendingStepsAsync(int, CancellationToken) \{#Kun_Saga_Persistence_Ef_EfSagaInstanceStore_FetchPendingStepsAsync_System_Int32_System_Threading_CancellationToken\_}

Returns pending (unprocessed) step queue entries ordered by `EnqueuedAt` ascending,
up to `batchSize` results.

```csharp
public Task<IReadOnlyList<SagaStepQueueEntry>> FetchPendingStepsAsync(int batchSize, CancellationToken ct = default)
```

#### Parameters

`batchSize` [int](https://learn.microsoft.com/dotnet/api/system.int32)

Maximum number of entries to return.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[SagaStepQueueEntry](/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md)>>

### FindAsync(Guid, CancellationToken) \{#Kun_Saga_Persistence_Ef_EfSagaInstanceStore_FindAsync_System_Guid_System_Threading_CancellationToken\_}

Finds a saga instance by its unique identifier.
Returns [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) if no matching instance exists.

```csharp
public Task<SagaInstance?> FindAsync(Guid sagaId, CancellationToken ct = default)
```

#### Parameters

`sagaId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

The saga instance identifier.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[SagaInstance](/sdk/api/kun/kun-saga-persistence/sagainstance.md)?>

### FindTimedOutAsync(DateTimeOffset, int, CancellationToken) \{#Kun_Saga_Persistence_Ef_EfSagaInstanceStore_FindTimedOutAsync_System_DateTimeOffset_System_Int32_System_Threading_CancellationToken\_}

Returns saga instances whose `TimeoutAt` is in the past and whose state is
[SagaState.Running](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Running), up to `batchSize` results.
Used by `SagaTimeoutDetector` to trigger compensation.

```csharp
public Task<IReadOnlyList<SagaInstance>> FindTimedOutAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default)
```

#### Parameters

`now` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

The current UTC time to compare against `TimeoutAt`.

`batchSize` [int](https://learn.microsoft.com/dotnet/api/system.int32)

Maximum number of timed-out instances to return.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[SagaInstance](/sdk/api/kun/kun-saga-persistence/sagainstance.md)>>

### UpdateAsync(SagaInstance, CancellationToken) \{#Kun_Saga_Persistence_Ef_EfSagaInstanceStore_UpdateAsync_Kun_Saga_Persistence_SagaInstance_System_Threading_CancellationToken\_}

Persists state changes to an existing saga instance.

```csharp
public Task UpdateAsync(SagaInstance instance, CancellationToken ct = default)
```

#### Parameters

`instance` [SagaInstance](/sdk/api/kun/kun-saga-persistence/sagainstance.md)

The saga instance with updated state.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### UpdateStepAsync(SagaStepQueueEntry, CancellationToken) \{#Kun_Saga_Persistence_Ef_EfSagaInstanceStore_UpdateStepAsync_Kun_Saga_Persistence_SagaStepQueueEntry_System_Threading_CancellationToken\_}

Persists state changes to an existing step queue entry (e.g. after processing).

```csharp
public Task UpdateStepAsync(SagaStepQueueEntry entry, CancellationToken ct = default)
```

#### Parameters

`entry` [SagaStepQueueEntry](/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md)

The step queue entry with updated state.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
