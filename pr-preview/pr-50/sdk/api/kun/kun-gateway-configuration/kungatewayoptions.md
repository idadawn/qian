> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class KunGatewayOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Gateway)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.Configuration](/pr-preview/pr-50/sdk/api/kun/kun-gateway-configuration/index.md)\
Assembly: Kun.Gateway.dll

Configuration options for the Kun Gateway.
Controls external JWT validation, internal S2S JWT issuance, and key paths.

```csharp
public sealed class KunGatewayOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunGatewayOptions](/pr-preview/pr-50/sdk/api/kun/kun-gateway-configuration/kungatewayoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunGatewayOptions() \{#Kun_Gateway_Configuration_KunGatewayOptions__ctor}

```csharp
public KunGatewayOptions()
```

## Properties

### EmitTraceIdClaim \{#Kun_Gateway_Configuration_KunGatewayOptions_EmitTraceIdClaim}

Retained for configuration compatibility; no longer changes the closed JWT payload.
Trace context is propagated through standard HTTP headers.

```csharp
public bool EmitTraceIdClaim { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### ExposeInternalJwks \{#Kun_Gateway_Configuration_KunGatewayOptions_ExposeInternalJwks}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), the gateway exposes the internal JWKS endpoint
`/internal/.well-known/jwks.json` in non-Development environments. Defaults to
[false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) so the internal endpoint returns 404 in production unless the
operator deliberately opts in. The public endpoint `/.well-known/jwks.json`
remains always-on; this option only gates the internal-network variant.

```csharp
public bool ExposeInternalJwks { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Remarks

Keep [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) in internet-facing Kubernetes deployments and rely on
NetworkPolicy / service-mesh egress rules to isolate the internal route. Flip to
[true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) only when JWKS fetching from internal pods must bypass the
public path (e.g. rate-limiting sidecars that reshape external traffic).

### ExternalAudience \{#Kun_Gateway_Configuration_KunGatewayOptions_ExternalAudience}

The expected audience of the external IdP-issued JWT.

```csharp
public required string ExternalAudience { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ExternalIssuer \{#Kun_Gateway_Configuration_KunGatewayOptions_ExternalIssuer}

The issuer of the external IdP-issued JWT (e.g. `https://login.example.com`).

```csharp
public required string ExternalIssuer { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ExternalJwksUri \{#Kun_Gateway_Configuration_KunGatewayOptions_ExternalJwksUri}

The JWKS endpoint URI of the external IdP, used to fetch public keys for validating incoming JWTs.

```csharp
public required string ExternalJwksUri { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ForwardLimit \{#Kun_Gateway_Configuration_KunGatewayOptions_ForwardLimit}

最多接受的可信代理跳数，默认一跳。

```csharp
public int ForwardLimit { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ForwardedClaimTypes \{#Kun_Gateway_Configuration_KunGatewayOptions_ForwardedClaimTypes}

Compatibility allowlist option. Only canonical scope/session/revision/project/plugin field names are accepted;
the canonical field set is fixed and does not require an allowlist. Arbitrary application claims are rejected.

```csharp
public string[] ForwardedClaimTypes { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)\[]

### InternalAudience \{#Kun_Gateway_Configuration_KunGatewayOptions_InternalAudience}

The audience claim placed in the minted internal S2S JWT. Defaults to `kun-internal`.

```csharp
public string InternalAudience { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### InternalIssuer \{#Kun_Gateway_Configuration_KunGatewayOptions_InternalIssuer}

The issuer claim placed in the minted internal S2S JWT. Defaults to `kun-gateway`.

```csharp
public string InternalIssuer { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### InternalTokenLifetime \{#Kun_Gateway_Configuration_KunGatewayOptions_InternalTokenLifetime}

Lifetime of the minted internal S2S JWT. Defaults to 60 seconds per zero-trust TTL policy.

```csharp
public TimeSpan InternalTokenLifetime { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### KeyOverlapPeriod \{#Kun_Gateway_Configuration_KunGatewayOptions_KeyOverlapPeriod}

轮换后旧公钥的重叠发布时长，默认五分钟；消费者仍须执行令牌寿命验证。

```csharp
public TimeSpan KeyOverlapPeriod { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### KeyReloadInterval \{#Kun_Gateway_Configuration_KunGatewayOptions_KeyReloadInterval}

检查私钥文件变更的最短间隔，默认一秒；零表示每次签发或获取 JWKS 都检查。

```csharp
public TimeSpan KeyReloadInterval { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### PublicKeyPath \{#Kun_Gateway_Configuration_KunGatewayOptions_PublicKeyPath}

Legacy public PEM path; generated in Development and used by standalone JWKS mapping.
With AddKunGateway, JWKS is derived from the active signing key and overlap key ring.

```csharp
public required string PublicKeyPath { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### SigningKeyPath \{#Kun_Gateway_Configuration_KunGatewayOptions_SigningKeyPath}

Absolute path to the PEM-encoded RSA private key file used to sign internal S2S JWTs.

```csharp
public required string SigningKeyPath { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### TrustedProxyAddresses \{#Kun_Gateway_Configuration_KunGatewayOptions_TrustedProxyAddresses}

允许转发客户端地址与协议的代理 IP；缺省不信任任何转发头。

```csharp
public string[] TrustedProxyAddresses { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)\[]

### TrustedProxyNetworks \{#Kun_Gateway_Configuration_KunGatewayOptions_TrustedProxyNetworks}

允许转发客户端地址与协议的代理 CIDR 网段；应只包含实际代理。

```csharp
public string[] TrustedProxyNetworks { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)\[]
