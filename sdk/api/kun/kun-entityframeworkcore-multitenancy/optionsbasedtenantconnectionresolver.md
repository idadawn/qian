> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class OptionsBasedTenantConnectionResolver

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EntityFrameworkCore)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.MultiTenancy](/sdk/api/kun/kun-entityframeworkcore-multitenancy/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Default [IDbConnectionResolver](/sdk/api/kun/kun-entityframeworkcore-multitenancy/idbconnectionresolver.md) backed by [TenantsConnectionOptions](/sdk/api/kun/kun-entityframeworkcore-multitenancy/tenantsconnectionoptions.md).
Reads the current snapshot on every call via [`IOptionsMonitor`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1)
so on-the-fly config reloads reach new tenants without restarting the host.

```csharp
public sealed class OptionsBasedTenantConnectionResolver : IDbConnectionResolver
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OptionsBasedTenantConnectionResolver](/sdk/api/kun/kun-entityframeworkcore-multitenancy/optionsbasedtenantconnectionresolver.md)

#### Implements

[IDbConnectionResolver](/sdk/api/kun/kun-entityframeworkcore-multitenancy/idbconnectionresolver.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### OptionsBasedTenantConnectionResolver(IOptionsMonitor\<TenantsConnectionOptions>) \{#Kun_EntityFrameworkCore_MultiTenancy_OptionsBasedTenantConnectionResolver__ctor_Microsoft_Extensions_Options_IOptionsMonitor_Kun_EntityFrameworkCore_MultiTenancy_TenantsConnectionOptions\_\_}

Creates a new resolver.

```csharp
public OptionsBasedTenantConnectionResolver(IOptionsMonitor<TenantsConnectionOptions> options)
```

#### Parameters

`options` [IOptionsMonitor](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1)\<[TenantsConnectionOptions](/sdk/api/kun/kun-entityframeworkcore-multitenancy/tenantsconnectionoptions.md)>

## Methods

### ResolveConnectionString(string) \{#Kun_EntityFrameworkCore_MultiTenancy_OptionsBasedTenantConnectionResolver_ResolveConnectionString_System_String\_}

Returns the connection string for `tenantId`.

```csharp
public string ResolveConnectionString(string tenantId)
```

#### Parameters

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

#### Exceptions

[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

Thrown when no connection string is known for `tenantId`.
