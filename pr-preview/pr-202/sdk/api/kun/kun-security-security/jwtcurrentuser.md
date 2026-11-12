> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class JwtCurrentUser

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.Security](/pr-preview/pr-202/sdk/api/kun/kun-security-security/index.md)\
Assembly: Kun.Security.dll

[ICurrentUser](/pr-preview/pr-202/sdk/api/kun/kun-core-security/icurrentuser.md) implementation that reads `sub`, `name`,
and `roles` claims from the authenticated [`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)
on the current HTTP request.
Also implements [ITracedCurrentUser](/pr-preview/pr-202/sdk/api/kun/kun-core-security/itracedcurrentuser.md) to expose the `trace_id` claim
propagated from the gateway S2S JWT for OpenTelemetry trace continuity.

```csharp
public sealed class JwtCurrentUser : ITracedCurrentUser, ICurrentUser
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[JwtCurrentUser](/pr-preview/pr-202/sdk/api/kun/kun-security-security/jwtcurrentuser.md)

#### Implements

ITracedCurrentUser,
ICurrentUser

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### JwtCurrentUser(IHttpContextAccessor) \{#Kun_Security_Security_JwtCurrentUser__ctor_Microsoft_AspNetCore_Http_IHttpContextAccessor\_}

Initializes the user context with an HTTP context accessor.

```csharp
public JwtCurrentUser(IHttpContextAccessor httpContextAccessor)
```

#### Parameters

`httpContextAccessor` [IHttpContextAccessor](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.ihttpcontextaccessor)

Accessor for the current HTTP context.

## Properties

### IsAuthenticated \{#Kun_Security_Security_JwtCurrentUser_IsAuthenticated}

是否已认证。

```csharp
public bool IsAuthenticated { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### PrincipalKind \{#Kun_Security_Security_JwtCurrentUser_PrincipalKind}

已认证主体类型（user / service / agent）；旧适配器默认按用户处理，身份适配器应透传真实类型。

```csharp
public string PrincipalKind { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Roles \{#Kun_Security_Security_JwtCurrentUser_Roles}

用户当前持有的角色集合。

```csharp
public IReadOnlyList<string> Roles { get; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### TraceId \{#Kun_Security_Security_JwtCurrentUser_TraceId}

The OpenTelemetry trace id propagated via the `trace_id` claim on the S2S JWT,
or `null` when the token was issued without trace context (e.g. background refresh).

```csharp
public string? TraceId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### UserId \{#Kun_Security_Security_JwtCurrentUser_UserId}

用户唯一标识（通常是 JWT 的 `sub` claim）。未登录时为 [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)。

```csharp
public string? UserId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
