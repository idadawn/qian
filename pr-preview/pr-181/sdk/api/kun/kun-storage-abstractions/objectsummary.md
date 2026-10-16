> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class ObjectSummary

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/index.md)\
Assembly: Kun.Storage.dll

Lightweight description of an object returned by
[IFileStorage.ListObjectsAsync](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_ListObjectsAsync_System_String_System_Threading_CancellationToken_).

```csharp
public sealed record ObjectSummary : IEquatable<ObjectSummary>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ObjectSummary](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/objectsummary.md)

#### Implements

[IEquatable\<ObjectSummary>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ObjectSummary(string, long, DateTimeOffset, string?) \{#Kun_Storage_Abstractions_ObjectSummary__ctor_System_String_System_Int64_System_DateTimeOffset_System_String\_}

Lightweight description of an object returned by
[IFileStorage.ListObjectsAsync](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_ListObjectsAsync_System_String_System_Threading_CancellationToken_).

```csharp
public ObjectSummary(string Path, long Size, DateTimeOffset LastModified, string? ETag = null)
```

#### Parameters

`Path` [string](https://learn.microsoft.com/dotnet/api/system.string)

Object key (logical path).

`Size` [long](https://learn.microsoft.com/dotnet/api/system.int64)

Object size in bytes.

`LastModified` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp of the last successful write.

`ETag` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Backend-assigned entity tag, if any.

## Properties

### ETag \{#Kun_Storage_Abstractions_ObjectSummary_ETag}

Backend-assigned entity tag, if any.

```csharp
public string? ETag { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### LastModified \{#Kun_Storage_Abstractions_ObjectSummary_LastModified}

UTC timestamp of the last successful write.

```csharp
public DateTimeOffset LastModified { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Path \{#Kun_Storage_Abstractions_ObjectSummary_Path}

Object key (logical path).

```csharp
public string Path { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Size \{#Kun_Storage_Abstractions_ObjectSummary_Size}

Object size in bytes.

```csharp
public long Size { get; init; }
```

#### Property Value

[long](https://learn.microsoft.com/dotnet/api/system.int64)

## Methods

### \<Clone>$() \{#Kun_Storage_Abstractions_ObjectSummary__Clone\_\_}

```csharp
public ObjectSummary <Clone>$()
```

#### Returns

[ObjectSummary](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/objectsummary.md)

### Deconstruct(out string, out long, out DateTimeOffset, out string?) \{#Kun_Storage_Abstractions_ObjectSummary_Deconstruct_System_String__System_Int64__System_DateTimeOffset__System_String\_\_}

```csharp
public void Deconstruct(out string Path, out long Size, out DateTimeOffset LastModified, out string? ETag)
```

#### Parameters

`Path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`Size` [long](https://learn.microsoft.com/dotnet/api/system.int64)

`LastModified` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

`ETag` [string](https://learn.microsoft.com/dotnet/api/system.string)?

### Equals(object?) \{#Kun_Storage_Abstractions_ObjectSummary_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(ObjectSummary?) \{#Kun_Storage_Abstractions_ObjectSummary_Equals_Kun_Storage_Abstractions_ObjectSummary\_}

```csharp
public bool Equals(ObjectSummary? other)
```

#### Parameters

`other` [ObjectSummary](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/objectsummary.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_Abstractions_ObjectSummary_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_Abstractions_ObjectSummary_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(ObjectSummary?, ObjectSummary?) \{#Kun_Storage_Abstractions_ObjectSummary_op_Equality_Kun_Storage_Abstractions_ObjectSummary_Kun_Storage_Abstractions_ObjectSummary\_}

```csharp
public static bool operator ==(ObjectSummary? left, ObjectSummary? right)
```

#### Parameters

`left` [ObjectSummary](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/objectsummary.md)?

`right` [ObjectSummary](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/objectsummary.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(ObjectSummary?, ObjectSummary?) \{#Kun_Storage_Abstractions_ObjectSummary_op_Inequality_Kun_Storage_Abstractions_ObjectSummary_Kun_Storage_Abstractions_ObjectSummary\_}

```csharp
public static bool operator !=(ObjectSummary? left, ObjectSummary? right)
```

#### Parameters

`left` [ObjectSummary](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/objectsummary.md)?

`right` [ObjectSummary](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/objectsummary.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
