> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class OutboxMessage

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Outbox](/sdk/api/kun/kun-eventbus-outbox/index.md)\
Assembly: Kun.EventBus.dll

Persisted Outbox message row. Created when an integration event is enqueued inside a
`SaveChangesAsync` transaction; published asynchronously by the background publisher.

```csharp
public sealed class OutboxMessage
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OutboxMessage](/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

ORM mapping (column names, index, table) lives in
`Kun.EntityFrameworkCore/OutboxMessageConfiguration.cs` — no ORM attributes here.

## Constructors

### OutboxMessage(Guid, string, string, string?, string?, string?, DateTimeOffset) \{#Kun_EventBus_Outbox_OutboxMessage__ctor_System_Guid_System_String_System_String_System_String_System_String_System_String_System_DateTimeOffset\_}

Initializes a new [OutboxMessage](/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) with all required fields and sets
[OutboxMessage.Status](/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md#Kun_EventBus_Outbox_OutboxMessage_Status) to [OutboxStatus.Pending](/sdk/api/kun/kun-eventbus-outbox/outboxstatus.md#Kun_EventBus_Outbox_OutboxStatus_Pending).

```csharp
public OutboxMessage(Guid id, string eventTypeId, string payload, string? tenantId, string? traceParent, string? traceState, DateTimeOffset occurredAt)
```

#### Parameters

`id` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

Deterministic GUID for this message row.

`eventTypeId` [string](https://learn.microsoft.com/dotnet/api/system.string)

Stable event type identifier (e.g. `order.placed.v1`).

`payload` [string](https://learn.microsoft.com/dotnet/api/system.string)

JSON-serialized event payload.

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Tenant scope; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for single-tenant.

`traceParent` [string](https://learn.microsoft.com/dotnet/api/system.string)?

W3C traceparent; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when no activity is active.

`traceState` [string](https://learn.microsoft.com/dotnet/api/system.string)?

W3C tracestate; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when absent.

`occurredAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp when the producing domain event fired.

#### Exceptions

[ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

Thrown when `eventTypeId` or `payload` is [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).

## Properties

### AttemptCount \{#Kun_EventBus_Outbox_OutboxMessage_AttemptCount}

Gets the number of publish attempts made so far.

```csharp
public int AttemptCount { get; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### EventTypeId \{#Kun_EventBus_Outbox_OutboxMessage_EventTypeId}

Gets the stable event type identifier, e.g. `order.placed.v1`.
Never derived from [`AssemblyQualifiedName`](https://learn.microsoft.com/dotnet/api/system.type.assemblyqualifiedname).

```csharp
public string EventTypeId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Id \{#Kun_EventBus_Outbox_OutboxMessage_Id}

Gets the primary key (deterministic GUID supplied by the caller).

```csharp
public Guid Id { get; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)

### LastError \{#Kun_EventBus_Outbox_OutboxMessage_LastError}

Gets the error message from the last failed publish attempt; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) if none.

```csharp
public string? LastError { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### NextAttemptAt \{#Kun_EventBus_Outbox_OutboxMessage_NextAttemptAt}

下一次允许重试的时间；null 表示首次发布或无需等待。

```csharp
public DateTimeOffset? NextAttemptAt { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

### OccurredAt \{#Kun_EventBus_Outbox_OutboxMessage_OccurredAt}

Gets the UTC timestamp at which the producing domain event fired.

```csharp
public DateTimeOffset OccurredAt { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Payload \{#Kun_EventBus_Outbox_OutboxMessage_Payload}

Gets the JSON-serialized event payload.

```csharp
public string Payload { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### PublishedAt \{#Kun_EventBus_Outbox_OutboxMessage_PublishedAt}

Gets the UTC timestamp at which this message was successfully published; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) until published.

```csharp
public DateTimeOffset? PublishedAt { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

### Status \{#Kun_EventBus_Outbox_OutboxMessage_Status}

Gets the current lifecycle state of this message.

```csharp
public OutboxStatus Status { get; }
```

#### Property Value

[OutboxStatus](/sdk/api/kun/kun-eventbus-outbox/outboxstatus.md)

### TenantId \{#Kun_EventBus_Outbox_OutboxMessage_TenantId}

Gets the tenant identifier for multi-tenant routing; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for single-tenant deployments.

```csharp
public string? TenantId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TraceParent \{#Kun_EventBus_Outbox_OutboxMessage_TraceParent}

Gets the W3C `traceparent` header captured at enqueue time; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when no activity was active.

```csharp
public string? TraceParent { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TraceState \{#Kun_EventBus_Outbox_OutboxMessage_TraceState}

Gets the W3C `tracestate` header captured at enqueue time; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when absent.

```csharp
public string? TraceState { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

## Methods

### IncrementAttempt() \{#Kun_EventBus_Outbox_OutboxMessage_IncrementAttempt}

Increments the publish attempt counter by one.

```csharp
public void IncrementAttempt()
```

### MarkFailed(string) \{#Kun_EventBus_Outbox_OutboxMessage_MarkFailed_System_String\_}

Transitions the message to [OutboxStatus.Failed](/sdk/api/kun/kun-eventbus-outbox/outboxstatus.md#Kun_EventBus_Outbox_OutboxStatus_Failed) and records the error.

```csharp
public void MarkFailed(string error)
```

#### Parameters

`error` [string](https://learn.microsoft.com/dotnet/api/system.string)

The error message describing why publishing failed permanently.

#### Exceptions

[ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

Thrown when `error` is [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).

### MarkPublished(DateTimeOffset) \{#Kun_EventBus_Outbox_OutboxMessage_MarkPublished_System_DateTimeOffset\_}

Transitions the message to [OutboxStatus.Published](/sdk/api/kun/kun-eventbus-outbox/outboxstatus.md#Kun_EventBus_Outbox_OutboxStatus_Published) and records the publish timestamp.

```csharp
public void MarkPublished(DateTimeOffset publishedAt)
```

#### Parameters

`publishedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

The UTC time at which the message was successfully published.

### ScheduleRetry(DateTimeOffset, string) \{#Kun_EventBus_Outbox_OutboxMessage_ScheduleRetry_System_DateTimeOffset_System_String\_}

记录失败原因与持久化重试时间，不占用工作线程等待。

```csharp
public void ScheduleRetry(DateTimeOffset nextAttemptAt, string error)
```

#### Parameters

`nextAttemptAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

`error` [string](https://learn.microsoft.com/dotnet/api/system.string)
