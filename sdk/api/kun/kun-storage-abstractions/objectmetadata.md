> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class ObjectMetadata

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Storage)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Abstractions](/sdk/api/kun/kun-storage-abstractions/index.md)\
Assembly: Kun.Storage.dll

Metadata describing a stored object.

```csharp
public sealed record ObjectMetadata : IEquatable<ObjectMetadata>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ObjectMetadata](/sdk/api/kun/kun-storage-abstractions/objectmetadata.md)

#### Implements

[IEquatable\<ObjectMetadata>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ObjectMetadata(string, long, string?, DateTimeOffset, string?, IReadOnlyDictionary\<string, string>?) \{#Kun_Storage_Abstractions_ObjectMetadata__ctor_System_String_System_Int64_System_String_System_DateTimeOffset_System_String_System_Collections_Generic_IReadOnlyDictionary_System_String_System_String\_\_}

Metadata describing a stored object.

```csharp
public ObjectMetadata(string ContentType, long ContentLength, string? ETag, DateTimeOffset LastModified, string? ContentDisposition = null, IReadOnlyDictionary<string, string>? UserMetadata = null)
```

#### Parameters

`ContentType` [string](https://learn.microsoft.com/dotnet/api/system.string)

MIME type declared at upload time.

`ContentLength` [long](https://learn.microsoft.com/dotnet/api/system.int64)

Object size in bytes.

`ETag` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Backend-assigned entity tag, if any. Absent for backends that do not emit ETags.

`LastModified` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

UTC timestamp of the last successful write.

`ContentDisposition` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Stored `Content-Disposition`, including any attachment enforcement applied by the backend.

`UserMetadata` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>?

Arbitrary key/value pairs supplied at upload time.

## Properties

### ContentDisposition \{#Kun_Storage_Abstractions_ObjectMetadata_ContentDisposition}

Stored `Content-Disposition`, including any attachment enforcement applied by the backend.

```csharp
public string? ContentDisposition { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ContentLength \{#Kun_Storage_Abstractions_ObjectMetadata_ContentLength}

Object size in bytes.

```csharp
public long ContentLength { get; init; }
```

#### Property Value

[long](https://learn.microsoft.com/dotnet/api/system.int64)

### ContentType \{#Kun_Storage_Abstractions_ObjectMetadata_ContentType}

MIME type declared at upload time.

```csharp
public string ContentType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ETag \{#Kun_Storage_Abstractions_ObjectMetadata_ETag}

Backend-assigned entity tag, if any. Absent for backends that do not emit ETags.

```csharp
public string? ETag { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### LastModified \{#Kun_Storage_Abstractions_ObjectMetadata_LastModified}

UTC timestamp of the last successful write.

```csharp
public DateTimeOffset LastModified { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### UserMetadata \{#Kun_Storage_Abstractions_ObjectMetadata_UserMetadata}

Arbitrary key/value pairs supplied at upload time.

```csharp
public IReadOnlyDictionary<string, string>? UserMetadata { get; init; }
```

#### Property Value

[IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>?

## Methods

### \<Clone>$() \{#Kun_Storage_Abstractions_ObjectMetadata__Clone\_\_}

```csharp
public ObjectMetadata <Clone>$()
```

#### Returns

[ObjectMetadata](/sdk/api/kun/kun-storage-abstractions/objectmetadata.md)

### Deconstruct(out string, out long, out string?, out DateTimeOffset, out string?, out IReadOnlyDictionary\<string, string>?) \{#Kun_Storage_Abstractions_ObjectMetadata_Deconstruct_System_String__System_Int64__System_String__System_DateTimeOffset__System_String__System_Collections_Generic_IReadOnlyDictionary_System_String_System_String\_\_\_}

```csharp
public void Deconstruct(out string ContentType, out long ContentLength, out string? ETag, out DateTimeOffset LastModified, out string? ContentDisposition, out IReadOnlyDictionary<string, string>? UserMetadata)
```

#### Parameters

`ContentType` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ContentLength` [long](https://learn.microsoft.com/dotnet/api/system.int64)

`ETag` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`LastModified` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

`ContentDisposition` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`UserMetadata` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>?

### Equals(object?) \{#Kun_Storage_Abstractions_ObjectMetadata_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(ObjectMetadata?) \{#Kun_Storage_Abstractions_ObjectMetadata_Equals_Kun_Storage_Abstractions_ObjectMetadata\_}

```csharp
public bool Equals(ObjectMetadata? other)
```

#### Parameters

`other` [ObjectMetadata](/sdk/api/kun/kun-storage-abstractions/objectmetadata.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_Abstractions_ObjectMetadata_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_Abstractions_ObjectMetadata_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(ObjectMetadata?, ObjectMetadata?) \{#Kun_Storage_Abstractions_ObjectMetadata_op_Equality_Kun_Storage_Abstractions_ObjectMetadata_Kun_Storage_Abstractions_ObjectMetadata\_}

```csharp
public static bool operator ==(ObjectMetadata? left, ObjectMetadata? right)
```

#### Parameters

`left` [ObjectMetadata](/sdk/api/kun/kun-storage-abstractions/objectmetadata.md)?

`right` [ObjectMetadata](/sdk/api/kun/kun-storage-abstractions/objectmetadata.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(ObjectMetadata?, ObjectMetadata?) \{#Kun_Storage_Abstractions_ObjectMetadata_op_Inequality_Kun_Storage_Abstractions_ObjectMetadata_Kun_Storage_Abstractions_ObjectMetadata\_}

```csharp
public static bool operator !=(ObjectMetadata? left, ObjectMetadata? right)
```

#### Parameters

`left` [ObjectMetadata](/sdk/api/kun/kun-storage-abstractions/objectmetadata.md)?

`right` [ObjectMetadata](/sdk/api/kun/kun-storage-abstractions/objectmetadata.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
