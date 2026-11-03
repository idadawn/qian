> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class AuditEntry

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit](/pr-preview/pr-202/sdk/api/kun/kun-audit/index.md)\
Assembly: Kun.Audit.dll

Immutable record describing one audited command execution. Each entry is
part of a per-tenant hash chain: [AuditEntry.ContentHash](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md#Kun_Audit_AuditEntry_ContentHash) covers this entry
plus the [AuditEntry.PrevHash](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md#Kun_Audit_AuditEntry_PrevHash) of the previous entry in the same chain,
so retroactive tampering with any row invalidates every following row.

```csharp
public sealed record AuditEntry : IEquatable<AuditEntry>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditEntry](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md)

#### Implements

[IEquatable\<AuditEntry>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### AuditEntry(Guid, string?, string?, string, DateTimeOffset, long, AuditOutcome, string?, string?, string) \{#Kun_Audit_AuditEntry__ctor_System_Guid_System_String_System_String_System_String_System_DateTimeOffset_System_Int64_Kun_Audit_AuditOutcome_System_String_System_String_System_String\_}

Immutable record describing one audited command execution. Each entry is
part of a per-tenant hash chain: [AuditEntry.ContentHash](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md#Kun_Audit_AuditEntry_ContentHash) covers this entry
plus the [AuditEntry.PrevHash](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md#Kun_Audit_AuditEntry_PrevHash) of the previous entry in the same chain,
so retroactive tampering with any row invalidates every following row.

```csharp
public AuditEntry(Guid Id, string? TenantId, string? ActorId, string CommandName, DateTimeOffset OccurredAt, long LatencyMs, AuditOutcome Outcome, string? ErrorCode, string? PrevHash, string ContentHash)
```

#### Parameters

`Id` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

Unique identifier for this audit row.

`TenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Tenant scope, [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when acting outside a tenant.

`ActorId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

User / service principal that invoked the command, [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when anonymous.

`CommandName` [string](https://learn.microsoft.com/dotnet/api/system.string)

Short name of the command type (e.g. `CreateOrderCommand`).

`OccurredAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp captured before the handler ran.

`LatencyMs` [long](https://learn.microsoft.com/dotnet/api/system.int64)

Wall-clock duration of the handler invocation, in milliseconds.

`Outcome` [AuditOutcome](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditoutcome.md)

Whether the handler completed normally or threw.

`ErrorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Exception type name on [AuditOutcome.Exception](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditoutcome.md#Kun_Audit_AuditOutcome_Exception), otherwise [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).
Business-level Result.Error codes are out of scope for the MVP and can be layered on later.

`PrevHash` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Content hash of the previous entry in the same tenant's chain; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for the first entry.

`ContentHash` [string](https://learn.microsoft.com/dotnet/api/system.string)

Lower-case hex SHA-256 chain hash produced by [AuditContentHasher.Compute](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditcontenthasher.md#Kun_Audit_AuditContentHasher_Compute_Kun_Audit_AuditEntry_).

## Properties

### ActorId \{#Kun_Audit_AuditEntry_ActorId}

User / service principal that invoked the command, [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when anonymous.

```csharp
public string? ActorId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ActorType \{#Kun_Audit_AuditEntry_ActorType}

主体类型；Unknown 保留历史条目的哈希算法。

```csharp
public string ActorType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### CommandName \{#Kun_Audit_AuditEntry_CommandName}

Short name of the command type (e.g. `CreateOrderCommand`).

```csharp
public string CommandName { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ContentHash \{#Kun_Audit_AuditEntry_ContentHash}

Lower-case hex SHA-256 chain hash produced by [AuditContentHasher.Compute](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditcontenthasher.md#Kun_Audit_AuditContentHasher_Compute_Kun_Audit_AuditEntry_).

```csharp
public string ContentHash { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ErrorCode \{#Kun_Audit_AuditEntry_ErrorCode}

Exception type name on [AuditOutcome.Exception](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditoutcome.md#Kun_Audit_AuditOutcome_Exception), otherwise [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).
Business-level Result.Error codes are out of scope for the MVP and can be layered on later.

```csharp
public string? ErrorCode { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Id \{#Kun_Audit_AuditEntry_Id}

Unique identifier for this audit row.

```csharp
public Guid Id { get; init; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)

### LatencyMs \{#Kun_Audit_AuditEntry_LatencyMs}

Wall-clock duration of the handler invocation, in milliseconds.

```csharp
public long LatencyMs { get; init; }
```

#### Property Value

[long](https://learn.microsoft.com/dotnet/api/system.int64)

### OccurredAt \{#Kun_Audit_AuditEntry_OccurredAt}

UTC timestamp captured before the handler ran.

```csharp
public DateTimeOffset OccurredAt { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Outcome \{#Kun_Audit_AuditEntry_Outcome}

Whether the handler completed normally or threw.

```csharp
public AuditOutcome Outcome { get; init; }
```

#### Property Value

[AuditOutcome](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditoutcome.md)

### PrevHash \{#Kun_Audit_AuditEntry_PrevHash}

Content hash of the previous entry in the same tenant's chain; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for the first entry.

```csharp
public string? PrevHash { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TenantId \{#Kun_Audit_AuditEntry_TenantId}

Tenant scope, [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when acting outside a tenant.

```csharp
public string? TenantId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

## Methods

### \<Clone>$() \{#Kun_Audit_AuditEntry__Clone\_\_}

```csharp
public AuditEntry <Clone>$()
```

#### Returns

[AuditEntry](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md)

### Deconstruct(out Guid, out string?, out string?, out string, out DateTimeOffset, out long, out AuditOutcome, out string?, out string?, out string) \{#Kun_Audit_AuditEntry_Deconstruct_System_Guid__System_String__System_String__System_String__System_DateTimeOffset__System_Int64__Kun_Audit_AuditOutcome__System_String__System_String__System_String\_\_}

```csharp
public void Deconstruct(out Guid Id, out string? TenantId, out string? ActorId, out string CommandName, out DateTimeOffset OccurredAt, out long LatencyMs, out AuditOutcome Outcome, out string? ErrorCode, out string? PrevHash, out string ContentHash)
```

#### Parameters

`Id` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

`TenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`ActorId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`CommandName` [string](https://learn.microsoft.com/dotnet/api/system.string)

`OccurredAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

`LatencyMs` [long](https://learn.microsoft.com/dotnet/api/system.int64)

`Outcome` [AuditOutcome](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditoutcome.md)

`ErrorCode` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`PrevHash` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`ContentHash` [string](https://learn.microsoft.com/dotnet/api/system.string)

### Equals(object?) \{#Kun_Audit_AuditEntry_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(AuditEntry?) \{#Kun_Audit_AuditEntry_Equals_Kun_Audit_AuditEntry\_}

```csharp
public bool Equals(AuditEntry? other)
```

#### Parameters

`other` [AuditEntry](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Audit_AuditEntry_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Audit_AuditEntry_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(AuditEntry?, AuditEntry?) \{#Kun_Audit_AuditEntry_op_Equality_Kun_Audit_AuditEntry_Kun_Audit_AuditEntry\_}

```csharp
public static bool operator ==(AuditEntry? left, AuditEntry? right)
```

#### Parameters

`left` [AuditEntry](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md)?

`right` [AuditEntry](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(AuditEntry?, AuditEntry?) \{#Kun_Audit_AuditEntry_op_Inequality_Kun_Audit_AuditEntry_Kun_Audit_AuditEntry\_}

```csharp
public static bool operator !=(AuditEntry? left, AuditEntry? right)
```

#### Parameters

`left` [AuditEntry](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md)?

`right` [AuditEntry](/pr-preview/pr-202/sdk/api/kun/kun-audit/auditentry.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
