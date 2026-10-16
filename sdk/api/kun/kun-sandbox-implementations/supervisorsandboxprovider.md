> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class SupervisorSandboxProvider

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Implementations](/sdk/api/kun/kun-sandbox-implementations/index.md)\
Assembly: Kun.Sandbox.dll

Production [ISandboxProvider](/sdk/api/kun/kun-sandbox-abstractions/isandboxprovider.md) that talks to the
configured supervisor's `/v2/sandboxes/*` endpoints.
The HTTP client is supplied by
the caller — typically via [`IHttpClientFactory`](https://learn.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory)
— so delegating handlers can attach S2S JWT, mTLS client cert, and
OpenTelemetry instrumentation without this class knowing about them.

```csharp
public sealed class SupervisorSandboxProvider : ISandboxProvider
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SupervisorSandboxProvider](/sdk/api/kun/kun-sandbox-implementations/supervisorsandboxprovider.md)

#### Implements

[ISandboxProvider](/sdk/api/kun/kun-sandbox-abstractions/isandboxprovider.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SupervisorSandboxProvider(HttpClient) \{#Kun_Sandbox_Implementations_SupervisorSandboxProvider__ctor_System_Net_Http_HttpClient\_}

Creates the provider with a caller-configured [`HttpClient`](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient). The [`BaseAddress`](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient.baseaddress) must point at the Supervisor (e.g. `https://sandbox-supervisor.internal:8443/`).

```csharp
public SupervisorSandboxProvider(HttpClient httpClient)
```

#### Parameters

`httpClient` [HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient)

## Fields

### HttpClientName \{#Kun_Sandbox_Implementations_SupervisorSandboxProvider_HttpClientName}

Logical name used when resolving the [`HttpClient`](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) from [`IHttpClientFactory`](https://learn.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory).

```csharp
public const string HttpClientName = "Kun.Sandbox.Supervisor"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### CreateAsync(SandboxCreateOptions, CancellationToken) \{#Kun_Sandbox_Implementations_SupervisorSandboxProvider_CreateAsync_Kun_Sandbox_Abstractions_SandboxCreateOptions_System_Threading_CancellationToken\_}

Creates a new sandbox with the supplied options and returns a
handle for process / file operations. The sandbox is `running`
when the task completes.

```csharp
public Task<ISandboxHandle> CreateAsync(SandboxCreateOptions options, CancellationToken cancellationToken = default)
```

#### Parameters

`options` [SandboxCreateOptions](/sdk/api/kun/kun-sandbox-abstractions/sandboxcreateoptions.md)

Sandbox creation options. Mirrors the E2B v2 create payload plus caller-selected provider extension data.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token for the create call.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ISandboxHandle](/sdk/api/kun/kun-sandbox-abstractions/isandboxhandle.md)>

A handle whose lifetime is tied to the sandbox.
