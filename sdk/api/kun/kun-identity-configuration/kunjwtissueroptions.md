> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunJwtIssuerOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Configuration](/sdk/api/kun/kun-identity-configuration/index.md)\
Assembly: Kun.Identity.dll

Signing / claims configuration for `KunJwtIssuer`.

```csharp
[Obsolete("Use Kun.Gateway.S2sTokenIssuer for canonical short-lived S2S tokens.", DiagnosticId = "KUNS2S001")]
public sealed class KunJwtIssuerOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunJwtIssuerOptions](/sdk/api/kun/kun-identity-configuration/kunjwtissueroptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunJwtIssuerOptions() \{#Kun_Identity_Configuration_KunJwtIssuerOptions__ctor}

```csharp
public KunJwtIssuerOptions()
```

## Properties

### Algorithm \{#Kun_Identity_Configuration_KunJwtIssuerOptions_Algorithm}

JWS algorithm; defaults to `RS256`.

```csharp
public string Algorithm { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Audience \{#Kun_Identity_Configuration_KunJwtIssuerOptions_Audience}

Value written to the `aud` claim. Required.

```csharp
public string Audience { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### DefaultLifetime \{#Kun_Identity_Configuration_KunJwtIssuerOptions_DefaultLifetime}

Default token lifetime when the caller does not pass an explicit value. Defaults to 60 seconds.

```csharp
public TimeSpan DefaultLifetime { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### Issuer \{#Kun_Identity_Configuration_KunJwtIssuerOptions_Issuer}

Value written to the `iss` claim. Required.

```csharp
public string Issuer { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### KeyId \{#Kun_Identity_Configuration_KunJwtIssuerOptions_KeyId}

Key ID written to the JWT header (`kid`). Optional — many providers derive it from the key itself.

```csharp
public string? KeyId { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### SigningKey \{#Kun_Identity_Configuration_KunJwtIssuerOptions_SigningKey}

Explicit [`SecurityKey`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.securitykey) override. Primarily for tests
that want to share an in-memory RSA handle with the validator.

```csharp
public SecurityKey? SigningKey { get; set; }
```

#### Property Value

[SecurityKey](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.securitykey)?

### SigningKeyPem \{#Kun_Identity_Configuration_KunJwtIssuerOptions_SigningKeyPem}

RSA private key in PEM format. When set, the issuer builds an
[`RsaSecurityKey`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.rsasecuritykey) from it. Mutually exclusive with
[KunJwtIssuerOptions.SigningKey](/sdk/api/kun/kun-identity-configuration/kunjwtissueroptions.md#Kun_Identity_Configuration_KunJwtIssuerOptions_SigningKey) — caller picks one.

```csharp
public string? SigningKeyPem { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
