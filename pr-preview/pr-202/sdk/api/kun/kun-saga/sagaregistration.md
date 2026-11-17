> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class SagaRegistration

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga](/pr-preview/pr-202/sdk/api/kun/kun-saga/index.md)\
Assembly: Kun.Saga.dll

Holds AOT-safe, closed-over delegates for a single saga type.
Generic types are captured at registration time so no runtime `MakeGenericType`
is needed during execution.

```csharp
public sealed class SagaRegistration
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SagaRegistration](/pr-preview/pr-202/sdk/api/kun/kun-saga/sagaregistration.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SagaRegistration(string, Func\<object, ISagaInstanceStore, ISystemClock, IServiceProvider, CancellationToken, Task\<Guid>>, Func\<int, StepDirection, string, IServiceProvider, CancellationToken, Task>, Func\<int, IServiceProvider, TimeSpan?>?, int) \{#Kun_Saga_SagaRegistration__ctor_System_String_System_Func_System_Object_Kun_Saga_Persistence_ISagaInstanceStore_Kun_Core_Time_ISystemClock_System_IServiceProvider_System_Threading_CancellationToken_System_Threading_Tasks_Task_System_Guid___System_Func_System_Int32_Kun_Saga_Persistence_StepDirection_System_String_System_IServiceProvider_System_Threading_CancellationToken_System_Threading_Tasks_Task__System_Func_System_Int32_System_IServiceProvider_System_Nullable_System_TimeSpan___System_Int32\_}

Creates a new [SagaRegistration](/pr-preview/pr-202/sdk/api/kun/kun-saga/sagaregistration.md).

```csharp
public SagaRegistration(string sagaName, Func<object, ISagaInstanceStore, ISystemClock, IServiceProvider, CancellationToken, Task<Guid>> startAsync, Func<int, StepDirection, string, IServiceProvider, CancellationToken, Task> executeStepAsync, Func<int, IServiceProvider, TimeSpan?>? getStepTimeout = null, int stepCount = 0)
```

#### Parameters

`sagaName` [string](https://learn.microsoft.com/dotnet/api/system.string)

The stable saga name.

`startAsync` [Func](https://learn.microsoft.com/dotnet/api/system.func-6)\<[object](https://learn.microsoft.com/dotnet/api/system.object), [ISagaInstanceStore](/pr-preview/pr-202/sdk/api/kun/kun-saga-persistence/isagainstancestore.md), ISystemClock, [IServiceProvider](https://learn.microsoft.com/dotnet/api/system.iserviceprovider), [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Guid](https://learn.microsoft.com/dotnet/api/system.guid)>>

The AOT-safe start delegate.

`executeStepAsync` [Func](https://learn.microsoft.com/dotnet/api/system.func-6)\<[int](https://learn.microsoft.com/dotnet/api/system.int32), [StepDirection](/pr-preview/pr-202/sdk/api/kun/kun-saga-persistence/stepdirection.md), [string](https://learn.microsoft.com/dotnet/api/system.string), [IServiceProvider](https://learn.microsoft.com/dotnet/api/system.iserviceprovider), [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)>

The AOT-safe step execution delegate.

`getStepTimeout` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<[int](https://learn.microsoft.com/dotnet/api/system.int32), [IServiceProvider](https://learn.microsoft.com/dotnet/api/system.iserviceprovider), [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?>?

Returns the timeout for a given step index. May return [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when
no per-step timeout is configured. Defaults to a delegate that always returns [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).

`stepCount` [int](https://learn.microsoft.com/dotnet/api/system.int32)

The total number of steps in this saga. Defaults to `0` when not provided
(e.g. in unit tests that construct registrations directly without a full saga class).

## Properties

### ExecuteStepAsync \{#Kun_Saga_SagaRegistration_ExecuteStepAsync}

Executes or compensates a single saga step using the provided context payload JSON.
Captures the concrete `TSaga`/`TContext` types at registration time.
Parameters: (stepIndex, direction, contextPayload, serviceProvider, cancellationToken) → Task

```csharp
public Func<int, StepDirection, string, IServiceProvider, CancellationToken, Task> ExecuteStepAsync { get; }
```

#### Property Value

[Func](https://learn.microsoft.com/dotnet/api/system.func-6)\<[int](https://learn.microsoft.com/dotnet/api/system.int32), [StepDirection](/pr-preview/pr-202/sdk/api/kun/kun-saga-persistence/stepdirection.md), [string](https://learn.microsoft.com/dotnet/api/system.string), [IServiceProvider](https://learn.microsoft.com/dotnet/api/system.iserviceprovider), [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)>

### GetStepTimeout \{#Kun_Saga_SagaRegistration_GetStepTimeout}

Returns the configured [ISagaStep\<TContext>.Timeout](/pr-preview/pr-202/sdk/api/kun/kun-saga-abstractions/isagastep-1.md#Kun_Saga_Abstractions_ISagaStep_1_Timeout) for
the given step index, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the step has no timeout.
Used by `SagaStepExecutor` to set per-step `TimeoutAt` when advancing.
Parameters: (stepIndex, serviceProvider) → TimeSpan?

```csharp
public Func<int, IServiceProvider, TimeSpan?> GetStepTimeout { get; }
```

#### Property Value

[Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<[int](https://learn.microsoft.com/dotnet/api/system.int32), [IServiceProvider](https://learn.microsoft.com/dotnet/api/system.iserviceprovider), [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?>

### SagaName \{#Kun_Saga_SagaRegistration_SagaName}

Gets the stable saga name (e.g. `OrderCreationSaga`).

```csharp
public string SagaName { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### StartAsync \{#Kun_Saga_SagaRegistration_StartAsync}

Starts a new saga instance. Captures `TSaga` and `TContext` at registration
time, so no reflection is required at call time.
Parameters: (contextJson, store, clock, serviceProvider, cancellationToken) → sagaId
The context is passed as the typed object (boxed as [object](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types)).
The [`IServiceProvider`](https://learn.microsoft.com/dotnet/api/system.iserviceprovider) is used to resolve the saga's step-0 timeout via
[SagaRegistration.GetStepTimeout](/pr-preview/pr-202/sdk/api/kun/kun-saga/sagaregistration.md#Kun_Saga_SagaRegistration_GetStepTimeout) so that `SagaInstance.TimeoutAt` reflects the actual
per-step timeout rather than an arbitrary default.

```csharp
public Func<object, ISagaInstanceStore, ISystemClock, IServiceProvider, CancellationToken, Task<Guid>> StartAsync { get; }
```

#### Property Value

[Func](https://learn.microsoft.com/dotnet/api/system.func-6)\<[object](https://learn.microsoft.com/dotnet/api/system.object), [ISagaInstanceStore](/pr-preview/pr-202/sdk/api/kun/kun-saga-persistence/isagainstancestore.md), ISystemClock, [IServiceProvider](https://learn.microsoft.com/dotnet/api/system.iserviceprovider), [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Guid](https://learn.microsoft.com/dotnet/api/system.guid)>>

### StepCount \{#Kun_Saga_SagaRegistration_StepCount}

Gets the total number of steps in this saga. Used by `SagaStepExecutor`
to determine when forward execution is complete.

```csharp
public int StepCount { get; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)
