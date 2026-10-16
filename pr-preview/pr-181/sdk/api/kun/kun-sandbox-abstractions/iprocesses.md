> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface IProcesses

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/index.md)\
Assembly: Kun.Sandbox.dll

Process-execution sub-API of an [ISandboxHandle](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md). Maps
to the E2B v2 `POST /v2/sandboxes/{id}/processes` endpoint.

```csharp
public interface IProcesses
```

## Methods

### RunAsync(string\[], ProcessRunOptions?, CancellationToken) \{#Kun_Sandbox_Abstractions_IProcesses_RunAsync_System_String___Kun_Sandbox_Abstractions_ProcessRunOptions_System_Threading_CancellationToken\_}

Runs a command inside the sandbox synchronously and returns its
aggregated [ProcessResult](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/processresult.md) when it exits (or the
process is killed by timeout).

```csharp
Task<ProcessResult> RunAsync(string[] command, ProcessRunOptions? options = null, CancellationToken cancellationToken = default)
```

#### Parameters

`command` [string](https://learn.microsoft.com/dotnet/api/system.string)\[]

The command to run. When supplied as a single string it is
interpreted by the sandbox shell; when supplied as an argv array
it is exec'd directly. The array form is preferred because it
does not require shell quoting.

`options` [ProcessRunOptions](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/processrunoptions.md)?

Per-call options (cwd / envs / timeout / stdin).

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token for the run.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ProcessResult](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/processresult.md)>

Exit code, stdout, stderr, duration, truncation flag.

### RunAsync(string, ProcessRunOptions?, CancellationToken) \{#Kun_Sandbox_Abstractions_IProcesses_RunAsync_System_String_Kun_Sandbox_Abstractions_ProcessRunOptions_System_Threading_CancellationToken\_}

Convenience overload accepting a single shell command string.
Equivalent to invoking [IProcesses.RunAsync](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/iprocesses.md#Kun_Sandbox_Abstractions_IProcesses_RunAsync_System_String___Kun_Sandbox_Abstractions_ProcessRunOptions_System_Threading_CancellationToken_)
with `["/bin/sh", "-c", command]`; backends are free to
interpret the string natively for efficiency.

```csharp
Task<ProcessResult> RunAsync(string command, ProcessRunOptions? options = null, CancellationToken cancellationToken = default)
```

#### Parameters

`command` [string](https://learn.microsoft.com/dotnet/api/system.string)

`options` [ProcessRunOptions](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/processrunoptions.md)?

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ProcessResult](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/processresult.md)>
