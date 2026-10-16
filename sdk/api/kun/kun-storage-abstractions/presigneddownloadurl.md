> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class PresignedDownloadUrl

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Abstractions](/sdk/api/kun/kun-storage-abstractions/index.md)\
Assembly: Kun.Storage.dll

Result of
[IFileStorage.CreatePresignedDownloadUrlAsync](/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_CreatePresignedDownloadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedDownloadRequest_System_Threading_CancellationToken_).

```csharp
public sealed record PresignedDownloadUrl : IEquatable<PresignedDownloadUrl>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PresignedDownloadUrl](/sdk/api/kun/kun-storage-abstractions/presigneddownloadurl.md)

#### Implements

[IEquatable\<PresignedDownloadUrl>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PresignedDownloadUrl(Uri, DateTimeOffset) \{#Kun_Storage_Abstractions_PresignedDownloadUrl__ctor_System_Uri_System_DateTimeOffset\_}

Result of
[IFileStorage.CreatePresignedDownloadUrlAsync](/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_CreatePresignedDownloadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedDownloadRequest_System_Threading_CancellationToken_).

```csharp
public PresignedDownloadUrl(Uri Url, DateTimeOffset ExpiresAt)
```

#### Parameters

`Url` Uri

Absolute URL the client can `GET` to download the object.

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

Absolute UTC time at which the URL stops being accepted.

## Properties

### ExpiresAt \{#Kun_Storage_Abstractions_PresignedDownloadUrl_ExpiresAt}

Absolute UTC time at which the URL stops being accepted.

```csharp
public DateTimeOffset ExpiresAt { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Url \{#Kun_Storage_Abstractions_PresignedDownloadUrl_Url}

Absolute URL the client can `GET` to download the object.

```csharp
public Uri Url { get; init; }
```

#### Property Value

Uri

## Methods

### \<Clone>$() \{#Kun_Storage_Abstractions_PresignedDownloadUrl__Clone\_\_}

```csharp
public PresignedDownloadUrl <Clone>$()
```

#### Returns

[PresignedDownloadUrl](/sdk/api/kun/kun-storage-abstractions/presigneddownloadurl.md)

### Deconstruct(out Uri, out DateTimeOffset) \{#Kun_Storage_Abstractions_PresignedDownloadUrl_Deconstruct_System_Uri__System_DateTimeOffset\_\_}

```csharp
public void Deconstruct(out Uri Url, out DateTimeOffset ExpiresAt)
```

#### Parameters

`Url` Uri

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Equals(object?) \{#Kun_Storage_Abstractions_PresignedDownloadUrl_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(PresignedDownloadUrl?) \{#Kun_Storage_Abstractions_PresignedDownloadUrl_Equals_Kun_Storage_Abstractions_PresignedDownloadUrl\_}

```csharp
public bool Equals(PresignedDownloadUrl? other)
```

#### Parameters

`other` [PresignedDownloadUrl](/sdk/api/kun/kun-storage-abstractions/presigneddownloadurl.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_Abstractions_PresignedDownloadUrl_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_Abstractions_PresignedDownloadUrl_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(PresignedDownloadUrl?, PresignedDownloadUrl?) \{#Kun_Storage_Abstractions_PresignedDownloadUrl_op_Equality_Kun_Storage_Abstractions_PresignedDownloadUrl_Kun_Storage_Abstractions_PresignedDownloadUrl\_}

```csharp
public static bool operator ==(PresignedDownloadUrl? left, PresignedDownloadUrl? right)
```

#### Parameters

`left` [PresignedDownloadUrl](/sdk/api/kun/kun-storage-abstractions/presigneddownloadurl.md)?

`right` [PresignedDownloadUrl](/sdk/api/kun/kun-storage-abstractions/presigneddownloadurl.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(PresignedDownloadUrl?, PresignedDownloadUrl?) \{#Kun_Storage_Abstractions_PresignedDownloadUrl_op_Inequality_Kun_Storage_Abstractions_PresignedDownloadUrl_Kun_Storage_Abstractions_PresignedDownloadUrl\_}

```csharp
public static bool operator !=(PresignedDownloadUrl? left, PresignedDownloadUrl? right)
```

#### Parameters

`left` [PresignedDownloadUrl](/sdk/api/kun/kun-storage-abstractions/presigneddownloadurl.md)?

`right` [PresignedDownloadUrl](/sdk/api/kun/kun-storage-abstractions/presigneddownloadurl.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
