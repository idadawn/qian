> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IAuditSink

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Auditing` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Auditing`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Auditing)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Auditing](/sdk/api/kun/kun-auditing/index.md)\
Assembly: Kun.Auditing.dll

Terminal destination for [AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md) records produced by
[AuditingSaveChangesInterceptor](/sdk/api/kun/kun-auditing/auditingsavechangesinterceptor.md). The default implementation forwards to
[IOutboxWriter](/sdk/api/kun/kun-eventbus-outbox/ioutboxwriter.md) so audit events flow atomically with
the current `SaveChangesAsync` transaction.

```csharp
public interface IAuditSink
```

## Remarks

Custom sinks (e.g. a direct-write ClickHouse adapter) must preserve the atomicity
contract: anything that can fail outside the ambient transaction defeats the audit
reliability guarantee. Prefer layering a decorator over the outbox sink instead of
replacing it.

## Methods

### WriteAsync(AuditEventIntegrationEvent, CancellationToken) \{#Kun_Auditing_IAuditSink_WriteAsync_Kun_Auditing_Events_AuditEventIntegrationEvent_System_Threading_CancellationToken\_}

Enqueues `event` for durable delivery. Implementations must be
safe to call from within EF Core's `SavingChangesAsync` interceptor phase —
no I/O, no blocking waits.

```csharp
ValueTask WriteAsync(AuditEventIntegrationEvent @event, CancellationToken cancellationToken)
```

#### Parameters

`event` [AuditEventIntegrationEvent](/sdk/api/kun/kun-auditing-events/auditeventintegrationevent.md)

The audit event to persist.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token forwarded from EF Core.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
