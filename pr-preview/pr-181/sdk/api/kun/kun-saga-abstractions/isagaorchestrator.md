> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface ISagaOrchestrator

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-saga-abstractions/index.md)\
Assembly: Kun.Saga.dll

Starts new saga instances and persists the initial state atomically within the caller's
ambient unit-of-work transaction. The background `SagaStepExecutor` then drives
step execution and compensation asynchronously.

```csharp
public interface ISagaOrchestrator
```

## Methods

### StartAsync\<TSaga, TContext>(TContext, CancellationToken) \{#Kun_Saga_Abstractions_ISagaOrchestrator_StartAsync__2___1_System_Threading_CancellationToken\_}

Begins a new saga instance for `TSaga`, persists its initial state,
and enqueues the first step for background execution — all within the caller's current
`IUnitOfWork` transaction.

```csharp
Task<Guid> StartAsync<TSaga, TContext>(TContext context, CancellationToken cancellationToken = default) where TSaga : ISaga<TContext> where TContext : ISagaContext
```

#### Parameters

`context` TContext

The initial context for the saga. [ISagaContext.SagaId](/pr-preview/pr-181/sdk/api/kun/kun-saga-abstractions/isagacontext.md#Kun_Saga_Abstractions_ISagaContext_SagaId) is used as the
instance primary key and must be unique across all running sagas.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Guid](https://learn.microsoft.com/dotnet/api/system.guid)>

The [`Guid`](https://learn.microsoft.com/dotnet/api/system.guid) saga instance ID (equals `context`.[ISagaContext.SagaId](/pr-preview/pr-181/sdk/api/kun/kun-saga-abstractions/isagacontext.md#Kun_Saga_Abstractions_ISagaContext_SagaId)).

#### Type Parameters

`TSaga`

The saga type to start.

`TContext`

The saga context type.
