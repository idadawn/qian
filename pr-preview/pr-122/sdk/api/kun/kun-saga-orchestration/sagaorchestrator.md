> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class SagaOrchestrator

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Orchestration](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/index.md)\
Assembly: Kun.Saga.dll

Default implementation of [ISagaOrchestrator](/pr-preview/pr-122/sdk/api/kun/kun-saga-abstractions/isagaorchestrator.md).
Persists the initial [SagaInstance](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence/sagainstance.md) and enqueues the first step entry within
the caller's ambient unit-of-work transaction. All generic dispatch is delegated to
AOT-safe typed delegates captured in the [SagaRegistry](/pr-preview/pr-122/sdk/api/kun/kun-saga/sagaregistry.md) at startup.

```csharp
public sealed class SagaOrchestrator : ISagaOrchestrator
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SagaOrchestrator](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/sagaorchestrator.md)

#### Implements

[ISagaOrchestrator](/pr-preview/pr-122/sdk/api/kun/kun-saga-abstractions/isagaorchestrator.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SagaOrchestrator(ISagaInstanceStore, SagaRegistry, ISystemClock, IServiceProvider) \{#Kun_Saga_Orchestration_SagaOrchestrator__ctor_Kun_Saga_Persistence_ISagaInstanceStore_Kun_Saga_SagaRegistry_Kun_Core_Time_ISystemClock_System_IServiceProvider\_}

Creates a new [SagaOrchestrator](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/sagaorchestrator.md).

```csharp
public SagaOrchestrator(ISagaInstanceStore store, SagaRegistry registry, ISystemClock clock, IServiceProvider serviceProvider)
```

#### Parameters

`store` [ISagaInstanceStore](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence/isagainstancestore.md)

The saga instance persistence store.

`registry` [SagaRegistry](/pr-preview/pr-122/sdk/api/kun/kun-saga/sagaregistry.md)

The AOT-safe saga registry.

`clock` ISystemClock

The system clock.

`serviceProvider` [IServiceProvider](https://learn.microsoft.com/dotnet/api/system.iserviceprovider)

The scoped service provider used to resolve the saga instance so its step-0 timeout
can be read when computing the initial `SagaInstance.TimeoutAt`.

## Methods

### StartAsync\<TSaga, TContext>(TContext, CancellationToken) \{#Kun_Saga_Orchestration_SagaOrchestrator_StartAsync__2___1_System_Threading_CancellationToken\_}

Begins a new saga instance for `TSaga`, persists its initial state,
and enqueues the first step for background execution — all within the caller's current
`IUnitOfWork` transaction.

```csharp
public Task<Guid> StartAsync<TSaga, TContext>(TContext context, CancellationToken cancellationToken = default) where TSaga : ISaga<TContext> where TContext : ISagaContext
```

#### Parameters

`context` TContext

The initial context for the saga. [ISagaContext.SagaId](/pr-preview/pr-122/sdk/api/kun/kun-saga-abstractions/isagacontext.md#Kun_Saga_Abstractions_ISagaContext_SagaId) is used as the
instance primary key and must be unique across all running sagas.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Guid](https://learn.microsoft.com/dotnet/api/system.guid)>

The [`Guid`](https://learn.microsoft.com/dotnet/api/system.guid) saga instance ID (equals `context`.[ISagaContext.SagaId](/pr-preview/pr-122/sdk/api/kun/kun-saga-abstractions/isagacontext.md#Kun_Saga_Abstractions_ISagaContext_SagaId)).

#### Type Parameters

`TSaga`

The saga type to start.

`TContext`

The saga context type.
