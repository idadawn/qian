> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface IAuditReader

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-audit-abstractions/index.md)\
Assembly: Kun.Audit.dll

Queries the audit log store. Tenant-scoped by contract — see [AuditQuery.TenantId](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditquery.md#Kun_Audit_AuditQuery_TenantId).

```csharp
public interface IAuditReader
```

## Methods

### QueryAsync(AuditQuery, CancellationToken) \{#Kun_Audit_Abstractions_IAuditReader_QueryAsync_Kun_Audit_AuditQuery_System_Threading_CancellationToken\_}

Yields audit entries matching `query`, newest first.

```csharp
IAsyncEnumerable<AuditLog> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
```

#### Parameters

`query` [AuditQuery](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditquery.md)

Filter + pagination parameters.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancels the async enumeration.

#### Returns

[IAsyncEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.iasyncenumerable-1)\<[AuditLog](/pr-preview/pr-181/sdk/api/kun/kun-audit/auditlog.md)>

An async stream of matching audit logs.
