> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class StorageServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Oss` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Oss`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage.Oss)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Oss.DependencyInjection](/pr-preview/pr-122/sdk/api/kun/kun-storage-oss-dependencyinjection/index.md)\
Assembly: Kun.Storage.Oss.dll

DI extension methods for registering [OssFileStorage](/pr-preview/pr-122/sdk/api/kun/kun-storage-oss/ossfilestorage.md).

```csharp
public static class StorageServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[StorageServiceCollectionExtensions](/pr-preview/pr-122/sdk/api/kun/kun-storage-oss-dependencyinjection/storageservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunOssFileStorage(IServiceCollection, Action\<OssStorageOptions>) \{#Kun_Storage_Oss_DependencyInjection_StorageServiceCollectionExtensions_AddKunOssFileStorage_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Storage_Oss_OssStorageOptions\_\_}

Registers [OssFileStorage](/pr-preview/pr-122/sdk/api/kun/kun-storage-oss/ossfilestorage.md) as the [IFileStorage](/pr-preview/pr-122/sdk/api/kun/kun-storage/ifilestorage.md) implementation
and configures an `IOss` client from [OssStorageOptions](/pr-preview/pr-122/sdk/api/kun/kun-storage-oss/ossstorageoptions.md).

```csharp
public static IServiceCollection AddKunOssFileStorage(this IServiceCollection services, Action<OssStorageOptions> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[OssStorageOptions](/pr-preview/pr-122/sdk/api/kun/kun-storage-oss/ossstorageoptions.md)>

Configures [OssStorageOptions](/pr-preview/pr-122/sdk/api/kun/kun-storage-oss/ossstorageoptions.md).

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
