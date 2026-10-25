> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class OssStorageOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Oss` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Oss`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage.Oss)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Oss](/pr-preview/pr-122/sdk/api/kun/kun-storage-oss/index.md)\
Assembly: Kun.Storage.Oss.dll

Configures [OssFileStorage](/pr-preview/pr-122/sdk/api/kun/kun-storage-oss/ossfilestorage.md).

```csharp
public sealed class OssStorageOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OssStorageOptions](/pr-preview/pr-122/sdk/api/kun/kun-storage-oss/ossstorageoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### OssStorageOptions() \{#Kun_Storage_Oss_OssStorageOptions__ctor}

```csharp
public OssStorageOptions()
```

## Properties

### AccessKeyId \{#Kun_Storage_Oss_OssStorageOptions_AccessKeyId}

OSS AccessKey id. Required — OSS has no ambient credential resolution chain.

```csharp
public string AccessKeyId { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### AccessKeySecret \{#Kun_Storage_Oss_OssStorageOptions_AccessKeySecret}

OSS AccessKey secret paired with [OssStorageOptions.AccessKeyId](/pr-preview/pr-122/sdk/api/kun/kun-storage-oss/ossstorageoptions.md#Kun_Storage_Oss_OssStorageOptions_AccessKeyId). Required.

```csharp
public string AccessKeySecret { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### BucketName \{#Kun_Storage_Oss_OssStorageOptions_BucketName}

Target bucket. Required.

```csharp
public string BucketName { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### DefaultPresignedUrlLifetime \{#Kun_Storage_Oss_OssStorageOptions_DefaultPresignedUrlLifetime}

Default presigned-URL lifetime when the caller does not specify one. OSS caps a single
presigned URL at 7 days.

```csharp
public TimeSpan DefaultPresignedUrlLifetime { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### Endpoint \{#Kun_Storage_Oss_OssStorageOptions_Endpoint}

OSS service endpoint, e.g. `https://oss-cn-hangzhou.aliyuncs.com` (public) or
`https://oss-cn-hangzhou-internal.aliyuncs.com` (intranet — no public egress cost).
Required.

```csharp
public string Endpoint { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)
