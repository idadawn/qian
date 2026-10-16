> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunStorageServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.DependencyInjection](/pr-preview/pr-181/sdk/api/kun/kun-storage-dependencyinjection/index.md)\
Assembly: Kun.Storage.dll

Registration extensions for Kun.Storage.

```csharp
public static class KunStorageServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunStorageServiceCollectionExtensions](/pr-preview/pr-181/sdk/api/kun/kun-storage-dependencyinjection/kunstorageservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunStorage(IServiceCollection, Action\<StorageOptions>) \{#Kun_Storage_DependencyInjection_KunStorageServiceCollectionExtensions_AddKunStorage_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Storage_Abstractions_StorageOptions\_\_}

Registers [IFileStorage](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/ifilestorage.md) using the provider specified in
[StorageOptions.Provider](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/storageoptions.md#Kun_Storage_Abstractions_StorageOptions_Provider). Prefer [KunStorageServiceCollectionExtensions.AddS3Storage](/pr-preview/pr-181/sdk/api/kun/kun-storage-dependencyinjection/kunstorageservicecollectionextensions.md#Kun_Storage_DependencyInjection_KunStorageServiceCollectionExtensions_AddS3Storage_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Storage_Abstractions_StorageOptions__)
or [KunStorageServiceCollectionExtensions.AddLocalStorage](/pr-preview/pr-181/sdk/api/kun/kun-storage-dependencyinjection/kunstorageservicecollectionextensions.md#Kun_Storage_DependencyInjection_KunStorageServiceCollectionExtensions_AddLocalStorage_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Storage_Abstractions_StorageOptions__) for a clearer call site.

```csharp
public static IServiceCollection AddKunStorage(this IServiceCollection services, Action<StorageOptions> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[StorageOptions](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/storageoptions.md)>

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

### AddLocalStorage(IServiceCollection, Action\<StorageOptions>?) \{#Kun_Storage_DependencyInjection_KunStorageServiceCollectionExtensions_AddLocalStorage_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Storage_Abstractions_StorageOptions\_\_}

Registers [LocalFileStorage](/pr-preview/pr-181/sdk/api/kun/kun-storage-implementations/localfilestorage.md) as the [IFileStorage](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/ifilestorage.md).

```csharp
public static IServiceCollection AddLocalStorage(this IServiceCollection services, Action<StorageOptions>? configure = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[StorageOptions](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/storageoptions.md)>?

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

### AddS3Storage(IServiceCollection, Action\<StorageOptions>) \{#Kun_Storage_DependencyInjection_KunStorageServiceCollectionExtensions_AddS3Storage_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Storage_Abstractions_StorageOptions\_\_}

Registers [S3FileStorage](/pr-preview/pr-181/sdk/api/kun/kun-storage-implementations/s3filestorage.md) as the [IFileStorage](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/ifilestorage.md).

```csharp
public static IServiceCollection AddS3Storage(this IServiceCollection services, Action<StorageOptions> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[StorageOptions](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/storageoptions.md)>

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
