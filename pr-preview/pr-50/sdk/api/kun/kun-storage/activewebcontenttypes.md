> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class ActiveWebContentTypes

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Abstractions` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Abstractions`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Storage.Abstractions)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage](/pr-preview/pr-50/sdk/api/kun/kun-storage/index.md)\
Assembly: Kun.Storage.Abstractions.dll

The canonical set of MIME types that browsers render as active web content —
i.e. payloads that could execute script or navigate against the serving origin
if opened inline. An [IFileStorage](/pr-preview/pr-50/sdk/api/kun/kun-storage/ifilestorage.md) implementation MUST force
`Content-Disposition: attachment` on presigned download URLs whose
[ObjectMetadata.ContentType](/pr-preview/pr-50/sdk/api/kun/kun-storage/objectmetadata.md#Kun_Storage_ObjectMetadata_ContentType) is contained in this set, regardless
of whether the caller requested [DownloadOptions.ForceDownload](/pr-preview/pr-50/sdk/api/kun/kun-storage/downloadoptions.md#Kun_Storage_DownloadOptions_ForceDownload).

```csharp
public static class ActiveWebContentTypes
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ActiveWebContentTypes](/pr-preview/pr-50/sdk/api/kun/kun-storage/activewebcontenttypes.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

The four entries are fixed by M1 of the Sprint-1 gap-closure plan:
`text/html`, `image/svg+xml`, `application/xhtml+xml`, `application/xml`.
Comparison is case-insensitive.

## Fields

### Value \{#Kun_Storage_ActiveWebContentTypes_Value}

The exact four MIME types for which presigned download URLs are forced to
`Content-Disposition: attachment`.

```csharp
public static readonly FrozenSet<string> Value
```

#### Field Value

[FrozenSet](https://learn.microsoft.com/dotnet/api/system.collections.frozen.frozenset-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

## Methods

### Contains(string?) \{#Kun_Storage_ActiveWebContentTypes_Contains_System_String\_}

Returns `true` when `contentType` is one of the four
canonical active web content types.

```csharp
public static bool Contains(string? contentType)
```

#### Parameters

`contentType` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The MIME type reported by the stored object's metadata.

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
