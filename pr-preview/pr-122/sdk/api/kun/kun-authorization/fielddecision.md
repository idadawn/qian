> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class FieldDecision

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Authorization` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Authorization`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Authorization)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Authorization](/pr-preview/pr-122/sdk/api/kun/kun-authorization/index.md)\
Assembly: Kun.Authorization.dll

字段可读、可写与需要脱敏的集合。

```csharp
public sealed record FieldDecision : IEquatable<FieldDecision>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[FieldDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/fielddecision.md)

#### Implements

[IEquatable\<FieldDecision>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### FieldDecision(IReadOnlyList\<string>, IReadOnlyList\<string>, IReadOnlyList\<string>) \{#Kun_Authorization_FieldDecision__ctor_System_Collections_Generic_IReadOnlyList_System_String__System_Collections_Generic_IReadOnlyList_System_String__System_Collections_Generic_IReadOnlyList_System_String\_\_}

字段可读、可写与需要脱敏的集合。

```csharp
public FieldDecision(IReadOnlyList<string> Readable, IReadOnlyList<string> Writable, IReadOnlyList<string> Masked)
```

#### Parameters

`Readable` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

`Writable` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

`Masked` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

## Properties

### Masked \{#Kun_Authorization_FieldDecision_Masked}

```csharp
public IReadOnlyList<string> Masked { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### Readable \{#Kun_Authorization_FieldDecision_Readable}

```csharp
public IReadOnlyList<string> Readable { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### Writable \{#Kun_Authorization_FieldDecision_Writable}

```csharp
public IReadOnlyList<string> Writable { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

## Methods

### \<Clone>$() \{#Kun_Authorization_FieldDecision__Clone\_\_}

```csharp
public FieldDecision <Clone>$()
```

#### Returns

[FieldDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/fielddecision.md)

### Deconstruct(out IReadOnlyList\<string>, out IReadOnlyList\<string>, out IReadOnlyList\<string>) \{#Kun_Authorization_FieldDecision_Deconstruct_System_Collections_Generic_IReadOnlyList_System_String___System_Collections_Generic_IReadOnlyList_System_String___System_Collections_Generic_IReadOnlyList_System_String\_\_\_}

```csharp
public void Deconstruct(out IReadOnlyList<string> Readable, out IReadOnlyList<string> Writable, out IReadOnlyList<string> Masked)
```

#### Parameters

`Readable` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

`Writable` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

`Masked` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### Equals(object?) \{#Kun_Authorization_FieldDecision_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(FieldDecision?) \{#Kun_Authorization_FieldDecision_Equals_Kun_Authorization_FieldDecision\_}

```csharp
public bool Equals(FieldDecision? other)
```

#### Parameters

`other` [FieldDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/fielddecision.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Authorization_FieldDecision_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Authorization_FieldDecision_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(FieldDecision?, FieldDecision?) \{#Kun_Authorization_FieldDecision_op_Equality_Kun_Authorization_FieldDecision_Kun_Authorization_FieldDecision\_}

```csharp
public static bool operator ==(FieldDecision? left, FieldDecision? right)
```

#### Parameters

`left` [FieldDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/fielddecision.md)?

`right` [FieldDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/fielddecision.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(FieldDecision?, FieldDecision?) \{#Kun_Authorization_FieldDecision_op_Inequality_Kun_Authorization_FieldDecision_Kun_Authorization_FieldDecision\_}

```csharp
public static bool operator !=(FieldDecision? left, FieldDecision? right)
```

#### Parameters

`left` [FieldDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/fielddecision.md)?

`right` [FieldDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/fielddecision.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
