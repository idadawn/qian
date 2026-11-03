> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Namespace Kun.EntityFrameworkCore.MultiTenancy

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.EntityFrameworkCore)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

### Classes

[DatabasePerTenantProvisioner\<TContext>](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/databasepertenantprovisioner-1.md)

[ITenantProvisioner](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/itenantprovisioner.md) for the
[TenantIsolationStrategy.DatabasePerTenant](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/tenantisolationstrategy.md#Kun_Core_MultiTenancy_TenantIsolationStrategy_DatabasePerTenant) strategy. Runs EF Core
migrations against the tenant's dedicated database on [DatabasePerTenantProvisioner\<TContext>.ProvisionAsync](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/databasepertenantprovisioner-1.md#Kun_EntityFrameworkCore_MultiTenancy_DatabasePerTenantProvisioner_1_ProvisionAsync_Kun_Core_MultiTenancy_TenantInfo_System_Threading_CancellationToken_).
[DatabasePerTenantProvisioner\<TContext>.DeprovisionAsync](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/databasepertenantprovisioner-1.md#Kun_EntityFrameworkCore_MultiTenancy_DatabasePerTenantProvisioner_1_DeprovisionAsync_System_String_System_Threading_CancellationToken_) is a no-op by default because dropping a tenant database
is an operations concern (needs admin credentials and often a separate control plane) —
the framework does not take that responsibility.

[KunMultiTenancyOptions](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/kunmultitenancyoptions.md)

Configuration for `AddKunMultiTenancy`. Selects the isolation strategy and tunes
the few knobs the framework exposes across all three strategies.

[KunMultiTenancyServiceCollectionExtensions](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/kunmultitenancyservicecollectionextensions.md)

DI entry point for Kun's SaaS multi-tenancy strategy selection. Dispatches based on
[KunMultiTenancyOptions.Strategy](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/kunmultitenancyoptions.md#Kun_EntityFrameworkCore_MultiTenancy_KunMultiTenancyOptions_Strategy).

[OptionsBasedTenantConnectionResolver](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/optionsbasedtenantconnectionresolver.md)

Default [IDbConnectionResolver](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/idbconnectionresolver.md) backed by [TenantsConnectionOptions](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/tenantsconnectionoptions.md).
Reads the current snapshot on every call via [`IOptionsMonitor`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1)
so on-the-fly config reloads reach new tenants without restarting the host.

[SchemaPerTenantProvisioner\<TContext>](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/schemapertenantprovisioner-1.md)

[ITenantProvisioner](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/itenantprovisioner.md) implementation for the
[TenantIsolationStrategy.SchemaPerTenant](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/tenantisolationstrategy.md#Kun_Core_MultiTenancy_TenantIsolationStrategy_SchemaPerTenant) strategy. On
[SchemaPerTenantProvisioner\<TContext>.ProvisionAsync](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/schemapertenantprovisioner-1.md#Kun_EntityFrameworkCore_MultiTenancy_SchemaPerTenantProvisioner_1_ProvisionAsync_Kun_Core_MultiTenancy_TenantInfo_System_Threading_CancellationToken_) executes `CREATE SCHEMA IF NOT EXISTS "{tenantId}"`
and runs EF Core migrations under that schema; on [SchemaPerTenantProvisioner\<TContext>.DeprovisionAsync](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/schemapertenantprovisioner-1.md#Kun_EntityFrameworkCore_MultiTenancy_SchemaPerTenantProvisioner_1_DeprovisionAsync_System_String_System_Threading_CancellationToken_)
drops the schema when [KunMultiTenancyOptions.AllowDestructiveDeprovision](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/kunmultitenancyoptions.md#Kun_EntityFrameworkCore_MultiTenancy_KunMultiTenancyOptions_AllowDestructiveDeprovision)
is enabled, else throws.

[TenantAwareModelCacheKeyFactory](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/tenantawaremodelcachekeyfactory.md)

[`IModelCacheKeyFactory`](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.infrastructure.imodelcachekeyfactory) that partitions EF Core's compiled-model cache by
tenant ID in addition to context type and design-time flag. Required for the
[TenantIsolationStrategy.SchemaPerTenant](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/tenantisolationstrategy.md#Kun_Core_MultiTenancy_TenantIsolationStrategy_SchemaPerTenant) strategy so
that tenant A's schema-specific model is not reused for tenant B.

[TenantsConnectionOptions](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/tenantsconnectionoptions.md)

Static tenant → connection-string mapping consumed by
[OptionsBasedTenantConnectionResolver](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/optionsbasedtenantconnectionresolver.md). Suitable for small deployments
where the full tenant list is known at configuration time (appsettings.json or
Kubernetes ConfigMap).

### Interfaces

[IDbConnectionResolver](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/idbconnectionresolver.md)

Resolves the connection string for a given tenant. Used by the
[TenantIsolationStrategy.DatabasePerTenant](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/tenantisolationstrategy.md#Kun_Core_MultiTenancy_TenantIsolationStrategy_DatabasePerTenant) strategy
to dispatch DbContext construction to the tenant's dedicated database.

[ITenantDbContextFactory\<TContext>](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/itenantdbcontextfactory-1.md)

Builds a `TContext` instance bound to a specific tenant's schema
or database. Used by [SchemaPerTenantProvisioner\<TContext>](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/schemapertenantprovisioner-1.md) at tenant
provisioning time, when the ambient [ITenantContext](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/itenantcontext.md)
may not yet reference the new tenant.
