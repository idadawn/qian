> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunServiceAuthenticationOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.Authentication](/pr-preview/pr-202/sdk/api/kun/kun-security-authentication/index.md)\
Assembly: Kun.Security.dll

Options for configuring S2S JWT Bearer authentication on a downstream service.
Supports both static key sets and dynamic JWKS endpoint resolution.

```csharp
public sealed class KunServiceAuthenticationOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunServiceAuthenticationOptions](/pr-preview/pr-202/sdk/api/kun/kun-security-authentication/kunserviceauthenticationoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunServiceAuthenticationOptions() \{#Kun_Security_Authentication_KunServiceAuthenticationOptions__ctor}

```csharp
public KunServiceAuthenticationOptions()
```

## Properties

### AdditionalValidAudiences \{#Kun_Security_Authentication_KunServiceAuthenticationOptions_AdditionalValidAudiences}

Additional valid audiences for audience migrations / dual-accept rollouts.
[KunServiceAuthenticationOptions.Audience](/pr-preview/pr-202/sdk/api/kun/kun-security-authentication/kunserviceauthenticationoptions.md#Kun_Security_Authentication_KunServiceAuthenticationOptions_Audience) is always included.

```csharp
public IReadOnlyList<string>? AdditionalValidAudiences { get; set; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>?

### AdditionalValidIssuers \{#Kun_Security_Authentication_KunServiceAuthenticationOptions_AdditionalValidIssuers}

Additional valid issuers for multi-issuer / blue-green gateway deployments.
[KunServiceAuthenticationOptions.Issuer](/pr-preview/pr-202/sdk/api/kun/kun-security-authentication/kunserviceauthenticationoptions.md#Kun_Security_Authentication_KunServiceAuthenticationOptions_Issuer) is always included.

```csharp
public IReadOnlyList<string>? AdditionalValidIssuers { get; set; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>?

### Audience \{#Kun_Security_Authentication_KunServiceAuthenticationOptions_Audience}

Expected audience. Defaults to `kun-internal`.

```csharp
public string Audience { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Issuer \{#Kun_Security_Authentication_KunServiceAuthenticationOptions_Issuer}

Expected token issuer. Required.

```csharp
public string Issuer { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### JwksUri \{#Kun_Security_Authentication_KunServiceAuthenticationOptions_JwksUri}

Optional URI to a remote JWKS endpoint for fetching signing keys.
Mutually exclusive with [KunServiceAuthenticationOptions.StaticKeySet](/pr-preview/pr-202/sdk/api/kun/kun-security-authentication/kunserviceauthenticationoptions.md#Kun_Security_Authentication_KunServiceAuthenticationOptions_StaticKeySet).

```csharp
public string? JwksUri { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### StaticKeySet \{#Kun_Security_Authentication_KunServiceAuthenticationOptions_StaticKeySet}

Optional statically-configured key set (e.g. from file or Key Vault).
Mutually exclusive with [KunServiceAuthenticationOptions.JwksUri](/pr-preview/pr-202/sdk/api/kun/kun-security-authentication/kunserviceauthenticationoptions.md#Kun_Security_Authentication_KunServiceAuthenticationOptions_JwksUri).

```csharp
public JsonWebKeySet? StaticKeySet { get; set; }
```

#### Property Value

[JsonWebKeySet](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.jsonwebkeyset)?
