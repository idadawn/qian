> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Interface IFiles

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Sandbox)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Abstractions](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/index.md)\
Assembly: Kun.Sandbox.dll

File sub-API of an [ISandboxHandle](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md). Maps to the E2B v2
`GET/POST/DELETE /v2/sandboxes/{id}/files?path=...` endpoints and
`GET /v2/sandboxes/{id}/files/list?path=...`.

```csharp
public interface IFiles
```

## Methods

### DeleteAsync(string, CancellationToken) \{#Kun_Sandbox_Abstractions_IFiles_DeleteAsync_System_String_System_Threading_CancellationToken\_}

Deletes a single file at `path`.

```csharp
Task DeleteAsync(string path, CancellationToken cancellationToken = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### ListAsync(string, bool, CancellationToken) \{#Kun_Sandbox_Abstractions_IFiles_ListAsync_System_String_System_Boolean_System_Threading_CancellationToken\_}

Lists directory entries at `path`.

```csharp
Task<IReadOnlyList<FileEntry>> ListAsync(string path, bool recursive = false, CancellationToken cancellationToken = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

Directory path inside the sandbox.

`recursive` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), walk sub-directories.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[FileEntry](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/fileentry.md)>>

### ReadAsync(string, CancellationToken) \{#Kun_Sandbox_Abstractions_IFiles_ReadAsync_System_String_System_Threading_CancellationToken\_}

Reads a file out of the sandbox as raw bytes.

```csharp
Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

Absolute path inside the sandbox. Paths must not contain `..` and must live under `/mnt/user-data/` (enforced by the Supervisor backend).

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[byte](https://learn.microsoft.com/dotnet/api/system.byte)\[]>

### WriteAsync(string, byte\[], CancellationToken) \{#Kun_Sandbox_Abstractions_IFiles_WriteAsync_System_String_System_Byte___System_Threading_CancellationToken\_}

Writes `content` to `path` inside the sandbox.

```csharp
Task WriteAsync(string path, byte[] content, CancellationToken cancellationToken = default)
```

#### Parameters

`path` [string](https://learn.microsoft.com/dotnet/api/system.string)

`content` [byte](https://learn.microsoft.com/dotnet/api/system.byte)\[]

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
