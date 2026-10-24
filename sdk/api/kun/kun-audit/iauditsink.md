> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IAuditSink

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Audit)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit](/sdk/api/kun/kun-audit/index.md)\
Assembly: Kun.Audit.dll

Pluggable write target for audit entries. Implementations decide where
entries land (DB table, append-only log file, remote audit service) and
how durable the write is. The pipeline behaviour surrounds calls to this
interface with the per-tenant chain-state lock, so implementations may
treat writes as already serialised within a tenant.

```csharp
public interface IAuditSink
```

## Methods

### WriteAsync(AuditEntry, CancellationToken) \{#Kun_Audit_IAuditSink_WriteAsync_Kun_Audit_AuditEntry_System_Threading_CancellationToken\_}

Persists a single audit entry.

```csharp
ValueTask WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
```

#### Parameters

`entry` [AuditEntry](/sdk/api/kun/kun-audit/auditentry.md)

The entry to persist.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
