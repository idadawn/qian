> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class OutboxMessageConfiguration

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.EntityFrameworkCore)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

EF Core mapping for [OutboxMessage](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md).

```csharp
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OutboxMessageConfiguration](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore/outboxmessageconfiguration.md)

#### Implements

[IEntityTypeConfiguration\<OutboxMessage>](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.ientitytypeconfiguration-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### OutboxMessageConfiguration() \{#Kun_EntityFrameworkCore_OutboxMessageConfiguration__ctor}

```csharp
public OutboxMessageConfiguration()
```

## Methods

### Configure(EntityTypeBuilder\<OutboxMessage>) \{#Kun_EntityFrameworkCore_OutboxMessageConfiguration_Configure_Microsoft_EntityFrameworkCore_Metadata_Builders_EntityTypeBuilder_Kun_EventBus_Outbox_OutboxMessage\_\_}

Configures the entity of type [OutboxMessage](/pr-preview/pr-50/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md).

```csharp
public void Configure(EntityTypeBuilder<OutboxMessage> builder)
```

#### Parameters

`builder` [EntityTypeBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.metadata.builders.entitytypebuilder-1)\<OutboxMessage>

The builder to be used to configure the entity type.
