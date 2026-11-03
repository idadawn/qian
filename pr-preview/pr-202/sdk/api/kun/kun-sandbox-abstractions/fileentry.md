> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class FileEntry

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Abstractions](/pr-preview/pr-202/sdk/api/kun/kun-sandbox-abstractions/index.md)\
Assembly: Kun.Sandbox.dll

Single entry returned by [IFiles.ListAsync](/pr-preview/pr-202/sdk/api/kun/kun-sandbox-abstractions/ifiles.md#Kun_Sandbox_Abstractions_IFiles_ListAsync_System_String_System_Boolean_System_Threading_CancellationToken_). Wire shape
matches the E2B v2 `files/list` response.

```csharp
public sealed class FileEntry
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[FileEntry](/pr-preview/pr-202/sdk/api/kun/kun-sandbox-abstractions/fileentry.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### FileEntry() \{#Kun_Sandbox_Abstractions_FileEntry__ctor}

```csharp
public FileEntry()
```

## Properties

### ModifiedAt \{#Kun_Sandbox_Abstractions_FileEntry_ModifiedAt}

Last-modified timestamp.

```csharp
[JsonPropertyName("modifiedAt")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public DateTimeOffset? ModifiedAt { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)?

### Path \{#Kun_Sandbox_Abstractions_FileEntry_Path}

Absolute path inside the sandbox.

```csharp
[JsonPropertyName("path")]
public string Path { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### SizeBytes \{#Kun_Sandbox_Abstractions_FileEntry_SizeBytes}

Size in bytes. Zero for directories.

```csharp
[JsonPropertyName("sizeBytes")]
public long SizeBytes { get; init; }
```

#### Property Value

[long](https://learn.microsoft.com/dotnet/api/system.int64)

### Type \{#Kun_Sandbox_Abstractions_FileEntry_Type}

Either `file` or `directory`.

```csharp
[JsonPropertyName("type")]
public string Type { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)
