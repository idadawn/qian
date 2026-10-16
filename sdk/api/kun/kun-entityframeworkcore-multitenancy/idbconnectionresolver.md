> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IDbConnectionResolver

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.MultiTenancy](/sdk/api/kun/kun-entityframeworkcore-multitenancy/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Resolves the connection string for a given tenant. Used by the
[TenantIsolationStrategy.DatabasePerTenant](/sdk/api/kun/kun-core-multitenancy/tenantisolationstrategy.md#Kun_Core_MultiTenancy_TenantIsolationStrategy_DatabasePerTenant) strategy
to dispatch DbContext construction to the tenant's dedicated database.

```csharp
public interface IDbConnectionResolver
```

## Remarks

Implementations must be thread-safe (called concurrently from scoped request handlers)
and idempotent. The default [OptionsBasedTenantConnectionResolver](/sdk/api/kun/kun-entityframeworkcore-multitenancy/optionsbasedtenantconnectionresolver.md) reads
a static tenant→connection-string map from [TenantsConnectionOptions](/sdk/api/kun/kun-entityframeworkcore-multitenancy/tenantsconnectionoptions.md).
Larger deployments typically swap in a custom implementation backed by a secrets store
or a tenant-metadata service.

## Methods

### ResolveConnectionString(string) \{#Kun_EntityFrameworkCore_MultiTenancy_IDbConnectionResolver_ResolveConnectionString_System_String\_}

Returns the connection string for `tenantId`.

```csharp
string ResolveConnectionString(string tenantId)
```

#### Parameters

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

#### Exceptions

[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

Thrown when no connection string is known for `tenantId`.
