> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class PresignedDownloadUrl

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Abstractions` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Abstractions`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage.Abstractions)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage](/pr-preview/pr-202/sdk/api/kun/kun-storage/index.md)\
Assembly: Kun.Storage.Abstractions.dll

Presigned GET URL that lets a client download the object directly from the store.

```csharp
public sealed record PresignedDownloadUrl : IEquatable<PresignedDownloadUrl>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PresignedDownloadUrl](/pr-preview/pr-202/sdk/api/kun/kun-storage/presigneddownloadurl.md)

#### Implements

[IEquatable\<PresignedDownloadUrl>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PresignedDownloadUrl(Uri, IReadOnlyDictionary\<string, string>, DateTimeOffset, bool) \{#Kun_Storage_PresignedDownloadUrl__ctor_System_Uri_System_Collections_Generic_IReadOnlyDictionary_System_String_System_String__System_DateTimeOffset_System_Boolean\_}

Presigned GET URL that lets a client download the object directly from the store.

```csharp
public PresignedDownloadUrl(Uri Url, IReadOnlyDictionary<string, string> ResponseHeaders, DateTimeOffset ExpiresAt, bool ContentDispositionForced)
```

#### Parameters

`Url` Uri

The signed URL. Should be used with `GET`.

`ResponseHeaders` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>

Response headers the store will return (e.g. `Content-Disposition`).

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

Absolute expiry time (UTC).

`ContentDispositionForced` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

`true` when the implementation applied `Content-Disposition: attachment` automatically
because the object's content type is an active web content type (see
[ActiveWebContentTypes.Value](/pr-preview/pr-202/sdk/api/kun/kun-storage/activewebcontenttypes.md#Kun_Storage_ActiveWebContentTypes_Value)).

## Properties

### ContentDispositionForced \{#Kun_Storage_PresignedDownloadUrl_ContentDispositionForced}

`true` when the implementation applied `Content-Disposition: attachment` automatically
because the object's content type is an active web content type (see
[ActiveWebContentTypes.Value](/pr-preview/pr-202/sdk/api/kun/kun-storage/activewebcontenttypes.md#Kun_Storage_ActiveWebContentTypes_Value)).

```csharp
public bool ContentDispositionForced { get; init; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### ExpiresAt \{#Kun_Storage_PresignedDownloadUrl_ExpiresAt}

Absolute expiry time (UTC).

```csharp
public DateTimeOffset ExpiresAt { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### ResponseHeaders \{#Kun_Storage_PresignedDownloadUrl_ResponseHeaders}

Response headers the store will return (e.g. `Content-Disposition`).

```csharp
public IReadOnlyDictionary<string, string> ResponseHeaders { get; init; }
```

#### Property Value

[IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>

### Url \{#Kun_Storage_PresignedDownloadUrl_Url}

The signed URL. Should be used with `GET`.

```csharp
public Uri Url { get; init; }
```

#### Property Value

Uri

## Methods

### \<Clone>$() \{#Kun_Storage_PresignedDownloadUrl__Clone\_\_}

```csharp
public PresignedDownloadUrl <Clone>$()
```

#### Returns

[PresignedDownloadUrl](/pr-preview/pr-202/sdk/api/kun/kun-storage/presigneddownloadurl.md)

### Deconstruct(out Uri, out IReadOnlyDictionary\<string, string>, out DateTimeOffset, out bool) \{#Kun_Storage_PresignedDownloadUrl_Deconstruct_System_Uri__System_Collections_Generic_IReadOnlyDictionary_System_String_System_String___System_DateTimeOffset__System_Boolean\_\_}

```csharp
public void Deconstruct(out Uri Url, out IReadOnlyDictionary<string, string> ResponseHeaders, out DateTimeOffset ExpiresAt, out bool ContentDispositionForced)
```

#### Parameters

`Url` Uri

`ResponseHeaders` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

`ContentDispositionForced` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(object?) \{#Kun_Storage_PresignedDownloadUrl_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(PresignedDownloadUrl?) \{#Kun_Storage_PresignedDownloadUrl_Equals_Kun_Storage_PresignedDownloadUrl\_}

```csharp
public bool Equals(PresignedDownloadUrl? other)
```

#### Parameters

`other` [PresignedDownloadUrl](/pr-preview/pr-202/sdk/api/kun/kun-storage/presigneddownloadurl.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_PresignedDownloadUrl_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_PresignedDownloadUrl_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(PresignedDownloadUrl?, PresignedDownloadUrl?) \{#Kun_Storage_PresignedDownloadUrl_op_Equality_Kun_Storage_PresignedDownloadUrl_Kun_Storage_PresignedDownloadUrl\_}

```csharp
public static bool operator ==(PresignedDownloadUrl? left, PresignedDownloadUrl? right)
```

#### Parameters

`left` [PresignedDownloadUrl](/pr-preview/pr-202/sdk/api/kun/kun-storage/presigneddownloadurl.md)?

`right` [PresignedDownloadUrl](/pr-preview/pr-202/sdk/api/kun/kun-storage/presigneddownloadurl.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(PresignedDownloadUrl?, PresignedDownloadUrl?) \{#Kun_Storage_PresignedDownloadUrl_op_Inequality_Kun_Storage_PresignedDownloadUrl_Kun_Storage_PresignedDownloadUrl\_}

```csharp
public static bool operator !=(PresignedDownloadUrl? left, PresignedDownloadUrl? right)
```

#### Parameters

`left` [PresignedDownloadUrl](/pr-preview/pr-202/sdk/api/kun/kun-storage/presigneddownloadurl.md)?

`right` [PresignedDownloadUrl](/pr-preview/pr-202/sdk/api/kun/kun-storage/presigneddownloadurl.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
