> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class NullAuditWriter

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Audit)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit](/pr-preview/pr-50/sdk/api/kun/kun-audit/index.md)\
Assembly: Kun.Audit.dll

[IAuditWriter](/pr-preview/pr-50/sdk/api/kun/kun-audit-abstractions/iauditwriter.md) that silently drops every entry. Registered as
the fallback when no concrete writer has been configured so that
interceptor / pipeline wiring stays safe in tests and samples.

```csharp
public sealed class NullAuditWriter : IAuditWriter
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[NullAuditWriter](/pr-preview/pr-50/sdk/api/kun/kun-audit/nullauditwriter.md)

#### Implements

[IAuditWriter](/pr-preview/pr-50/sdk/api/kun/kun-audit-abstractions/iauditwriter.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Emits a [`Warning`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel.warning) on the first call so operators
notice missing configuration in production. Subsequent calls are silent
— at-most-once warning avoids log-flooding.

## Constructors

### NullAuditWriter(ILogger\<NullAuditWriter>) \{#Kun_Audit_NullAuditWriter__ctor_Microsoft_Extensions_Logging_ILogger_Kun_Audit_NullAuditWriter\_\_}

Creates a new [NullAuditWriter](/pr-preview/pr-50/sdk/api/kun/kun-audit/nullauditwriter.md).

```csharp
public NullAuditWriter(ILogger<NullAuditWriter> logger)
```

#### Parameters

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[NullAuditWriter](/pr-preview/pr-50/sdk/api/kun/kun-audit/nullauditwriter.md)>

## Methods

### WriteAsync(AuditLog, CancellationToken) \{#Kun_Audit_NullAuditWriter_WriteAsync_Kun_Audit_AuditLog_System_Threading_CancellationToken\_}

Persists or publishes the `entry`.

```csharp
public Task WriteAsync(AuditLog entry, CancellationToken cancellationToken = default)
```

#### Parameters

`entry` [AuditLog](/pr-preview/pr-50/sdk/api/kun/kun-audit/auditlog.md)

The audit event to record.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancels the write operation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
