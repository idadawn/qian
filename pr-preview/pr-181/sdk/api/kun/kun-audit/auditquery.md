> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class AuditQuery

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit](/pr-preview/pr-181/sdk/api/kun/kun-audit/index.md)\
Assembly: Kun.Audit.dll

Filter + pagination parameters for [IAuditReader.QueryAsync](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/iauditreader.md#Kun_Audit_Abstractions_IAuditReader_QueryAsync_Kun_Audit_AuditQuery_System_Threading_CancellationToken_).
Filters combine with `AND`; unset filters match anything.

```csharp
public sealed record AuditQuery : IEquatable<AuditQuery>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditQuery](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditquery.md)

#### Implements

[IEquatable\<AuditQuery>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### AuditQuery(string, string?, string?, string?, string?, DateTimeOffset?, DateTimeOffset?, int) \{#Kun_Audit_AuditQuery__ctor_System_String_System_String_System_String_System_String_System_String_System_Nullable_System_DateTimeOffset__System_Nullable_System_DateTimeOffset__System_Int32\_}

Filter + pagination parameters for [IAuditReader.QueryAsync](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/iauditreader.md#Kun_Audit_Abstractions_IAuditReader_QueryAsync_Kun_Audit_AuditQuery_System_Threading_CancellationToken_).
Filters combine with `AND`; unset filters match anything.

```csharp
public AuditQuery(string TenantId, string? ActorId = null, string? Action = null, string? ResourceType = null, string? ResourceId = null, DateTimeOffset? From = null, DateTimeOffset? To = null, int Limit = 100)
```

#### Parameters

`TenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

Restrict to a single tenant. Required — audit reads are tenant-scoped by convention and a [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) value would cross tenant boundaries.

`ActorId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Restrict to one actor's actions. Optional.

`Action` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Restrict to a single stable action string (e.g. `agent.created`). Optional.

`ResourceType` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Restrict to one resource type name. Optional.

`ResourceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Restrict to a single resource primary key. Optional.

`From` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

Inclusive lower bound on [AuditLog.Timestamp](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md#Kun_Audit_AuditLog_Timestamp).

`To` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

Exclusive upper bound on [AuditLog.Timestamp](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md#Kun_Audit_AuditLog_Timestamp).

`Limit` [int](https://learn.microsoft.com/dotnet/api/system.int32)

Maximum number of rows to yield (default 100; implementations should cap at 1000 to avoid accidental table scans).

## Properties

### Action \{#Kun_Audit_AuditQuery_Action}

Restrict to a single stable action string (e.g. `agent.created`). Optional.

```csharp
public string? Action { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ActorId \{#Kun_Audit_AuditQuery_ActorId}

Restrict to one actor's actions. Optional.

```csharp
public string? ActorId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### From \{#Kun_Audit_AuditQuery_From}

Inclusive lower bound on [AuditLog.Timestamp](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md#Kun_Audit_AuditLog_Timestamp).

```csharp
public DateTimeOffset? From { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

### Limit \{#Kun_Audit_AuditQuery_Limit}

Maximum number of rows to yield (default 100; implementations should cap at 1000 to avoid accidental table scans).

```csharp
public int Limit { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ResourceId \{#Kun_Audit_AuditQuery_ResourceId}

Restrict to a single resource primary key. Optional.

```csharp
public string? ResourceId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ResourceType \{#Kun_Audit_AuditQuery_ResourceType}

Restrict to one resource type name. Optional.

```csharp
public string? ResourceType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TenantId \{#Kun_Audit_AuditQuery_TenantId}

Restrict to a single tenant. Required — audit reads are tenant-scoped by convention and a [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) value would cross tenant boundaries.

```csharp
public string TenantId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### To \{#Kun_Audit_AuditQuery_To}

Exclusive upper bound on [AuditLog.Timestamp](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md#Kun_Audit_AuditLog_Timestamp).

```csharp
public DateTimeOffset? To { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

## Methods

### \<Clone>$() \{#Kun_Audit_AuditQuery__Clone\_\_}

```csharp
public AuditQuery <Clone>$()
```

#### Returns

[AuditQuery](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditquery.md)

### Deconstruct(out string, out string?, out string?, out string?, out string?, out DateTimeOffset?, out DateTimeOffset?, out int) \{#Kun_Audit_AuditQuery_Deconstruct_System_String__System_String__System_String__System_String__System_String__System_Nullable_System_DateTimeOffset___System_Nullable_System_DateTimeOffset___System_Int32\_\_}

```csharp
public void Deconstruct(out string TenantId, out string? ActorId, out string? Action, out string? ResourceType, out string? ResourceId, out DateTimeOffset? From, out DateTimeOffset? To, out int Limit)
```

#### Parameters

`TenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ActorId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Action` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`ResourceType` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`ResourceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`From` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

`To` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

`Limit` [int](https://learn.microsoft.com/dotnet/api/system.int32)

### Equals(object?) \{#Kun_Audit_AuditQuery_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(AuditQuery?) \{#Kun_Audit_AuditQuery_Equals_Kun_Audit_AuditQuery\_}

```csharp
public bool Equals(AuditQuery? other)
```

#### Parameters

`other` [AuditQuery](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditquery.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Audit_AuditQuery_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Audit_AuditQuery_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(AuditQuery?, AuditQuery?) \{#Kun_Audit_AuditQuery_op_Equality_Kun_Audit_AuditQuery_Kun_Audit_AuditQuery\_}

```csharp
public static bool operator ==(AuditQuery? left, AuditQuery? right)
```

#### Parameters

`left` [AuditQuery](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditquery.md)?

`right` [AuditQuery](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditquery.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(AuditQuery?, AuditQuery?) \{#Kun_Audit_AuditQuery_op_Inequality_Kun_Audit_AuditQuery_Kun_Audit_AuditQuery\_}

```csharp
public static bool operator !=(AuditQuery? left, AuditQuery? right)
```

#### Parameters

`left` [AuditQuery](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditquery.md)?

`right` [AuditQuery](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditquery.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
