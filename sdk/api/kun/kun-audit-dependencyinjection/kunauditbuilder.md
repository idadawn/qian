> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunAuditBuilder

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Audit)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit.DependencyInjection](/sdk/api/kun/kun-audit-dependencyinjection/index.md)\
Assembly: Kun.Audit.dll

Fluent builder returned by `AddKunAudit`. Extension methods on
this type register concrete `IAuditWriter` backends without
forcing consumers to call multiple top-level `Add*` methods.

```csharp
public sealed class KunAuditBuilder
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunAuditBuilder](/sdk/api/kun/kun-audit-dependencyinjection/kunauditbuilder.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

#### Extension Methods

[KunAuditServiceCollectionExtensions.UseEventBus(KunAuditBuilder)](/sdk/api/kun/kun-audit-dependencyinjection/kunauditservicecollectionextensions.md#Kun_Audit_DependencyInjection_KunAuditServiceCollectionExtensions_UseEventBus_Kun_Audit_DependencyInjection_KunAuditBuilder_)

## Constructors

### KunAuditBuilder(IServiceCollection) \{#Kun_Audit_DependencyInjection_KunAuditBuilder__ctor_Microsoft_Extensions_DependencyInjection_IServiceCollection\_}

Creates a builder bound to `services`.

```csharp
public KunAuditBuilder(IServiceCollection services)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

## Properties

### Services \{#Kun_Audit_DependencyInjection_KunAuditBuilder_Services}

The service collection being configured.

```csharp
public IServiceCollection Services { get; }
```

#### Property Value

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
