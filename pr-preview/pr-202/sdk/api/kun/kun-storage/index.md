> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.Storage

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Abstractions` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Abstractions`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage.Abstractions)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Storage.Abstractions](/pr-preview/pr-202/sdk/api/kun/kun-storage-abstractions/index.md)

[Kun.Storage.DependencyInjection](/pr-preview/pr-202/sdk/api/kun/kun-storage-dependencyinjection/index.md)

[Kun.Storage.Implementations](/pr-preview/pr-202/sdk/api/kun/kun-storage-implementations/index.md)

[Kun.Storage.Local](/pr-preview/pr-202/sdk/api/kun/kun-storage-local/index.md)

[Kun.Storage.Oss](/pr-preview/pr-202/sdk/api/kun/kun-storage-oss/index.md)

[Kun.Storage.S3](/pr-preview/pr-202/sdk/api/kun/kun-storage-s3/index.md)

### Classes

[ActiveWebContentTypes](/pr-preview/pr-202/sdk/api/kun/kun-storage/activewebcontenttypes.md)

The canonical set of MIME types that browsers render as active web content —
i.e. payloads that could execute script or navigate against the serving origin
if opened inline. An [IFileStorage](/pr-preview/pr-202/sdk/api/kun/kun-storage/ifilestorage.md) implementation MUST force
`Content-Disposition: attachment` on presigned download URLs whose
[ObjectMetadata.ContentType](/pr-preview/pr-202/sdk/api/kun/kun-storage/objectmetadata.md#Kun_Storage_ObjectMetadata_ContentType) is contained in this set, regardless
of whether the caller requested [DownloadOptions.ForceDownload](/pr-preview/pr-202/sdk/api/kun/kun-storage/downloadoptions.md#Kun_Storage_DownloadOptions_ForceDownload).

[DownloadOptions](/pr-preview/pr-202/sdk/api/kun/kun-storage/downloadoptions.md)

Per-call download behavior hints.

[ListObjectsResult](/pr-preview/pr-202/sdk/api/kun/kun-storage/listobjectsresult.md)

A page of objects returned by [IFileStorage.ListAsync](/pr-preview/pr-202/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_ListAsync_System_String_System_Int32_System_String_System_Threading_CancellationToken_).

[ObjectMetadata](/pr-preview/pr-202/sdk/api/kun/kun-storage/objectmetadata.md)

Metadata describing a stored object.

[PresignedDownloadUrl](/pr-preview/pr-202/sdk/api/kun/kun-storage/presigneddownloadurl.md)

Presigned GET URL that lets a client download the object directly from the store.

[PresignedPostUpload](/pr-preview/pr-202/sdk/api/kun/kun-storage/presignedpostupload.md)

Presigned POST descriptor for direct browser/client uploads. Unlike the PUT-based
[PresignedUploadUrl](/pr-preview/pr-202/sdk/api/kun/kun-storage/presigneduploadurl.md), the signed policy baked into the form fields
enforces [UploadConstraints.MaxBytes](/pr-preview/pr-202/sdk/api/kun/kun-storage/uploadconstraints.md#Kun_Storage_UploadConstraints_MaxBytes): S3 itself rejects oversized bodies
with HTTP 400 before any bytes land in the bucket.

[PresignedUploadUrl](/pr-preview/pr-202/sdk/api/kun/kun-storage/presigneduploadurl.md)

Presigned PUT URL that a client can use to upload directly to the store.

[UploadConstraints](/pr-preview/pr-202/sdk/api/kun/kun-storage/uploadconstraints.md)

Upload policy applied by [IFileStorage.UploadAsync](/pr-preview/pr-202/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_UploadAsync_System_String_System_IO_Stream_System_String_Kun_Storage_UploadConstraints_System_Threading_CancellationToken_) and encoded into
[IFileStorage.GetPresignedUploadAsync](/pr-preview/pr-202/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_GetPresignedUploadAsync_System_String_System_String_Kun_Storage_UploadConstraints_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_) signatures.

### Interfaces

[IFileStorage](/pr-preview/pr-202/sdk/api/kun/kun-storage/ifilestorage.md)

Transport-agnostic object-storage abstraction used by Kun services to upload,
download, and manage blob artifacts (attachments, user uploads, exported files).
Implementations map the seven operations to a backing store (local filesystem,
Amazon S3, MinIO, Azure Blob, GCS).
