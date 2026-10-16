> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class PutObjectOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Abstractions](/sdk/api/kun/kun-storage-abstractions/index.md)\
Assembly: Kun.Storage.dll

Options supplied when uploading an object.

```csharp
public sealed record PutObjectOptions : IEquatable<PutObjectOptions>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PutObjectOptions](/sdk/api/kun/kun-storage-abstractions/putobjectoptions.md)

#### Implements

[IEquatable\<PutObjectOptions>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PutObjectOptions(string, string?, string?, string?, IReadOnlyDictionary\<string, string>?) \{#Kun_Storage_Abstractions_PutObjectOptions__ctor_System_String_System_String_System_String_System_String_System_Collections_Generic_IReadOnlyDictionary_System_String_System_String\_\_}

Options supplied when uploading an object.

```csharp
public PutObjectOptions(string ContentType, string? ContentDisposition = null, string? CacheControl = null, string? FileName = null, IReadOnlyDictionary<string, string>? UserMetadata = null)
```

#### Parameters

`ContentType` [string](https://learn.microsoft.com/dotnet/api/system.string)

MIME type. Required.

`ContentDisposition` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Optional explicit `Content-Disposition`. If [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) and
`ContentType` is in the unsafe-for-inline-display list,
implementations MUST populate this with an `attachment` disposition.

`CacheControl` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Optional `Cache-Control` header value.

`FileName` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Optional original file name; used to enrich the enforced attachment disposition.

`UserMetadata` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>?

Arbitrary user metadata. Keys should be lowercase ASCII.

## Properties

### CacheControl \{#Kun_Storage_Abstractions_PutObjectOptions_CacheControl}

Optional `Cache-Control` header value.

```csharp
public string? CacheControl { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ContentDisposition \{#Kun_Storage_Abstractions_PutObjectOptions_ContentDisposition}

Optional explicit `Content-Disposition`. If [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) and
`ContentType` is in the unsafe-for-inline-display list,
implementations MUST populate this with an `attachment` disposition.

```csharp
public string? ContentDisposition { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ContentType \{#Kun_Storage_Abstractions_PutObjectOptions_ContentType}

MIME type. Required.

```csharp
public string ContentType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### FileName \{#Kun_Storage_Abstractions_PutObjectOptions_FileName}

Optional original file name; used to enrich the enforced attachment disposition.

```csharp
public string? FileName { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### UserMetadata \{#Kun_Storage_Abstractions_PutObjectOptions_UserMetadata}

Arbitrary user metadata. Keys should be lowercase ASCII.

```csharp
public IReadOnlyDictionary<string, string>? UserMetadata { get; init; }
```

#### Property Value

[IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>?

## Methods

### \<Clone>$() \{#Kun_Storage_Abstractions_PutObjectOptions__Clone\_\_}

```csharp
public PutObjectOptions <Clone>$()
```

#### Returns

[PutObjectOptions](/sdk/api/kun/kun-storage-abstractions/putobjectoptions.md)

### Deconstruct(out string, out string?, out string?, out string?, out IReadOnlyDictionary\<string, string>?) \{#Kun_Storage_Abstractions_PutObjectOptions_Deconstruct_System_String__System_String__System_String__System_String__System_Collections_Generic_IReadOnlyDictionary_System_String_System_String\_\_\_}

```csharp
public void Deconstruct(out string ContentType, out string? ContentDisposition, out string? CacheControl, out string? FileName, out IReadOnlyDictionary<string, string>? UserMetadata)
```

#### Parameters

`ContentType` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ContentDisposition` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`CacheControl` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`FileName` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`UserMetadata` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>?

### Equals(object?) \{#Kun_Storage_Abstractions_PutObjectOptions_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(PutObjectOptions?) \{#Kun_Storage_Abstractions_PutObjectOptions_Equals_Kun_Storage_Abstractions_PutObjectOptions\_}

```csharp
public bool Equals(PutObjectOptions? other)
```

#### Parameters

`other` [PutObjectOptions](/sdk/api/kun/kun-storage-abstractions/putobjectoptions.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_Abstractions_PutObjectOptions_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_Abstractions_PutObjectOptions_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(PutObjectOptions?, PutObjectOptions?) \{#Kun_Storage_Abstractions_PutObjectOptions_op_Equality_Kun_Storage_Abstractions_PutObjectOptions_Kun_Storage_Abstractions_PutObjectOptions\_}

```csharp
public static bool operator ==(PutObjectOptions? left, PutObjectOptions? right)
```

#### Parameters

`left` [PutObjectOptions](/sdk/api/kun/kun-storage-abstractions/putobjectoptions.md)?

`right` [PutObjectOptions](/sdk/api/kun/kun-storage-abstractions/putobjectoptions.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(PutObjectOptions?, PutObjectOptions?) \{#Kun_Storage_Abstractions_PutObjectOptions_op_Inequality_Kun_Storage_Abstractions_PutObjectOptions_Kun_Storage_Abstractions_PutObjectOptions\_}

```csharp
public static bool operator !=(PutObjectOptions? left, PutObjectOptions? right)
```

#### Parameters

`left` [PutObjectOptions](/sdk/api/kun/kun-storage-abstractions/putobjectoptions.md)?

`right` [PutObjectOptions](/sdk/api/kun/kun-storage-abstractions/putobjectoptions.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
