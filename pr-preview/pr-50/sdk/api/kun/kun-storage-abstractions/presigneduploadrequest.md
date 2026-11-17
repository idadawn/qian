> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class PresignedUploadRequest

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Storage)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Abstractions](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/index.md)\
Assembly: Kun.Storage.dll

Parameters for
[IFileStorage.CreatePresignedUploadUrlAsync](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_CreatePresignedUploadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedUploadRequest_System_Threading_CancellationToken_).

```csharp
public sealed record PresignedUploadRequest : IEquatable<PresignedUploadRequest>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PresignedUploadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneduploadrequest.md)

#### Implements

[IEquatable\<PresignedUploadRequest>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PresignedUploadRequest(TimeSpan, string?, long?, long?) \{#Kun_Storage_Abstractions_PresignedUploadRequest__ctor_System_TimeSpan_System_String_System_Nullable_System_Int64__System_Nullable_System_Int64\_\_}

Parameters for
[IFileStorage.CreatePresignedUploadUrlAsync](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_CreatePresignedUploadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedUploadRequest_System_Threading_CancellationToken_).

```csharp
public PresignedUploadRequest(TimeSpan ExpiresIn, string? ContentType = null, long? ContentLengthMin = null, long? ContentLengthMax = null)
```

#### Parameters

`ExpiresIn` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

URL lifetime. Should be kept short (minutes, not hours).

`ContentType` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Expected content type. If the backend supports signing the Content-Type header
(S3 does), it will be bound into the signature.

`ContentLengthMin` [long](https://learn.microsoft.com/dotnet/api/system.int64)?

Optional lower bound on the object size.

`ContentLengthMax` [long](https://learn.microsoft.com/dotnet/api/system.int64)?

Optional upper bound on the object size.

## Properties

### ContentLengthMax \{#Kun_Storage_Abstractions_PresignedUploadRequest_ContentLengthMax}

Optional upper bound on the object size.

```csharp
public long? ContentLengthMax { get; init; }
```

#### Property Value

[long](https://learn.microsoft.com/dotnet/api/system.int64)?

### ContentLengthMin \{#Kun_Storage_Abstractions_PresignedUploadRequest_ContentLengthMin}

Optional lower bound on the object size.

```csharp
public long? ContentLengthMin { get; init; }
```

#### Property Value

[long](https://learn.microsoft.com/dotnet/api/system.int64)?

### ContentType \{#Kun_Storage_Abstractions_PresignedUploadRequest_ContentType}

Expected content type. If the backend supports signing the Content-Type header
(S3 does), it will be bound into the signature.

```csharp
public string? ContentType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ExpiresIn \{#Kun_Storage_Abstractions_PresignedUploadRequest_ExpiresIn}

URL lifetime. Should be kept short (minutes, not hours).

```csharp
public TimeSpan ExpiresIn { get; init; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

## Methods

### \<Clone>$() \{#Kun_Storage_Abstractions_PresignedUploadRequest__Clone\_\_}

```csharp
public PresignedUploadRequest <Clone>$()
```

#### Returns

[PresignedUploadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneduploadrequest.md)

### Deconstruct(out TimeSpan, out string?, out long?, out long?) \{#Kun_Storage_Abstractions_PresignedUploadRequest_Deconstruct_System_TimeSpan__System_String__System_Nullable_System_Int64___System_Nullable_System_Int64\_\_\_}

```csharp
public void Deconstruct(out TimeSpan ExpiresIn, out string? ContentType, out long? ContentLengthMin, out long? ContentLengthMax)
```

#### Parameters

`ExpiresIn` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

`ContentType` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`ContentLengthMin` [long](https://learn.microsoft.com/dotnet/api/system.int64)?

`ContentLengthMax` [long](https://learn.microsoft.com/dotnet/api/system.int64)?

### Equals(object?) \{#Kun_Storage_Abstractions_PresignedUploadRequest_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(PresignedUploadRequest?) \{#Kun_Storage_Abstractions_PresignedUploadRequest_Equals_Kun_Storage_Abstractions_PresignedUploadRequest\_}

```csharp
public bool Equals(PresignedUploadRequest? other)
```

#### Parameters

`other` [PresignedUploadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneduploadrequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_Abstractions_PresignedUploadRequest_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_Abstractions_PresignedUploadRequest_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(PresignedUploadRequest?, PresignedUploadRequest?) \{#Kun_Storage_Abstractions_PresignedUploadRequest_op_Equality_Kun_Storage_Abstractions_PresignedUploadRequest_Kun_Storage_Abstractions_PresignedUploadRequest\_}

```csharp
public static bool operator ==(PresignedUploadRequest? left, PresignedUploadRequest? right)
```

#### Parameters

`left` [PresignedUploadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneduploadrequest.md)?

`right` [PresignedUploadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneduploadrequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(PresignedUploadRequest?, PresignedUploadRequest?) \{#Kun_Storage_Abstractions_PresignedUploadRequest_op_Inequality_Kun_Storage_Abstractions_PresignedUploadRequest_Kun_Storage_Abstractions_PresignedUploadRequest\_}

```csharp
public static bool operator !=(PresignedUploadRequest? left, PresignedUploadRequest? right)
```

#### Parameters

`left` [PresignedUploadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneduploadrequest.md)?

`right` [PresignedUploadRequest](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/presigneduploadrequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
