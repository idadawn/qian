> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class InMemorySandboxProvider

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Sandbox)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Implementations](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-implementations/index.md)\
Assembly: Kun.Sandbox.dll

In-process fake [ISandboxProvider](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/isandboxprovider.md) for unit tests and
local dev loops. Does not run anything: `processes.run` returns
a scripted [ProcessResult](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/processresult.md) and file APIs operate on a
[`ConcurrentDictionary`](https://learn.microsoft.com/dotnet/api/system.collections.concurrent.concurrentdictionary-2). Clearly labelled
"test only" so no-one accidentally wires it into production.

```csharp
public sealed class InMemorySandboxProvider : ISandboxProvider
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[InMemorySandboxProvider](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-implementations/inmemorysandboxprovider.md)

#### Implements

[ISandboxProvider](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/isandboxprovider.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### InMemorySandboxProvider() \{#Kun_Sandbox_Implementations_InMemorySandboxProvider__ctor}

Creates a provider that returns an empty `ProcessResult` for every `run` call (exit 0, empty stdout/stderr).

```csharp
public InMemorySandboxProvider()
```

### InMemorySandboxProvider(Func\<IReadOnlyList\<string>, ProcessRunOptions?, ProcessResult>?) \{#Kun_Sandbox_Implementations_InMemorySandboxProvider__ctor_System_Func_System_Collections_Generic_IReadOnlyList_System_String__Kun_Sandbox_Abstractions_ProcessRunOptions_Kun_Sandbox_Abstractions_ProcessResult\_\_}

Creates a provider with a caller-supplied scripted response for
[IProcesses.RunAsync](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/iprocesses.md#Kun_Sandbox_Abstractions_IProcesses_RunAsync_System_String___Kun_Sandbox_Abstractions_ProcessRunOptions_System_Threading_CancellationToken_).

```csharp
public InMemorySandboxProvider(Func<IReadOnlyList<string>, ProcessRunOptions?, ProcessResult>? runScript)
```

#### Parameters

`runScript` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>, [ProcessRunOptions](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/processrunoptions.md)?, [ProcessResult](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/processresult.md)>?

Invoked on every `run` call; receives the argv and options
and must return the [ProcessResult](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/processresult.md) the test expects.

## Methods

### CreateAsync(SandboxCreateOptions, CancellationToken) \{#Kun_Sandbox_Implementations_InMemorySandboxProvider_CreateAsync_Kun_Sandbox_Abstractions_SandboxCreateOptions_System_Threading_CancellationToken\_}

Creates a new sandbox with the supplied options and returns a
handle for process / file operations. The sandbox is `running`
when the task completes.

```csharp
public Task<ISandboxHandle> CreateAsync(SandboxCreateOptions options, CancellationToken cancellationToken = default)
```

#### Parameters

`options` [SandboxCreateOptions](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/sandboxcreateoptions.md)

Sandbox creation options. Mirrors the E2B v2 create payload plus caller-selected provider extension data.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token for the create call.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ISandboxHandle](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md)>

A handle whose lifetime is tied to the sandbox.
