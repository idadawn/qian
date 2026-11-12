> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class TenantsConnectionOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.EntityFrameworkCore)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.MultiTenancy](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Static tenant → connection-string mapping consumed by
[OptionsBasedTenantConnectionResolver](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/optionsbasedtenantconnectionresolver.md). Suitable for small deployments
where the full tenant list is known at configuration time (appsettings.json or
Kubernetes ConfigMap).

```csharp
public sealed class TenantsConnectionOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[TenantsConnectionOptions](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/tenantsconnectionoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

For scale, swap in a custom [IDbConnectionResolver](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore-multitenancy/idbconnectionresolver.md) implementation backed
by a secret manager + tenant-metadata service. See the P2-D Part 3 notes in
`docs/architecture/adr-p2d-multitenancy.md`.

## Constructors

### TenantsConnectionOptions() \{#Kun_EntityFrameworkCore_MultiTenancy_TenantsConnectionOptions__ctor}

```csharp
public TenantsConnectionOptions()
```

## Fields

### SectionName \{#Kun_EntityFrameworkCore_MultiTenancy_TenantsConnectionOptions_SectionName}

Config section name bound via `Configure<TenantsConnectionOptions>`.

```csharp
public const string SectionName = "Kun:MultiTenancy:Tenants"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Properties

### ConnectionStrings \{#Kun_EntityFrameworkCore_MultiTenancy_TenantsConnectionOptions_ConnectionStrings}

Map of tenant ID to full ADO.NET connection string. Keys are matched by the
[`Ordinal`](https://learn.microsoft.com/dotnet/api/system.stringcomparer.ordinal) comparer; consumers should use consistent
casing for tenant IDs across the system.

```csharp
public IDictionary<string, string> ConnectionStrings { get; set; }
```

#### Property Value

[IDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.idictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>
