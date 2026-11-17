> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class DownloadOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Abstractions` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Abstractions`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Storage.Abstractions)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage](/pr-preview/pr-50/sdk/api/kun/kun-storage/index.md)\
Assembly: Kun.Storage.Abstractions.dll

Per-call download behavior hints.

```csharp
public sealed record DownloadOptions : IEquatable<DownloadOptions>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[DownloadOptions](/pr-preview/pr-50/sdk/api/kun/kun-storage/downloadoptions.md)

#### Implements

[IEquatable\<DownloadOptions>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### DownloadOptions(bool, string?, IReadOnlyDictionary\<string, string>?) \{#Kun_Storage_DownloadOptions__ctor_System_Boolean_System_String_System_Collections_Generic_IReadOnlyDictionary_System_String_System_String\_\_}

Per-call download behavior hints.

```csharp
public DownloadOptions(bool ForceDownload = false, string? DownloadFileName = null, IReadOnlyDictionary<string, string>? ResponseHeaders = null)
```

#### Parameters

`ForceDownload` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

When `true`, the URL MUST carry `Content-Disposition: attachment` regardless of content type.
Implementations MUST also force attachment when the content type is in
[ActiveWebContentTypes.Value](/pr-preview/pr-50/sdk/api/kun/kun-storage/activewebcontenttypes.md#Kun_Storage_ActiveWebContentTypes_Value), even if this flag is `false`.

`DownloadFileName` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Optional file name used in the `filename` parameter of `Content-Disposition`.
Implementations sanitize the value to exclude control characters and path separators.

`ResponseHeaders` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>?

Additional response headers the store should return (e.g. `Cache-Control`).
Implementations may drop headers they cannot sign.

## Properties

### DownloadFileName \{#Kun_Storage_DownloadOptions_DownloadFileName}

Optional file name used in the `filename` parameter of `Content-Disposition`.
Implementations sanitize the value to exclude control characters and path separators.

```csharp
public string? DownloadFileName { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ForceDownload \{#Kun_Storage_DownloadOptions_ForceDownload}

When `true`, the URL MUST carry `Content-Disposition: attachment` regardless of content type.
Implementations MUST also force attachment when the content type is in
[ActiveWebContentTypes.Value](/pr-preview/pr-50/sdk/api/kun/kun-storage/activewebcontenttypes.md#Kun_Storage_ActiveWebContentTypes_Value), even if this flag is `false`.

```csharp
public bool ForceDownload { get; init; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### ResponseHeaders \{#Kun_Storage_DownloadOptions_ResponseHeaders}

Additional response headers the store should return (e.g. `Cache-Control`).
Implementations may drop headers they cannot sign.

```csharp
public IReadOnlyDictionary<string, string>? ResponseHeaders { get; init; }
```

#### Property Value

[IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>?

## Methods

### \<Clone>$() \{#Kun_Storage_DownloadOptions__Clone\_\_}

```csharp
public DownloadOptions <Clone>$()
```

#### Returns

[DownloadOptions](/pr-preview/pr-50/sdk/api/kun/kun-storage/downloadoptions.md)

### Deconstruct(out bool, out string?, out IReadOnlyDictionary\<string, string>?) \{#Kun_Storage_DownloadOptions_Deconstruct_System_Boolean__System_String__System_Collections_Generic_IReadOnlyDictionary_System_String_System_String\_\_\_}

```csharp
public void Deconstruct(out bool ForceDownload, out string? DownloadFileName, out IReadOnlyDictionary<string, string>? ResponseHeaders)
```

#### Parameters

`ForceDownload` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

`DownloadFileName` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`ResponseHeaders` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>?

### Equals(object?) \{#Kun_Storage_DownloadOptions_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(DownloadOptions?) \{#Kun_Storage_DownloadOptions_Equals_Kun_Storage_DownloadOptions\_}

```csharp
public bool Equals(DownloadOptions? other)
```

#### Parameters

`other` [DownloadOptions](/pr-preview/pr-50/sdk/api/kun/kun-storage/downloadoptions.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_DownloadOptions_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_DownloadOptions_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(DownloadOptions?, DownloadOptions?) \{#Kun_Storage_DownloadOptions_op_Equality_Kun_Storage_DownloadOptions_Kun_Storage_DownloadOptions\_}

```csharp
public static bool operator ==(DownloadOptions? left, DownloadOptions? right)
```

#### Parameters

`left` [DownloadOptions](/pr-preview/pr-50/sdk/api/kun/kun-storage/downloadoptions.md)?

`right` [DownloadOptions](/pr-preview/pr-50/sdk/api/kun/kun-storage/downloadoptions.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(DownloadOptions?, DownloadOptions?) \{#Kun_Storage_DownloadOptions_op_Inequality_Kun_Storage_DownloadOptions_Kun_Storage_DownloadOptions\_}

```csharp
public static bool operator !=(DownloadOptions? left, DownloadOptions? right)
```

#### Parameters

`left` [DownloadOptions](/pr-preview/pr-50/sdk/api/kun/kun-storage/downloadoptions.md)?

`right` [DownloadOptions](/pr-preview/pr-50/sdk/api/kun/kun-storage/downloadoptions.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
