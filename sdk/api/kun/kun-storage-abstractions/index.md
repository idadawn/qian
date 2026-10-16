> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Storage.Abstractions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[ContentDispositionPolicy](/sdk/api/kun/kun-storage-abstractions/contentdispositionpolicy.md)

Policy for deciding when browsers must be forced to download (rather than render)
a stored object. Active-web MIME types (HTML/SVG/XML/JS/WASM) are served back as
`attachment` so that user-uploaded content cannot execute in the origin of
the download domain.

[ObjectDownload](/sdk/api/kun/kun-storage-abstractions/objectdownload.md)

Content stream plus metadata returned from
[IFileStorage.GetObjectAsync](/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_GetObjectAsync_System_String_System_Threading_CancellationToken_).

[ObjectMetadata](/sdk/api/kun/kun-storage-abstractions/objectmetadata.md)

Metadata describing a stored object.

[ObjectSummary](/sdk/api/kun/kun-storage-abstractions/objectsummary.md)

Lightweight description of an object returned by
[IFileStorage.ListObjectsAsync](/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_ListObjectsAsync_System_String_System_Threading_CancellationToken_).

[PresignedDownloadRequest](/sdk/api/kun/kun-storage-abstractions/presigneddownloadrequest.md)

Parameters for
[IFileStorage.CreatePresignedDownloadUrlAsync](/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_CreatePresignedDownloadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedDownloadRequest_System_Threading_CancellationToken_).

[PresignedDownloadUrl](/sdk/api/kun/kun-storage-abstractions/presigneddownloadurl.md)

Result of
[IFileStorage.CreatePresignedDownloadUrlAsync](/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_CreatePresignedDownloadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedDownloadRequest_System_Threading_CancellationToken_).

[PresignedUploadRequest](/sdk/api/kun/kun-storage-abstractions/presigneduploadrequest.md)

Parameters for
[IFileStorage.CreatePresignedUploadUrlAsync](/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_CreatePresignedUploadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedUploadRequest_System_Threading_CancellationToken_).

[PresignedUploadUrl](/sdk/api/kun/kun-storage-abstractions/presigneduploadurl.md)

Result of
[IFileStorage.CreatePresignedUploadUrlAsync](/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_CreatePresignedUploadUrlAsync_System_String_Kun_Storage_Abstractions_PresignedUploadRequest_System_Threading_CancellationToken_).

[PutObjectOptions](/sdk/api/kun/kun-storage-abstractions/putobjectoptions.md)

Options supplied when uploading an object.

[StorageOptions](/sdk/api/kun/kun-storage-abstractions/storageoptions.md)

Configuration options for Kun.Storage.

### Interfaces

[IFileStorage](/sdk/api/kun/kun-storage-abstractions/ifilestorage.md)

Abstraction for object-storage operations. Supports multiple backends
(local filesystem, S3, MinIO) behind a uniform contract.

### Enums

[StorageProvider](/sdk/api/kun/kun-storage-abstractions/storageprovider.md)

Supported storage providers.
