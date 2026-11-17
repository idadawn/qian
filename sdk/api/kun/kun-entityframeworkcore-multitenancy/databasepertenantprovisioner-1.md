> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class DatabasePerTenantProvisioner\<TContext>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EntityFrameworkCore)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.MultiTenancy](/sdk/api/kun/kun-entityframeworkcore-multitenancy/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

[ITenantProvisioner](/sdk/api/kun/kun-core-multitenancy/itenantprovisioner.md) for the
[TenantIsolationStrategy.DatabasePerTenant](/sdk/api/kun/kun-core-multitenancy/tenantisolationstrategy.md#Kun_Core_MultiTenancy_TenantIsolationStrategy_DatabasePerTenant) strategy. Runs EF Core
migrations against the tenant's dedicated database on [DatabasePerTenantProvisioner\<TContext>.ProvisionAsync](/sdk/api/kun/kun-entityframeworkcore-multitenancy/databasepertenantprovisioner-1.md#Kun_EntityFrameworkCore_MultiTenancy_DatabasePerTenantProvisioner_1_ProvisionAsync_Kun_Core_MultiTenancy_TenantInfo_System_Threading_CancellationToken_).
[DatabasePerTenantProvisioner\<TContext>.DeprovisionAsync](/sdk/api/kun/kun-entityframeworkcore-multitenancy/databasepertenantprovisioner-1.md#Kun_EntityFrameworkCore_MultiTenancy_DatabasePerTenantProvisioner_1_DeprovisionAsync_System_String_System_Threading_CancellationToken_) is a no-op by default because dropping a tenant database
is an operations concern (needs admin credentials and often a separate control plane) —
the framework does not take that responsibility.

```csharp
public class DatabasePerTenantProvisioner<TContext> : ITenantProvisioner where TContext : DbContext
```

#### Type Parameters

`TContext`

The KunDbContext-derived context whose migrations to apply.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[DatabasePerTenantProvisioner\<TContext>](/sdk/api/kun/kun-entityframeworkcore-multitenancy/databasepertenantprovisioner-1.md)

#### Implements

ITenantProvisioner

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Assumes the tenant's database has already been **created** at the server
level by an operations workflow (Terraform, K8s Operator, cloud provider API). This
provisioner's job is to bring the schema up-to-date via
`Database.MigrateAsync()`. Databases cannot be reliably created in a portable way
across Postgres / MySQL / SQL Server from EF Core itself, so that step is delegated.

For scenarios where the app owns database creation, subclass this provisioner and
override [DatabasePerTenantProvisioner\<TContext>.ProvisionAsync](/sdk/api/kun/kun-entityframeworkcore-multitenancy/databasepertenantprovisioner-1.md#Kun_EntityFrameworkCore_MultiTenancy_DatabasePerTenantProvisioner_1_ProvisionAsync_Kun_Core_MultiTenancy_TenantInfo_System_Threading_CancellationToken_) to run your vendor-specific `CREATE DATABASE`
before calling `base.ProvisionAsync`.

## Constructors

### DatabasePerTenantProvisioner(ITenantDbContextFactory\<TContext>, ILogger\<DatabasePerTenantProvisioner\<TContext>>?) \{#Kun_EntityFrameworkCore_MultiTenancy_DatabasePerTenantProvisioner_1__ctor_Kun_EntityFrameworkCore_MultiTenancy_ITenantDbContextFactory__0__Microsoft_Extensions_Logging_ILogger_Kun_EntityFrameworkCore_MultiTenancy_DatabasePerTenantProvisioner__0\_\_\_}

Creates a new provisioner.

```csharp
public DatabasePerTenantProvisioner(ITenantDbContextFactory<TContext> factory, ILogger<DatabasePerTenantProvisioner<TContext>>? logger = null)
```

#### Parameters

`factory` [ITenantDbContextFactory](/sdk/api/kun/kun-entityframeworkcore-multitenancy/itenantdbcontextfactory-1.md)\<TContext>

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[DatabasePerTenantProvisioner](/sdk/api/kun/kun-entityframeworkcore-multitenancy/databasepertenantprovisioner-1.md)\<TContext>>?

## Methods

### DeprovisionAsync(string, CancellationToken) \{#Kun_EntityFrameworkCore_MultiTenancy_DatabasePerTenantProvisioner_1_DeprovisionAsync_System_String_System_Threading_CancellationToken\_}

清理指定租户的持久化基础件。幂等：对未开户的租户调用不得抛异常。

```csharp
public virtual Task DeprovisionAsync(string tenantId, CancellationToken cancellationToken = default)
```

#### Parameters

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

#### Remarks

Default no-op: dropping a tenant database is an operations concern that requires
admin credentials and coordinated cleanup (connection pools, replication, backups).
Override this method in a subclass or swap in a custom [ITenantProvisioner](/sdk/api/kun/kun-core-multitenancy/itenantprovisioner.md)
when the app owns database lifecycle end-to-end.

### ProvisionAsync(TenantInfo, CancellationToken) \{#Kun_EntityFrameworkCore_MultiTenancy_DatabasePerTenantProvisioner_1_ProvisionAsync_Kun_Core_MultiTenancy_TenantInfo_System_Threading_CancellationToken\_}

为指定租户准备持久化基础件（schema / database / seed 数据）。幂等。

```csharp
public virtual Task ProvisionAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
```

#### Parameters

`tenant` TenantInfo

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
