> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class S2SJwtIssueRequest

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.S2SJwt](/sdk/api/kun/kun-security-s2sjwt/index.md)\
Assembly: Kun.Security.dll

Input for [IS2SJwtIssuer.IssueAsync](/sdk/api/kun/kun-security-s2sjwt/is2sjwtissuer.md#Kun_Security_S2SJwt_IS2SJwtIssuer_IssueAsync_Kun_Security_S2SJwt_S2SJwtIssueRequest_System_Threading_CancellationToken_).
Carries the full SAD v3.0 §12 legacy S2S claim set.

```csharp
[Obsolete("Use Kun.Gateway.S2sTokenIssuer and Kun.Security service authentication.", DiagnosticId = "KUNS2S001")]
public sealed record S2SJwtIssueRequest : IEquatable<S2SJwtIssueRequest>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[S2SJwtIssueRequest](/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md)

#### Implements

[IEquatable\<S2SJwtIssueRequest>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### S2SJwtIssueRequest(string, string, IReadOnlyList\<string>, IReadOnlyList\<string>?, IReadOnlyList\<string>?, string?, string?, string?, string?, TimeSpan?) \{#Kun_Security_S2SJwt_S2SJwtIssueRequest__ctor_System_String_System_String_System_Collections_Generic_IReadOnlyList_System_String__System_Collections_Generic_IReadOnlyList_System_String__System_Collections_Generic_IReadOnlyList_System_String__System_String_System_String_System_String_System_String_System_Nullable_System_TimeSpan\_\_}

Input for [IS2SJwtIssuer.IssueAsync](/sdk/api/kun/kun-security-s2sjwt/is2sjwtissuer.md#Kun_Security_S2SJwt_IS2SJwtIssuer_IssueAsync_Kun_Security_S2SJwt_S2SJwtIssueRequest_System_Threading_CancellationToken_).
Carries the full SAD v3.0 §12 legacy S2S claim set.

```csharp
public S2SJwtIssueRequest(string UserId, string TenantId, IReadOnlyList<string> Audience, IReadOnlyList<string>? Roles = null, IReadOnlyList<string>? Scopes = null, string? SessionId = null, string? MembershipId = null, string? ProjectId = null, string? TraceId = null, TimeSpan? Lifetime = null)
```

#### Parameters

`UserId` [string](https://learn.microsoft.com/dotnet/api/system.string)

User subject (UUID). Emitted as the `sub` claim.

`TenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

Tenant identifier (UUID). Emitted as `tenant_id`.

`Audience` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

Target audience(s). A single entry becomes a string `aud`; multiple entries are
emitted as an `aud` array in the payload.

`Roles` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>?

Roles granted to the caller. Each role is emitted as a distinct `roles` claim
(per SAD §12, which models roles as a multi-valued claim rather than a space-joined scope).

`Scopes` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>?

OAuth2-style permission scopes. Joined with a single space and emitted as one
`scope` claim. Use this when the consumer expects an OAuth2-compatible scope string;
prefer `Roles` for RBAC semantics.

`SessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Session identifier for revocation support. Emitted as `session_id` when set.

`MembershipId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Current membership id (when a user has multiple memberships within a tenant).
Emitted as `membership_id` when set.

`ProjectId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Project identifier (UUID) — the usage unit under the tenant (payer) in the
two-level tenant/project scope model. Emitted as `project_id` when set.

`TraceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

W3C trace id (hex) for OpenTelemetry trace continuity across the service mesh.
Emitted as `trace_id` when set.

`Lifetime` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

Optional per-call override for token lifetime. When [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null),
[S2SJwtOptions.DefaultLifetimeSeconds](/sdk/api/kun/kun-security-s2sjwt/s2sjwtoptions.md#Kun_Security_S2SJwt_S2SJwtOptions_DefaultLifetimeSeconds) applies.

## Properties

### Audience \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_Audience}

Target audience(s). A single entry becomes a string `aud`; multiple entries are
emitted as an `aud` array in the payload.

```csharp
public IReadOnlyList<string> Audience { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### Lifetime \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_Lifetime}

Optional per-call override for token lifetime. When [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null),
[S2SJwtOptions.DefaultLifetimeSeconds](/sdk/api/kun/kun-security-s2sjwt/s2sjwtoptions.md#Kun_Security_S2SJwt_S2SJwtOptions_DefaultLifetimeSeconds) applies.

```csharp
public TimeSpan? Lifetime { get; init; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

### MembershipId \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_MembershipId}

Current membership id (when a user has multiple memberships within a tenant).
Emitted as `membership_id` when set.

```csharp
public string? MembershipId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### ProjectId \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_ProjectId}

Project identifier (UUID) — the usage unit under the tenant (payer) in the
two-level tenant/project scope model. Emitted as `project_id` when set.

```csharp
public string? ProjectId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Roles \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_Roles}

Roles granted to the caller. Each role is emitted as a distinct `roles` claim
(per SAD §12, which models roles as a multi-valued claim rather than a space-joined scope).

```csharp
public IReadOnlyList<string>? Roles { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>?

### Scopes \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_Scopes}

OAuth2-style permission scopes. Joined with a single space and emitted as one
`scope` claim. Use this when the consumer expects an OAuth2-compatible scope string;
prefer `Roles` for RBAC semantics.

```csharp
public IReadOnlyList<string>? Scopes { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>?

### SessionId \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_SessionId}

Session identifier for revocation support. Emitted as `session_id` when set.

```csharp
public string? SessionId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TenantId \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_TenantId}

Tenant identifier (UUID). Emitted as `tenant_id`.

```csharp
public string TenantId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### TraceId \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_TraceId}

W3C trace id (hex) for OpenTelemetry trace continuity across the service mesh.
Emitted as `trace_id` when set.

```csharp
public string? TraceId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### UserId \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_UserId}

User subject (UUID). Emitted as the `sub` claim.

```csharp
public string UserId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### \<Clone>$() \{#Kun_Security_S2SJwt_S2SJwtIssueRequest__Clone\_\_}

```csharp
public S2SJwtIssueRequest <Clone>$()
```

#### Returns

[S2SJwtIssueRequest](/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md)

### Deconstruct(out string, out string, out IReadOnlyList\<string>, out IReadOnlyList\<string>?, out IReadOnlyList\<string>?, out string?, out string?, out string?, out string?, out TimeSpan?) \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_Deconstruct_System_String__System_String__System_Collections_Generic_IReadOnlyList_System_String___System_Collections_Generic_IReadOnlyList_System_String___System_Collections_Generic_IReadOnlyList_System_String___System_String__System_String__System_String__System_String__System_Nullable_System_TimeSpan\_\_\_}

```csharp
public void Deconstruct(out string UserId, out string TenantId, out IReadOnlyList<string> Audience, out IReadOnlyList<string>? Roles, out IReadOnlyList<string>? Scopes, out string? SessionId, out string? MembershipId, out string? ProjectId, out string? TraceId, out TimeSpan? Lifetime)
```

#### Parameters

`UserId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`TenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

`Audience` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

`Roles` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>?

`Scopes` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>?

`SessionId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`MembershipId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`ProjectId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`TraceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Lifetime` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

### Equals(object?) \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(S2SJwtIssueRequest?) \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_Equals_Kun_Security_S2SJwt_S2SJwtIssueRequest\_}

```csharp
public bool Equals(S2SJwtIssueRequest? other)
```

#### Parameters

`other` [S2SJwtIssueRequest](/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(S2SJwtIssueRequest?, S2SJwtIssueRequest?) \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_op_Equality_Kun_Security_S2SJwt_S2SJwtIssueRequest_Kun_Security_S2SJwt_S2SJwtIssueRequest\_}

```csharp
public static bool operator ==(S2SJwtIssueRequest? left, S2SJwtIssueRequest? right)
```

#### Parameters

`left` [S2SJwtIssueRequest](/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md)?

`right` [S2SJwtIssueRequest](/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(S2SJwtIssueRequest?, S2SJwtIssueRequest?) \{#Kun_Security_S2SJwt_S2SJwtIssueRequest_op_Inequality_Kun_Security_S2SJwt_S2SJwtIssueRequest_Kun_Security_S2SJwt_S2SJwtIssueRequest\_}

```csharp
public static bool operator !=(S2SJwtIssueRequest? left, S2SJwtIssueRequest? right)
```

#### Parameters

`left` [S2SJwtIssueRequest](/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md)?

`right` [S2SJwtIssueRequest](/sdk/api/kun/kun-security-s2sjwt/s2sjwtissuerequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
