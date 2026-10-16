> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class SchemaPerTenantProvisioner\<TContext>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.MultiTenancy](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

[ITenantProvisioner](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/itenantprovisioner.md) implementation for the
[TenantIsolationStrategy.SchemaPerTenant](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/tenantisolationstrategy.md#Kun_Core_MultiTenancy_TenantIsolationStrategy_SchemaPerTenant) strategy. On
[SchemaPerTenantProvisioner\<TContext>.ProvisionAsync](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/schemapertenantprovisioner-1.md#Kun_EntityFrameworkCore_MultiTenancy_SchemaPerTenantProvisioner_1_ProvisionAsync_Kun_Core_MultiTenancy_TenantInfo_System_Threading_CancellationToken_) executes `CREATE SCHEMA IF NOT EXISTS "{tenantId}"`
and runs EF Core migrations under that schema; on [SchemaPerTenantProvisioner\<TContext>.DeprovisionAsync](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/schemapertenantprovisioner-1.md#Kun_EntityFrameworkCore_MultiTenancy_SchemaPerTenantProvisioner_1_DeprovisionAsync_System_String_System_Threading_CancellationToken_)
drops the schema when [KunMultiTenancyOptions.AllowDestructiveDeprovision](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/kunmultitenancyoptions.md#Kun_EntityFrameworkCore_MultiTenancy_KunMultiTenancyOptions_AllowDestructiveDeprovision)
is enabled, else throws.

```csharp
public sealed class SchemaPerTenantProvisioner<TContext> : ITenantProvisioner where TContext : DbContext
```

#### Type Parameters

`TContext`

The KunDbContext-derived context whose migrations to apply.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SchemaPerTenantProvisioner\<TContext>](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/schemapertenantprovisioner-1.md)

#### Implements

ITenantProvisioner

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Idempotent by construction: both `IF NOT EXISTS` (provision) and `IF EXISTS`
(deprovision) are baked into the SQL. Tenant IDs are validated for safe schema-name
characters (letters, digits, underscore, hyphen) before being interpolated — invalid
IDs throw [`ArgumentException`](https://learn.microsoft.com/dotnet/api/system.argumentexception) with a remediation message rather than
reaching the database.

## Constructors

### SchemaPerTenantProvisioner(ITenantDbContextFactory\<TContext>, IOptions\<KunMultiTenancyOptions>, ILogger\<SchemaPerTenantProvisioner\<TContext>>?) \{#Kun_EntityFrameworkCore_MultiTenancy_SchemaPerTenantProvisioner_1__ctor_Kun_EntityFrameworkCore_MultiTenancy_ITenantDbContextFactory__0__Microsoft_Extensions_Options_IOptions_Kun_EntityFrameworkCore_MultiTenancy_KunMultiTenancyOptions__Microsoft_Extensions_Logging_ILogger_Kun_EntityFrameworkCore_MultiTenancy_SchemaPerTenantProvisioner__0\_\_\_}

Creates a new provisioner.

```csharp
public SchemaPerTenantProvisioner(ITenantDbContextFactory<TContext> factory, IOptions<KunMultiTenancyOptions> options, ILogger<SchemaPerTenantProvisioner<TContext>>? logger = null)
```

#### Parameters

`factory` [ITenantDbContextFactory](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/itenantdbcontextfactory-1.md)\<TContext>

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[KunMultiTenancyOptions](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/kunmultitenancyoptions.md)>

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[SchemaPerTenantProvisioner](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-multitenancy/schemapertenantprovisioner-1.md)\<TContext>>?

## Methods

### DeprovisionAsync(string, CancellationToken) \{#Kun_EntityFrameworkCore_MultiTenancy_SchemaPerTenantProvisioner_1_DeprovisionAsync_System_String_System_Threading_CancellationToken\_}

清理指定租户的持久化基础件。幂等：对未开户的租户调用不得抛异常。

```csharp
public Task DeprovisionAsync(string tenantId, CancellationToken cancellationToken = default)
```

#### Parameters

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### ProvisionAsync(TenantInfo, CancellationToken) \{#Kun_EntityFrameworkCore_MultiTenancy_SchemaPerTenantProvisioner_1_ProvisionAsync_Kun_Core_MultiTenancy_TenantInfo_System_Threading_CancellationToken\_}

为指定租户准备持久化基础件（schema / database / seed 数据）。幂等。

```csharp
public Task ProvisionAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
```

#### Parameters

`tenant` TenantInfo

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
