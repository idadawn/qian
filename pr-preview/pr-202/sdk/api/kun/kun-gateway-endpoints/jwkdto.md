> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class JwkDto

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.Endpoints](/pr-preview/pr-202/sdk/api/kun/kun-gateway-endpoints/index.md)\
Assembly: Kun.Gateway.dll

A single JSON Web Key entry in the JWKS response.

```csharp
public sealed class JwkDto
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[JwkDto](/pr-preview/pr-202/sdk/api/kun/kun-gateway-endpoints/jwkdto.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### JwkDto(JsonWebKey) \{#Kun_Gateway_Endpoints_JwkDto__ctor_Microsoft_IdentityModel_Tokens_JsonWebKey\_}

Initializes the DTO from a [`JsonWebKey`](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.jsonwebkey).

```csharp
public JwkDto(JsonWebKey jwk)
```

#### Parameters

`jwk` [JsonWebKey](https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.jsonwebkey)

## Properties

### Alg \{#Kun_Gateway_Endpoints_JwkDto_Alg}

Algorithm (e.g. `RS256`).

```csharp
[JsonPropertyName("alg")]
public string? Alg { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### E \{#Kun_Gateway_Endpoints_JwkDto_E}

RSA public exponent (Base64Url-encoded).

```csharp
[JsonPropertyName("e")]
public string? E { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Kid \{#Kun_Gateway_Endpoints_JwkDto_Kid}

Key ID.

```csharp
[JsonPropertyName("kid")]
public string? Kid { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Kty \{#Kun_Gateway_Endpoints_JwkDto_Kty}

Key type (e.g. `RSA`).

```csharp
[JsonPropertyName("kty")]
public string Kty { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### N \{#Kun_Gateway_Endpoints_JwkDto_N}

RSA modulus (Base64Url-encoded).

```csharp
[JsonPropertyName("n")]
public string? N { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Use \{#Kun_Gateway_Endpoints_JwkDto_Use}

Key use (e.g. `sig`).

```csharp
[JsonPropertyName("use")]
public string? Use { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
