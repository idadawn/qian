> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class SagaInstance

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Saga)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Persistence](/sdk/api/kun/kun-saga-persistence/index.md)\
Assembly: Kun.Saga.dll

Durable state for a running or completed saga instance.
ORM mapping lives in `SagaInstanceConfiguration` — no ORM attributes here.

```csharp
public sealed class SagaInstance
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SagaInstance](/sdk/api/kun/kun-saga-persistence/sagainstance.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SagaInstance(Guid, string, string?, string, DateTimeOffset, DateTimeOffset?, string?) \{#Kun_Saga_Persistence_SagaInstance__ctor_System_Guid_System_String_System_String_System_String_System_DateTimeOffset_System_Nullable_System_DateTimeOffset__System_String\_}

Creates a new [SagaInstance](/sdk/api/kun/kun-saga-persistence/sagainstance.md) in the [SagaState.Running](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Running) state at step 0.

```csharp
public SagaInstance(Guid id, string sagaName, string? tenantId, string contextPayload, DateTimeOffset startedAt, DateTimeOffset? timeoutAt, string? traceId)
```

#### Parameters

`id` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

Unique saga instance identifier.

`sagaName` [string](https://learn.microsoft.com/dotnet/api/system.string)

Stable saga type name.

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Tenant identifier; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for single-tenant.

`contextPayload` [string](https://learn.microsoft.com/dotnet/api/system.string)

JSON-serialized context.

`startedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp of saga start.

`timeoutAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

Optional UTC timestamp for step timeout.

`traceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

W3C traceparent for trace correlation.

## Properties

### CompletedAt \{#Kun_Saga_Persistence_SagaInstance_CompletedAt}

Gets the UTC timestamp when this saga reached a terminal state; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) while running.

