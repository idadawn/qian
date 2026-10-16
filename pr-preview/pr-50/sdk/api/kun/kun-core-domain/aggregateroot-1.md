> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class AggregateRoot\<TKey>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Core)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

便捷重载：审计用户 ID 默认为 [`Guid`](https://learn.microsoft.com/dotnet/api/system.guid)。

```csharp
public abstract class AggregateRoot<TKey> : AggregateRoot<TKey, Guid>, IEquatable<Entity<TKey>>, IAggregateRoot<TKey>, IAggregateRoot, ISoftDelete, IAuditedObject<Guid>, IAuditedObject where TKey : notnull
```

#### Type Parameters

`TKey`

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[Entity\<TKey>](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/entity-1.md) ←
[AggregateRoot\<TKey, Guid>](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/aggregateroot-2.md) ←
[AggregateRoot\<TKey>](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/aggregateroot-1.md)

#### Implements

[IEquatable\<Entity\<TKey>>](https://learn.microsoft.com/dotnet/api/system.iequatable-1),
[IAggregateRoot\<TKey>](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/iaggregateroot-1.md),
[IAggregateRoot](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/iaggregateroot.md),
[ISoftDelete](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/isoftdelete.md),
[IAuditedObject\<Guid>](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/iauditedobject-1.md),
[IAuditedObject](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/iauditedobject.md)

#### Inherited Members

[AggregateRoot\<TKey, Guid>.AddDomainEvent(IDomainEvent)](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_AddDomainEvent_Kun_Core_Domain_IDomainEvent_),
[AggregateRoot\<TKey, Guid>.ClearDomainEvents()](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_ClearDomainEvents),
[AggregateRoot\<TKey, Guid>.Delete()](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_Delete),
[AggregateRoot\<TKey, Guid>.CreatedAt](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_CreatedAt),
[AggregateRoot\<TKey, Guid>.CreatedBy](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_CreatedBy),
[AggregateRoot\<TKey, Guid>.UpdatedAt](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_UpdatedAt),
[AggregateRoot\<TKey, Guid>.UpdatedBy](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_UpdatedBy),
[AggregateRoot\<TKey, Guid>.IsDeleted](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_IsDeleted),
[AggregateRoot\<TKey, Guid>.DeletedAt](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_DeletedAt),
[AggregateRoot\<TKey, Guid>.DomainEvents](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_DomainEvents),
[Entity\<TKey>.Equals(Entity\<TKey>?)](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Equals_Kun_Core_Domain_Entity__0__),
[Entity\<TKey>.Equals(object?)](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Equals_System_Object_),
[Entity\<TKey>.GetHashCode()](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_GetHashCode),
[Entity\<TKey>.Id](/pr-preview/pr-50/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Id),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### AggregateRoot() \{#Kun_Core_Domain_AggregateRoot_1__ctor}

```csharp
protected AggregateRoot()
```
