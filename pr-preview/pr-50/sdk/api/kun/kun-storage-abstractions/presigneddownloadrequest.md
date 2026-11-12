> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class PresignedDownloadRequest

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Storage)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Abstractions](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/index.md)\
Assembly: Kun.Storage.dll

Parameters for
[IFileStorage.CreatePresignedDownloadUrlAsync](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_CreatePresignedDownloadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedDownloadRequest_System_Threading_CancellationToken_).

```csharp
public sealed record PresignedDownloadRequest : IEquatable<PresignedDownloadRequest>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PresignedDownloadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneddownloadrequest.md)

#### Implements

[IEquatable\<PresignedDownloadRequest>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PresignedDownloadRequest(TimeSpan, string?) \{#Kun_Storage_Abstractions_PresignedDownloadRequest__ctor_System_TimeSpan_System_String\_}

Parameters for
[IFileStorage.CreatePresignedDownloadUrlAsync](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_CreatePresignedDownloadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedDownloadRequest_System_Threading_CancellationToken_).

```csharp
public PresignedDownloadRequest(TimeSpan ExpiresIn, string? ResponseContentDisposition = null)
```

#### Parameters

`ExpiresIn` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

URL lifetime. Keep short for public-facing download links.

`ResponseContentDisposition` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Optional override for the response `Content-Disposition`. When not specified,
the backend still enforces [ContentDispositionPolicy](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/contentdispositionpolicy.md): active-web MIME types
(HTML/SVG/XML/JS/WASM) are served as `attachment` regardless of stored disposition.

## Properties

### ExpiresIn \{#Kun_Storage_Abstractions_PresignedDownloadRequest_ExpiresIn}

URL lifetime. Keep short for public-facing download links.

```csharp
public TimeSpan ExpiresIn { get; init; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### ResponseContentDisposition \{#Kun_Storage_Abstractions_PresignedDownloadRequest_ResponseContentDisposition}

Optional override for the response `Content-Disposition`. When not specified,
the backend still enforces [ContentDispositionPolicy](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/contentdispositionpolicy.md): active-web MIME types
(HTML/SVG/XML/JS/WASM) are served as `attachment` regardless of stored disposition.

```csharp
public string? ResponseContentDisposition { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

## Methods

### \<Clone>$() \{#Kun_Storage_Abstractions_PresignedDownloadRequest__Clone\_\_}

```csharp
public PresignedDownloadRequest <Clone>$()
```

#### Returns

[PresignedDownloadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneddownloadrequest.md)

### Deconstruct(out TimeSpan, out string?) \{#Kun_Storage_Abstractions_PresignedDownloadRequest_Deconstruct_System_TimeSpan__System_String\_\_}

```csharp
public void Deconstruct(out TimeSpan ExpiresIn, out string? ResponseContentDisposition)
```

#### Parameters

`ExpiresIn` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

`ResponseContentDisposition` [string](https://learn.microsoft.com/dotnet/api/system.string)?

### Equals(object?) \{#Kun_Storage_Abstractions_PresignedDownloadRequest_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(PresignedDownloadRequest?) \{#Kun_Storage_Abstractions_PresignedDownloadRequest_Equals_Kun_Storage_Abstractions_PresignedDownloadRequest\_}

```csharp
public bool Equals(PresignedDownloadRequest? other)
```

#### Parameters

`other` [PresignedDownloadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneddownloadrequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_Abstractions_PresignedDownloadRequest_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_Abstractions_PresignedDownloadRequest_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(PresignedDownloadRequest?, PresignedDownloadRequest?) \{#Kun_Storage_Abstractions_PresignedDownloadRequest_op_Equality_Kun_Storage_Abstractions_PresignedDownloadRequest_Kun_Storage_Abstractions_PresignedDownloadRequest\_}

```csharp
public static bool operator ==(PresignedDownloadRequest? left, PresignedDownloadRequest? right)
```

#### Parameters

`left` [PresignedDownloadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneddownloadrequest.md)?

`right` [PresignedDownloadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneddownloadrequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(PresignedDownloadRequest?, PresignedDownloadRequest?) \{#Kun_Storage_Abstractions_PresignedDownloadRequest_op_Inequality_Kun_Storage_Abstractions_PresignedDownloadRequest_Kun_Storage_Abstractions_PresignedDownloadRequest\_}

```csharp
public static bool operator !=(PresignedDownloadRequest? left, PresignedDownloadRequest? right)
```

#### Parameters

`left` [PresignedDownloadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneddownloadrequest.md)?

`right` [PresignedDownloadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneddownloadrequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
