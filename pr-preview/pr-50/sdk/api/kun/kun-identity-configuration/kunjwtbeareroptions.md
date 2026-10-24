> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class KunJwtBearerOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Identity)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Configuration](/pr-preview/pr-50/sdk/api/kun/kun-identity-configuration/index.md)\
Assembly: Kun.Identity.dll

Validation parameters for external Bearer JWTs issued by an OIDC
provider (Authentik, Keycloak, Auth0, Azure AD, …). Feeds
`KunJwtValidator`.

```csharp
public sealed class KunJwtBearerOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunJwtBearerOptions](/pr-preview/pr-50/sdk/api/kun/kun-identity-configuration/kunjwtbeareroptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunJwtBearerOptions() \{#Kun_Identity_Configuration_KunJwtBearerOptions__ctor}

```csharp
public KunJwtBearerOptions()
```

## Properties

### Audience \{#Kun_Identity_Configuration_KunJwtBearerOptions_Audience}

Expected `aud` claim. Required — must be set explicitly.

```csharp
public string? Audience { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ClockSkew \{#Kun_Identity_Configuration_KunJwtBearerOptions_ClockSkew}

Allowed wall-clock skew between issuer and verifier (default 60 seconds).

```csharp
public TimeSpan ClockSkew { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### Issuer \{#Kun_Identity_Configuration_KunJwtBearerOptions_Issuer}

Expected `iss` claim. Required — must be set explicitly.

```csharp
public string? Issuer { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### IssuerSigningKeys \{#Kun_Identity_Configuration_KunJwtBearerOptions_IssuerSigningKeys}

Static signing keys — overrides [KunJwtBearerOptions.JwksUri](/pr-preview/pr-50/sdk/api/kun/kun-identity-configuration/kunjwtbeareroptions.md#Kun_Identity_Configuration_KunJwtBearerOptions_JwksUri) when set. Primarily for tests.

```csharp
public IList<SecurityKey> IssuerSigningKeys { get; }
```

#### Property Value

[IList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ilist-1)\<[SecurityKey](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.securitykey)>

### JwksUri \{#Kun_Identity_Configuration_KunJwtBearerOptions_JwksUri}

OIDC JWKS URL. When set, signing keys are fetched and rotated
automatically. Leave [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) and populate
[KunJwtBearerOptions.IssuerSigningKeys](/pr-preview/pr-50/sdk/api/kun/kun-identity-configuration/kunjwtbeareroptions.md#Kun_Identity_Configuration_KunJwtBearerOptions_IssuerSigningKeys) for offline / test scenarios.

```csharp
public string? JwksUri { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### RequireExpirationTime \{#Kun_Identity_Configuration_KunJwtBearerOptions_RequireExpirationTime}

Reject tokens without an `exp` claim (default [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool)).

```csharp
public bool RequireExpirationTime { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### ValidateSignature \{#Kun_Identity_Configuration_KunJwtBearerOptions_ValidateSignature}

Validate the token's signature (default [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool)).
Only disable for tests — accepting unsigned tokens in production is a critical security risk.

```csharp
[Obsolete("Only disable for tests; will be removed in a future version.")]
public bool ValidateSignature { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
