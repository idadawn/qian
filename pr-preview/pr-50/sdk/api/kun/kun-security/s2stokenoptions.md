> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class S2sTokenOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Security)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security](/pr-preview/pr-50/sdk/api/kun/kun-security/index.md)\
Assembly: Kun.Security.dll

Configuration for S2S JWT issuance and validation.
Algorithm is always asymmetric (RS256/ES256) — HS256 is rejected per zero-trust policy.

```csharp
public sealed record S2sTokenOptions : IEquatable<S2sTokenOptions>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[S2sTokenOptions](/pr-preview/pr-50/sdk/api/kun/kun-security/s2stokenoptions.md)

#### Implements

[IEquatable\<S2sTokenOptions>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### S2sTokenOptions() \{#Kun_Security_S2sTokenOptions__ctor}

```csharp
public S2sTokenOptions()
```

## Properties

### Audience \{#Kun_Security_S2sTokenOptions_Audience}

Expected audience. Defaults to `kun-internal`.

```csharp
public string Audience { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Issuer \{#Kun_Security_S2sTokenOptions_Issuer}

Token issuer (e.g. `https://kun-gateway.internal`).

```csharp
public required string Issuer { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### JwksUri \{#Kun_Security_S2sTokenOptions_JwksUri}

Absolute or relative URI to the JWKS endpoint on the internal network.
Used by downstream services to fetch public keys for validation.

```csharp
public string? JwksUri { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Lifetime \{#Kun_Security_S2sTokenOptions_Lifetime}

Token lifetime. Defaults to 60 seconds per zero-trust TTL policy.

```csharp
public TimeSpan Lifetime { get; init; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### SigningAlgorithm \{#Kun_Security_S2sTokenOptions_SigningAlgorithm}

Signing algorithm. Must be an asymmetric algorithm (RS256 or ES256).
HS256 is explicitly forbidden — tokens signed with HS256 are rejected.

```csharp
public string SigningAlgorithm { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ValidIssuers \{#Kun_Security_S2sTokenOptions_ValidIssuers}

Additional valid issuers for multi-issuer scenarios (e.g. blue/green gateway deployments).
[S2sTokenOptions.Issuer](/pr-preview/pr-50/sdk/api/kun/kun-security/s2stokenoptions.md#Kun_Security_S2sTokenOptions_Issuer) is always included; this list supplements it.

```csharp
public IReadOnlyList<string>? ValidIssuers { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>?

## Methods

### \<Clone>$() \{#Kun_Security_S2sTokenOptions__Clone\_\_}

```csharp
public S2sTokenOptions <Clone>$()
```

#### Returns

[S2sTokenOptions](/pr-preview/pr-50/sdk/api/kun/kun-security/s2stokenoptions.md)

### Equals(object?) \{#Kun_Security_S2sTokenOptions_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(S2sTokenOptions?) \{#Kun_Security_S2sTokenOptions_Equals_Kun_Security_S2sTokenOptions\_}

```csharp
public bool Equals(S2sTokenOptions? other)
```

#### Parameters

`other` [S2sTokenOptions](/pr-preview/pr-50/sdk/api/kun/kun-security/s2stokenoptions.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Security_S2sTokenOptions_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Security_S2sTokenOptions_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(S2sTokenOptions?, S2sTokenOptions?) \{#Kun_Security_S2sTokenOptions_op_Equality_Kun_Security_S2sTokenOptions_Kun_Security_S2sTokenOptions\_}

```csharp
public static bool operator ==(S2sTokenOptions? left, S2sTokenOptions? right)
```

#### Parameters

`left` [S2sTokenOptions](/pr-preview/pr-50/sdk/api/kun/kun-security/s2stokenoptions.md)?

`right` [S2sTokenOptions](/pr-preview/pr-50/sdk/api/kun/kun-security/s2stokenoptions.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(S2sTokenOptions?, S2sTokenOptions?) \{#Kun_Security_S2sTokenOptions_op_Inequality_Kun_Security_S2sTokenOptions_Kun_Security_S2sTokenOptions\_}

```csharp
public static bool operator !=(S2sTokenOptions? left, S2sTokenOptions? right)
```

#### Parameters

`left` [S2sTokenOptions](/pr-preview/pr-50/sdk/api/kun/kun-security/s2stokenoptions.md)?

`right` [S2sTokenOptions](/pr-preview/pr-50/sdk/api/kun/kun-security/s2stokenoptions.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
