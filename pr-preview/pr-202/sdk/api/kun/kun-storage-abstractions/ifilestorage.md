> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface IFileStorage

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Abstractions](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/index.md)\
Assembly: Kun.Storage.dll

Abstraction for object-storage operations. Supports multiple backends
(local filesystem, S3, MinIO) behind a uniform contract.

```csharp
[Obsolete("请引用 Kun.Storage.Abstractions 包里的 Kun.Storage.IFileStorage；旧包仅保留兼容。", DiagnosticId = "KUNLEGACY001")]
public interface IFileStorage
```

## Remarks

Implementations must enforce [ContentDispositionPolicy](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/contentdispositionpolicy.md) on uploads
and presigned download URLs, so that browsers never render user-uploaded
active-web types (HTML, SVG, XML, JavaScript, WASM) inline.

## Methods

### CreatePresignedDownloadUrlAsync(string, PresignedDownloadRequest, CancellationToken) \{#Kun_Storage_Abstractions_IFileStorage_CreatePresignedDownloadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedDownloadRequest_System_Threading_CancellationToken\_}

Generates a short-lived URL that the client can use to download an object directly.

```csharp
Task<PresignedDownloadUrl> CreatePresignedDownloadUrlAsync(string path, PresignedDownloadRequest request, CancellationToken ct = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`request` [PresignedDownloadRequest](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/presigneddownloadrequest.md)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[PresignedDownloadUrl](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/presigneddownloadurl.md)>

#### Remarks

If the stored object's content-type is in [ContentDispositionPolicy](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/contentdispositionpolicy.md)'s
unsafe-for-inline-display list and the request did not already specify
[PresignedDownloadRequest.ResponseContentDisposition](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/presigneddownloadrequest.md#Kun_Storage_Abstractions_PresignedDownloadRequest_ResponseContentDisposition), implementations
MUST override the response disposition to force `attachment`.

### CreatePresignedUploadUrlAsync(string, PresignedUploadRequest, CancellationToken) \{#Kun_Storage_Abstractions_IFileStorage_CreatePresignedUploadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedUploadRequest_System_Threading_CancellationToken\_}

Generates a short-lived URL that the client can use to upload an object directly.

```csharp
Task<PresignedUploadUrl> CreatePresignedUploadUrlAsync(string path, PresignedUploadRequest request, CancellationToken ct = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`request` [PresignedUploadRequest](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/presigneduploadrequest.md)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[PresignedUploadUrl](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/presigneduploadurl.md)>

#### Remarks

The returned URL carries [PresignedUploadUrl.RequiredHeaders](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/presigneduploadurl.md#Kun_Storage_Abstractions_PresignedUploadUrl_RequiredHeaders) that the caller
MUST forward to the storage backend so the pre-signature matches.

### DeleteObjectAsync(string, CancellationToken) \{#Kun_Storage_Abstractions_IFileStorage_DeleteObjectAsync_System_String_System_Threading_CancellationToken\_}

Deletes an object. No-op if the object does not exist.

```csharp
Task DeleteObjectAsync(string path, CancellationToken ct = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### GetObjectAsync(string, CancellationToken) \{#Kun_Storage_Abstractions_IFileStorage_GetObjectAsync_System_String_System_Threading_CancellationToken\_}

Fetches an object by path.

```csharp
Task<ObjectDownload> GetObjectAsync(string path, CancellationToken ct = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ObjectDownload](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/objectdownload.md)>

Content stream and metadata. Dispose to release the underlying stream.

#### Exceptions

[FileNotFoundException](https://learn.microsoft.com/dotnet/api/system.io.filenotfoundexception)

Thrown if the object does not exist.

### HeadObjectAsync(string, CancellationToken) \{#Kun_Storage_Abstractions_IFileStorage_HeadObjectAsync_System_String_System_Threading_CancellationToken\_}

Fetches metadata for an object without downloading its body.

```csharp
Task<ObjectMetadata?> HeadObjectAsync(string path, CancellationToken ct = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ObjectMetadata](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/objectmetadata.md)?>

Metadata, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) if the object does not exist.

### ListObjectsAsync(string, CancellationToken) \{#Kun_Storage_Abstractions_IFileStorage_ListObjectsAsync_System_String_System_Threading_CancellationToken\_}

Enumerates objects whose path starts with `prefix`.

```csharp
IAsyncEnumerable<ObjectSummary> ListObjectsAsync(string prefix, CancellationToken ct = default)
```

#### Parameters

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)

Logical path prefix. Use empty string to list the whole bucket/root.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[IAsyncEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.iasyncenumerable-1)\<[ObjectSummary](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/objectsummary.md)>

### PutObjectAsync(string, Stream, PutObjectOptions, CancellationToken) \{#Kun_Storage_Abstractions_IFileStorage_PutObjectAsync_System_String_System_IO_Stream_Kun_Storage_Abstractions_PutObjectOptions_System_Threading_CancellationToken\_}

Uploads an object to `path`.

```csharp
Task PutObjectAsync(string path, Stream content, PutObjectOptions options, CancellationToken ct = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

Object key (logical path, forward-slash separated).

`content` [Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)

Object bytes. The stream is read from its current position to end.

`options` [PutObjectOptions](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/putobjectoptions.md)

Content type, disposition, cache-control, and user metadata.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
