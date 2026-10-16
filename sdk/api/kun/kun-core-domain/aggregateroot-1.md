> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class AggregateRoot\<TKey>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

便捷重载：审计用户 ID 默认为 [`Guid`](https://learn.microsoft.com/dotnet/api/system.guid)。

```csharp
public abstract class AggregateRoot<TKey> : AggregateRoot<TKey, Guid>, IEquatable<Entity<TKey>>, IAggregateRoot<TKey>, IAggregateRoot, ISoftDelete, IAuditedObject<Guid>, IAuditedObject where TKey : notnull
```

#### Type Parameters

`TKey`

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[Entity\<TKey>](/sdk/api/kun/kun-core-domain/entity-1.md) ←
[AggregateRoot\<TKey, Guid>](/sdk/api/kun/kun-core-domain/aggregateroot-2.md) ←
[AggregateRoot\<TKey>](/sdk/api/kun/kun-core-domain/aggregateroot-1.md)

#### Implements

[IEquatable\<Entity\<TKey>>](https://learn.microsoft.com/dotnet/api/system.iequatable-1),
[IAggregateRoot\<TKey>](/sdk/api/kun/kun-core-domain/iaggregateroot-1.md),
[IAggregateRoot](/sdk/api/kun/kun-core-domain/iaggregateroot.md),
[ISoftDelete](/sdk/api/kun/kun-core-domain/isoftdelete.md),
[IAuditedObject\<Guid>](/sdk/api/kun/kun-core-domain/iauditedobject-1.md),
[IAuditedObject](/sdk/api/kun/kun-core-domain/iauditedobject.md)

#### Inherited Members

[AggregateRoot\<TKey, Guid>.AddDomainEvent(IDomainEvent)](/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_AddDomainEvent_Kun_Core_Domain_IDomainEvent_),
[AggregateRoot\<TKey, Guid>.ClearDomainEvents()](/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_ClearDomainEvents),
[AggregateRoot\<TKey, Guid>.Delete()](/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_Delete),
[AggregateRoot\<TKey, Guid>.CreatedAt](/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_CreatedAt),
[AggregateRoot\<TKey, Guid>.CreatedBy](/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_CreatedBy),
[AggregateRoot\<TKey, Guid>.UpdatedAt](/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_UpdatedAt),
[AggregateRoot\<TKey, Guid>.UpdatedBy](/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_UpdatedBy),
[AggregateRoot\<TKey, Guid>.IsDeleted](/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_IsDeleted),
[AggregateRoot\<TKey, Guid>.DeletedAt](/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_DeletedAt),
[AggregateRoot\<TKey, Guid>.DomainEvents](/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_DomainEvents),
[Entity\<TKey>.Equals(Entity\<TKey>?)](/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Equals_Kun_Core_Domain_Entity__0__),
[Entity\<TKey>.Equals(object?)](/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Equals_System_Object_),
[Entity\<TKey>.GetHashCode()](/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_GetHashCode),
[Entity\<TKey>.Id](/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Id),
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
