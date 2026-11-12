> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class EfAuditWriter\<TDbContext>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit.EntityFrameworkCore](/pr-preview/pr-122/sdk/api/kun/kun-audit-entityframeworkcore/index.md)\
Assembly: Kun.Audit.EntityFrameworkCore.dll

[IAuditWriter](/pr-preview/pr-122/sdk/api/kun/kun-audit-abstractions/iauditwriter.md) that persists entries through a typed
`TDbContext`. The context may be the same
`KunDbContext` that holds the business aggregates (audit rows
land in the same transaction) or a dedicated audit database context
(decoupled lifecycle, different retention policy).

```csharp
public sealed class EfAuditWriter<TDbContext> : IAuditWriter where TDbContext : DbContext
```

#### Type Parameters

`TDbContext`

The EF Core context that owns the `AuditLog` `DbSet`.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[EfAuditWriter\<TDbContext>](/pr-preview/pr-122/sdk/api/kun/kun-audit-entityframeworkcore/efauditwriter-1.md)

#### Implements

IAuditWriter

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Does **not** call [`SaveChangesAsync`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext.savechangesasync).
When invoked from inside [AuditSaveChangesInterceptor](/pr-preview/pr-122/sdk/api/kun/kun-audit-entityframeworkcore/auditsavechangesinterceptor.md),
the outer EF `SaveChanges` commits the audit rows atomically
with the business change. When invoked standalone (e.g. from a
Mediator pipeline behavior) the caller is responsible for flushing.

## Constructors

### EfAuditWriter(TDbContext) \{#Kun_Audit_EntityFrameworkCore_EfAuditWriter_1__ctor__0\_}

Creates a new writer bound to `dbContext`.

```csharp
public EfAuditWriter(TDbContext dbContext)
```

#### Parameters

`dbContext` TDbContext

## Methods

### WriteAsync(AuditLog, CancellationToken) \{#Kun_Audit_EntityFrameworkCore_EfAuditWriter_1_WriteAsync_Kun_Audit_AuditLog_System_Threading_CancellationToken\_}

Persists or publishes the `entry`.

```csharp
public Task WriteAsync(AuditLog entry, CancellationToken cancellationToken = default)
```

#### Parameters

`entry` AuditLog

The audit event to record.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancels the write operation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
