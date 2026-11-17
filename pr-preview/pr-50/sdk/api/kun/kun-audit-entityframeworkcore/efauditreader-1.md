> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class EfAuditReader\<TContext>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit.EntityFrameworkCore` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Audit.EntityFrameworkCore)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit.EntityFrameworkCore](/pr-preview/pr-50/sdk/api/kun/kun-audit-entityframeworkcore/index.md)\
Assembly: Kun.Audit.EntityFrameworkCore.dll

在数据库内完成租户过滤与限量，禁止以查询参数切换当前租户。

```csharp
public sealed class EfAuditReader<TContext> : IAuditReader where TContext : DbContext
```

#### Type Parameters

`TContext`

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[EfAuditReader\<TContext>](/pr-preview/pr-50/sdk/api/kun/kun-audit-entityframeworkcore/efauditreader-1.md)

#### Implements

IAuditReader

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### EfAuditReader(TContext, ITenantContext) \{#Kun_Audit_EntityFrameworkCore_EfAuditReader_1__ctor__0_Kun_Core_MultiTenancy_ITenantContext\_}

在数据库内完成租户过滤与限量，禁止以查询参数切换当前租户。

```csharp
public EfAuditReader(TContext context, ITenantContext tenantContext)
```

#### Parameters

`context` TContext

`tenantContext` ITenantContext

## Methods

### QueryAsync(AuditQuery, CancellationToken) \{#Kun_Audit_EntityFrameworkCore_EfAuditReader_1_QueryAsync_Kun_Audit_AuditQuery_System_Threading_CancellationToken\_}

Yields audit entries matching `query`, newest first.

```csharp
public IAsyncEnumerable<AuditLog> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
```

#### Parameters

`query` AuditQuery

Filter + pagination parameters.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancels the async enumeration.

#### Returns

[IAsyncEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.iasyncenumerable-1)\<AuditLog>

An async stream of matching audit logs.
