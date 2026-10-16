> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class ContentDispositionPolicy

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Storage.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/index.md)\
Assembly: Kun.Storage.dll

Policy for deciding when browsers must be forced to download (rather than render)
a stored object. Active-web MIME types (HTML/SVG/XML/JS/WASM) are served back as
`attachment` so that user-uploaded content cannot execute in the origin of
the download domain.

```csharp
public static class ContentDispositionPolicy
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ContentDispositionPolicy](/pr-preview/pr-181/sdk/api/kun/kun-storage-abstractions/contentdispositionpolicy.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### BuildAttachmentDisposition(string?) \{#Kun_Storage_Abstractions_ContentDispositionPolicy_BuildAttachmentDisposition_System_String\_}

Builds a canonical `attachment` disposition. When `fileName`
is supplied, the name is encoded per RFC 5987 so non-ASCII characters survive.

```csharp
public static string BuildAttachmentDisposition(string? fileName)
```

#### Parameters

`fileName` [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

### EnforceAttachment(string?, string?, string?) \{#Kun_Storage_Abstractions_ContentDispositionPolicy_EnforceAttachment_System_String_System_String_System_String\_}

Returns a `Content-Disposition` value for the given context. If
`contentType` is unsafe and `currentDisposition`
is not already an `attachment`, replaces it with an attachment disposition.
Otherwise returns `currentDisposition` unchanged (possibly [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)).

```csharp
public static string? EnforceAttachment(string? contentType, string? currentDisposition, string? fileName = null)
```

#### Parameters

`contentType` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`currentDisposition` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`fileName` [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### IsUnsafeForInlineDisplay(string?) \{#Kun_Storage_Abstractions_ContentDispositionPolicy_IsUnsafeForInlineDisplay_System_String\_}

Returns [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) if `contentType` would be rendered
inline by common browsers and therefore should be served as an attachment.

```csharp
public static bool IsUnsafeForInlineDisplay(string? contentType)
```

#### Parameters

`contentType` [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
