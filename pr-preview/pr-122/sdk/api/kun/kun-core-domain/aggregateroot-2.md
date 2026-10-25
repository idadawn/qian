> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class AggregateRoot\<TKey, TUserKey>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

DDD 聚合根基类。

聚合根是领域一致性边界的持有者，所有外部修改必须通过聚合根的公开方法进行。
持久化细节（ORM 属性、表映射）一律不在此类出现——它们属于基础设施层的职责。

```csharp
public abstract class AggregateRoot<TKey, TUserKey> : Entity<TKey>, IEquatable<Entity<TKey>>, IAggregateRoot<TKey>, IAggregateRoot, ISoftDelete, IAuditedObject<TUserKey>, IAuditedObject where TKey : notnull
```

#### Type Parameters

`TKey`

主键类型。

`TUserKey`

审计用户 ID 类型，支持 [`Guid`](https://learn.microsoft.com/dotnet/api/system.guid)、[string](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types)、[long](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/integral-numeric-types)。

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[Entity\<TKey>](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/entity-1.md) ←
[AggregateRoot\<TKey, TUserKey>](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/aggregateroot-2.md)

#### Implements

[IEquatable\<Entity\<TKey>>](https://learn.microsoft.com/dotnet/api/system.iequatable-1),
[IAggregateRoot\<TKey>](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/iaggregateroot-1.md),
[IAggregateRoot](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/iaggregateroot.md),
[ISoftDelete](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/isoftdelete.md),
[IAuditedObject\<TUserKey>](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/iauditedobject-1.md),
[IAuditedObject](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/iauditedobject.md)

#### Inherited Members

[Entity\<TKey>.Equals(Entity\<TKey>?)](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Equals_Kun_Core_Domain_Entity__0__),
[Entity\<TKey>.Equals(object?)](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Equals_System_Object_),
[Entity\<TKey>.GetHashCode()](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_GetHashCode),
[Entity\<TKey>.Id](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Id),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### AggregateRoot() \{#Kun_Core_Domain_AggregateRoot_2__ctor}

```csharp
protected AggregateRoot()
```

## Properties

### CreatedAt \{#Kun_Core_Domain_AggregateRoot_2_CreatedAt}

UTC timestamp when the aggregate was created.

```csharp
public DateTime CreatedAt { get; protected set; }
```

#### Property Value

[DateTime](https://learn.microsoft.com/dotnet/api/system.datetime)

### CreatedBy \{#Kun_Core_Domain_AggregateRoot_2_CreatedBy}

Identity of the user who created the aggregate.

```csharp
public TUserKey? CreatedBy { get; protected set; }
```

#### Property Value

TUserKey?

### DeletedAt \{#Kun_Core_Domain_AggregateRoot_2_DeletedAt}

UTC timestamp when the aggregate was soft-deleted; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) if not deleted.

```csharp
public DateTime? DeletedAt { get; protected set; }
```

#### Property Value

[DateTime](https://learn.microsoft.com/dotnet/api/system.datetime)?

### DomainEvents \{#Kun_Core_Domain_AggregateRoot_2_DomainEvents}

Domain events raised by this aggregate since the last [AggregateRoot\<TKey, TUserKey>.ClearDomainEvents](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/aggregateroot-2.md#Kun_Core_Domain_AggregateRoot_2_ClearDomainEvents) call.

```csharp
public IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
```

#### Property Value

[IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)\<[IDomainEvent](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/idomainevent.md)>

### IsDeleted \{#Kun_Core_Domain_AggregateRoot_2_IsDeleted}

[true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) when the aggregate has been soft-deleted.

```csharp
public bool IsDeleted { get; protected set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### UpdatedAt \{#Kun_Core_Domain_AggregateRoot_2_UpdatedAt}

UTC timestamp of the last update; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) if never updated.

```csharp
public DateTime? UpdatedAt { get; protected set; }
```

#### Property Value

[DateTime](https://learn.microsoft.com/dotnet/api/system.datetime)?

### UpdatedBy \{#Kun_Core_Domain_AggregateRoot_2_UpdatedBy}

Identity of the user who last updated the aggregate.

```csharp
public TUserKey? UpdatedBy { get; protected set; }
```

#### Property Value

TUserKey?

## Methods

### AddDomainEvent(IDomainEvent) \{#Kun_Core_Domain_AggregateRoot_2_AddDomainEvent_Kun_Core_Domain_IDomainEvent\_}

在聚合根内部产生的业务变更添加一个领域事件。

```csharp
protected void AddDomainEvent(IDomainEvent domainEvent)
```

#### Parameters

`domainEvent` [IDomainEvent](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/idomainevent.md)

### ClearDomainEvents() \{#Kun_Core_Domain_AggregateRoot_2_ClearDomainEvents}

由 UoW 在持久化完成后调用。

```csharp
public void ClearDomainEvents()
```

### Delete() \{#Kun_Core_Domain_AggregateRoot_2_Delete}

软删除。子类可 override 追加领域事件（如 `ProductDeactivatedEvent`）。

```csharp
public virtual void Delete()
```
