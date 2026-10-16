> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class EventBusAuditWriter

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit](/pr-preview/pr-181/sdk/api/kun/kun-audit/index.md)\
Assembly: Kun.Audit.dll

[IAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/iauditwriter.md) that fans audit entries out as
`kun.audit.record.v1` integration events via
[IIntegrationEventBus](/pr-preview/pr-181/sdk/api/kun/kun-eventbus/iintegrationeventbus.md), riding the consumer's Outbox for
atomicity with the business transaction.

```csharp
[Obsolete("kun.audit.record.v1 已废弃；实体审计请迁移到 Kun.Auditing.AddKunAuditing<TContext>()。", DiagnosticId = "KUNAUDIT001")]
public sealed class EventBusAuditWriter : IAuditWriter
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[EventBusAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit/eventbusauditwriter.md)

#### Implements

[IAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/iauditwriter.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Consumers must register [AuditLog](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md) in their event type
registry under the stable id `kun.audit.record.v1` and register
the corresponding `JsonTypeInfo` in their
`JsonSerializerContext` — Kun does not own the consumer's JSON
context and therefore cannot register the type itself.

Because this writer stages an Outbox message, audit fan-out joins the
business unit-of-work: if the business transaction rolls back, the
audit integration event is rolled back as well. This is the intended
atomicity guarantee. Consumers who want fire-and-forget semantics
should implement a custom [IAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/iauditwriter.md).

## Constructors

### EventBusAuditWriter(IIntegrationEventBus) \{#Kun_Audit_EventBusAuditWriter__ctor_Kun_EventBus_IIntegrationEventBus\_}

Creates a new [EventBusAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit/eventbusauditwriter.md).

```csharp
public EventBusAuditWriter(IIntegrationEventBus eventBus)
```

#### Parameters

`eventBus` IIntegrationEventBus

## Fields

### EventTypeId \{#Kun_Audit_EventBusAuditWriter_EventTypeId}

Stable integration-event identifier used when consumers register [AuditLog](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md) with the event-type registry.

```csharp
public const string EventTypeId = "kun.audit.record.v1"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### WriteAsync(AuditLog, CancellationToken) \{#Kun_Audit_EventBusAuditWriter_WriteAsync_Kun_Audit_AuditLog_System_Threading_CancellationToken\_}

Persists or publishes the `entry`.

```csharp
public Task WriteAsync(AuditLog entry, CancellationToken cancellationToken = default)
```

#### Parameters

`entry` [AuditLog](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md)

The audit event to record.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancels the write operation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
