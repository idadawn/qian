> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Interface ISandboxProvider

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Abstractions](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-abstractions/index.md)\
Assembly: Kun.Sandbox.dll

Pluggable sandbox backend. Creates an [ISandboxHandle](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md)
that represents a short-lived isolated execution environment.

```csharp
public interface ISandboxProvider
```

## Remarks

The contract is deliberately aligned with the E2B v2 `POST /v2/sandboxes`
API so that consumers can swap between the in-memory fake, the
configured supervisor, and the E2B Cloud backend (Mode D)
without changing the portable call surface.

## Methods

### CreateAsync(SandboxCreateOptions, CancellationToken) \{#Kun_Sandbox_Abstractions_ISandboxProvider_CreateAsync_Kun_Sandbox_Abstractions_SandboxCreateOptions_System_Threading_CancellationToken\_}

Creates a new sandbox with the supplied options and returns a
handle for process / file operations. The sandbox is `running`
when the task completes.

```csharp
Task<ISandboxHandle> CreateAsync(SandboxCreateOptions options, CancellationToken cancellationToken = default)
```

#### Parameters

`options` [SandboxCreateOptions](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-abstractions/sandboxcreateoptions.md)

Sandbox creation options. Mirrors the E2B v2 create payload plus caller-selected provider extension data.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token for the create call.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ISandboxHandle](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md)>

A handle whose lifetime is tied to the sandbox.
