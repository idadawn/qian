> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class S3FileStorage

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.S3` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.S3`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage.S3)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.S3](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/index.md)\
Assembly: Kun.Storage.S3.dll

Amazon S3 (and S3-compatible: MinIO, Ceph, LocalStack) implementation of [IFileStorage](/pr-preview/pr-181/sdk/api/kun/kun-storage/ifilestorage.md).

```csharp
public sealed class S3FileStorage : IFileStorage
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[S3FileStorage](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/s3filestorage.md)

#### Implements

IFileStorage

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

This type's critical security guarantee: [S3FileStorage.GetPresignedDownloadAsync](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/s3filestorage.md#Kun_Storage_S3_S3FileStorage_GetPresignedDownloadAsync_System_String_Kun_Storage_DownloadOptions_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_) ALWAYS
sets `Content-Disposition: attachment; filename="…"` on the signed URL when the
stored object's content type is one of the four active web content types enumerated in
[ActiveWebContentTypes.Value](/pr-preview/pr-181/sdk/api/kun/kun-storage/activewebcontenttypes.md#Kun_Storage_ActiveWebContentTypes_Value) — `text/html`, `image/svg+xml`,
`application/xhtml+xml`, `application/xml`. This holds regardless of whether
the caller sets [DownloadOptions.ForceDownload](/pr-preview/pr-181/sdk/api/kun/kun-storage/downloadoptions.md#Kun_Storage_DownloadOptions_ForceDownload).

## Constructors

### S3FileStorage(IAmazonS3, IOptions\<S3StorageOptions>, ILogger\<S3FileStorage>, TimeProvider?) \{#Kun_Storage_S3_S3FileStorage__ctor_Amazon_S3_IAmazonS3_Microsoft_Extensions_Options_IOptions_Kun_Storage_S3_S3StorageOptions__Microsoft_Extensions_Logging_ILogger_Kun_Storage_S3_S3FileStorage__System_TimeProvider\_}

Initializes a new instance of the [S3FileStorage](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/s3filestorage.md) class.

```csharp
public S3FileStorage(IAmazonS3 s3, IOptions<S3StorageOptions> options, ILogger<S3FileStorage> logger, TimeProvider? timeProvider = null)
```

#### Parameters

`s3` IAmazonS3

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[S3StorageOptions](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/s3storageoptions.md)>

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[S3FileStorage](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/s3filestorage.md)>

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)?

## Methods

### DeleteAsync(string, CancellationToken) \{#Kun_Storage_S3_S3FileStorage_DeleteAsync_System_String_System_Threading_CancellationToken\_}

Removes the object. Returns `true` if the object existed and was deleted; `false` otherwise.

```csharp
public Task<bool> DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
```

#### Parameters

`objectKey` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

### ExistsAsync(string, CancellationToken) \{#Kun_Storage_S3_S3FileStorage_ExistsAsync_System_String_System_Threading_CancellationToken\_}

Returns `true` when an object with the given key exists.

```csharp
public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
```

#### Parameters

`objectKey` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

### GetMetadataAsync(string, CancellationToken) \{#Kun_Storage_S3_S3FileStorage_GetMetadataAsync_System_String_System_Threading_CancellationToken\_}

Reads the metadata (size, content type, ETag, last-modified) of the object, or `null` if missing.

```csharp
public Task<ObjectMetadata?> GetMetadataAsync(string objectKey, CancellationToken cancellationToken = default)
```

#### Parameters

`objectKey` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<ObjectMetadata?>

### GetPresignedDownloadAsync(string, DownloadOptions?, TimeSpan?, CancellationToken) \{#Kun_Storage_S3_S3FileStorage_GetPresignedDownloadAsync_System_String_Kun_Storage_DownloadOptions_System_Nullable_System_TimeSpan__System_Threading_CancellationToken\_}

Produces a time-limited URL the caller can use to download the object directly
from the backing store. The URL MUST include `Content-Disposition: attachment`
when the stored content type is an active web content type (see remarks on
[IFileStorage](/pr-preview/pr-181/sdk/api/kun/kun-storage/ifilestorage.md)).

```csharp
public Task<PresignedDownloadUrl> GetPresignedDownloadAsync(string objectKey, DownloadOptions? options = null, TimeSpan? expiresIn = null, CancellationToken cancellationToken = default)
```

#### Parameters

`objectKey` [string](https://learn.microsoft.com/dotnet/api/system.string)

Storage object key.

`options` DownloadOptions?

Optional download behavior (download-name override, custom response headers).

`expiresIn` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

Validity window for the presigned URL.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<PresignedDownloadUrl>

### GetPresignedPostUploadAsync(string, UploadConstraints?, TimeSpan?, CancellationToken) \{#Kun_Storage_S3_S3FileStorage_GetPresignedPostUploadAsync_System_String_Kun_Storage_UploadConstraints_System_Nullable_System_TimeSpan__System_Threading_CancellationToken\_}

Creates a presigned POST upload descriptor. The signed policy embeds
[UploadConstraints.MaxBytes](/pr-preview/pr-181/sdk/api/kun/kun-storage/uploadconstraints.md#Kun_Storage_UploadConstraints_MaxBytes) when set, so the backing store (not the
framework) rejects oversized uploads before any bytes are persisted. Allowed
content types are encoded into the policy when
[UploadConstraints.AllowedContentTypes](/pr-preview/pr-181/sdk/api/kun/kun-storage/uploadconstraints.md#Kun_Storage_UploadConstraints_AllowedContentTypes) is supplied.

```csharp
public Task<PresignedPostUpload> GetPresignedPostUploadAsync(string objectKey, UploadConstraints? constraints = null, TimeSpan? expiresIn = null, CancellationToken cancellationToken = default)
```

#### Parameters

`objectKey` [string](https://learn.microsoft.com/dotnet/api/system.string)

Target object key.

`constraints` UploadConstraints?

Optional upload constraints. When `null`, no size/type
limits are baked into the policy.

`expiresIn` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

Lifetime of the signed policy.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<PresignedPostUpload>

### GetPresignedUploadAsync(string, string, UploadConstraints?, TimeSpan?, CancellationToken) \{#Kun_Storage_S3_S3FileStorage_GetPresignedUploadAsync_System_String_System_String_Kun_Storage_UploadConstraints_System_Nullable_System_TimeSpan__System_Threading_CancellationToken\_}

Produces a time-limited URL the caller can `PUT` content to. The returned
record carries the exact HTTP headers the caller must send so server-side
constraint enforcement works.

```csharp
public Task<PresignedUploadUrl> GetPresignedUploadAsync(string objectKey, string contentType, UploadConstraints? constraints = null, TimeSpan? expiresIn = null, CancellationToken cancellationToken = default)
```

#### Parameters

`objectKey` [string](https://learn.microsoft.com/dotnet/api/system.string)

Storage object key.

`contentType` [string](https://learn.microsoft.com/dotnet/api/system.string)

Declared content type for the upload.

`constraints` UploadConstraints?

Size / content-type policy enforced by the URL signature.

`expiresIn` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

Validity window.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<PresignedUploadUrl>

#### Remarks

The PUT URL returned here cannot embed [UploadConstraints.MaxBytes](/pr-preview/pr-181/sdk/api/kun/kun-storage/uploadconstraints.md#Kun_Storage_UploadConstraints_MaxBytes) in
the signature; consumers that need size enforcement at the S3 layer should use
[IFileStorage.GetPresignedPostUploadAsync](/pr-preview/pr-181/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_GetPresignedPostUploadAsync_System_String_Kun_Storage_UploadConstraints_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_) instead — its signed POST policy bakes
`content-length-range` into the signature so S3 rejects oversized bodies with
HTTP 400 before any bytes are persisted.

### ListAsync(string?, int, string?, CancellationToken) \{#Kun_Storage_S3_S3FileStorage_ListAsync_System_String_System_Int32_System_String_System_Threading_CancellationToken\_}

Lists objects whose keys start with `prefix`.

```csharp
public Task<ListObjectsResult> ListAsync(string? prefix = null, int maxKeys = 1000, string? continuationToken = null, CancellationToken cancellationToken = default)
```

#### Parameters

`prefix` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Key prefix (e.g. `tenants/42/`). Pass `null` or empty for the root listing.

`maxKeys` [int](https://learn.microsoft.com/dotnet/api/system.int32)

Upper bound on the number of objects returned in a single call. Implementations may clamp.

`continuationToken` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Opaque token returned from a previous call. Pass `null` for the first page.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<ListObjectsResult>

### OpenReadAsync(string, CancellationToken) \{#Kun_Storage_S3_S3FileStorage_OpenReadAsync_System_String_System_Threading_CancellationToken\_}

Opens a readable stream over the object's content for server-side reads (e.g.
computing a content diff). The caller MUST dispose the returned stream.

```csharp
public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
```

#### Parameters

`objectKey` [string](https://learn.microsoft.com/dotnet/api/system.string)

Storage object key.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)>

#### Remarks

Optional capability. Backends that only expose presigned URLs leave the default,
which throws [`NotSupportedException`](https://learn.microsoft.com/dotnet/api/system.notsupportedexception) — callers should treat that as
"server-side read unavailable" and degrade (e.g. metadata-only diff). Overriding
implementations SHOULD throw [`FileNotFoundException`](https://learn.microsoft.com/dotnet/api/system.io.filenotfoundexception) when the object is missing.

### UploadAsync(string, Stream, string, UploadConstraints?, CancellationToken) \{#Kun_Storage_S3_S3FileStorage_UploadAsync_System_String_System_IO_Stream_System_String_Kun_Storage_UploadConstraints_System_Threading_CancellationToken\_}

Uploads `content` to `objectKey`.

```csharp
public Task<ObjectMetadata> UploadAsync(string objectKey, Stream content, string contentType, UploadConstraints? constraints = null, CancellationToken cancellationToken = default)
```

#### Parameters

`objectKey` [string](https://learn.microsoft.com/dotnet/api/system.string)

Storage object key (forward-slash-delimited path).

`content` [Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)

Readable stream with the payload. The stream is consumed fully.

`contentType` [string](https://learn.microsoft.com/dotnet/api/system.string)

MIME type of the payload (e.g. `image/png`).

`constraints` UploadConstraints?

Optional size and content-type policy; the store validates and rejects on violation.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<ObjectMetadata>

The final [ObjectMetadata](/pr-preview/pr-181/sdk/api/kun/kun-storage/objectmetadata.md) of the uploaded object.
