> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class UploadConstraints

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Abstractions` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Abstractions`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage.Abstractions)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage](/pr-preview/pr-202/sdk/api/kun/kun-storage/index.md)\
Assembly: Kun.Storage.Abstractions.dll

Upload policy applied by [IFileStorage.UploadAsync](/pr-preview/pr-202/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_UploadAsync_System_String_System_IO_Stream_System_String_Kun_Storage_UploadConstraints_System_Threading_CancellationToken_) and encoded into
[IFileStorage.GetPresignedUploadAsync](/pr-preview/pr-202/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_GetPresignedUploadAsync_System_String_System_String_Kun_Storage_UploadConstraints_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_) signatures.

```csharp
public sealed record UploadConstraints : IEquatable<UploadConstraints>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[UploadConstraints](/pr-preview/pr-202/sdk/api/kun/kun-storage/uploadconstraints.md)

#### Implements

[IEquatable\<UploadConstraints>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### UploadConstraints(long?, IReadOnlyCollection\<string>?) \{#Kun_Storage_UploadConstraints__ctor_System_Nullable_System_Int64__System_Collections_Generic_IReadOnlyCollection_System_String\_\_}

Upload policy applied by [IFileStorage.UploadAsync](/pr-preview/pr-202/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_UploadAsync_System_String_System_IO_Stream_System_String_Kun_Storage_UploadConstraints_System_Threading_CancellationToken_) and encoded into
[IFileStorage.GetPresignedUploadAsync](/pr-preview/pr-202/sdk/api/kun/kun-storage/ifilestorage.md#Kun_Storage_IFileStorage_GetPresignedUploadAsync_System_String_System_String_Kun_Storage_UploadConstraints_System_Nullable_System_TimeSpan__System_Threading_CancellationToken_) signatures.

```csharp
public UploadConstraints(long? MaxBytes = null, IReadOnlyCollection<string>? AllowedContentTypes = null)
```

#### Parameters

`MaxBytes` [long](https://learn.microsoft.com/dotnet/api/system.int64)?

Maximum payload size in bytes. `null` means no explicit limit.

`AllowedContentTypes` [IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>?

Whitelist of permitted MIME types. `null` or empty means any content type is accepted.
Comparison is case-insensitive.

## Properties

### AllowedContentTypes \{#Kun_Storage_UploadConstraints_AllowedContentTypes}

Whitelist of permitted MIME types. `null` or empty means any content type is accepted.
Comparison is case-insensitive.

```csharp
public IReadOnlyCollection<string>? AllowedContentTypes { get; init; }
```

#### Property Value

[IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>?

### MaxBytes \{#Kun_Storage_UploadConstraints_MaxBytes}

Maximum payload size in bytes. `null` means no explicit limit.

```csharp
public long? MaxBytes { get; init; }
```

#### Property Value

[long](https://learn.microsoft.com/dotnet/api/system.int64)?

## Methods

### \<Clone>$() \{#Kun_Storage_UploadConstraints__Clone\_\_}

```csharp
public UploadConstraints <Clone>$()
```

#### Returns

[UploadConstraints](/pr-preview/pr-202/sdk/api/kun/kun-storage/uploadconstraints.md)

### Deconstruct(out long?, out IReadOnlyCollection\<string>?) \{#Kun_Storage_UploadConstraints_Deconstruct_System_Nullable_System_Int64___System_Collections_Generic_IReadOnlyCollection_System_String\_\_\_}

```csharp
public void Deconstruct(out long? MaxBytes, out IReadOnlyCollection<string>? AllowedContentTypes)
```

#### Parameters

`MaxBytes` [long](https://learn.microsoft.com/dotnet/api/system.int64)?

`AllowedContentTypes` [IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>?

### Equals(object?) \{#Kun_Storage_UploadConstraints_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(UploadConstraints?) \{#Kun_Storage_UploadConstraints_Equals_Kun_Storage_UploadConstraints\_}

```csharp
public bool Equals(UploadConstraints? other)
```

#### Parameters

`other` [UploadConstraints](/pr-preview/pr-202/sdk/api/kun/kun-storage/uploadconstraints.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Storage_UploadConstraints_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Storage_UploadConstraints_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(UploadConstraints?, UploadConstraints?) \{#Kun_Storage_UploadConstraints_op_Equality_Kun_Storage_UploadConstraints_Kun_Storage_UploadConstraints\_}

```csharp
public static bool operator ==(UploadConstraints? left, UploadConstraints? right)
```

#### Parameters

`left` [UploadConstraints](/pr-preview/pr-202/sdk/api/kun/kun-storage/uploadconstraints.md)?

`right` [UploadConstraints](/pr-preview/pr-202/sdk/api/kun/kun-storage/uploadconstraints.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(UploadConstraints?, UploadConstraints?) \{#Kun_Storage_UploadConstraints_op_Inequality_Kun_Storage_UploadConstraints_Kun_Storage_UploadConstraints\_}

```csharp
public static bool operator !=(UploadConstraints? left, UploadConstraints? right)
```

#### Parameters

`left` [UploadConstraints](/pr-preview/pr-202/sdk/api/kun/kun-storage/uploadconstraints.md)?

`right` [UploadConstraints](/pr-preview/pr-202/sdk/api/kun/kun-storage/uploadconstraints.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
