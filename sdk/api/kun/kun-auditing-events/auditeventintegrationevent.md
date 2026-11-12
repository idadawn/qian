> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class AuditEventIntegrationEvent

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Auditing` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Auditing`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Auditing)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Auditing.Events](/sdk/api/kun/kun-auditing-events/index.md)\
Assembly: Kun.Auditing.dll

Integration event emitted by [AuditingSaveChangesInterceptor](/sdk/api/kun/kun-auditing/auditingsavechangesinterceptor.md) for every
tracked mutation of an entity registered via `services.AddAuditable<TEntity>()`.
Published through the Outbox with stable id `kun.audit.event.v1`.

```csharp
public sealed record AuditEventIntegrationEvent : IEquatable<AuditEventIntegrationEvent>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md)

#### Implements

[IEquatable\<AuditEventIntegrationEvent>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### AuditEventIntegrationEvent(Guid?, Guid?, string, string?, string, string?, string?, string, string?, JsonDocument?, string?, string?, DateTimeOffset) \{#Kun_Auditing_Events_AuditEventIntegrationEvent__ctor_System_Nullable_System_Guid__System_Nullable_System_Guid__System_String_System_String_System_String_System_String_System_String_System_String_System_String_System_Text_Json_JsonDocument_System_String_System_String_System_DateTimeOffset\_}

Integration event emitted by [AuditingSaveChangesInterceptor](/sdk/api/kun/kun-auditing/auditingsavechangesinterceptor.md) for every
tracked mutation of an entity registered via `services.AddAuditable<TEntity>()`.
Published through the Outbox with stable id `kun.audit.event.v1`.

```csharp
public AuditEventIntegrationEvent(Guid? TenantId, Guid? UserId, string ActorType, string? ActorId, string Action, string? ResourceType, string? ResourceId, string Outcome, string? TraceId, JsonDocument? Metadata, string? IpAddress, string? UserAgent, DateTimeOffset CreatedAt)
```

#### Parameters

`TenantId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)?

Tenant scope; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for non-multi-tenant hosts.

`UserId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)?

User identifier when the acting principal is a human user; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)
for system / agent actors. When both `UserId` and `ActorId`
are populated for a user-type actor they should carry the same value.

`ActorType` [string](https://learn.microsoft.com/dotnet/api/system.string)

Kind of actor: `"user"`, `"agent"`, or `"system"`.

`ActorId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Subject identifier of the actor (claim `sub`, service id, …).

