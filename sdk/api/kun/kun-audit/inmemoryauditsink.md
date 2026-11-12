> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class InMemoryAuditSink

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Audit)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit](/sdk/api/kun/kun-audit/index.md)\
Assembly: Kun.Audit.dll

Process-local [IAuditSink](/sdk/api/kun/kun-audit/iauditsink.md) that keeps all written entries in
memory. Intended for unit tests and non-durable development; do not use in
production because entries are lost on process restart and there is no
durability guarantee if the process crashes mid-write.

```csharp
public sealed class InMemoryAuditSink : IAuditSink
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[InMemoryAuditSink](/sdk/api/kun/kun-audit/inmemoryauditsink.md)

#### Implements

[IAuditSink](/sdk/api/kun/kun-audit/iauditsink.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### InMemoryAuditSink() \{#Kun_Audit_InMemoryAuditSink__ctor}

```csharp
public InMemoryAuditSink()
```

## Properties

### Entries \{#Kun_Audit_InMemoryAuditSink_Entries}

Snapshot of all entries written so far, in write order.

```csharp
public IReadOnlyCollection<AuditEntry> Entries { get; }
```

#### Property Value

[IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)\<[AuditEntry](/sdk/api/kun/kun-audit/auditentry.md)>

## Methods

### Clear() \{#Kun_Audit_InMemoryAuditSink_Clear}

Clears all recorded entries (test helper).

```csharp
public void Clear()
```

### WriteAsync(AuditEntry, CancellationToken) \{#Kun_Audit_InMemoryAuditSink_WriteAsync_Kun_Audit_AuditEntry_System_Threading_CancellationToken\_}

Persists a single audit entry.

```csharp
public ValueTask WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
```

#### Parameters

`entry` [AuditEntry](/sdk/api/kun/kun-audit/auditentry.md)

The entry to persist.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
