> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class LocalFileStorageOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Local` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Local`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage.Local)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Local](/pr-preview/pr-202/sdk/api/kun/kun-storage-local/index.md)\
Assembly: Kun.Storage.Local.dll

Configures [LocalFileSystemStorage](/pr-preview/pr-202/sdk/api/kun/kun-storage-local/localfilesystemstorage.md).

```csharp
public sealed class LocalFileStorageOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[LocalFileStorageOptions](/pr-preview/pr-202/sdk/api/kun/kun-storage-local/localfilestorageoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### LocalFileStorageOptions() \{#Kun_Storage_Local_LocalFileStorageOptions__ctor}

```csharp
public LocalFileStorageOptions()
```

## Properties

### DefaultPresignedUrlLifetime \{#Kun_Storage_Local_LocalFileStorageOptions_DefaultPresignedUrlLifetime}

Default validity window applied when the caller does not specify `expiresIn`.

```csharp
public TimeSpan DefaultPresignedUrlLifetime { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### PublicBaseUrl \{#Kun_Storage_Local_LocalFileStorageOptions_PublicBaseUrl}

Base URL prefix used to build presigned URLs. The host app must expose an HTTP
endpoint at this prefix that streams files from [LocalFileStorageOptions.RootPath](/pr-preview/pr-202/sdk/api/kun/kun-storage-local/localfilestorageoptions.md#Kun_Storage_Local_LocalFileStorageOptions_RootPath). The
final URL shape is `{PublicBaseUrl}/{objectKey}?expires=…&disposition=…`.

```csharp
public Uri PublicBaseUrl { get; set; }
```

#### Property Value

Uri

### RootPath \{#Kun_Storage_Local_LocalFileStorageOptions_RootPath}

Root directory on the local filesystem where objects are stored. Absolute path.
The directory is created if it does not exist at first use.

```csharp
public string RootPath { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)