```csharp
public DateTimeOffset? CompletedAt { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

### ContextPayload \{#Kun_Saga_Persistence_SagaInstance_ContextPayload}

Gets the JSON-serialized saga context payload used to resume execution.

```csharp
public string ContextPayload { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### CurrentStepIndex \{#Kun_Saga_Persistence_SagaInstance_CurrentStepIndex}

Gets the zero-based index of the step currently being executed or compensated.

```csharp
public int CurrentStepIndex { get; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### FailureReason \{#Kun_Saga_Persistence_SagaInstance_FailureReason}

Gets the reason string captured on failure; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) while running.

```csharp
public string? FailureReason { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Id \{#Kun_Saga_Persistence_SagaInstance_Id}

Gets the unique saga instance identifier (equals `ISagaContext.SagaId`).

```csharp
public Guid Id { get; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)

### SagaName \{#Kun_Saga_Persistence_SagaInstance_SagaName}

Gets the stable saga type name, e.g. `OrderCreationSaga`.

```csharp
public string SagaName { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### StartedAt \{#Kun_Saga_Persistence_SagaInstance_StartedAt}

Gets the UTC timestamp when this saga was started.

```csharp
public DateTimeOffset StartedAt { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### State \{#Kun_Saga_Persistence_SagaInstance_State}

Gets the current lifecycle state of this saga instance.

```csharp
public SagaState State { get; }
```

#### Property Value

[SagaState](/sdk/api/kun/kun-saga-persistence/sagastate.md)

### TenantId \{#Kun_Saga_Persistence_SagaInstance_TenantId}

Gets the tenant identifier; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for single-tenant deployments.

```csharp
public string? TenantId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TimeoutAt \{#Kun_Saga_Persistence_SagaInstance_TimeoutAt}

Gets the UTC timestamp at which the current step will time out and trigger compensation;
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when no timeout is configured.

```csharp
public DateTimeOffset? TimeoutAt { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

### TraceId \{#Kun_Saga_Persistence_SagaInstance_TraceId}

Gets the W3C `traceparent` captured at saga start; used for distributed trace correlation.

```csharp
public string? TraceId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### UpdatedAt \{#Kun_Saga_Persistence_SagaInstance_UpdatedAt}

Gets the UTC timestamp of the most recent state or step update.

```csharp
public DateTimeOffset UpdatedAt { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

## Methods

### Fail(string, DateTimeOffset) \{#Kun_Saga_Persistence_SagaInstance_Fail_System_String_System_DateTimeOffset\_}

Transitions the saga to [SagaState.Failed](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Failed) when an unrecoverable error
occurs (e.g. compensation itself fails). Manual intervention is required.

```csharp
public void Fail(string reason, DateTimeOffset updatedAt)
```

#### Parameters

`reason` [string](https://learn.microsoft.com/dotnet/api/system.string)

Description of the terminal failure.

`updatedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp of this transition.

### MarkCompensated(DateTimeOffset) \{#Kun_Saga_Persistence_SagaInstance_MarkCompensated_System_DateTimeOffset\_}

Transitions the saga to [SagaState.Compensated](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Compensated) once all compensation
steps have completed successfully.

```csharp
public void MarkCompensated(DateTimeOffset compensatedAt)
```

#### Parameters

`compensatedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp of compensation completion.

#### Exceptions

[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

Thrown if the saga is not in [SagaState.Compensating](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Compensating).

### MarkCompleted(DateTimeOffset) \{#Kun_Saga_Persistence_SagaInstance_MarkCompleted_System_DateTimeOffset\_}

Transitions the saga to [SagaState.Completed](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Completed) and records the terminal timestamp.

```csharp
public void MarkCompleted(DateTimeOffset completedAt)
```

#### Parameters

`completedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp of completion.

#### Exceptions

[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

Thrown if the saga is not in [SagaState.Running](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Running).

### MoveCompensationToStep(int, DateTimeOffset) \{#Kun_Saga_Persistence_SagaInstance_MoveCompensationToStep_System_Int32_System_DateTimeOffset\_}

Decrements [SagaInstance.CurrentStepIndex](/sdk/api/kun/kun-saga-persistence/sagainstance.md#Kun_Saga_Persistence_SagaInstance_CurrentStepIndex) during compensation. When the index reaches
-1 (all steps compensated), call [SagaInstance.MarkCompensated](/sdk/api/kun/kun-saga-persistence/sagainstance.md#Kun_Saga_Persistence_SagaInstance_MarkCompensated_System_DateTimeOffset_) instead.

```csharp
public void MoveCompensationToStep(int nextCompensationIndex, DateTimeOffset updatedAt)
```

#### Parameters

`nextCompensationIndex` [int](https://learn.microsoft.com/dotnet/api/system.int32)

The next step index to compensate (one less than current).

`updatedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp of this update.

#### Exceptions

[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

Thrown if the saga is not in [SagaState.Compensating](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Compensating).

### MoveToStep(int, DateTimeOffset, DateTimeOffset?, string) \{#Kun_Saga_Persistence_SagaInstance_MoveToStep_System_Int32_System_DateTimeOffset_System_Nullable_System_DateTimeOffset__System_String\_}

Updates [SagaInstance.ContextPayload](/sdk/api/kun/kun-saga-persistence/sagainstance.md#Kun_Saga_Persistence_SagaInstance_ContextPayload) and advances [SagaInstance.CurrentStepIndex](/sdk/api/kun/kun-saga-persistence/sagainstance.md#Kun_Saga_Persistence_SagaInstance_CurrentStepIndex)
to `stepIndex`. Also resets [SagaInstance.TimeoutAt](/sdk/api/kun/kun-saga-persistence/sagainstance.md#Kun_Saga_Persistence_SagaInstance_TimeoutAt) for the new step.

```csharp
public void MoveToStep(int stepIndex, DateTimeOffset updatedAt, DateTimeOffset? newTimeoutAt, string contextPayload)
```

#### Parameters

`stepIndex` [int](https://learn.microsoft.com/dotnet/api/system.int32)

The next step index to execute.

`updatedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp of this update.

`newTimeoutAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

New timeout deadline for the upcoming step.

`contextPayload` [string](https://learn.microsoft.com/dotnet/api/system.string)

Updated JSON-serialized context.

#### Exceptions

[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

Thrown if the saga is not in [SagaState.Running](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Running).

### StartCompensating(int, string, DateTimeOffset) \{#Kun_Saga_Persistence_SagaInstance_StartCompensating_System_Int32_System_String_System_DateTimeOffset\_}

Transitions the saga from [SagaState.Running](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Running) to [SagaState.Compensating](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Compensating),
recording the failure reason and setting [SagaInstance.CurrentStepIndex](/sdk/api/kun/kun-saga-persistence/sagainstance.md#Kun_Saga_Persistence_SagaInstance_CurrentStepIndex) to the step that failed.

```csharp
public void StartCompensating(int failedStepIndex, string failureReason, DateTimeOffset updatedAt)
```

#### Parameters

`failedStepIndex` [int](https://learn.microsoft.com/dotnet/api/system.int32)

The index of the step that triggered compensation.

`failureReason` [string](https://learn.microsoft.com/dotnet/api/system.string)

Human-readable description of the failure.

`updatedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp of this transition.

#### Exceptions

[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

Thrown if the saga is not in [SagaState.Running](/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Running).
