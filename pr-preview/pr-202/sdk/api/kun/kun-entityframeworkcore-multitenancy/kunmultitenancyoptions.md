> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunMultiTenancyOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.MultiTenancy](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore-multitenancy/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Configuration for `AddKunMultiTenancy`. Selects the isolation strategy and tunes
the few knobs the framework exposes across all three strategies.

```csharp
public sealed class KunMultiTenancyOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunMultiTenancyOptions](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore-multitenancy/kunmultitenancyoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunMultiTenancyOptions() \{#Kun_EntityFrameworkCore_MultiTenancy_KunMultiTenancyOptions__ctor}

```csharp
public KunMultiTenancyOptions()
```

## Properties

### AllowDestructiveDeprovision \{#Kun_EntityFrameworkCore_MultiTenancy_KunMultiTenancyOptions_AllowDestructiveDeprovision}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), `SchemaPerTenantProvisioner.DeprovisionAsync` is
permitted to run `DROP SCHEMA IF EXISTS "{tenantId}" CASCADE`. Default is
[false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) — deprovisioning is a data-loss operation and must be an
explicit opt-in. When left at the default, `DeprovisionAsync` throws
[`NotSupportedException`](https://learn.microsoft.com/dotnet/api/system.notsupportedexception) with a remediation message.

```csharp
public bool AllowDestructiveDeprovision { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Strategy \{#Kun_EntityFrameworkCore_MultiTenancy_KunMultiTenancyOptions_Strategy}

The active isolation strategy. Defaults to [TenantIsolationStrategy.Shared](/pr-preview/pr-202/sdk/api/kun/kun-core-multitenancy/tenantisolationstrategy.md#Kun_Core_MultiTenancy_TenantIsolationStrategy_Shared).

```csharp
public TenantIsolationStrategy Strategy { get; set; }
```

#### Property Value

TenantIsolationStrategy
