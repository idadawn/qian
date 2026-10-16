> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class S3StorageOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.S3` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.S3`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage.S3)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.S3](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/index.md)\
Assembly: Kun.Storage.S3.dll

Configures [S3FileStorage](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/s3filestorage.md).

```csharp
public sealed class S3StorageOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[S3StorageOptions](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/s3storageoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### S3StorageOptions() \{#Kun_Storage_S3_S3StorageOptions__ctor}

```csharp
public S3StorageOptions()
```

## Properties

### AccessKey \{#Kun_Storage_S3_S3StorageOptions_AccessKey}

Optional static access key. When both [S3StorageOptions.AccessKey](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/s3storageoptions.md#Kun_Storage_S3_S3StorageOptions_AccessKey) and [S3StorageOptions.SecretKey](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/s3storageoptions.md#Kun_Storage_S3_S3StorageOptions_SecretKey)
are non-null, they override the SDK's default credential resolution chain.

```csharp
public string? AccessKey { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### BucketName \{#Kun_Storage_S3_S3StorageOptions_BucketName}

Target bucket. Required.

```csharp
public string BucketName { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### DefaultPresignedUrlLifetime \{#Kun_Storage_S3_S3StorageOptions_DefaultPresignedUrlLifetime}

Default presigned-URL lifetime when the caller does not specify one.

```csharp
public TimeSpan DefaultPresignedUrlLifetime { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### ForcePathStyle \{#Kun_Storage_S3_S3StorageOptions_ForcePathStyle}

When `true`, URLs use path-style addressing (`https://host/bucket/key`)
instead of virtual-host style (`https://bucket.host/key`). Required for MinIO
and LocalStack; incompatible with some new AWS buckets.

```csharp
public bool ForcePathStyle { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Region \{#Kun_Storage_S3_S3StorageOptions_Region}

AWS region (e.g. `us-east-1`). Required when [S3StorageOptions.ServiceUrl](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/s3storageoptions.md#Kun_Storage_S3_S3StorageOptions_ServiceUrl) is not set.

```csharp
public string? Region { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### SecretKey \{#Kun_Storage_S3_S3StorageOptions_SecretKey}

Optional static secret key paired with [S3StorageOptions.AccessKey](/pr-preview/pr-181/sdk/api/kun/kun-storage-s3/s3storageoptions.md#Kun_Storage_S3_S3StorageOptions_AccessKey).

```csharp
public string? SecretKey { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ServiceUrl \{#Kun_Storage_S3_S3StorageOptions_ServiceUrl}

Optional service URL override for S3-compatible stores (MinIO, LocalStack, Ceph).
When `null`, the SDK resolves the regional AWS endpoint.

```csharp
public Uri? ServiceUrl { get; set; }
```

#### Property Value

Uri?
