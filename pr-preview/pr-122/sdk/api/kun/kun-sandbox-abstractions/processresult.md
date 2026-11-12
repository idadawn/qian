> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class ProcessResult

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Abstractions](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-abstractions/index.md)\
Assembly: Kun.Sandbox.dll

Outcome of a completed [IProcesses.RunAsync](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-abstractions/iprocesses.md#Kun_Sandbox_Abstractions_IProcesses_RunAsync_System_String___Kun_Sandbox_Abstractions_ProcessRunOptions_System_Threading_CancellationToken_)
call. Wire shape matches the E2B v2 `processes` response.

```csharp
public sealed class ProcessResult
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ProcessResult](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-abstractions/processresult.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ProcessResult() \{#Kun_Sandbox_Abstractions_ProcessResult__ctor}

```csharp
public ProcessResult()
```

## Properties

### DurationMs \{#Kun_Sandbox_Abstractions_ProcessResult_DurationMs}

Wall-clock duration in milliseconds.

```csharp
[JsonPropertyName("durationMs")]
public long DurationMs { get; init; }
```

#### Property Value

[long](https://learn.microsoft.com/dotnet/api/system.int64)

### ExitCode \{#Kun_Sandbox_Abstractions_ProcessResult_ExitCode}

Process exit code. `0` for clean exits; `-1` when the backend killed it before exit (e.g. timeout).

```csharp
[JsonPropertyName("exitCode")]
public int ExitCode { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ProcessId \{#Kun_Sandbox_Abstractions_ProcessResult_ProcessId}

Backend-assigned id for this process invocation.

```csharp
[JsonPropertyName("processId")]
public string ProcessId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Stderr \{#Kun_Sandbox_Abstractions_ProcessResult_Stderr}

Captured stderr as UTF-8 text.

```csharp
[JsonPropertyName("stderr")]
public string Stderr { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Stdout \{#Kun_Sandbox_Abstractions_ProcessResult_Stdout}

Captured stdout as UTF-8 text.

```csharp
[JsonPropertyName("stdout")]
public string Stdout { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Truncated \{#Kun_Sandbox_Abstractions_ProcessResult_Truncated}

[true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) when stdout / stderr were capped at the backend's configured size and trimmed.

```csharp
[JsonPropertyName("truncated")]
public bool Truncated { get; init; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
