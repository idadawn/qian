> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class OidcLogoutToken

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Abstractions](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/index.md)\
Assembly: Kun.Identity.dll

Validated back-channel logout instruction. Subject or SessionId (or both) identifies sessions within Issuer.

```csharp
public sealed record OidcLogoutToken : IEquatable<OidcLogoutToken>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OidcLogoutToken](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidclogouttoken.md)

#### Implements

[IEquatable\<OidcLogoutToken>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### OidcLogoutToken(string, string?, string?, string, DateTimeOffset) \{#Kun_Identity_Abstractions_OidcLogoutToken__ctor_System_String_System_String_System_String_System_String_System_DateTimeOffset\_}

Validated back-channel logout instruction. Subject or SessionId (or both) identifies sessions within Issuer.

```csharp
public OidcLogoutToken(string Issuer, string? Subject, string? SessionId, string TokenId, DateTimeOffset ExpiresAt)
```

#### Parameters

`Issuer` [string](https://learn.microsoft.com/dotnet/api/system.string)

`Subject` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`SessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`TokenId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

## Properties

### ExpiresAt \{#Kun_Identity_Abstractions_OidcLogoutToken_ExpiresAt}

```csharp
public DateTimeOffset ExpiresAt { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Issuer \{#Kun_Identity_Abstractions_OidcLogoutToken_Issuer}

```csharp
public string Issuer { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### SessionId \{#Kun_Identity_Abstractions_OidcLogoutToken_SessionId}

```csharp
public string? SessionId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Subject \{#Kun_Identity_Abstractions_OidcLogoutToken_Subject}

```csharp
public string? Subject { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TokenId \{#Kun_Identity_Abstractions_OidcLogoutToken_TokenId}

```csharp
public string TokenId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### \<Clone>$() \{#Kun_Identity_Abstractions_OidcLogoutToken__Clone\_\_}

```csharp
public OidcLogoutToken <Clone>$()
```

#### Returns

[OidcLogoutToken](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidclogouttoken.md)

### Deconstruct(out string, out string?, out string?, out string, out DateTimeOffset) \{#Kun_Identity_Abstractions_OidcLogoutToken_Deconstruct_System_String__System_String__System_String__System_String__System_DateTimeOffset\_\_}

```csharp
public void Deconstruct(out string Issuer, out string? Subject, out string? SessionId, out string TokenId, out DateTimeOffset ExpiresAt)
```

#### Parameters

`Issuer` [string](https://learn.microsoft.com/dotnet/api/system.string)

`Subject` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`SessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`TokenId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Equals(object?) \{#Kun_Identity_Abstractions_OidcLogoutToken_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(OidcLogoutToken?) \{#Kun_Identity_Abstractions_OidcLogoutToken_Equals_Kun_Identity_Abstractions_OidcLogoutToken\_}

```csharp
public bool Equals(OidcLogoutToken? other)
```

#### Parameters

`other` [OidcLogoutToken](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidclogouttoken.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Identity_Abstractions_OidcLogoutToken_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Identity_Abstractions_OidcLogoutToken_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(OidcLogoutToken?, OidcLogoutToken?) \{#Kun_Identity_Abstractions_OidcLogoutToken_op_Equality_Kun_Identity_Abstractions_OidcLogoutToken_Kun_Identity_Abstractions_OidcLogoutToken\_}

```csharp
public static bool operator ==(OidcLogoutToken? left, OidcLogoutToken? right)
```

#### Parameters

`left` [OidcLogoutToken](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidclogouttoken.md)?

`right` [OidcLogoutToken](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidclogouttoken.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(OidcLogoutToken?, OidcLogoutToken?) \{#Kun_Identity_Abstractions_OidcLogoutToken_op_Inequality_Kun_Identity_Abstractions_OidcLogoutToken_Kun_Identity_Abstractions_OidcLogoutToken\_}

```csharp
public static bool operator !=(OidcLogoutToken? left, OidcLogoutToken? right)
```

#### Parameters

`left` [OidcLogoutToken](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidclogouttoken.md)?

`right` [OidcLogoutToken](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/oidclogouttoken.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
