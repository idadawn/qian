> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class ListObjectsResult

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Abstractions` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Abstractions`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Storage.Abstractions)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage](/sdk/api/kun/kun-storage/index.md)\
Assembly: Kun.Storage.Abstractions.dll

A page of objects returned by [IFileStorage.ListAsync](/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_ListAsync_System_String_System_Int32_System_String_System_Threading_CancellationToken_).

```csharp
public sealed record ListObjectsResult : IEquatable<ListObjectsResult>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ListObjectsResult](/sdk/api/kun/kun-storage/listobjectsresult.md)

#### Implements

[IEquatable\<ListObjectsResult>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ListObjectsResult(IReadOnlyList\<ObjectMetadata>, string?) \{#Kun_Storage_ListObjectsResult__ctor_System_Collections_Generic_IReadOnlyList_Kun_Storage_ObjectMetadata__System_String\_}

A page of objects returned by [IFileStorage.ListAsync](/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_ListAsync_System_String_System_Int32_System_String_System_Threading_CancellationToken_).

```csharp
public ListObjectsResult(IReadOnlyList<ObjectMetadata> Objects, string? ContinuationToken)
```

#### Parameters

`Objects` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[ObjectMetadata](/sdk/api/kun/kun-storage/objectmetadata.md)>

The objects in this page.

`ContinuationToken` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Opaque token to pass to the next `ListAsync` call to fetch the next page, or
`null` when the current page is the last.

## Properties

### ContinuationToken \{#Kun_Storage_ListObjectsResult_ContinuationToken}

Opaque token to pass to the next `ListAsync` call to fetch the next page, or
`null` when the current page is the last.

```csharp
public string? ContinuationToken { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Objects \{#Kun_Storage_ListObjectsResult_Objects}

The objects in this page.

```csharp
public IReadOnlyList<ObjectMetadata> Objects { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[ObjectMetadata](/sdk/api/kun/kun-storage/objectmetadata.md)>

## Methods

### \<Clone>$() \{#Kun_Storage_ListObjectsResult__Clone\_\_}

```csharp
public ListObjectsResult <Clone>$()
```

#### Returns

[ListObjectsResult](/sdk/api/kun/kun-storage/listobjectsresult.md)

### Deconstruct(out IReadOnlyList\<ObjectMetadata>, out string?) \{#Kun_Storage_ListObjectsResult_Deconstruct_System_Collections_Generic_IReadOnlyList_Kun_Storage_ObjectMetadata___System_String\_\_}

```csharp
public void Deconstruct(out IReadOnlyList<ObjectMetadata> Objects, out string? ContinuationToken)
```

#### Parameters

`Objects` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[ObjectMetadata](/sdk/api/kun/kun-storage/objectmetadata.md)>

`ContinuationToken` [string](https://learn.microsoft.com/dotnet/api/system.string)?

### Equals(object?) \{#Kun_Storage_ListObjectsResult_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(ListObjectsResult?) \{#Kun_Storage_ListObjectsResult_Equals_Kun_Storage_ListObjectsResult\_}

```csharp
public bool Equals(ListObjectsResult? other)
```

#### Parameters

`other` [ListObjectsResult](/sdk/api/kun/kun-storage/listobjectsresult.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_ListObjectsResult_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_ListObjectsResult_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(ListObjectsResult?, ListObjectsResult?) \{#Kun_Storage_ListObjectsResult_op_Equality_Kun_Storage_ListObjectsResult_Kun_Storage_ListObjectsResult\_}

```csharp
public static bool operator ==(ListObjectsResult? left, ListObjectsResult? right)
```

#### Parameters

`left` [ListObjectsResult](/sdk/api/kun/kun-storage/listobjectsresult.md)?

`right` [ListObjectsResult](/sdk/api/kun/kun-storage/listobjectsresult.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(ListObjectsResult?, ListObjectsResult?) \{#Kun_Storage_ListObjectsResult_op_Inequality_Kun_Storage_ListObjectsResult_Kun_Storage_ListObjectsResult\_}

```csharp
public static bool operator !=(ListObjectsResult? left, ListObjectsResult? right)
```

#### Parameters

`left` [ListObjectsResult](/sdk/api/kun/kun-storage/listobjectsresult.md)?

`right` [ListObjectsResult](/sdk/api/kun/kun-storage/listobjectsresult.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
