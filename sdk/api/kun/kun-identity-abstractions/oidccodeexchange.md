> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class OidcCodeExchange

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Identity)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Abstractions](/sdk/api/kun/kun-identity-abstractions/index.md)\
Assembly: Kun.Identity.dll

Authorization code and the original browser transaction's PKCE verifier and nonce.

```csharp
public sealed record OidcCodeExchange : IEquatable<OidcCodeExchange>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[OidcCodeExchange](/sdk/api/kun/kun-identity-abstractions/oidccodeexchange.md)

#### Implements

[IEquatable\<OidcCodeExchange>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### OidcCodeExchange(string, string, string, string) \{#Kun_Identity_Abstractions_OidcCodeExchange__ctor_System_String_System_String_System_String_System_String\_}

Authorization code and the original browser transaction's PKCE verifier and nonce.

```csharp
public OidcCodeExchange(string Code, string RedirectUri, string CodeVerifier, string ExpectedNonce)
```

#### Parameters

`Code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`RedirectUri` [string](https://learn.microsoft.com/dotnet/api/system.string)

`CodeVerifier` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ExpectedNonce` [string](https://learn.microsoft.com/dotnet/api/system.string)

## Properties

### Code \{#Kun_Identity_Abstractions_OidcCodeExchange_Code}

```csharp
public string Code { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### CodeVerifier \{#Kun_Identity_Abstractions_OidcCodeExchange_CodeVerifier}

```csharp
public string CodeVerifier { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ExpectedNonce \{#Kun_Identity_Abstractions_OidcCodeExchange_ExpectedNonce}

```csharp
public string ExpectedNonce { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### RedirectUri \{#Kun_Identity_Abstractions_OidcCodeExchange_RedirectUri}

```csharp
public string RedirectUri { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### \<Clone>$() \{#Kun_Identity_Abstractions_OidcCodeExchange__Clone\_\_}

```csharp
public OidcCodeExchange <Clone>$()
```

#### Returns

[OidcCodeExchange](/sdk/api/kun/kun-identity-abstractions/oidccodeexchange.md)

### Deconstruct(out string, out string, out string, out string) \{#Kun_Identity_Abstractions_OidcCodeExchange_Deconstruct_System_String__System_String__System_String__System_String\_\_}

```csharp
public void Deconstruct(out string Code, out string RedirectUri, out string CodeVerifier, out string ExpectedNonce)
```

#### Parameters

`Code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`RedirectUri` [string](https://learn.microsoft.com/dotnet/api/system.string)

`CodeVerifier` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ExpectedNonce` [string](https://learn.microsoft.com/dotnet/api/system.string)

### Equals(object?) \{#Kun_Identity_Abstractions_OidcCodeExchange_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(OidcCodeExchange?) \{#Kun_Identity_Abstractions_OidcCodeExchange_Equals_Kun_Identity_Abstractions_OidcCodeExchange\_}

```csharp
public bool Equals(OidcCodeExchange? other)
```

#### Parameters

`other` [OidcCodeExchange](/sdk/api/kun/kun-identity-abstractions/oidccodeexchange.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Identity_Abstractions_OidcCodeExchange_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Identity_Abstractions_OidcCodeExchange_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(OidcCodeExchange?, OidcCodeExchange?) \{#Kun_Identity_Abstractions_OidcCodeExchange_op_Equality_Kun_Identity_Abstractions_OidcCodeExchange_Kun_Identity_Abstractions_OidcCodeExchange\_}

```csharp
public static bool operator ==(OidcCodeExchange? left, OidcCodeExchange? right)
```

#### Parameters

`left` [OidcCodeExchange](/sdk/api/kun/kun-identity-abstractions/oidccodeexchange.md)?

`right` [OidcCodeExchange](/sdk/api/kun/kun-identity-abstractions/oidccodeexchange.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(OidcCodeExchange?, OidcCodeExchange?) \{#Kun_Identity_Abstractions_OidcCodeExchange_op_Inequality_Kun_Identity_Abstractions_OidcCodeExchange_Kun_Identity_Abstractions_OidcCodeExchange\_}

```csharp
public static bool operator !=(OidcCodeExchange? left, OidcCodeExchange? right)
```

#### Parameters

`left` [OidcCodeExchange](/sdk/api/kun/kun-identity-abstractions/oidccodeexchange.md)?

`right` [OidcCodeExchange](/sdk/api/kun/kun-identity-abstractions/oidccodeexchange.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
