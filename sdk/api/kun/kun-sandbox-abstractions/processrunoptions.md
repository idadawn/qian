> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class ProcessRunOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Abstractions](/sdk/api/kun/kun-sandbox-abstractions/index.md)\
Assembly: Kun.Sandbox.dll

Per-call options for [IProcesses.RunAsync](/sdk/api/kun/kun-sandbox-abstractions/iprocesses.md#Kun_Sandbox_Abstractions_IProcesses_RunAsync_System_String___Kun_Sandbox_Abstractions_ProcessRunOptions_System_Threading_CancellationToken_).
Mirrors the E2B v2 `POST /v2/sandboxes/{id}/processes` request body.

```csharp
public sealed class ProcessRunOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ProcessRunOptions](/sdk/api/kun/kun-sandbox-abstractions/processrunoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ProcessRunOptions() \{#Kun_Sandbox_Abstractions_ProcessRunOptions__ctor}

```csharp
public ProcessRunOptions()
```

## Properties

### Background \{#Kun_Sandbox_Abstractions_ProcessRunOptions_Background}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), start the process detached and
return its id immediately (maps to E2B `background`). The
synchronous [IProcesses.RunAsync](/sdk/api/kun/kun-sandbox-abstractions/iprocesses.md#Kun_Sandbox_Abstractions_IProcesses_RunAsync_System_String___Kun_Sandbox_Abstractions_ProcessRunOptions_System_Threading_CancellationToken_)
method **does not** support this; streaming is an out-of-scope
v1 follow-up.

```csharp
[JsonPropertyName("background")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
public bool Background { get; init; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Cwd \{#Kun_Sandbox_Abstractions_ProcessRunOptions_Cwd}

Working directory for the process. Defaults to the sandbox home when null.

```csharp
[JsonPropertyName("cwd")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? Cwd { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Envs \{#Kun_Sandbox_Abstractions_ProcessRunOptions_Envs}

Extra environment variables merged on top of the sandbox-wide envs.

```csharp
[JsonPropertyName("envs")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public IReadOnlyDictionary<string, string>? Envs { get; init; }
```

#### Property Value

[IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>?

### Stdin \{#Kun_Sandbox_Abstractions_ProcessRunOptions_Stdin}

Optional stdin payload streamed to the process.

```csharp
[JsonPropertyName("stdin")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? Stdin { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TimeoutMs \{#Kun_Sandbox_Abstractions_ProcessRunOptions_TimeoutMs}

Per-command timeout in milliseconds. Null = backend default.

```csharp
[JsonPropertyName("timeoutMs")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public int? TimeoutMs { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)?
