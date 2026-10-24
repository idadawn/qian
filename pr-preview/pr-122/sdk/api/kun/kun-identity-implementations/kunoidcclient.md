> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunOidcClient

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Implementations](/pr-preview/pr-122/sdk/api/kun/kun-identity-implementations/index.md)\
Assembly: Kun.Identity.dll

OIDC protocol client with bounded metadata caching and signed, issuer/audience/nonce-bound ID Tokens.

```csharp
public sealed class KunOidcClient : IOidcClient, IDisposable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunOidcClient](/pr-preview/pr-122/sdk/api/kun/kun-identity-implementations/kunoidcclient.md)

#### Implements

[IOidcClient](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/ioidcclient.md),
[IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunOidcClient(KunOidcClientOptions, IHttpClientFactory) \{#Kun_Identity_Implementations_KunOidcClient__ctor_Kun_Identity_Configuration_KunOidcClientOptions_System_Net_Http_IHttpClientFactory\_}

Creates a client using the system clock.

```csharp
public KunOidcClient(KunOidcClientOptions options, IHttpClientFactory httpClientFactory)
```

#### Parameters

`options` [KunOidcClientOptions](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/kunoidcclientoptions.md)

`httpClientFactory` [IHttpClientFactory](https://learn.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory)

### KunOidcClient(KunOidcClientOptions, IHttpClientFactory, TimeProvider) \{#Kun_Identity_Implementations_KunOidcClient__ctor_Kun_Identity_Configuration_KunOidcClientOptions_System_Net_Http_IHttpClientFactory_System_TimeProvider\_}

Creates a client with an explicit clock for metadata freshness and token lifetimes.

```csharp
public KunOidcClient(KunOidcClientOptions options, IHttpClientFactory httpClientFactory, TimeProvider clock)
```

#### Parameters

`options` [KunOidcClientOptions](/pr-preview/pr-122/sdk/api/kun/kun-identity-configuration/kunoidcclientoptions.md)

`httpClientFactory` [IHttpClientFactory](https://learn.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory)

`clock` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

## Properties

### Name \{#Kun_Identity_Implementations_KunOidcClient_Name}

Name under which this client was registered. Useful when a
consumer registers multiple OIDC providers (e.g. both
`"authentik"` and `"google"`) and keyed-service
resolution picks one.

```csharp
public string Name { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### ClientCredentialsAsync(string, CancellationToken) \{#Kun_Identity_Implementations_KunOidcClient_ClientCredentialsAsync_System_String_System_Threading_CancellationToken\_}

Obtains a machine access token from the configured identity provider.

```csharp
public Task<OidcTokenResponse> ClientCredentialsAsync(string scope, CancellationToken cancellationToken = default)
```

#### Parameters

`scope` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[OidcTokenResponse](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidctokenresponse.md)>

### DiscoverAsync(CancellationToken) \{#Kun_Identity_Implementations_KunOidcClient_DiscoverAsync_System_Threading_CancellationToken\_}

Fetches and caches the provider's OpenID configuration / JWKS metadata.

```csharp
public Task<OidcConfiguration> DiscoverAsync(CancellationToken cancellationToken = default)
```

#### Parameters

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[OidcConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidcconfiguration.md)>

### Dispose() \{#Kun_Identity_Implementations_KunOidcClient_Dispose}

```csharp
public void Dispose()
```

### ExchangeCodeAsync(string, string, CancellationToken) \{#Kun_Identity_Implementations_KunOidcClient_ExchangeCodeAsync_System_String_System_String_System_Threading_CancellationToken\_}

Exchanges an authorization `code` (and
matching `redirectUri`) for an access /
refresh / id-token triple at the provider's token endpoint.

```csharp
[Obsolete("Pass an OidcCodeExchange with PKCE and nonce.", DiagnosticId = "KUNOIDC001")]
public Task<OidcTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
```

#### Parameters

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`redirectUri` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[OidcTokenResponse](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidctokenresponse.md)>

### ExchangeCodeAsync(OidcCodeExchange, CancellationToken) \{#Kun_Identity_Implementations_KunOidcClient_ExchangeCodeAsync_Kun_Identity_Abstractions_OidcCodeExchange_System_Threading_CancellationToken\_}

Exchanges a code with its original PKCE verifier and expected nonce; validates the returned ID Token.

```csharp
public Task<OidcTokenResponse> ExchangeCodeAsync(OidcCodeExchange request, CancellationToken cancellationToken = default)
```

#### Parameters

`request` [OidcCodeExchange](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidccodeexchange.md)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[OidcTokenResponse](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidctokenresponse.md)>

### RefreshAsync(string, string, CancellationToken) \{#Kun_Identity_Implementations_KunOidcClient_RefreshAsync_System_String_System_String_System_Threading_CancellationToken\_}

Exchanges a refresh token; a returned ID Token must belong to the expected subject.

```csharp
public Task<OidcTokenResponse> RefreshAsync(string refreshToken, string expectedSubject, CancellationToken cancellationToken = default)
```

#### Parameters

`refreshToken` [string](https://learn.microsoft.com/dotnet/api/system.string)

`expectedSubject` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[OidcTokenResponse](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidctokenresponse.md)>

### ValidateLogoutTokenAsync(string, CancellationToken) \{#Kun_Identity_Implementations_KunOidcClient_ValidateLogoutTokenAsync_System_String_System_Threading_CancellationToken\_}

Validates a signed logout token for this issuer/client; does not itself revoke sessions.

```csharp
public Task<OidcLogoutToken> ValidateLogoutTokenAsync(string token, CancellationToken cancellationToken = default)
```

#### Parameters

`token` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[OidcLogoutToken](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidclogouttoken.md)>
