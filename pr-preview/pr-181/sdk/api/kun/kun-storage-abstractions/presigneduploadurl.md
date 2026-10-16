> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class PresignedUploadUrl

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/index.md)\
Assembly: Kun.Storage.dll

Result of
[IFileStorage.CreatePresignedUploadUrlAsync](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_CreatePresignedUploadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedUploadRequest_System_Threading_CancellationToken_).

```csharp
public sealed record PresignedUploadUrl : IEquatable<PresignedUploadUrl>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PresignedUploadUrl](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/presigneduploadurl.md)

#### Implements

[IEquatable\<PresignedUploadUrl>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PresignedUploadUrl(Uri, string, IReadOnlyDictionary\<string, string>, DateTimeOffset) \{#Kun_Storage_Abstractions_PresignedUploadUrl__ctor_System_Uri_System_String_System_Collections_Generic_IReadOnlyDictionary_System_String_System_String__System_DateTimeOffset\_}

Result of
[IFileStorage.CreatePresignedUploadUrlAsync](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_CreatePresignedUploadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedUploadRequest_System_Threading_CancellationToken_).

```csharp
public PresignedUploadUrl(Uri Url, string HttpMethod, IReadOnlyDictionary<string, string> RequiredHeaders, DateTimeOffset ExpiresAt)
```

#### Parameters

`Url` Uri

Absolute URL the client should call to upload the object.

`HttpMethod` [string](https://learn.microsoft.com/dotnet/api/system.string)

HTTP method to use (usually `PUT`).

`RequiredHeaders` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>

Headers the client MUST send with the upload request. Signatures are computed over these values,
so omitting or altering them will cause the backend to reject the upload.

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

Absolute UTC time at which the URL stops being accepted.

## Properties

### ExpiresAt \{#Kun_Storage_Abstractions_PresignedUploadUrl_ExpiresAt}

Absolute UTC time at which the URL stops being accepted.

```csharp
public DateTimeOffset ExpiresAt { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### HttpMethod \{#Kun_Storage_Abstractions_PresignedUploadUrl_HttpMethod}

HTTP method to use (usually `PUT`).

```csharp
public string HttpMethod { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### RequiredHeaders \{#Kun_Storage_Abstractions_PresignedUploadUrl_RequiredHeaders}

Headers the client MUST send with the upload request. Signatures are computed over these values,
so omitting or altering them will cause the backend to reject the upload.

```csharp
public IReadOnlyDictionary<string, string> RequiredHeaders { get; init; }
```

#### Property Value

[IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>

### Url \{#Kun_Storage_Abstractions_PresignedUploadUrl_Url}

Absolute URL the client should call to upload the object.

```csharp
public Uri Url { get; init; }
```

#### Property Value

Uri

## Methods

### \<Clone>$() \{#Kun_Storage_Abstractions_PresignedUploadUrl__Clone\_\_}

```csharp
public PresignedUploadUrl <Clone>$()
```

#### Returns

[PresignedUploadUrl](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/presigneduploadurl.md)

### Deconstruct(out Uri, out string, out IReadOnlyDictionary\<string, string>, out DateTimeOffset) \{#Kun_Storage_Abstractions_PresignedUploadUrl_Deconstruct_System_Uri__System_String__System_Collections_Generic_IReadOnlyDictionary_System_String_System_String___System_DateTimeOffset\_\_}

```csharp
public void Deconstruct(out Uri Url, out string HttpMethod, out IReadOnlyDictionary<string, string> RequiredHeaders, out DateTimeOffset ExpiresAt)
```

#### Parameters

`Url` Uri

`HttpMethod` [string](https://learn.microsoft.com/dotnet/api/system.string)

`RequiredHeaders` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Equals(object?) \{#Kun_Storage_Abstractions_PresignedUploadUrl_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(PresignedUploadUrl?) \{#Kun_Storage_Abstractions_PresignedUploadUrl_Equals_Kun_Storage_Abstractions_PresignedUploadUrl\_}

```csharp
public bool Equals(PresignedUploadUrl? other)
```

#### Parameters

`other` [PresignedUploadUrl](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/presigneduploadurl.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_Abstractions_PresignedUploadUrl_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_Abstractions_PresignedUploadUrl_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(PresignedUploadUrl?, PresignedUploadUrl?) \{#Kun_Storage_Abstractions_PresignedUploadUrl_op_Equality_Kun_Storage_Abstractions_PresignedUploadUrl_Kun_Storage_Abstractions_PresignedUploadUrl\_}

```csharp
public static bool operator ==(PresignedUploadUrl? left, PresignedUploadUrl? right)
```

#### Parameters

`left` [PresignedUploadUrl](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/presigneduploadurl.md)?

`right` [PresignedUploadUrl](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/presigneduploadurl.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(PresignedUploadUrl?, PresignedUploadUrl?) \{#Kun_Storage_Abstractions_PresignedUploadUrl_op_Inequality_Kun_Storage_Abstractions_PresignedUploadUrl_Kun_Storage_Abstractions_PresignedUploadUrl\_}

```csharp
public static bool operator !=(PresignedUploadUrl? left, PresignedUploadUrl? right)
```

#### Parameters

`left` [PresignedUploadUrl](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/presigneduploadurl.md)?

`right` [PresignedUploadUrl](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/presigneduploadurl.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
