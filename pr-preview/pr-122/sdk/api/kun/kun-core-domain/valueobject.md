> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class ValueObject

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

DDD 值对象基类。值对象没有独立标识，相等性由其所有组成属性决定。
值对象应当是不可变的——修改应返回新实例，而非改变现有实例。

```csharp
public abstract class ValueObject : IEquatable<ValueObject>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ValueObject](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/valueobject.md)

#### Implements

[IEquatable\<ValueObject>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ValueObject() \{#Kun_Core_Domain_ValueObject__ctor}

```csharp
protected ValueObject()
```

## Methods

### Equals(ValueObject?) \{#Kun_Core_Domain_ValueObject_Equals_Kun_Core_Domain_ValueObject\_}

Returns [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) when all equality components of both value objects are equal.

```csharp
public bool Equals(ValueObject? other)
```

#### Parameters

`other` [ValueObject](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/valueobject.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(object?) \{#Kun_Core_Domain_ValueObject_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetEqualityComponents() \{#Kun_Core_Domain_ValueObject_GetEqualityComponents}

返回参与相等性比较的所有组成部分。子类必须覆盖此方法，yield return 每个组成字段。

```csharp
protected abstract IEnumerable<object?> GetEqualityComponents()
```

#### Returns

[IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<[object](https://learn.microsoft.com/dotnet/api/system.object)?>

### GetHashCode() \{#Kun_Core_Domain_ValueObject_GetHashCode}

Returns a hash code derived from all equality components.

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

## Operators

### operator ==(ValueObject?, ValueObject?) \{#Kun_Core_Domain_ValueObject_op_Equality_Kun_Core_Domain_ValueObject_Kun_Core_Domain_ValueObject\_}

Equality operator delegating to [ValueObject.Equals](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/valueobject.md#Kun_Core_Domain_ValueObject_Equals_Kun_Core_Domain_ValueObject_).

```csharp
public static bool operator ==(ValueObject? left, ValueObject? right)
```

#### Parameters

`left` [ValueObject](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/valueobject.md)?

`right` [ValueObject](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/valueobject.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(ValueObject?, ValueObject?) \{#Kun_Core_Domain_ValueObject_op_Inequality_Kun_Core_Domain_ValueObject_Kun_Core_Domain_ValueObject\_}

Inequality operator delegating to [ValueObject.op_Equality](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/valueobject.md#Kun_Core_Domain_ValueObject_op_Equality_Kun_Core_Domain_ValueObject_Kun_Core_Domain_ValueObject_).

```csharp
public static bool operator !=(ValueObject? left, ValueObject? right)
```

#### Parameters

`left` [ValueObject](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/valueobject.md)?

`right` [ValueObject](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/valueobject.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
