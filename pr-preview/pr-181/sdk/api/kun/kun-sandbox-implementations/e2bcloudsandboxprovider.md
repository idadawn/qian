> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class E2BCloudSandboxProvider

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Implementations](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-implementations/index.md)\
Assembly: Kun.Sandbox.dll

Placeholder [ISandboxProvider](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/isandboxprovider.md) targeting E2B Cloud
(`https://api.e2b.dev`). The constructor signature is fixed so
DI wiring can be exercised in tests and reviewed in PRs, but every
method throws [`NotImplementedException`](https://learn.microsoft.com/dotnet/api/system.notimplementedexception).

```csharp
public sealed class E2BCloudSandboxProvider : ISandboxProvider
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[E2BCloudSandboxProvider](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-implementations/e2bcloudsandboxprovider.md)

#### Implements

[ISandboxProvider](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/isandboxprovider.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

This provider corresponds to the future cloud backend mode — "delegate sandbox execution to E2B Cloud, Supervisor acts as
an auth + extension-layer proxy". Mode D is explicitly **Phase 2+**
and is not scoped for the current milestone. Do not fill in the
implementation here as a drive-by; it requires workspace-sync design,
secret-injection policy, and a separate ADR update.

## Constructors

### E2BCloudSandboxProvider(string, string) \{#Kun_Sandbox_Implementations_E2BCloudSandboxProvider__ctor_System_String_System_String\_}

Creates the provider with the credentials needed to eventually call the E2B Cloud API.

```csharp
public E2BCloudSandboxProvider(string apiKey, string teamId)
```

#### Parameters

`apiKey` [string](https://learn.microsoft.com/dotnet/api/system.string)

E2B API key for the tenant.

`teamId` [string](https://learn.microsoft.com/dotnet/api/system.string)

E2B team id the sandboxes will bill against.

## Properties

### ApiKey \{#Kun_Sandbox_Implementations_E2BCloudSandboxProvider_ApiKey}

The E2B API key held for the future Mode D implementation.

```csharp
public string ApiKey { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### TeamId \{#Kun_Sandbox_Implementations_E2BCloudSandboxProvider_TeamId}

The E2B team id held for the future Mode D implementation.

```csharp
public string TeamId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### CreateAsync(SandboxCreateOptions, CancellationToken) \{#Kun_Sandbox_Implementations_E2BCloudSandboxProvider_CreateAsync_Kun_Sandbox_Abstractions_SandboxCreateOptions_System_Threading_CancellationToken\_}

Creates a new sandbox with the supplied options and returns a
handle for process / file operations. The sandbox is `running`
when the task completes.

```csharp
public Task<ISandboxHandle> CreateAsync(SandboxCreateOptions options, CancellationToken cancellationToken = default)
```

#### Parameters

`options` [SandboxCreateOptions](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/sandboxcreateoptions.md)

Sandbox creation options. Mirrors the E2B v2 create payload plus caller-selected provider extension data.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token for the create call.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ISandboxHandle](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md)>

A handle whose lifetime is tied to the sandbox.
