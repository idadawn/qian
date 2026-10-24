> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class PresignedUploadUrl

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Abstractions` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Abstractions`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage.Abstractions)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage](/pr-preview/pr-122/sdk/api/kun/kun-storage/index.md)\
Assembly: Kun.Storage.Abstractions.dll

Presigned PUT URL that a client can use to upload directly to the store.

```csharp
public sealed record PresignedUploadUrl : IEquatable<PresignedUploadUrl>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PresignedUploadUrl](/pr-preview/pr-122/sdk/api/kun/kun-storage/presigneduploadurl.md)

#### Implements

[IEquatable\<PresignedUploadUrl>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PresignedUploadUrl(Uri, IReadOnlyDictionary\<string, string>, DateTimeOffset) \{#Kun_Storage_PresignedUploadUrl__ctor_System_Uri_System_Collections_Generic_IReadOnlyDictionary_System_String_System_String__System_DateTimeOffset\_}

Presigned PUT URL that a client can use to upload directly to the store.

```csharp
public PresignedUploadUrl(Uri Url, IReadOnlyDictionary<string, string> RequiredHeaders, DateTimeOffset ExpiresAt)
```

#### Parameters

`Url` Uri

The signed URL. Must be used with `PUT`.

`RequiredHeaders` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>

HTTP headers the caller MUST send verbatim for the signature to validate.

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

Absolute expiry time (UTC).

## Properties

### ExpiresAt \{#Kun_Storage_PresignedUploadUrl_ExpiresAt}

Absolute expiry time (UTC).

```csharp
public DateTimeOffset ExpiresAt { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### RequiredHeaders \{#Kun_Storage_PresignedUploadUrl_RequiredHeaders}

HTTP headers the caller MUST send verbatim for the signature to validate.

```csharp
public IReadOnlyDictionary<string, string> RequiredHeaders { get; init; }
```

#### Property Value

[IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>

### Url \{#Kun_Storage_PresignedUploadUrl_Url}

The signed URL. Must be used with `PUT`.

```csharp
public Uri Url { get; init; }
```

#### Property Value

Uri

## Methods

### \<Clone>$() \{#Kun_Storage_PresignedUploadUrl__Clone\_\_}

```csharp
public PresignedUploadUrl <Clone>$()
```

#### Returns

[PresignedUploadUrl](/pr-preview/pr-122/sdk/api/kun/kun-storage/presigneduploadurl.md)

### Deconstruct(out Uri, out IReadOnlyDictionary\<string, string>, out DateTimeOffset) \{#Kun_Storage_PresignedUploadUrl_Deconstruct_System_Uri__System_Collections_Generic_IReadOnlyDictionary_System_String_System_String___System_DateTimeOffset\_\_}

```csharp
public void Deconstruct(out Uri Url, out IReadOnlyDictionary<string, string> RequiredHeaders, out DateTimeOffset ExpiresAt)
```

#### Parameters

`Url` Uri

`RequiredHeaders` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Equals(object?) \{#Kun_Storage_PresignedUploadUrl_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(PresignedUploadUrl?) \{#Kun_Storage_PresignedUploadUrl_Equals_Kun_Storage_PresignedUploadUrl\_}

```csharp
public bool Equals(PresignedUploadUrl? other)
```

#### Parameters

`other` [PresignedUploadUrl](/pr-preview/pr-122/sdk/api/kun/kun-storage/presigneduploadurl.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_PresignedUploadUrl_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_PresignedUploadUrl_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(PresignedUploadUrl?, PresignedUploadUrl?) \{#Kun_Storage_PresignedUploadUrl_op_Equality_Kun_Storage_PresignedUploadUrl_Kun_Storage_PresignedUploadUrl\_}

```csharp
public static bool operator ==(PresignedUploadUrl? left, PresignedUploadUrl? right)
```

#### Parameters

`left` [PresignedUploadUrl](/pr-preview/pr-122/sdk/api/kun/kun-storage/presigneduploadurl.md)?

`right` [PresignedUploadUrl](/pr-preview/pr-122/sdk/api/kun/kun-storage/presigneduploadurl.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(PresignedUploadUrl?, PresignedUploadUrl?) \{#Kun_Storage_PresignedUploadUrl_op_Inequality_Kun_Storage_PresignedUploadUrl_Kun_Storage_PresignedUploadUrl\_}

```csharp
public static bool operator !=(PresignedUploadUrl? left, PresignedUploadUrl? right)
```

#### Parameters

`left` [PresignedUploadUrl](/pr-preview/pr-122/sdk/api/kun/kun-storage/presigneduploadurl.md)?

`right` [PresignedUploadUrl](/pr-preview/pr-122/sdk/api/kun/kun-storage/presigneduploadurl.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
