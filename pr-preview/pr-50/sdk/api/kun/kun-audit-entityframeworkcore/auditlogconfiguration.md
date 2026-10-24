> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class AuditLogConfiguration

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Audit.EntityFrameworkCore` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Audit.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Audit.EntityFrameworkCore)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Audit.EntityFrameworkCore](/pr-preview/pr-50/sdk/api/kun/kun-audit-entityframeworkcore/index.md)\
Assembly: Kun.Audit.EntityFrameworkCore.dll

EF Core mapping for [AuditLog](/pr-preview/pr-50/sdk/api/kun/kun-audit/auditlog.md): table name, column shapes,
query indexes, and the append-only convention (no concurrency token —
rows never update; no soft-delete — rows never delete).

```csharp
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuditLogConfiguration](/pr-preview/pr-50/sdk/api/kun/kun-audit-entityframeworkcore/auditlogconfiguration.md)

#### Implements

[IEntityTypeConfiguration\<AuditLog>](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.ientitytypeconfiguration-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### AuditLogConfiguration() \{#Kun_Audit_EntityFrameworkCore_AuditLogConfiguration__ctor}

```csharp
public AuditLogConfiguration()
```

## Methods

### Configure(EntityTypeBuilder\<AuditLog>) \{#Kun_Audit_EntityFrameworkCore_AuditLogConfiguration_Configure_Microsoft_EntityFrameworkCore_Metadata_Builders_EntityTypeBuilder_Kun_Audit_AuditLog\_\_}

Configures the entity of type [AuditLog](/pr-preview/pr-50/sdk/api/kun/kun-audit/auditlog.md).

```csharp
public void Configure(EntityTypeBuilder<AuditLog> builder)
```

#### Parameters

`builder` [EntityTypeBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.entitytypebuilder-1)\<AuditLog>

The builder to be used to configure the entity type.
