> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class StorageOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Storage)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Abstractions](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/index.md)\
Assembly: Kun.Storage.dll

Configuration options for Kun.Storage.

```csharp
public sealed class StorageOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[StorageOptions](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/storageoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### StorageOptions() \{#Kun_Storage_Abstractions_StorageOptions__ctor}

```csharp
public StorageOptions()
```

## Properties

### AccessKey \{#Kun_Storage_Abstractions_StorageOptions_AccessKey}

Access key for S3/MinIO authentication.

```csharp
public string AccessKey { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### BucketName \{#Kun_Storage_Abstractions_StorageOptions_BucketName}

S3/MinIO bucket name.

```csharp
public string BucketName { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Endpoint \{#Kun_Storage_Abstractions_StorageOptions_Endpoint}

Custom S3-compatible endpoint (e.g. MinIO). Leave empty for AWS S3.

```csharp
public string Endpoint { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Provider \{#Kun_Storage_Abstractions_StorageOptions_Provider}

The storage provider to use.

```csharp
public StorageProvider Provider { get; set; }
```

#### Property Value

[StorageProvider](/pr-preview/pr-50/sdk/api/kun/kun-storage-abstractions/storageprovider.md)

### Region \{#Kun_Storage_Abstractions_StorageOptions_Region}

AWS region for S3.

```csharp
public string Region { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### RootPath \{#Kun_Storage_Abstractions_StorageOptions_RootPath}

Root directory for local filesystem storage. Defaults to "./storage".

```csharp
public string RootPath { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### SecretKey \{#Kun_Storage_Abstractions_StorageOptions_SecretKey}

Secret key for S3/MinIO authentication.

```csharp
public string SecretKey { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)
