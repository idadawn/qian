> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class OidcTokenResponse

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Abstractions](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/index.md)\
Assembly: Kun.Identity.dll

Result of an OIDC authorization-code exchange.

```csharp
public sealed record OidcTokenResponse : IEquatable<OidcTokenResponse>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OidcTokenResponse](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidctokenresponse.md)

#### Implements

[IEquatable\<OidcTokenResponse>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### OidcTokenResponse(string, string?, string?, string, int) \{#Kun_Identity_Abstractions_OidcTokenResponse__ctor_System_String_System_String_System_String_System_String_System_Int32\_}

Result of an OIDC authorization-code exchange.

```csharp
public OidcTokenResponse(string AccessToken, string? IdToken, string? RefreshToken, string TokenType, int ExpiresIn)
```

#### Parameters

`AccessToken` [string](https://learn.microsoft.com/dotnet/api/system.string)

The bearer access token to present to resource servers.

`IdToken` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The OIDC id-token; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the provider does not issue one for this grant.

`RefreshToken` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Refresh token to obtain new access tokens; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when not offered.

`TokenType` [string](https://learn.microsoft.com/dotnet/api/system.string)

Token type hint returned by the server (typically `Bearer`).

`ExpiresIn` [int](https://learn.microsoft.com/dotnet/api/system.int32)

Access-token lifetime in seconds as reported by the server.

## Properties

### AccessToken \{#Kun_Identity_Abstractions_OidcTokenResponse_AccessToken}

The bearer access token to present to resource servers.

```csharp
public string AccessToken { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ExpiresIn \{#Kun_Identity_Abstractions_OidcTokenResponse_ExpiresIn}

Access-token lifetime in seconds as reported by the server.

```csharp
public int ExpiresIn { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### IdToken \{#Kun_Identity_Abstractions_OidcTokenResponse_IdToken}

The OIDC id-token; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the provider does not issue one for this grant.

```csharp
public string? IdToken { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### RefreshToken \{#Kun_Identity_Abstractions_OidcTokenResponse_RefreshToken}

Refresh token to obtain new access tokens; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when not offered.

```csharp
public string? RefreshToken { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TokenType \{#Kun_Identity_Abstractions_OidcTokenResponse_TokenType}

Token type hint returned by the server (typically `Bearer`).

```csharp
public string TokenType { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ValidatedPrincipal \{#Kun_Identity_Abstractions_OidcTokenResponse_ValidatedPrincipal}

Identity validated by the OIDC client for this grant; null for machine grants without an ID Token.

```csharp
public ClaimsPrincipal? ValidatedPrincipal { get; }
```

#### Property Value

[ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)?

## Methods

### \<Clone>$() \{#Kun_Identity_Abstractions_OidcTokenResponse__Clone\_\_}

```csharp
public OidcTokenResponse <Clone>$()
```

#### Returns

[OidcTokenResponse](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidctokenresponse.md)

### Deconstruct(out string, out string?, out string?, out string, out int) \{#Kun_Identity_Abstractions_OidcTokenResponse_Deconstruct_System_String__System_String__System_String__System_String__System_Int32\_\_}

```csharp
public void Deconstruct(out string AccessToken, out string? IdToken, out string? RefreshToken, out string TokenType, out int ExpiresIn)
```

#### Parameters

`AccessToken` [string](https://learn.microsoft.com/dotnet/api/system.string)

`IdToken` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`RefreshToken` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`TokenType` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ExpiresIn` [int](https://learn.microsoft.com/dotnet/api/system.int32)

### Equals(object?) \{#Kun_Identity_Abstractions_OidcTokenResponse_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(OidcTokenResponse?) \{#Kun_Identity_Abstractions_OidcTokenResponse_Equals_Kun_Identity_Abstractions_OidcTokenResponse\_}

```csharp
public bool Equals(OidcTokenResponse? other)
```

#### Parameters

`other` [OidcTokenResponse](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidctokenresponse.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Identity_Abstractions_OidcTokenResponse_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Identity_Abstractions_OidcTokenResponse_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(OidcTokenResponse?, OidcTokenResponse?) \{#Kun_Identity_Abstractions_OidcTokenResponse_op_Equality_Kun_Identity_Abstractions_OidcTokenResponse_Kun_Identity_Abstractions_OidcTokenResponse\_}

```csharp
public static bool operator ==(OidcTokenResponse? left, OidcTokenResponse? right)
```

#### Parameters

`left` [OidcTokenResponse](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidctokenresponse.md)?

`right` [OidcTokenResponse](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidctokenresponse.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(OidcTokenResponse?, OidcTokenResponse?) \{#Kun_Identity_Abstractions_OidcTokenResponse_op_Inequality_Kun_Identity_Abstractions_OidcTokenResponse_Kun_Identity_Abstractions_OidcTokenResponse\_}

```csharp
public static bool operator !=(OidcTokenResponse? left, OidcTokenResponse? right)
```

#### Parameters

`left` [OidcTokenResponse](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidctokenresponse.md)?

`right` [OidcTokenResponse](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidctokenresponse.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
