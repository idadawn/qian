> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class ChallengeHelper

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity.AspNetCore` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Identity.AspNetCore)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.AspNetCore](/pr-preview/pr-50/sdk/api/kun/kun-identity-aspnetcore/index.md)\
Assembly: Kun.Identity.AspNetCore.dll

Builds the `/authorize` redirect URL for an OIDC provider
registered via `AddKunIdentity(id => id.AddOidcClient(...))`.
Generates the one-shot PKCE `code_verifier` /
`code_challenge` pair, a `state`, and a `nonce`,
stashes the verifier + state + nonce + returnUrl in a short-lived
cookie so the callback handler can replay them, and returns the
URL the caller should redirect the user to.

```csharp
public sealed class ChallengeHelper
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ChallengeHelper](/pr-preview/pr-50/sdk/api/kun/kun-identity-aspnetcore/challengehelper.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ChallengeHelper(IEnumerable\<IOidcClient>, IOptionsMonitor\<KunOidcClientOptions>, TimeProvider) \{#Kun_Identity_AspNetCore_ChallengeHelper__ctor_System_Collections_Generic_IEnumerable_Kun_Identity_Abstractions_IOidcClient__Microsoft_Extensions_Options_IOptionsMonitor_Kun_Identity_Configuration_KunOidcClientOptions__System_TimeProvider\_}

Creates the helper.

```csharp
public ChallengeHelper(IEnumerable<IOidcClient> oidcClients, IOptionsMonitor<KunOidcClientOptions> oidcOptions, TimeProvider timeProvider)
```

#### Parameters

`oidcClients` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<IOidcClient>

`oidcOptions` [IOptionsMonitor](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1)\<KunOidcClientOptions>

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

### ChallengeHelper(IEnumerable\<IOidcClient>, IOptionsMonitor\<KunOidcClientOptions>, TimeProvider, IDataProtectionProvider, ISessionRegistry) \{#Kun_Identity_AspNetCore_ChallengeHelper__ctor_System_Collections_Generic_IEnumerable_Kun_Identity_Abstractions_IOidcClient__Microsoft_Extensions_Options_IOptionsMonitor_Kun_Identity_Configuration_KunOidcClientOptions__System_TimeProvider_Microsoft_AspNetCore_DataProtection_IDataProtectionProvider_Kun_Identity_Abstractions_Session_ISessionRegistry\_}

Creates a helper with shared data protection and an atomic replay registry.

```csharp
public ChallengeHelper(IEnumerable<IOidcClient> oidcClients, IOptionsMonitor<KunOidcClientOptions> oidcOptions, TimeProvider timeProvider, IDataProtectionProvider protection, ISessionRegistry registry)
```

#### Parameters

`oidcClients` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable-1)\<IOidcClient>

`oidcOptions` [IOptionsMonitor](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1)\<KunOidcClientOptions>

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

`protection` [IDataProtectionProvider](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.dataprotection.idataprotectionprovider)

`registry` ISessionRegistry

## Fields

### ChallengeCookieName \{#Kun_Identity_AspNetCore_ChallengeHelper_ChallengeCookieName}

Name of the ephemeral challenge cookie.

```csharp
public const string ChallengeCookieName = "kun.challenge"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ChallengeCookieTtl \{#Kun_Identity_AspNetCore_ChallengeHelper_ChallengeCookieTtl}

Lifetime of the challenge cookie. Intentionally short — the IdP round-trip is seconds.

```csharp
public static readonly TimeSpan ChallengeCookieTtl
```

#### Field Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

## Methods

### BuildLoginUrlAsync(HttpContext, string, string, string, string, CancellationToken) \{#Kun_Identity_AspNetCore_ChallengeHelper_BuildLoginUrlAsync_Microsoft_AspNetCore_Http_HttpContext_System_String_System_String_System_String_System_String_System_Threading_CancellationToken\_}

Builds the login redirect URL. Side effect: writes the
[ChallengeHelper.ChallengeCookieName](/pr-preview/pr-50/sdk/api/kun/kun-identity-aspnetcore/challengehelper.md#Kun_Identity_AspNetCore_ChallengeHelper_ChallengeCookieName) cookie onto
`context` — the callback handler must read it
to recover the PKCE verifier + state to validate.

```csharp
public Task<string> BuildLoginUrlAsync(HttpContext context, string oidcClientName, string redirectUri, string returnUrl = "/", string scope = "openid profile email", CancellationToken cancellationToken = default)
```

#### Parameters

`context` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

Current request context.

`oidcClientName` [string](https://learn.microsoft.com/dotnet/api/system.string)

Registered OIDC client name (e.g. `"authentik"`).

`redirectUri` [string](https://learn.microsoft.com/dotnet/api/system.string)

Absolute redirect URL the IdP will send the user back to after
consent. Must match one of the URLs configured on the OIDC
application.

`returnUrl` [string](https://learn.microsoft.com/dotnet/api/system.string)

Local URL to redirect to after sign-in completes; round-tripped
through the challenge cookie. Validation against open-redirect
is the caller's responsibility.

`scope` [string](https://learn.microsoft.com/dotnet/api/system.string)

OIDC scope parameter (default `openid profile email`).

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### ConsumeAsync(HttpContext, string, CancellationToken) \{#Kun_Identity_AspNetCore_ChallengeHelper_ConsumeAsync_Microsoft_AspNetCore_Http_HttpContext_System_String_System_Threading_CancellationToken\_}

Consumes a protected browser transaction after state/expiry checks; rejects replay across replicas.

```csharp
public Task<ChallengePayload?> ConsumeAsync(HttpContext context, string state, CancellationToken cancellationToken = default)
```

#### Parameters

`context` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

`state` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[ChallengePayload](/pr-preview/pr-50/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md)?>

### ConsumeChallengeCookie(HttpContext) \{#Kun_Identity_AspNetCore_ChallengeHelper_ConsumeChallengeCookie_Microsoft_AspNetCore_Http_HttpContext\_}

Compatibility signature only; unprotected challenge consumption is disabled.

```csharp
[Obsolete("Use ConsumeAsync to validate protected state, expiry and replay.", DiagnosticId = "KUNOIDC001")]
public static ChallengePayload? ConsumeChallengeCookie(HttpContext context)
```

#### Parameters

`context` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

#### Returns

[ChallengePayload](/pr-preview/pr-50/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md)?

#### Remarks

Use ConsumeAsync for authenticated encryption, expiry, state and atomic replay checks.