`Action` [string](https://learn.microsoft.com/dotnet/api/system.string)

Domain-level action identifier, e.g. `order.placed` or the EF
`EntityState` (`Added` / `Modified` / `Deleted`) when no richer
action is in scope.

`ResourceType` [string](https://learn.microsoft.com/dotnet/api/system.string)?

CLR type name of the audited entity (e.g. `Order`).

`ResourceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Primary key of the audited entity as a string.

`Outcome` [string](https://learn.microsoft.com/dotnet/api/system.string)

One of `"success"`, `"denied"`, `"error"`.

`TraceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

W3C trace id captured from `Activity.Current`.

`Metadata` [JsonDocument](https://learn.microsoft.com/dotnet/api/system.text.json.jsondocument)?

Optional caller-supplied metadata. Persisted as raw JSON on the outbox payload. Kept
as [`JsonDocument`](https://learn.microsoft.com/dotnet/api/system.text.json.jsondocument) so the shape is opaque to the framework — consumers
decide how to parse it.

`IpAddress` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Remote IP of the originating request, when available.

`UserAgent` [string](https://learn.microsoft.com/dotnet/api/system.string)?

User-Agent header of the originating request, when available.

`CreatedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp at which the audit event was produced.

## Properties

### Action \{#Kun_Auditing_Events_AuditEventIntegrationEvent_Action}

Domain-level action identifier, e.g. `order.placed` or the EF
`EntityState` (`Added` / `Modified` / `Deleted`) when no richer
action is in scope.

```csharp
public string Action { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ActorId \{#Kun_Auditing_Events_AuditEventIntegrationEvent_ActorId}

Subject identifier of the actor (claim `sub`, service id, …).

```csharp
public string? ActorId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ActorType \{#Kun_Auditing_Events_AuditEventIntegrationEvent_ActorType}

Kind of actor: `"user"`, `"agent"`, or `"system"`.

```csharp
public string ActorType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### CreatedAt \{#Kun_Auditing_Events_AuditEventIntegrationEvent_CreatedAt}

UTC timestamp at which the audit event was produced.

```csharp
public DateTimeOffset CreatedAt { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### IpAddress \{#Kun_Auditing_Events_AuditEventIntegrationEvent_IpAddress}

Remote IP of the originating request, when available.

```csharp
public string? IpAddress { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Metadata \{#Kun_Auditing_Events_AuditEventIntegrationEvent_Metadata}

Optional caller-supplied metadata. Persisted as raw JSON on the outbox payload. Kept
as [`JsonDocument`](https://learn.microsoft.com/dotnet/api/system.text.json.jsondocument) so the shape is opaque to the framework — consumers
decide how to parse it.

```csharp
public JsonDocument? Metadata { get; init; }
```

#### Property Value

[JsonDocument](https://learn.microsoft.com/dotnet/api/system.text.json.jsondocument)?

### Outcome \{#Kun_Auditing_Events_AuditEventIntegrationEvent_Outcome}

One of `"success"`, `"denied"`, `"error"`.

```csharp
public string Outcome { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ResourceId \{#Kun_Auditing_Events_AuditEventIntegrationEvent_ResourceId}

Primary key of the audited entity as a string.

```csharp
public string? ResourceId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ResourceType \{#Kun_Auditing_Events_AuditEventIntegrationEvent_ResourceType}

CLR type name of the audited entity (e.g. `Order`).

```csharp
public string? ResourceType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TenantId \{#Kun_Auditing_Events_AuditEventIntegrationEvent_TenantId}

Tenant scope; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for non-multi-tenant hosts.

```csharp
public Guid? TenantId { get; init; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)?

### TraceId \{#Kun_Auditing_Events_AuditEventIntegrationEvent_TraceId}

W3C trace id captured from `Activity.Current`.

```csharp
public string? TraceId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### UserAgent \{#Kun_Auditing_Events_AuditEventIntegrationEvent_UserAgent}

User-Agent header of the originating request, when available.

```csharp
public string? UserAgent { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### UserId \{#Kun_Auditing_Events_AuditEventIntegrationEvent_UserId}

User identifier when the acting principal is a human user; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)
for system / agent actors. When both `UserId` and `ActorId`
are populated for a user-type actor they should carry the same value.

```csharp
public Guid? UserId { get; init; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)?

## Methods

### \<Clone>$() \{#Kun_Auditing_Events_AuditEventIntegrationEvent__Clone\_\_}

```csharp
public AuditEventIntegrationEvent <Clone>$()
```

#### Returns

[AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md)

### Deconstruct(out Guid?, out Guid?, out string, out string?, out string, out string?, out string?, out string, out string?, out JsonDocument?, out string?, out string?, out DateTimeOffset) \{#Kun_Auditing_Events_AuditEventIntegrationEvent_Deconstruct_System_Nullable_System_Guid___System_Nullable_System_Guid___System_String__System_String__System_String__System_String__System_String__System_String__System_String__System_Text_Json_JsonDocument__System_String__System_String__System_DateTimeOffset\_\_}

```csharp
public void Deconstruct(out Guid? TenantId, out Guid? UserId, out string ActorType, out string? ActorId, out string Action, out string? ResourceType, out string? ResourceId, out string Outcome, out string? TraceId, out JsonDocument? Metadata, out string? IpAddress, out string? UserAgent, out DateTimeOffset CreatedAt)
```

#### Parameters

`TenantId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)?

`UserId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)?

`ActorType` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ActorId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Action` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ResourceType` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`ResourceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Outcome` [string](https://learn.microsoft.com/dotnet/api/system.string)

`TraceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Metadata` [JsonDocument](https://learn.microsoft.com/dotnet/api/system.text.json.jsondocument)?

`IpAddress` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`UserAgent` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`CreatedAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Equals(object?) \{#Kun_Auditing_Events_AuditEventIntegrationEvent_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(AuditEventIntegrationEvent?) \{#Kun_Auditing_Events_AuditEventIntegrationEvent_Equals_Kun_Auditing_Events_AuditEventIntegrationEvent\_}

```csharp
public bool Equals(AuditEventIntegrationEvent? other)
```

#### Parameters

`other` [AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Auditing_Events_AuditEventIntegrationEvent_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Auditing_Events_AuditEventIntegrationEvent_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(AuditEventIntegrationEvent?, AuditEventIntegrationEvent?) \{#Kun_Auditing_Events_AuditEventIntegrationEvent_op_Equality_Kun_Auditing_Events_AuditEventIntegrationEvent_Kun_Auditing_Events_AuditEventIntegrationEvent\_}

```csharp
public static bool operator ==(AuditEventIntegrationEvent? left, AuditEventIntegrationEvent? right)
```

#### Parameters

`left` [AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md)?

`right` [AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(AuditEventIntegrationEvent?, AuditEventIntegrationEvent?) \{#Kun_Auditing_Events_AuditEventIntegrationEvent_op_Inequality_Kun_Auditing_Events_AuditEventIntegrationEvent_Kun_Auditing_Events_AuditEventIntegrationEvent\_}

```csharp
public static bool operator !=(AuditEventIntegrationEvent? left, AuditEventIntegrationEvent? right)
```

#### Parameters

`left` [AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md)?

`right` [AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
