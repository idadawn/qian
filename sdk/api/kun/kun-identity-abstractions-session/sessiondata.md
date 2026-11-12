> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class SessionData

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Identity)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Abstractions.Session](/sdk/api/kun/kun-identity-abstractions-session/index.md)\
Assembly: Kun.Identity.dll

Serialized payload held by [ISessionStore](/sdk/api/kun/kun-identity-abstractions-session/isessionstore.md) implementations.
The session cookie carries only an opaque session id; the record
below is what that id resolves to.

```csharp
public sealed record SessionData : IEquatable<SessionData>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SessionData](/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md)

#### Implements

[IEquatable\<SessionData>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SessionData(string, string?, string?, string?, IReadOnlyList\<string>, string, string?, DateTimeOffset) \{#Kun_Identity_Abstractions_Session_SessionData__ctor_System_String_System_String_System_String_System_String_System_Collections_Generic_IReadOnlyList_System_String__System_String_System_String_System_DateTimeOffset\_}

Serialized payload held by [ISessionStore](/sdk/api/kun/kun-identity-abstractions-session/isessionstore.md) implementations.
The session cookie carries only an opaque session id; the record
below is what that id resolves to.

```csharp
public SessionData(string UserId, string? TenantId, string? Email, string? Name, IReadOnlyList<string> Roles, string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt)
```

#### Parameters

`UserId` [string](https://learn.microsoft.com/dotnet/api/system.string)

Stable user identifier. Mirrors the `sub` claim from the OIDC
id-token / access-token. Required — sessions without a user are not
authenticated.

`TenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Active tenant identifier resolved from the `tenant_id` claim, or
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the consumer is single-tenant.

`Email` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Email claim, when present on the id-token.

`Name` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Display name claim, when present on the id-token.

`Roles` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

Flattened role list from `roles` / `groups` claim. Empty
when no role claim was issued.

`AccessToken` [string](https://learn.microsoft.com/dotnet/api/system.string)

OIDC access token. Persisted so downstream API calls made on the
user's behalf (e.g. userinfo refresh) can reuse it without
re-running the code-exchange flow.

`RefreshToken` [string](https://learn.microsoft.com/dotnet/api/system.string)?

OIDC refresh token when the IdP issued one. Stored server-side only
— never round-tripped through the cookie.

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

Absolute instant at which [SessionData.AccessToken](/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md#Kun_Identity_Abstractions_Session_SessionData_AccessToken) stops being
valid. When this is sooner than the session TTL, background refresh
is responsible for rotating the access token; session expiry itself
is driven by the cookie / store TTL set at sign-in.

## Properties

### AccessToken \{#Kun_Identity_Abstractions_Session_SessionData_AccessToken}

OIDC access token. Persisted so downstream API calls made on the
user's behalf (e.g. userinfo refresh) can reuse it without
re-running the code-exchange flow.

```csharp
public string AccessToken { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Email \{#Kun_Identity_Abstractions_Session_SessionData_Email}

Email claim, when present on the id-token.

```csharp
public string? Email { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ExpiresAt \{#Kun_Identity_Abstractions_Session_SessionData_ExpiresAt}

Absolute instant at which [SessionData.AccessToken](/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md#Kun_Identity_Abstractions_Session_SessionData_AccessToken) stops being
valid. When this is sooner than the session TTL, background refresh
is responsible for rotating the access token; session expiry itself
is driven by the cookie / store TTL set at sign-in.

```csharp
public DateTimeOffset ExpiresAt { get; init; }
```

#### Property Value

[DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Issuer \{#Kun_Identity_Abstractions_Session_SessionData_Issuer}

Verified identity-provider issuer, used to scope back-channel logout.

```csharp
public string? Issuer { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Name \{#Kun_Identity_Abstractions_Session_SessionData_Name}

Display name claim, when present on the id-token.

```csharp
public string? Name { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ProviderSessionId \{#Kun_Identity_Abstractions_Session_SessionData_ProviderSessionId}

Verified provider sid; distinct from the opaque local browser cookie.

```csharp
public string? ProviderSessionId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### RefreshToken \{#Kun_Identity_Abstractions_Session_SessionData_RefreshToken}

OIDC refresh token when the IdP issued one. Stored server-side only
— never round-tripped through the cookie.

```csharp
public string? RefreshToken { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Roles \{#Kun_Identity_Abstractions_Session_SessionData_Roles}

Flattened role list from `roles` / `groups` claim. Empty
when no role claim was issued.

```csharp
public IReadOnlyList<string> Roles { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### TenantId \{#Kun_Identity_Abstractions_Session_SessionData_TenantId}

Active tenant identifier resolved from the `tenant_id` claim, or
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the consumer is single-tenant.

```csharp
public string? TenantId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### UserId \{#Kun_Identity_Abstractions_Session_SessionData_UserId}

Stable user identifier. Mirrors the `sub` claim from the OIDC
id-token / access-token. Required — sessions without a user are not
authenticated.

```csharp
public string UserId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### \<Clone>$() \{#Kun_Identity_Abstractions_Session_SessionData__Clone\_\_}

```csharp
public SessionData <Clone>$()
```

#### Returns

[SessionData](/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md)

### Deconstruct(out string, out string?, out string?, out string?, out IReadOnlyList\<string>, out string, out string?, out DateTimeOffset) \{#Kun_Identity_Abstractions_Session_SessionData_Deconstruct_System_String__System_String__System_String__System_String__System_Collections_Generic_IReadOnlyList_System_String___System_String__System_String__System_DateTimeOffset\_\_}

```csharp
public void Deconstruct(out string UserId, out string? TenantId, out string? Email, out string? Name, out IReadOnlyList<string> Roles, out string AccessToken, out string? RefreshToken, out DateTimeOffset ExpiresAt)
```

#### Parameters

`UserId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`TenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Email` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Name` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Roles` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

`AccessToken` [string](https://learn.microsoft.com/dotnet/api/system.string)

`RefreshToken` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`ExpiresAt` [DateTimeOffset](https://learn.microsoft.com/dotnet/api/system.datetimeoffset)

### Equals(object?) \{#Kun_Identity_Abstractions_Session_SessionData_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(SessionData?) \{#Kun_Identity_Abstractions_Session_SessionData_Equals_Kun_Identity_Abstractions_Session_SessionData\_}

```csharp
public bool Equals(SessionData? other)
```

#### Parameters

`other` [SessionData](/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Identity_Abstractions_Session_SessionData_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Identity_Abstractions_Session_SessionData_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(SessionData?, SessionData?) \{#Kun_Identity_Abstractions_Session_SessionData_op_Equality_Kun_Identity_Abstractions_Session_SessionData_Kun_Identity_Abstractions_Session_SessionData\_}

```csharp
public static bool operator ==(SessionData? left, SessionData? right)
```

#### Parameters

`left` [SessionData](/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md)?

`right` [SessionData](/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(SessionData?, SessionData?) \{#Kun_Identity_Abstractions_Session_SessionData_op_Inequality_Kun_Identity_Abstractions_Session_SessionData_Kun_Identity_Abstractions_Session_SessionData\_}

```csharp
public static bool operator !=(SessionData? left, SessionData? right)
```

#### Parameters

`left` [SessionData](/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md)?

`right` [SessionData](/sdk/api/kun/kun-identity-abstractions-session/sessiondata.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
