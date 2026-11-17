> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class ObjectMetadata

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Abstractions` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Abstractions`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Storage.Abstractions)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage](/pr-preview/pr-50/sdk/api/kun/kun-storage/index.md)\
Assembly: Kun.Storage.Abstractions.dll

Metadata describing a stored object.

```csharp
public sealed record ObjectMetadata : IEquatable<ObjectMetadata>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ObjectMetadata](/pr-preview/pr-50/sdk/api/kun/kun-storage/objectmetadata.md)

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

### ObjectMetadata(string, long, string?, string?, DateTimeOffset) \{#Kun_Storage_ObjectMetadata__ctor_System_String_System_Int64_System_String_System_String_System_DateTimeOffset\_}

Metadata describing a stored object.

```csharp
public ObjectMetadata(string Key, long Size, string? ContentType, string? ETag, DateTimeOffset LastModified)
```

#### Parameters

`Key` [string](https://learn.microsoft.com/dotnet/api/system.string)

Object key (store-relative path).

`Size` [long](https://learn.microsoft.com/dotnet/api/system.int64)

Object size in bytes.

`ContentType` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Declared MIME type (e.g. `image/png`, `application/pdf`). **Nullable** —
[IFileStorage.ListAsync](/pr-preview/pr-50/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_ListAsync_System_String_System_Int32_System_String_System_Threading_CancellationToken_) results leave this [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) on
backends whose listing API omits content type (e.g. S3 `ListObjectsV2`).
[IFileStorage.GetMetadataAsync](/pr-preview/pr-50/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_GetMetadataAsync_System_String_System_Threading_CancellationToken_) and [IFileStorage.UploadAsync](/pr-preview/pr-50/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_UploadAsync_System_String_System_IO_Stream_System_String_Kun_Storage_UploadConstraints_System_Threading_CancellationToken_)
always populate it. Consumers iterating listings that need the content type should
follow up with [IFileStorage.GetMetadataAsync](/pr-preview/pr-50/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_GetMetadataAsync_System_String_System_Threading_CancellationToken_) on the items that matter.

`ETag` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Content-hash or version ETag supplied by the backing store.

`LastModified` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

Last modification timestamp reported by the store.

## Properties

### ContentType \{#Kun_Storage_ObjectMetadata_ContentType}

Declared MIME type (e.g. `image/png`, `application/pdf`). **Nullable** —
[IFileStorage.ListAsync](/pr-preview/pr-50/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_ListAsync_System_String_System_Int32_System_String_System_Threading_CancellationToken_) results leave this [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) on
backends whose listing API omits content type (e.g. S3 `ListObjectsV2`).
[IFileStorage.GetMetadataAsync](/pr-preview/pr-50/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_GetMetadataAsync_System_String_System_Threading_CancellationToken_) and [IFileStorage.UploadAsync](/pr-preview/pr-50/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_UploadAsync_System_String_System_IO_Stream_System_String_Kun_Storage_UploadConstraints_System_Threading_CancellationToken_)
always populate it. Consumers iterating listings that need the content type should
follow up with [IFileStorage.GetMetadataAsync](/pr-preview/pr-50/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_GetMetadataAsync_System_String_System_Threading_CancellationToken_) on the items that matter.

```csharp
public string? ContentType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ETag \{#Kun_Storage_ObjectMetadata_ETag}

Content-hash or version ETag supplied by the backing store.

```csharp
public string? ETag { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Key \{#Kun_Storage_ObjectMetadata_Key}

Object key (store-relative path).

```csharp
public string Key { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### LastModified \{#Kun_Storage_ObjectMetadata_LastModified}

Last modification timestamp reported by the store.

```csharp
public DateTimeOffset LastModified { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Size \{#Kun_Storage_ObjectMetadata_Size}

Object size in bytes.

```csharp
public long Size { get; init; }
```

#### Property Value

[long](https://learn.microsoft.com/dotnet/api/system.int64)

## Methods

### \<Clone>$() \{#Kun_Storage_ObjectMetadata__Clone\_\_}

```csharp
public ObjectMetadata <Clone>$()
```

#### Returns

[ObjectMetadata](/pr-preview/pr-50/sdk/api/kun/kun-storage/objectmetadata.md)

### Deconstruct(out string, out long, out string?, out string?, out DateTimeOffset) \{#Kun_Storage_ObjectMetadata_Deconstruct_System_String__System_Int64__System_String__System_String__System_DateTimeOffset\_\_}

```csharp
public void Deconstruct(out string Key, out long Size, out string? ContentType, out string? ETag, out DateTimeOffset LastModified)
```

#### Parameters

`Key` [string](https://learn.microsoft.com/dotnet/api/system.string)

`Size` [long](https://learn.microsoft.com/dotnet/api/system.int64)

`ContentType` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`ETag` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`LastModified` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Equals(object?) \{#Kun_Storage_ObjectMetadata_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(ObjectMetadata?) \{#Kun_Storage_ObjectMetadata_Equals_Kun_Storage_ObjectMetadata\_}

```csharp
public bool Equals(ObjectMetadata? other)
```

#### Parameters

`other` [ObjectMetadata](/pr-preview/pr-50/sdk/api/kun/kun-storage/objectmetadata.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_ObjectMetadata_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_ObjectMetadata_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(ObjectMetadata?, ObjectMetadata?) \{#Kun_Storage_ObjectMetadata_op_Equality_Kun_Storage_ObjectMetadata_Kun_Storage_ObjectMetadata\_}

```csharp
public static bool operator ==(ObjectMetadata? left, ObjectMetadata? right)
```

#### Parameters

`left` [ObjectMetadata](/pr-preview/pr-50/sdk/api/kun/kun-storage/objectmetadata.md)?

`right` [ObjectMetadata](/pr-preview/pr-50/sdk/api/kun/kun-storage/objectmetadata.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(ObjectMetadata?, ObjectMetadata?) \{#Kun_Storage_ObjectMetadata_op_Inequality_Kun_Storage_ObjectMetadata_Kun_Storage_ObjectMetadata\_}

```csharp
public static bool operator !=(ObjectMetadata? left, ObjectMetadata? right)
```

#### Parameters

`left` [ObjectMetadata](/pr-preview/pr-50/sdk/api/kun/kun-storage/objectmetadata.md)?

`right` [ObjectMetadata](/pr-preview/pr-50/sdk/api/kun/kun-storage/objectmetadata.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
