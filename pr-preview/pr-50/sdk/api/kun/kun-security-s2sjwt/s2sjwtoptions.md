> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class S2SJwtOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Security)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.S2SJwt](/pr-preview/pr-50/sdk/api/kun/kun-security-s2sjwt/index.md)\
Assembly: Kun.Security.dll

Configuration for S2S JWT issuance and verification using ES256 (ECDSA P-256).
Per the zero-trust policy, ES256 is the required algorithm for legacy S2S tokens.

```csharp
[Obsolete("Use Kun.Gateway.S2sTokenIssuer and Kun.Security service authentication.", DiagnosticId = "KUNS2S001")]
public sealed class S2SJwtOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[S2SJwtOptions](/pr-preview/pr-50/sdk/api/kun/kun-security-s2sjwt/s2sjwtoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### S2SJwtOptions() \{#Kun_Security_S2SJwt_S2SJwtOptions__ctor}

```csharp
public S2SJwtOptions()
```

## Properties

### DefaultLifetimeSeconds \{#Kun_Security_S2SJwt_S2SJwtOptions_DefaultLifetimeSeconds}

Default token lifetime. Defaults to 300 seconds (5 minutes), the upper bound of the
whitepaper §9.6 range ("60 秒到 5 分钟"). Individual calls to
[IS2SJwtIssuer.IssueAsync](/pr-preview/pr-50/sdk/api/kun/kun-security-s2sjwt/is2sjwtissuer.md#Kun_Security_S2SJwt_IS2SJwtIssuer_IssueAsync_Kun_Security_S2SJwt_S2SJwtIssueRequest_System_Threading_CancellationToken_)
may tighten this via [S2SJwtIssueRequest.Lifetime](/pr-preview/pr-50/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md#Kun_Security_S2SJwt_S2SJwtIssueRequest_Lifetime).

```csharp
public int DefaultLifetimeSeconds { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### Issuer \{#Kun_Security_S2SJwt_S2SJwtOptions_Issuer}

Token issuer. Must be `kun-gateway` per contract.

```csharp
public string Issuer { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### KeyId \{#Kun_Security_S2SJwt_S2SJwtOptions_KeyId}

Key identifier for the current signing key.

```csharp
public required string KeyId { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### PreviousKeyId \{#Kun_Security_S2SJwt_S2SJwtOptions_PreviousKeyId}

Key identifier for the previous signing key. Required when [S2SJwtOptions.PreviousSigningKeyPem](/pr-preview/pr-50/sdk/api/kun/kun-security-s2sjwt/s2sjwtoptions.md#Kun_Security_S2SJwt_S2SJwtOptions_PreviousSigningKeyPem) is set.

```csharp
public string? PreviousKeyId { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### PreviousSigningKeyPem \{#Kun_Security_S2SJwt_S2SJwtOptions_PreviousSigningKeyPem}

Previous ECDSA P-256 private key PEM for graceful key rotation. Optional.

```csharp
public string? PreviousSigningKeyPem { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### SigningKeyPem \{#Kun_Security_S2SJwt_S2SJwtOptions_SigningKeyPem}

ECDSA P-256 private key in PEM format for signing tokens.

```csharp
public required string SigningKeyPem { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)
