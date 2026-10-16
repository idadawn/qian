> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class PresignedPostUpload

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Abstractions` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Abstractions`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage.Abstractions)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage](/pr-preview/pr-181/sdk/api/kun/kun-storage/index.md)\
Assembly: Kun.Storage.Abstractions.dll

Presigned POST descriptor for direct browser/client uploads. Unlike the PUT-based
[PresignedUploadUrl](/pr-preview/pr-181/sdk/api/kun/kun-storage/presigneduploadurl.md), the signed policy baked into the form fields
enforces [UploadConstraints.MaxBytes](/pr-preview/pr-181/sdk/api/kun/kun-storage/uploadconstraints.md#Kun_Storage_UploadConstraints_MaxBytes): S3 itself rejects oversized bodies
with HTTP 400 before any bytes land in the bucket.

```csharp
public sealed record PresignedPostUpload : IEquatable<PresignedPostUpload>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PresignedPostUpload](/pr-preview/pr-181/sdk/api/kun/kun-storage/presignedpostupload.md)

#### Implements

[IEquatable\<PresignedPostUpload>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PresignedPostUpload(Uri, IReadOnlyDictionary\<string, string>, DateTimeOffset) \{#Kun_Storage_PresignedPostUpload__ctor_System_Uri_System_Collections_Generic_IReadOnlyDictionary_System_String_System_String__System_DateTimeOffset\_}

Presigned POST descriptor for direct browser/client uploads. Unlike the PUT-based
[PresignedUploadUrl](/pr-preview/pr-181/sdk/api/kun/kun-storage/presigneduploadurl.md), the signed policy baked into the form fields
enforces [UploadConstraints.MaxBytes](/pr-preview/pr-181/sdk/api/kun/kun-storage/uploadconstraints.md#Kun_Storage_UploadConstraints_MaxBytes): S3 itself rejects oversized bodies
with HTTP 400 before any bytes land in the bucket.

```csharp
public PresignedPostUpload(Uri Url, IReadOnlyDictionary<string, string> FormFields, DateTimeOffset ExpiresAt)
```

#### Parameters

`Url` Uri

Endpoint to POST a multipart/form-data body to.

`FormFields` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>

Form fields that MUST appear verbatim in the multipart body
before the `file` field (policy, x-amz-signature, x-amz-credential, x-amz-date,
x-amz-algorithm, key, Content-Type, etc.). Order matters — callers should iterate the
dictionary and append each key/value in order, then append the file bytes last.

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

Absolute expiry (UTC) of the signed policy.

## Properties

### ExpiresAt \{#Kun_Storage_PresignedPostUpload_ExpiresAt}

Absolute expiry (UTC) of the signed policy.

```csharp
public DateTimeOffset ExpiresAt { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### FormFields \{#Kun_Storage_PresignedPostUpload_FormFields}

Form fields that MUST appear verbatim in the multipart body
before the `file` field (policy, x-amz-signature, x-amz-credential, x-amz-date,
x-amz-algorithm, key, Content-Type, etc.). Order matters — callers should iterate the
dictionary and append each key/value in order, then append the file bytes last.

```csharp
public IReadOnlyDictionary<string, string> FormFields { get; init; }
```

#### Property Value

[IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>

### Url \{#Kun_Storage_PresignedPostUpload_Url}

Endpoint to POST a multipart/form-data body to.

```csharp
public Uri Url { get; init; }
```

#### Property Value

Uri

## Methods

### \<Clone>$() \{#Kun_Storage_PresignedPostUpload__Clone\_\_}

```csharp
public PresignedPostUpload <Clone>$()
```

#### Returns

[PresignedPostUpload](/pr-preview/pr-181/sdk/api/kun/kun-storage/presignedpostupload.md)

### Deconstruct(out Uri, out IReadOnlyDictionary\<string, string>, out DateTimeOffset) \{#Kun_Storage_PresignedPostUpload_Deconstruct_System_Uri__System_Collections_Generic_IReadOnlyDictionary_System_String_System_String___System_DateTimeOffset\_\_}

```csharp
public void Deconstruct(out Uri Url, out IReadOnlyDictionary<string, string> FormFields, out DateTimeOffset ExpiresAt)
```

#### Parameters

`Url` Uri

`FormFields` [IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Equals(object?) \{#Kun_Storage_PresignedPostUpload_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(PresignedPostUpload?) \{#Kun_Storage_PresignedPostUpload_Equals_Kun_Storage_PresignedPostUpload\_}

```csharp
public bool Equals(PresignedPostUpload? other)
```

#### Parameters

`other` [PresignedPostUpload](/pr-preview/pr-181/sdk/api/kun/kun-storage/presignedpostupload.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_PresignedPostUpload_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_PresignedPostUpload_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(PresignedPostUpload?, PresignedPostUpload?) \{#Kun_Storage_PresignedPostUpload_op_Equality_Kun_Storage_PresignedPostUpload_Kun_Storage_PresignedPostUpload\_}

```csharp
public static bool operator ==(PresignedPostUpload? left, PresignedPostUpload? right)
```

#### Parameters

`left` [PresignedPostUpload](/pr-preview/pr-181/sdk/api/kun/kun-storage/presignedpostupload.md)?

`right` [PresignedPostUpload](/pr-preview/pr-181/sdk/api/kun/kun-storage/presignedpostupload.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(PresignedPostUpload?, PresignedPostUpload?) \{#Kun_Storage_PresignedPostUpload_op_Inequality_Kun_Storage_PresignedPostUpload_Kun_Storage_PresignedPostUpload\_}

```csharp
public static bool operator !=(PresignedPostUpload? left, PresignedPostUpload? right)
```

#### Parameters

`left` [PresignedPostUpload](/pr-preview/pr-181/sdk/api/kun/kun-storage/presignedpostupload.md)?

`right` [PresignedPostUpload](/pr-preview/pr-181/sdk/api/kun/kun-storage/presignedpostupload.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
