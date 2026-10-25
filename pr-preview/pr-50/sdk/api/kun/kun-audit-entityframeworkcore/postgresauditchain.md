> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class PostgresAuditChain

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit.EntityFrameworkCore` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Audit.EntityFrameworkCore)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit.EntityFrameworkCore](/pr-preview/pr-50/sdk/api/kun/kun-audit-entityframeworkcore/index.md)\
Assembly: Kun.Audit.EntityFrameworkCore.dll

按租户串行追加的 PostgreSQL 审计链；记录与链头在同一事务中提交。

```csharp
public sealed class PostgresAuditChain : IAuditChainState, IAuditSink
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PostgresAuditChain](/pr-preview/pr-50/sdk/api/kun/kun-audit-entityframeworkcore/postgresauditchain.md)

#### Implements

IAuditChainState,
IAuditSink

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PostgresAuditChain(NpgsqlDataSource, string) \{#Kun_Audit_EntityFrameworkCore_PostgresAuditChain__ctor_Npgsql_NpgsqlDataSource_System_String\_}

数据源由调用方管理；schema 必须是简单 PostgreSQL 标识符。

```csharp
public PostgresAuditChain(NpgsqlDataSource dataSource, string schema = "audit")
```

#### Parameters

`dataSource` NpgsqlDataSource

`schema` [string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### AppendAsync(string?, Func\<string?, ValueTask\<string>>, CancellationToken) \{#Kun_Audit_EntityFrameworkCore_PostgresAuditChain_AppendAsync_System_String_System_Func_System_String_System_Threading_Tasks_ValueTask_System_String___System_Threading_CancellationToken\_}

Appends one entry to the tenant's chain. Within `computeNewHash`
the caller should build the [AuditEntry](/pr-preview/pr-50/sdk/api/kun/kun-audit/auditentry.md), call
[AuditContentHasher.Compute](/pr-preview/pr-50/sdk/api/kun/kun-audit/auditcontenthasher.md#Kun_Audit_AuditContentHasher_Compute_Kun_Audit_AuditEntry_) with the supplied previous hash, and
return the freshly computed hash. The implementation guarantees the
read-compute-write sequence is serialised per `tenantKey`
so concurrent commands on the same tenant do not fork the chain.

```csharp
public ValueTask<string> AppendAsync(string? tenantKey, Func<string?, ValueTask<string>> computeNewHash, CancellationToken cancellationToken = default)
```

#### Parameters

`tenantKey` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Tenant identifier, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) for the shared/global chain.

`computeNewHash` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string)?, [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>>

Factory that, given the previous hash (or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the chain is empty),
produces the new [AuditEntry.ContentHash](/pr-preview/pr-50/sdk/api/kun/kun-audit/auditentry.md#Kun_Audit_AuditEntry_ContentHash). May perform the sink write.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

The new content hash, now stored as the chain tip.

### EnsureCreatedAsync(CancellationToken) \{#Kun_Audit_EntityFrameworkCore_PostgresAuditChain_EnsureCreatedAsync_System_Threading_CancellationToken\_}

显式初始化审计存储；生产中由迁移阶段调用，不在业务追加时建表。

```csharp
public Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
```

#### Parameters

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### ReadAsync(string?, CancellationToken) \{#Kun_Audit_EntityFrameworkCore_PostgresAuditChain_ReadAsync_System_String_System_Threading_CancellationToken\_}

按持久化序号读取指定租户的链；调用方负责授权。

```csharp
public IAsyncEnumerable<AuditEntry> ReadAsync(string? tenantId, CancellationToken cancellationToken = default)
```

#### Parameters

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[IAsyncEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.iasyncenumerable-1)\<AuditEntry>

### WriteAsync(AuditEntry, CancellationToken) \{#Kun_Audit_EntityFrameworkCore_PostgresAuditChain_WriteAsync_Kun_Audit_AuditEntry_System_Threading_CancellationToken\_}

Persists a single audit entry.

```csharp
public ValueTask WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
```

#### Parameters

`entry` AuditEntry

The entry to persist.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
