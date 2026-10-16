> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class S3FileStorage

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Implementations](/pr-preview/pr-122/sdk/api/kun/kun-storage-implementations/index.md)\
Assembly: Kun.Storage.dll

Amazon S3 / MinIO-compatible implementation of [IFileStorage](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/ifilestorage.md).

```csharp
public sealed class S3FileStorage : IFileStorage
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[S3FileStorage](/pr-preview/pr-122/sdk/api/kun/kun-storage-implementations/s3filestorage.md)

#### Implements

[IFileStorage](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/ifilestorage.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### S3FileStorage(IAmazonS3, IOptions\<StorageOptions>, ILogger\<S3FileStorage>) \{#Kun_Storage_Implementations_S3FileStorage__ctor_Amazon_S3_IAmazonS3_Microsoft_Extensions_Options_IOptions_Kun_Storage_Abstractions_StorageOptions__Microsoft_Extensions_Logging_ILogger_Kun_Storage_Implementations_S3FileStorage\_\_}

Initializes a new instance.

```csharp
public S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options, ILogger<S3FileStorage> logger)
```

#### Parameters

`s3` IAmazonS3

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[StorageOptions](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/storageoptions.md)>

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[S3FileStorage](/pr-preview/pr-122/sdk/api/kun/kun-storage-implementations/s3filestorage.md)>

## Methods

### CreatePresignedDownloadUrlAsync(string, PresignedDownloadRequest, CancellationToken) \{#Kun_Storage_Implementations_S3FileStorage_CreatePresignedDownloadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedDownloadRequest_System_Threading_CancellationToken\_}

Generates a short-lived URL that the client can use to download an object directly.

```csharp
public Task<PresignedDownloadUrl> CreatePresignedDownloadUrlAsync(string path, PresignedDownloadRequest request, CancellationToken ct = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`request` [PresignedDownloadRequest](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/presigneddownloadrequest.md)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[PresignedDownloadUrl](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/presigneddownloadurl.md)>

#### Remarks

If the stored object's content-type is in [ContentDispositionPolicy](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/contentdispositionpolicy.md)'s
unsafe-for-inline-display list and the request did not already specify
[PresignedDownloadRequest.ResponseContentDisposition](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/presigneddownloadrequest.md#Kun_Storage_Abstractions_PresignedDownloadRequest_ResponseContentDisposition), implementations
MUST override the response disposition to force `attachment`.

### CreatePresignedUploadUrlAsync(string, PresignedUploadRequest, CancellationToken) \{#Kun_Storage_Implementations_S3FileStorage_CreatePresignedUploadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedUploadRequest_System_Threading_CancellationToken\_}

Generates a short-lived URL that the client can use to upload an object directly.

```csharp
public Task<PresignedUploadUrl> CreatePresignedUploadUrlAsync(string path, PresignedUploadRequest request, CancellationToken ct = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`request` [PresignedUploadRequest](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/presigneduploadrequest.md)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[PresignedUploadUrl](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/presigneduploadurl.md)>

#### Remarks

The returned URL carries [PresignedUploadUrl.RequiredHeaders](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/presigneduploadurl.md#Kun_Storage_Abstractions_PresignedUploadUrl_RequiredHeaders) that the caller
MUST forward to the storage backend so the pre-signature matches.

### DeleteObjectAsync(string, CancellationToken) \{#Kun_Storage_Implementations_S3FileStorage_DeleteObjectAsync_System_String_System_Threading_CancellationToken\_}

Deletes an object. No-op if the object does not exist.

```csharp
public Task DeleteObjectAsync(string path, CancellationToken ct = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### GetObjectAsync(string, CancellationToken) \{#Kun_Storage_Implementations_S3FileStorage_GetObjectAsync_System_String_System_Threading_CancellationToken\_}

Fetches an object by path.

```csharp
public Task<ObjectDownload> GetObjectAsync(string path, CancellationToken ct = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ObjectDownload](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/objectdownload.md)>

Content stream and metadata. Dispose to release the underlying stream.

#### Exceptions

[FileNotFoundException](https://learn.microsoft.com/dotnet/api/system.io.filenotfoundexception)

Thrown if the object does not exist.

### HeadObjectAsync(string, CancellationToken) \{#Kun_Storage_Implementations_S3FileStorage_HeadObjectAsync_System_String_System_Threading_CancellationToken\_}

Fetches metadata for an object without downloading its body.

```csharp
public Task<ObjectMetadata?> HeadObjectAsync(string path, CancellationToken ct = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ObjectMetadata](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/objectmetadata.md)?>

Metadata, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) if the object does not exist.

### ListObjectsAsync(string, CancellationToken) \{#Kun_Storage_Implementations_S3FileStorage_ListObjectsAsync_System_String_System_Threading_CancellationToken\_}

Enumerates objects whose path starts with `prefix`.

```csharp
public IAsyncEnumerable<ObjectSummary> ListObjectsAsync(string prefix, CancellationToken ct = default)
```

#### Parameters

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)

Logical path prefix. Use empty string to list the whole bucket/root.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[IAsyncEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.iasyncenumerable-1)\<[ObjectSummary](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/objectsummary.md)>

### PutObjectAsync(string, Stream, PutObjectOptions, CancellationToken) \{#Kun_Storage_Implementations_S3FileStorage_PutObjectAsync_System_String_System_IO_Stream_Kun_Storage_Abstractions_PutObjectOptions_System_Threading_CancellationToken\_}

Uploads an object to `path`.

```csharp
public Task PutObjectAsync(string path, Stream content, PutObjectOptions options, CancellationToken ct = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

Object key (logical path, forward-slash separated).

`content` [Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)

Object bytes. The stream is read from its current position to end.

`options` [PutObjectOptions](/pr-preview/pr-122/sdk/api/kun/kun-storage-abstractions/putobjectoptions.md)

Content type, disposition, cache-control, and user metadata.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
