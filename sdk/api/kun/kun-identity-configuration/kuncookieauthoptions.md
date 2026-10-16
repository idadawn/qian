> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunCookieAuthOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Configuration](/sdk/api/kun/kun-identity-configuration/index.md)\
Assembly: Kun.Identity.dll

Options for the cookie session layer wired by
`AddKunIdentity(id => id.AddCookieSession(...))`. The defaults
encode the zero-trust baseline `Kun.Gateway` already applies to
outbound cookies.

```csharp
public class KunCookieAuthOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunCookieAuthOptions](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md)

#### Derived

[KunCookieSessionBuilder](/sdk/api/kun/kun-identity-configuration/kuncookiesessionbuilder.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

This options type lives in `Kun.Identity` (not
`Kun.Identity.AspNetCore`) so non-ASP.NET-Core hosts — worker
services that replay a signed-in request, integration tests that
synthesize a session — can configure it without pulling the ASP.NET
Core dependency. [KunCookieAuthOptions.SameSite](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_SameSite) and [KunCookieAuthOptions.Secure](/sdk/api/kun/kun-identity-configuration/kuncookieauthoptions.md#Kun_Identity_Configuration_KunCookieAuthOptions_Secure)
use Kun-owned enums ([KunSameSiteMode](/sdk/api/kun/kun-identity-configuration/kunsamesitemode.md),
[KunCookieSecurePolicy](/sdk/api/kun/kun-identity-configuration/kuncookiesecurepolicy.md)); the middleware in
`Kun.Identity.AspNetCore` maps them to the equivalent ASP.NET
Core values at registration time.

## Constructors

### KunCookieAuthOptions() \{#Kun_Identity_Configuration_KunCookieAuthOptions__ctor}

```csharp
public KunCookieAuthOptions()
```

## Properties

### CookieName \{#Kun_Identity_Configuration_KunCookieAuthOptions_CookieName}

Session cookie name. Default `kun.sid`.

```csharp
public string CookieName { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Domain \{#Kun_Identity_Configuration_KunCookieAuthOptions_Domain}

Cookie `Domain`. Leave [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) to scope the
cookie to the exact host that set it (recommended for
multi-tenant isolation; only override when the session must be
shared across subdomains).

```csharp
public string? Domain { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### HttpOnly \{#Kun_Identity_Configuration_KunCookieAuthOptions_HttpOnly}

Emit the cookie with `HttpOnly`. Default [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool)
and strongly recommended to stay that way — JavaScript must not
be able to read the session id.

```csharp
public bool HttpOnly { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Path \{#Kun_Identity_Configuration_KunCookieAuthOptions_Path}

Cookie `Path`. Default `/`.

```csharp
public string Path { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### RequireAntiforgery \{#Kun_Identity_Configuration_KunCookieAuthOptions_RequireAntiforgery}

Validate antiforgery tokens on unsafe requests authenticated by the session cookie. Defaults to true.

```csharp
public bool RequireAntiforgery { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### SameSite \{#Kun_Identity_Configuration_KunCookieAuthOptions_SameSite}

`SameSite` policy. Default [KunSameSiteMode.Lax](/sdk/api/kun/kun-identity-configuration/kunsamesitemode.md#Kun_Identity_Configuration_KunSameSiteMode_Lax)
so top-level redirects from the IdP's /authorize response still
carry the cookie, while third-party iframes / XHR do not.

```csharp
public KunSameSiteMode SameSite { get; set; }
```

#### Property Value

[KunSameSiteMode](/sdk/api/kun/kun-identity-configuration/kunsamesitemode.md)

### Secure \{#Kun_Identity_Configuration_KunCookieAuthOptions_Secure}

Whether the cookie is emitted only over HTTPS. Default
[KunCookieSecurePolicy.Always](/sdk/api/kun/kun-identity-configuration/kuncookiesecurepolicy.md#Kun_Identity_Configuration_KunCookieSecurePolicy_Always); drop to
[KunCookieSecurePolicy.SameAsRequest](/sdk/api/kun/kun-identity-configuration/kuncookiesecurepolicy.md#Kun_Identity_Configuration_KunCookieSecurePolicy_SameAsRequest) only for
development against `http://localhost`.

```csharp
public KunCookieSecurePolicy Secure { get; set; }
```

#### Property Value

[KunCookieSecurePolicy](/sdk/api/kun/kun-identity-configuration/kuncookiesecurepolicy.md)

### SessionTtl \{#Kun_Identity_Configuration_KunCookieAuthOptions_SessionTtl}

Absolute session lifetime. Default 8 hours. The cookie
`Max-Age` and the [ISessionStore](/sdk/api/kun/kun-identity-abstractions-session/isessionstore.md)
entry TTL are both bound to this value at sign-in.

```csharp
public TimeSpan SessionTtl { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### SlidingExpiration \{#Kun_Identity_Configuration_KunCookieAuthOptions_SlidingExpiration}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), authenticated requests extend the
session on both the cookie and the store (matching the
`SessionTtl` window from the current instant). Default
[true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool).

```csharp
public bool SlidingExpiration { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
