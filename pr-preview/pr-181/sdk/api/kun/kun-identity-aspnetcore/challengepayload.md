> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class ChallengePayload

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity.AspNetCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity.AspNetCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.AspNetCore](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/index.md)\
Assembly: Kun.Identity.AspNetCore.dll

Payload written to the one-shot challenge cookie by
[ChallengeHelper.BuildLoginUrlAsync](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengehelper.md#Kun_Identity_AspNetCore_ChallengeHelper_BuildLoginUrlAsync_Microsoft_AspNetCore_Http_HttpContext_System_String_System_String_System_String_System_String_System_Threading_CancellationToken_). Consumer
callback handlers read it via [ChallengeHelper.ConsumeAsync](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengehelper.md#Kun_Identity_AspNetCore_ChallengeHelper_ConsumeAsync_Microsoft_AspNetCore_Http_HttpContext_System_String_System_Threading_CancellationToken_).

```csharp
public sealed record ChallengePayload : IEquatable<ChallengePayload>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ChallengePayload](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md)

#### Implements

[IEquatable\<ChallengePayload>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ChallengePayload(string, string, string, string, string) \{#Kun_Identity_AspNetCore_ChallengePayload__ctor_System_String_System_String_System_String_System_String_System_String\_}

Payload written to the one-shot challenge cookie by
[ChallengeHelper.BuildLoginUrlAsync](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengehelper.md#Kun_Identity_AspNetCore_ChallengeHelper_BuildLoginUrlAsync_Microsoft_AspNetCore_Http_HttpContext_System_String_System_String_System_String_System_String_System_Threading_CancellationToken_). Consumer
callback handlers read it via [ChallengeHelper.ConsumeAsync](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengehelper.md#Kun_Identity_AspNetCore_ChallengeHelper_ConsumeAsync_Microsoft_AspNetCore_Http_HttpContext_System_String_System_Threading_CancellationToken_).

```csharp
public ChallengePayload(string Client, string CodeVerifier, string State, string Nonce, string ReturnUrl)
```

#### Parameters

`Client` [string](https://learn.microsoft.com/dotnet/api/system.string)

OIDC client name.

`CodeVerifier` [string](https://learn.microsoft.com/dotnet/api/system.string)

PKCE code_verifier to replay at the token endpoint.

`State` [string](https://learn.microsoft.com/dotnet/api/system.string)

Anti-CSRF state; match against the `state` query arg.

`Nonce` [string](https://learn.microsoft.com/dotnet/api/system.string)

id-token nonce; match against the id-token `nonce` claim.

`ReturnUrl` [string](https://learn.microsoft.com/dotnet/api/system.string)

Post-login return URL chosen by the caller.

## Properties

### Client \{#Kun_Identity_AspNetCore_ChallengePayload_Client}

OIDC client name.

```csharp
public string Client { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### CodeVerifier \{#Kun_Identity_AspNetCore_ChallengePayload_CodeVerifier}

PKCE code_verifier to replay at the token endpoint.

```csharp
public string CodeVerifier { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Nonce \{#Kun_Identity_AspNetCore_ChallengePayload_Nonce}

id-token nonce; match against the id-token `nonce` claim.

```csharp
public string Nonce { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### RedirectUri \{#Kun_Identity_AspNetCore_ChallengePayload_RedirectUri}

Original redirect URI, bound into the protected login transaction.

```csharp
public string? RedirectUri { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ReturnUrl \{#Kun_Identity_AspNetCore_ChallengePayload_ReturnUrl}

Post-login return URL chosen by the caller.

```csharp
public string ReturnUrl { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### State \{#Kun_Identity_AspNetCore_ChallengePayload_State}

Anti-CSRF state; match against the `state` query arg.

```csharp
public string State { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### \<Clone>$() \{#Kun_Identity_AspNetCore_ChallengePayload__Clone\_\_}

```csharp
public ChallengePayload <Clone>$()
```

#### Returns

[ChallengePayload](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md)

### Deconstruct(out string, out string, out string, out string, out string) \{#Kun_Identity_AspNetCore_ChallengePayload_Deconstruct_System_String__System_String__System_String__System_String__System_String\_\_}

```csharp
public void Deconstruct(out string Client, out string CodeVerifier, out string State, out string Nonce, out string ReturnUrl)
```

#### Parameters

`Client` [string](https://learn.microsoft.com/dotnet/api/system.string)

`CodeVerifier` [string](https://learn.microsoft.com/dotnet/api/system.string)

`State` [string](https://learn.microsoft.com/dotnet/api/system.string)

`Nonce` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ReturnUrl` [string](https://learn.microsoft.com/dotnet/api/system.string)

### Equals(object?) \{#Kun_Identity_AspNetCore_ChallengePayload_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(ChallengePayload?) \{#Kun_Identity_AspNetCore_ChallengePayload_Equals_Kun_Identity_AspNetCore_ChallengePayload\_}

```csharp
public bool Equals(ChallengePayload? other)
```

#### Parameters

`other` [ChallengePayload](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Identity_AspNetCore_ChallengePayload_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### Serialize() \{#Kun_Identity_AspNetCore_ChallengePayload_Serialize}

Serializes the internal payload before authenticated encryption; never write this plaintext directly to a cookie.

```csharp
public string Serialize()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

#### Remarks

Pipe-separated format keeps the encoding reflection-free and
auditable at a glance. Each field is assumed to be url-safe
(base64url for the random components, a local URL for
[ChallengePayload.ReturnUrl](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md#Kun_Identity_AspNetCore_ChallengePayload_ReturnUrl)); consumers must validate
[ChallengePayload.ReturnUrl](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md#Kun_Identity_AspNetCore_ChallengePayload_ReturnUrl) against their own open-redirect policy.

### ToString() \{#Kun_Identity_AspNetCore_ChallengePayload_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

### TryParse(string, out ChallengePayload?) \{#Kun_Identity_AspNetCore_ChallengePayload_TryParse_System_String_Kun_Identity_AspNetCore_ChallengePayload\_\_}

Parses the on-wire cookie string back into a [ChallengePayload](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md).

```csharp
public static bool TryParse(string raw, out ChallengePayload? payload)
```

#### Parameters

`raw` [string](https://learn.microsoft.com/dotnet/api/system.string)

`payload` [ChallengePayload](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

## Operators

### operator ==(ChallengePayload?, ChallengePayload?) \{#Kun_Identity_AspNetCore_ChallengePayload_op_Equality_Kun_Identity_AspNetCore_ChallengePayload_Kun_Identity_AspNetCore_ChallengePayload\_}

```csharp
public static bool operator ==(ChallengePayload? left, ChallengePayload? right)
```

#### Parameters

`left` [ChallengePayload](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md)?

`right` [ChallengePayload](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(ChallengePayload?, ChallengePayload?) \{#Kun_Identity_AspNetCore_ChallengePayload_op_Inequality_Kun_Identity_AspNetCore_ChallengePayload_Kun_Identity_AspNetCore_ChallengePayload\_}

```csharp
public static bool operator !=(ChallengePayload? left, ChallengePayload? right)
```

#### Parameters

`left` [ChallengePayload](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md)?

`right` [ChallengePayload](/pr-preview/pr-181/sdk/api/kun/kun-identity-aspnetcore/challengepayload.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
