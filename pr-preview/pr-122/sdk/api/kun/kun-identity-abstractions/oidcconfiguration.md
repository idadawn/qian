> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class OidcConfiguration

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Abstractions](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/index.md)\
Assembly: Kun.Identity.dll

Subset of an OpenID Connect discovery document surfaced by [IOidcClient.DiscoverAsync](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/ioidcclient.md#Kun_Identity_Abstractions_IOidcClient_DiscoverAsync_System_Threading_CancellationToken_).

```csharp
public sealed record OidcConfiguration : IEquatable<OidcConfiguration>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OidcConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidcconfiguration.md)

#### Implements

[IEquatable\<OidcConfiguration>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### OidcConfiguration(string, string, string, string?, string) \{#Kun_Identity_Abstractions_OidcConfiguration__ctor_System_String_System_String_System_String_System_String_System_String\_}

Subset of an OpenID Connect discovery document surfaced by [IOidcClient.DiscoverAsync](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/ioidcclient.md#Kun_Identity_Abstractions_IOidcClient_DiscoverAsync_System_Threading_CancellationToken_).

```csharp
public OidcConfiguration(string Issuer, string AuthorizationEndpoint, string TokenEndpoint, string? UserInfoEndpoint, string JwksUri)
```

#### Parameters

`Issuer` [string](https://learn.microsoft.com/dotnet/api/system.string)

Canonical issuer URL as published by the provider.

`AuthorizationEndpoint` [string](https://learn.microsoft.com/dotnet/api/system.string)

URL for starting the authorization-code flow.

`TokenEndpoint` [string](https://learn.microsoft.com/dotnet/api/system.string)

URL for token exchange.

`UserInfoEndpoint` [string](https://learn.microsoft.com/dotnet/api/system.string)?

URL for fetching user claims; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the provider does not publish one.

`JwksUri` [string](https://learn.microsoft.com/dotnet/api/system.string)

URL for the JSON Web Key Set used to validate issued tokens.

## Properties

### AuthorizationEndpoint \{#Kun_Identity_Abstractions_OidcConfiguration_AuthorizationEndpoint}

URL for starting the authorization-code flow.

```csharp
public string AuthorizationEndpoint { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Issuer \{#Kun_Identity_Abstractions_OidcConfiguration_Issuer}

Canonical issuer URL as published by the provider.

```csharp
public string Issuer { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### JwksUri \{#Kun_Identity_Abstractions_OidcConfiguration_JwksUri}

URL for the JSON Web Key Set used to validate issued tokens.

```csharp
public string JwksUri { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### TokenEndpoint \{#Kun_Identity_Abstractions_OidcConfiguration_TokenEndpoint}

URL for token exchange.

```csharp
public string TokenEndpoint { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### UserInfoEndpoint \{#Kun_Identity_Abstractions_OidcConfiguration_UserInfoEndpoint}

URL for fetching user claims; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the provider does not publish one.

```csharp
public string? UserInfoEndpoint { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

## Methods

### \<Clone>$() \{#Kun_Identity_Abstractions_OidcConfiguration__Clone\_\_}

```csharp
public OidcConfiguration <Clone>$()
```

#### Returns

[OidcConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidcconfiguration.md)

### Deconstruct(out string, out string, out string, out string?, out string) \{#Kun_Identity_Abstractions_OidcConfiguration_Deconstruct_System_String__System_String__System_String__System_String__System_String\_\_}

```csharp
public void Deconstruct(out string Issuer, out string AuthorizationEndpoint, out string TokenEndpoint, out string? UserInfoEndpoint, out string JwksUri)
```

#### Parameters

`Issuer` [string](https://learn.microsoft.com/dotnet/api/system.string)

`AuthorizationEndpoint` [string](https://learn.microsoft.com/dotnet/api/system.string)

`TokenEndpoint` [string](https://learn.microsoft.com/dotnet/api/system.string)

`UserInfoEndpoint` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`JwksUri` [string](https://learn.microsoft.com/dotnet/api/system.string)

### Equals(object?) \{#Kun_Identity_Abstractions_OidcConfiguration_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(OidcConfiguration?) \{#Kun_Identity_Abstractions_OidcConfiguration_Equals_Kun_Identity_Abstractions_OidcConfiguration\_}

```csharp
public bool Equals(OidcConfiguration? other)
```

#### Parameters

`other` [OidcConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidcconfiguration.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Identity_Abstractions_OidcConfiguration_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Identity_Abstractions_OidcConfiguration_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(OidcConfiguration?, OidcConfiguration?) \{#Kun_Identity_Abstractions_OidcConfiguration_op_Equality_Kun_Identity_Abstractions_OidcConfiguration_Kun_Identity_Abstractions_OidcConfiguration\_}

```csharp
public static bool operator ==(OidcConfiguration? left, OidcConfiguration? right)
```

#### Parameters

`left` [OidcConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidcconfiguration.md)?

`right` [OidcConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidcconfiguration.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(OidcConfiguration?, OidcConfiguration?) \{#Kun_Identity_Abstractions_OidcConfiguration_op_Inequality_Kun_Identity_Abstractions_OidcConfiguration_Kun_Identity_Abstractions_OidcConfiguration\_}

```csharp
public static bool operator !=(OidcConfiguration? left, OidcConfiguration? right)
```

#### Parameters

`left` [OidcConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidcconfiguration.md)?

`right` [OidcConfiguration](/pr-preview/pr-122/sdk/api/kun/kun-identity-abstractions/oidcconfiguration.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
