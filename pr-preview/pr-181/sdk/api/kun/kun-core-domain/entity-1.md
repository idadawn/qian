> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class Entity\<TKey>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

DDD 实体基类。实体的相等性由 [Entity\<TKey>.Id](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Id) 决定，而非属性集合（这是实体与值对象的核心区别）。

```csharp
public abstract class Entity<TKey> : IEquatable<Entity<TKey>> where TKey : notnull
```

#### Type Parameters

`TKey`

主键类型，通常为 [`Guid`](https://learn.microsoft.com/dotnet/api/system.guid)、[long](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/integral-numeric-types) 或 [string](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types)。

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[Entity\<TKey>](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/entity-1.md)

#### Implements

[IEquatable\<Entity\<TKey>>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### Entity() \{#Kun_Core_Domain_Entity_1__ctor}

```csharp
protected Entity()
```

## Properties

### Id \{#Kun_Core_Domain_Entity_1_Id}

Entity primary key.

```csharp
public TKey Id { get; protected set; }
```

#### Property Value

TKey

## Methods

### Equals(Entity\<TKey>?) \{#Kun_Core_Domain_Entity_1_Equals_Kun_Core_Domain_Entity__0\_\_}

Returns [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) if this entity has the same type and id as `other`.

```csharp
public bool Equals(Entity<TKey>? other)
```

#### Parameters

`other` [Entity](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/entity-1.md)\<TKey>?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(object?) \{#Kun_Core_Domain_Entity_1_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Core_Domain_Entity_1_GetHashCode}

Returns a hash code based on [Entity\<TKey>.Id](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Id).

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

## Operators

### operator ==(Entity\<TKey>?, Entity\<TKey>?) \{#Kun_Core_Domain_Entity_1_op_Equality_Kun_Core_Domain_Entity__0__Kun_Core_Domain_Entity__0\_\_}

Equality operator delegating to [Entity\<TKey>.Equals](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_Equals_Kun_Core_Domain_Entity__0__).

```csharp
public static bool operator ==(Entity<TKey>? left, Entity<TKey>? right)
```

#### Parameters

`left` [Entity](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/entity-1.md)\<TKey>?

`right` [Entity](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/entity-1.md)\<TKey>?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(Entity\<TKey>?, Entity\<TKey>?) \{#Kun_Core_Domain_Entity_1_op_Inequality_Kun_Core_Domain_Entity__0__Kun_Core_Domain_Entity__0\_\_}

Inequality operator delegating to [Entity\<TKey>.op_Equality](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/entity-1.md#Kun_Core_Domain_Entity_1_op_Equality_Kun_Core_Domain_Entity__0__Kun_Core_Domain_Entity__0__).

```csharp
public static bool operator !=(Entity<TKey>? left, Entity<TKey>? right)
```

#### Parameters

`left` [Entity](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/entity-1.md)\<TKey>?

`right` [Entity](/pr-preview/pr-181/sdk/api/kun/kun-core-domain/entity-1.md)\<TKey>?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
