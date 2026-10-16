> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface ISandboxHandle

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/index.md)\
Assembly: Kun.Sandbox.dll

Handle to a running sandbox. Exposes process and file sub-APIs that
map one-to-one to E2B v2's `processes` and `files`
endpoints, plus a [ISandboxHandle.KillAsync](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md#Kun_Sandbox_Abstractions_ISandboxHandle_KillAsync_System_Threading_CancellationToken_) operation that maps to
`DELETE /v2/sandboxes/{id}`.

```csharp
public interface ISandboxHandle
```

## Remarks

The handle is logically [`IAsyncDisposable`](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)-shaped — but
we intentionally do **not** implement the interface because disposal
semantics are not equivalent across backends (e.g. in-memory does
nothing, Supervisor does a best-effort DELETE, E2B Cloud may bill
partial seconds). Callers explicitly call [ISandboxHandle.KillAsync](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md#Kun_Sandbox_Abstractions_ISandboxHandle_KillAsync_System_Threading_CancellationToken_).

## Properties

### EndAt \{#Kun_Sandbox_Abstractions_ISandboxHandle_EndAt}

UTC timestamp the sandbox is scheduled to be torn down (start + `timeoutMs`).

```csharp
DateTimeOffset EndAt { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Files \{#Kun_Sandbox_Abstractions_ISandboxHandle_Files}

File sub-API: read / write / list / delete under the sandbox workspace.

```csharp
IFiles Files { get; }
```

#### Property Value

[IFiles](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/ifiles.md)

### Processes \{#Kun_Sandbox_Abstractions_ISandboxHandle_Processes}

Process sub-API: run / stream / kill commands inside the sandbox.

```csharp
IProcesses Processes { get; }
```

#### Property Value

[IProcesses](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/iprocesses.md)

### SandboxId \{#Kun_Sandbox_Abstractions_ISandboxHandle_SandboxId}

Stable sandbox identifier returned by the backend.

```csharp
string SandboxId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### StartedAt \{#Kun_Sandbox_Abstractions_ISandboxHandle_StartedAt}

UTC timestamp the sandbox transitioned to `running`.

```csharp
DateTimeOffset StartedAt { get; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### State \{#Kun_Sandbox_Abstractions_ISandboxHandle_State}

Current lifecycle state: `creating`, `running`, `killed`, `failed`, or `timedOut`.

```csharp
SandboxState State { get; }
```

#### Property Value

[SandboxState](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/sandboxstate.md)

### TemplateId \{#Kun_Sandbox_Abstractions_ISandboxHandle_TemplateId}

Template id the sandbox was created from.

```csharp
string TemplateId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### KillAsync(CancellationToken) \{#Kun_Sandbox_Abstractions_ISandboxHandle_KillAsync_System_Threading_CancellationToken\_}

Destroys the sandbox. Safe to call multiple times; subsequent calls no-op when state is already `killed`.

```csharp
Task KillAsync(CancellationToken cancellationToken = default)
```

#### Parameters

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
