> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface IAuditWriter

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/index.md)\
Assembly: Kun.Audit.dll

Persists or publishes an [AuditLog](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md) entry.

```csharp
public interface IAuditWriter
```

## Remarks

The default implementation in this assembly is
[NullAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit/nullauditwriter.md) (drops entries), so interceptor wiring
is safe even when no audit backend has been configured. Production
deployments register one of:

- `EfAuditWriter` (from `Kun.Audit.EntityFrameworkCore`) — synchronous DB insert inside the business transaction.
- [EventBusAuditWriter](/pr-preview/pr-181/sdk/api/kun/kun-audit/eventbusauditwriter.md) — stages the entry on the Outbox as a `kun.audit.record.v1` integration event.
- Custom implementations (SIEM forwarder, file-append, …).

Implementations should be idempotent with respect to [AuditLog.Id](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md#Kun_Audit_AuditLog_Id):
interceptor re-entry (EF Core retries, transient failures followed by
Outbox replay) must not duplicate audit rows.

## Methods

### WriteAsync(AuditLog, CancellationToken) \{#Kun_Audit_Abstractions_IAuditWriter_WriteAsync_Kun_Audit_AuditLog_System_Threading_CancellationToken\_}

Persists or publishes the `entry`.

```csharp
Task WriteAsync(AuditLog entry, CancellationToken cancellationToken = default)
```

#### Parameters

`entry` [AuditLog](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md)

The audit event to record.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancels the write operation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
