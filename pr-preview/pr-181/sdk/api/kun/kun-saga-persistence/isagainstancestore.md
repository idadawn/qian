> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface ISagaInstanceStore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Persistence](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence/index.md)\
Assembly: Kun.Saga.dll

Persistence contract for saga instance state and step queue entries.
Implementations are responsible for durable storage and querying of saga lifecycle data.

```csharp
public interface ISagaInstanceStore
```

## Methods

### AddAsync(SagaInstance, CancellationToken) \{#Kun_Saga_Persistence_ISagaInstanceStore_AddAsync_Kun_Saga_Persistence_SagaInstance_System_Threading_CancellationToken\_}

Persists a newly created saga instance.

```csharp
Task AddAsync(SagaInstance instance, CancellationToken ct = default)
```

#### Parameters

`instance` [SagaInstance](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence/sagainstance.md)

The saga instance to add.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### EnqueueStepAsync(SagaStepQueueEntry, CancellationToken) \{#Kun_Saga_Persistence_ISagaInstanceStore_EnqueueStepAsync_Kun_Saga_Persistence_SagaStepQueueEntry_System_Threading_CancellationToken\_}

Persists a newly enqueued step entry.

```csharp
Task EnqueueStepAsync(SagaStepQueueEntry entry, CancellationToken ct = default)
```

#### Parameters

`entry` [SagaStepQueueEntry](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md)

The step queue entry to add.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### FetchPendingStepsAsync(int, CancellationToken) \{#Kun_Saga_Persistence_ISagaInstanceStore_FetchPendingStepsAsync_System_Int32_System_Threading_CancellationToken\_}

Returns pending (unprocessed) step queue entries ordered by `EnqueuedAt` ascending,
up to `batchSize` results.

```csharp
Task<IReadOnlyList<SagaStepQueueEntry>> FetchPendingStepsAsync(int batchSize, CancellationToken ct = default)
```

#### Parameters

`batchSize` [int](https://learn.microsoft.com/dotnet/api/system.int32)

Maximum number of entries to return.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[SagaStepQueueEntry](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md)>>

### FindAsync(Guid, CancellationToken) \{#Kun_Saga_Persistence_ISagaInstanceStore_FindAsync_System_Guid_System_Threading_CancellationToken\_}

Finds a saga instance by its unique identifier.
Returns [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) if no matching instance exists.

```csharp
Task<SagaInstance?> FindAsync(Guid sagaId, CancellationToken ct = default)
```

#### Parameters

`sagaId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

The saga instance identifier.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[SagaInstance](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence/sagainstance.md)?>

### FindTimedOutAsync(DateTimeOffset, int, CancellationToken) \{#Kun_Saga_Persistence_ISagaInstanceStore_FindTimedOutAsync_System_DateTimeOffset_System_Int32_System_Threading_CancellationToken\_}

Returns saga instances whose `TimeoutAt` is in the past and whose state is
[SagaState.Running](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Running), up to `batchSize` results.
Used by `SagaTimeoutDetector` to trigger compensation.

```csharp
Task<IReadOnlyList<SagaInstance>> FindTimedOutAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default)
```

#### Parameters

`now` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

The current UTC time to compare against `TimeoutAt`.

`batchSize` [int](https://learn.microsoft.com/dotnet/api/system.int32)

Maximum number of timed-out instances to return.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[SagaInstance](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence/sagainstance.md)>>

### UpdateAsync(SagaInstance, CancellationToken) \{#Kun_Saga_Persistence_ISagaInstanceStore_UpdateAsync_Kun_Saga_Persistence_SagaInstance_System_Threading_CancellationToken\_}

Persists state changes to an existing saga instance.

```csharp
Task UpdateAsync(SagaInstance instance, CancellationToken ct = default)
```

#### Parameters

`instance` [SagaInstance](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence/sagainstance.md)

The saga instance with updated state.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### UpdateStepAsync(SagaStepQueueEntry, CancellationToken) \{#Kun_Saga_Persistence_ISagaInstanceStore_UpdateStepAsync_Kun_Saga_Persistence_SagaStepQueueEntry_System_Threading_CancellationToken\_}

Persists state changes to an existing step queue entry (e.g. after processing).

```csharp
Task UpdateStepAsync(SagaStepQueueEntry entry, CancellationToken ct = default)
```

#### Parameters

`entry` [SagaStepQueueEntry](/pr-preview/pr-181/sdk/api/kun/kun-saga-persistence/sagastepqueueentry.md)

The step queue entry with updated state.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
