> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class ObjectDownload

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Storage)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Abstractions](/sdk/api/kun/kun-storage-abstractions/index.md)\
Assembly: Kun.Storage.dll

Content stream plus metadata returned from
[IFileStorage.GetObjectAsync](/sdk/api/kun/kun-storage-abstractions/ifilestorage.md#Kun_Storage_Abstractions_IFileStorage_GetObjectAsync_System_String_System_Threading_CancellationToken_).

```csharp
public sealed class ObjectDownload : IAsyncDisposable, IDisposable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ObjectDownload](/sdk/api/kun/kun-storage-abstractions/objectdownload.md)

#### Implements

[IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable),
[IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Dispose this instance to release the underlying [`Stream`](https://learn.microsoft.com/dotnet/api/system.io.stream).

## Constructors

### ObjectDownload(Stream, ObjectMetadata) \{#Kun_Storage_Abstractions_ObjectDownload__ctor_System_IO_Stream_Kun_Storage_Abstractions_ObjectMetadata\_}

Initializes a new download result.

```csharp
public ObjectDownload(Stream content, ObjectMetadata metadata)
```

#### Parameters

`content` [Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)

`metadata` [ObjectMetadata](/sdk/api/kun/kun-storage-abstractions/objectmetadata.md)

## Properties

### Content \{#Kun_Storage_Abstractions_ObjectDownload_Content}

The object's payload. Ownership is transferred to the caller.

```csharp
public Stream Content { get; }
```

#### Property Value

[Stream](https://learn.microsoft.com/dotnet/api/system.io.stream)

### Metadata \{#Kun_Storage_Abstractions_ObjectDownload_Metadata}

Metadata for the downloaded object.

```csharp
public ObjectMetadata Metadata { get; }
```

#### Property Value

[ObjectMetadata](/sdk/api/kun/kun-storage-abstractions/objectmetadata.md)

## Methods

### Dispose() \{#Kun_Storage_Abstractions_ObjectDownload_Dispose}

```csharp
public void Dispose()
```

### DisposeAsync() \{#Kun_Storage_Abstractions_ObjectDownload_DisposeAsync}

```csharp
public ValueTask DisposeAsync()
```

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
