> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface ISagaStep\<TContext>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Abstractions](/pr-preview/pr-202/sdk/api/kun/kun-saga-abstractions/index.md)\
Assembly: Kun.Saga.dll

A single compensatable step in a saga. Each step must implement both a forward
[ISagaStep\<TContext>.ExecuteAsync](/pr-preview/pr-202/sdk/api/kun/kun-saga-abstractions/isagastep-1.md#Kun_Saga_Abstractions_ISagaStep_1_ExecuteAsync__0_System_Threading_CancellationToken_) and a reverse
[ISagaStep\<TContext>.CompensateAsync](/pr-preview/pr-202/sdk/api/kun/kun-saga-abstractions/isagastep-1.md#Kun_Saga_Abstractions_ISagaStep_1_CompensateAsync__0_System_Threading_CancellationToken_) path, both of which must be idempotent.

```csharp
public interface ISagaStep<TContext> where TContext : ISagaContext
```

#### Type Parameters

`TContext`

The saga context type that carries state between steps.

## Properties

### Name \{#Kun_Saga_Abstractions_ISagaStep_1_Name}

Gets the human-readable name of this step, used for logging and diagnostics.

```csharp
string Name { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Timeout \{#Kun_Saga_Abstractions_ISagaStep_1_Timeout}

Gets the per-step timeout override. When [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null), the saga's
[ISaga\<TContext>.DefaultStepTimeout](/pr-preview/pr-202/sdk/api/kun/kun-saga-abstractions/isaga-1.md#Kun_Saga_Abstractions_ISaga_1_DefaultStepTimeout) is used.

```csharp
TimeSpan? Timeout { get; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

## Methods

### CompensateAsync(TContext, CancellationToken) \{#Kun_Saga_Abstractions_ISagaStep_1_CompensateAsync__0_System_Threading_CancellationToken\_}

Reverses the effects of a previously executed step.
Must be idempotent — may be called even if [ISagaStep\<TContext>.ExecuteAsync](/pr-preview/pr-202/sdk/api/kun/kun-saga-abstractions/isagastep-1.md#Kun_Saga_Abstractions_ISagaStep_1_ExecuteAsync__0_System_Threading_CancellationToken_) never succeeded.

```csharp
ValueTask CompensateAsync(TContext context, CancellationToken cancellationToken)
```

#### Parameters

`context` TContext

The saga context carrying state for this execution.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

### ExecuteAsync(TContext, CancellationToken) \{#Kun_Saga_Abstractions_ISagaStep_1_ExecuteAsync__0_System_Threading_CancellationToken\_}

Executes the forward action of this step.
Must be idempotent — may be called more than once due to at-least-once delivery.

```csharp
ValueTask ExecuteAsync(TContext context, CancellationToken cancellationToken)
```

#### Parameters

`context` TContext

The saga context carrying state for this execution.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Token to observe for cancellation.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
