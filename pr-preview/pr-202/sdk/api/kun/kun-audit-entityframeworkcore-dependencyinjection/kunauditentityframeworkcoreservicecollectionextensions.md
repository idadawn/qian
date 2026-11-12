> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunAuditEntityFrameworkCoreServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Audit.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit.EntityFrameworkCore.DependencyInjection](/pr-preview/pr-202/sdk/api/kun/kun-audit-entityframeworkcore-dependencyinjection/index.md)\
Assembly: Kun.Audit.EntityFrameworkCore.dll

Service-collection extensions that wire up Kun.Audit's EF Core adapter.

```csharp
public static class KunAuditEntityFrameworkCoreServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunAuditEntityFrameworkCoreServiceCollectionExtensions](/pr-preview/pr-202/sdk/api/kun/kun-audit-entityframeworkcore-dependencyinjection/kunauditentityframeworkcoreservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunAuditEntityFrameworkCore(KunAuditBuilder) \{#Kun_Audit_EntityFrameworkCore_DependencyInjection_KunAuditEntityFrameworkCoreServiceCollectionExtensions_AddKunAuditEntityFrameworkCore_Kun_Audit_DependencyInjection_KunAuditBuilder\_}

Registers the [AuditSaveChangesInterceptor](/pr-preview/pr-202/sdk/api/kun/kun-audit-entityframeworkcore/auditsavechangesinterceptor.md) so consumers
can attach it to their `DbContextOptionsBuilder.AddInterceptors`
call. Does **not** replace the [IAuditWriter](/pr-preview/pr-202/sdk/api/kun/kun-audit-abstractions/iauditwriter.md)
registration on its own — pair this with
[KunAuditEntityFrameworkCoreServiceCollectionExtensions.UseEntityFramework](/pr-preview/pr-202/sdk/api/kun/kun-audit-entityframeworkcore-dependencyinjection/kunauditentityframeworkcoreservicecollectionextensions.md#Kun_Audit_EntityFrameworkCore_DependencyInjection_KunAuditEntityFrameworkCoreServiceCollectionExtensions_UseEntityFramework__1_Kun_Audit_DependencyInjection_KunAuditBuilder_) to persist audits via
a specific [`DbContext`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext).

```csharp
[Obsolete("请迁移到 AddKunAuditing<TContext>()，旧拦截器在业务提交后写入审计。", DiagnosticId = "KUNAUDIT001")]
public static KunAuditBuilder AddKunAuditEntityFrameworkCore(this KunAuditBuilder builder)
```

#### Parameters

`builder` KunAuditBuilder

#### Returns

KunAuditBuilder

### UseEntityFramework\<TDbContext>(KunAuditBuilder) \{#Kun_Audit_EntityFrameworkCore_DependencyInjection_KunAuditEntityFrameworkCoreServiceCollectionExtensions_UseEntityFramework__1_Kun_Audit_DependencyInjection_KunAuditBuilder\_}

Replaces the registered [IAuditWriter](/pr-preview/pr-202/sdk/api/kun/kun-audit-abstractions/iauditwriter.md) with
[EfAuditWriter\<TDbContext>](/pr-preview/pr-202/sdk/api/kun/kun-audit-entityframeworkcore/efauditwriter-1.md), causing audit entries to
be appended to `TDbContext`'s
`DbSet<AuditLog>` and committed in the same transaction
as the business change.

```csharp
public static KunAuditBuilder UseEntityFramework<TDbContext>(this KunAuditBuilder builder) where TDbContext : DbContext
```

#### Parameters

`builder` KunAuditBuilder

#### Returns

KunAuditBuilder

#### Type Parameters

`TDbContext`

The EF Core context that owns the `AuditLog` set and has [AuditLogConfiguration](/pr-preview/pr-202/sdk/api/kun/kun-audit-entityframeworkcore/auditlogconfiguration.md) applied in its `OnModelCreating`.

### UsePostgresChain(KunAuditBuilder, NpgsqlDataSource, string) \{#Kun_Audit_EntityFrameworkCore_DependencyInjection_KunAuditEntityFrameworkCoreServiceCollectionExtensions_UsePostgresChain_Kun_Audit_DependencyInjection_KunAuditBuilder_Npgsql_NpgsqlDataSource_System_String\_}

将链状态与写入端绑定到同一个 PostgreSQL 原子追加实现。

```csharp
public static KunAuditBuilder UsePostgresChain(this KunAuditBuilder builder, NpgsqlDataSource dataSource, string schema = "audit")
```

#### Parameters

`builder` KunAuditBuilder

`dataSource` NpgsqlDataSource

`schema` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

KunAuditBuilder
