> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunSandboxOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Extensions](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/index.md)\
Assembly: Kun.Sandbox.dll

Builder passed to [KunSandboxServiceCollectionExtensions.AddKunSandbox](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxservicecollectionextensions.md#Kun_Sandbox_Extensions_KunSandboxServiceCollectionExtensions_AddKunSandbox_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Sandbox_Extensions_KunSandboxOptions__).
Exactly one of [KunSandboxOptions.UseSupervisor](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md#Kun_Sandbox_Extensions_KunSandboxOptions_UseSupervisor_System_Uri_System_Security_Cryptography_X509Certificates_X509Certificate2_System_Action_System_Net_Http_HttpClient__), [KunSandboxOptions.UseE2BCloud](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md#Kun_Sandbox_Extensions_KunSandboxOptions_UseE2BCloud_System_String_System_String_),
or [KunSandboxOptions.UseInMemory](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md#Kun_Sandbox_Extensions_KunSandboxOptions_UseInMemory_System_Func_System_Collections_Generic_IReadOnlyList_System_String__Kun_Sandbox_Abstractions_ProcessRunOptions_Kun_Sandbox_Abstractions_ProcessResult__) must be called; calling a second one
replaces the previous choice.

```csharp
public sealed class KunSandboxOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunSandboxOptions](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunSandboxOptions() \{#Kun_Sandbox_Extensions_KunSandboxOptions__ctor}

```csharp
public KunSandboxOptions()
```

## Methods

### UseE2BCloud(string, string) \{#Kun_Sandbox_Extensions_KunSandboxOptions_UseE2BCloud_System_String_System_String\_}

Wire the E2B Cloud backend ([E2BCloudSandboxProvider](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-implementations/e2bcloudsandboxprovider.md),
Phase 2+ placeholder). Registration succeeds but
[ISandboxProvider.CreateAsync](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-abstractions/isandboxprovider.md#Kun_Sandbox_Abstractions_ISandboxProvider_CreateAsync_Kun_Sandbox_Abstractions_SandboxCreateOptions_System_Threading_CancellationToken_) throws
[`NotImplementedException`](https://learn.microsoft.com/dotnet/api/system.notimplementedexception) at call time until Mode D
(Sandbox Spec v1.1 §14.4) is implemented.

```csharp
public KunSandboxOptions UseE2BCloud(string apiKey, string teamId)
```

#### Parameters

`apiKey` [string](https://learn.microsoft.com/dotnet/api/system.string)

`teamId` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[KunSandboxOptions](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md)

### UseInMemory(Func\<IReadOnlyList\<string>, ProcessRunOptions?, ProcessResult>?) \{#Kun_Sandbox_Extensions_KunSandboxOptions_UseInMemory_System_Func_System_Collections_Generic_IReadOnlyList_System_String__Kun_Sandbox_Abstractions_ProcessRunOptions_Kun_Sandbox_Abstractions_ProcessResult\_\_}

Wire the in-memory fake backend ([InMemorySandboxProvider](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-implementations/inmemorysandboxprovider.md)).
Intended for unit tests and developer-loop prototyping — there is
no isolation and no network egress.

```csharp
public KunSandboxOptions UseInMemory(Func<IReadOnlyList<string>, ProcessRunOptions?, ProcessResult>? runScript = null)
```

#### Parameters

`runScript` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>, [ProcessRunOptions](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-abstractions/processrunoptions.md)?, [ProcessResult](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-abstractions/processresult.md)>?

Optional scripted response for `processes.run`. See [InMemorySandboxProvider](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-implementations/inmemorysandboxprovider.md).

#### Returns

[KunSandboxOptions](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md)

### UseSupervisor(Uri, X509Certificate2?, Action\<HttpClient>?) \{#Kun_Sandbox_Extensions_KunSandboxOptions_UseSupervisor_System_Uri_System_Security_Cryptography_X509Certificates_X509Certificate2_System_Action_System_Net_Http_HttpClient\_\_}

Wire the Supervisor backend: [SupervisorSandboxProvider](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-implementations/supervisorsandboxprovider.md)
talking to `/v2/sandboxes/*` at `baseUrl`.

```csharp
public KunSandboxOptions UseSupervisor(Uri baseUrl, X509Certificate2? clientCertificate = null, Action<HttpClient>? configureClient = null)
```

#### Parameters

`baseUrl` Uri

Supervisor base URL, e.g. `https://sandbox-supervisor.internal:8443/`.

`clientCertificate` [X509Certificate2](https://learn.microsoft.com/dotnet/api/system.security.cryptography.x509certificates.x509certificate2)?

Optional mTLS client certificate — attached to the primary handler when supplied.

`configureClient` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[HttpClient](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient)>?

Optional hook to configure the [`HttpClient`](https://learn.microsoft.com/dotnet/api/system.net.http.httpclient) (e.g. add S2S JWT delegating handler, set a `User-Agent`).

#### Returns

[KunSandboxOptions](/pr-preview/pr-122/sdk/api/kun/kun-sandbox-extensions/kunsandboxoptions.md)
