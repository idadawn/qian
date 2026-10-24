> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class StorageServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Local` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Local`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Storage.Local)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Local.DependencyInjection](/sdk/api/kun/kun-storage-local-dependencyinjection/index.md)\
Assembly: Kun.Storage.Local.dll

DI extension methods for registering [LocalFileSystemStorage](/sdk/api/kun/kun-storage-local/localfilesystemstorage.md).

```csharp
public static class StorageServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[StorageServiceCollectionExtensions](/sdk/api/kun/kun-storage-local-dependencyinjection/storageservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunLocalFileStorage(IServiceCollection, Action\<LocalFileStorageOptions>) \{#Kun_Storage_Local_DependencyInjection_StorageServiceCollectionExtensions_AddKunLocalFileStorage_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Storage_Local_LocalFileStorageOptions\_\_}

Registers [LocalFileSystemStorage](/sdk/api/kun/kun-storage-local/localfilesystemstorage.md) as the [IFileStorage](/sdk/api/kun/kun-storage/ifilestorage.md) implementation.

```csharp
public static IServiceCollection AddKunLocalFileStorage(this IServiceCollection services, Action<LocalFileStorageOptions> configure)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[LocalFileStorageOptions](/sdk/api/kun/kun-storage-local/localfilestorageoptions.md)>

Configures [LocalFileStorageOptions](/sdk/api/kun/kun-storage-local/localfilestorageoptions.md).

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
