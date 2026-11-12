> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class SignInHelper

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity.AspNetCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity.AspNetCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.AspNetCore](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/index.md)\
Assembly: Kun.Identity.AspNetCore.dll

Turns a successful OIDC code-exchange into a signed-in browser
session: writes the [SessionData](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md) into the configured
[ISessionStore](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions-session/isessionstore.md) and emits the opaque session cookie on
the current response.

```csharp
public sealed class SignInHelper
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SignInHelper](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/signinhelper.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SignInHelper(ISessionStore, IJwtValidator, IOptions\<KunCookieAuthOptions>, TimeProvider) \{#Kun_Identity_AspNetCore_SignInHelper__ctor_Kun_Identity_Abstractions_Session_ISessionStore_Kun_Identity_Abstractions_IJwtValidator_Microsoft_Extensions_Options_IOptions_Kun_Identity_Configuration_KunCookieAuthOptions__System_TimeProvider\_}

Creates a new [SignInHelper](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/signinhelper.md).

```csharp
public SignInHelper(ISessionStore store, IJwtValidator idTokenValidator, IOptions<KunCookieAuthOptions> options, TimeProvider timeProvider)
```

#### Parameters

`store` ISessionStore

`idTokenValidator` IJwtValidator

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<KunCookieAuthOptions>

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

### SignInHelper(ISessionStore, IOptions\<KunCookieAuthOptions>, TimeProvider) \{#Kun_Identity_AspNetCore_SignInHelper__ctor_Kun_Identity_Abstractions_Session_ISessionStore_Microsoft_Extensions_Options_IOptions_Kun_Identity_Configuration_KunCookieAuthOptions__System_TimeProvider\_}

Creates a helper for nonce-validated OIDC responses and trusted application session data.

```csharp
public SignInHelper(ISessionStore store, IOptions<KunCookieAuthOptions> options, TimeProvider timeProvider)
```

#### Parameters

`store` ISessionStore

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<KunCookieAuthOptions>

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

## Methods

### SignInAsync(HttpContext, SessionData, CancellationToken) \{#Kun_Identity_AspNetCore_SignInHelper_SignInAsync_Microsoft_AspNetCore_Http_HttpContext_Kun_Identity_Abstractions_Session_SessionData_System_Threading_CancellationToken\_}

Writes `data` to the session store and emits
the cookie on `context`. Returns the newly
minted opaque session id (256 bits of entropy, base64url-encoded).

```csharp
public Task<string> SignInAsync(HttpContext context, SessionData data, CancellationToken cancellationToken = default)
```

#### Parameters

`context` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

`data` SessionData

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### SignInAsync(HttpContext, OidcTokenResponse, CancellationToken) \{#Kun_Identity_AspNetCore_SignInHelper_SignInAsync_Microsoft_AspNetCore_Http_HttpContext_Kun_Identity_Abstractions_OidcTokenResponse_System_Threading_CancellationToken\_}

Accepts only `tokens` with a client-validated identity,
builds [SessionData](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md) from those claims, and delegates to
[SignInHelper.SignInAsync](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/signinhelper.md#Kun_Identity_AspNetCore_SignInHelper_SignInAsync_Microsoft_AspNetCore_Http_HttpContext_Kun_Identity_Abstractions_Session_SessionData_System_Threading_CancellationToken_).

```csharp
public Task<string> SignInAsync(HttpContext context, OidcTokenResponse tokens, CancellationToken cancellationToken = default)
```

#### Parameters

`context` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

`tokens` OidcTokenResponse

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

#### Remarks

The id-token is validated — not merely parsed — before any
claim is trusted. An id-token that fails validation throws
[`InvalidOperationException`](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception); callers decide
whether to surface a 400 or a 401.

### SignOutAsync(HttpContext, CancellationToken) \{#Kun_Identity_AspNetCore_SignInHelper_SignOutAsync_Microsoft_AspNetCore_Http_HttpContext_System_Threading_CancellationToken\_}

Removes the cookie and deletes the corresponding session entry.
Safe to call on anonymous requests — a no-op when no cookie is
present.

```csharp
public Task SignOutAsync(HttpContext context, CancellationToken cancellationToken = default)
```

#### Parameters

`context` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
