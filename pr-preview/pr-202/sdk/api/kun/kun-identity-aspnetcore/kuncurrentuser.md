> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunCurrentUser

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity.AspNetCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity.AspNetCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Identity.AspNetCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.AspNetCore](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/index.md)\
Assembly: Kun.Identity.AspNetCore.dll

Request-scoped [IAuthenticatedUser](/pr-preview/pr-202/sdk/api/kun/kun-identity-abstractions/iauthenticateduser.md) (and
[ICurrentUser](/pr-preview/pr-202/sdk/api/kun/kun-core-security/icurrentuser.md)) resolver. Reads claims off
`HttpContext.User` — which `UseKunCookieAuth` populates
from `ISessionStore` earlier in the pipeline. Anonymous when
no [`IHttpContextAccessor`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.ihttpcontextaccessor) is available or the
authentication middleware has not run.

```csharp
public sealed class KunCurrentUser : IAuthenticatedUser, ICurrentUser
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunCurrentUser](/pr-preview/pr-202/sdk/api/kun/kun-identity-aspnetcore/kuncurrentuser.md)

#### Implements

IAuthenticatedUser,
ICurrentUser

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunCurrentUser(IHttpContextAccessor) \{#Kun_Identity_AspNetCore_KunCurrentUser__ctor_Microsoft_AspNetCore_Http_IHttpContextAccessor\_}

Creates a resolver bound to the supplied HTTP context accessor.

```csharp
public KunCurrentUser(IHttpContextAccessor accessor)
```

#### Parameters

`accessor` [IHttpContextAccessor](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.ihttpcontextaccessor)

## Fields

### TenantIdClaimType \{#Kun_Identity_AspNetCore_KunCurrentUser_TenantIdClaimType}

Claim type used for tenant id. The cookie middleware populates
this from `SessionData.TenantId`; the S2S path populates
it from the signed `tenant_id` claim on the internal JWT.

```csharp
public const string TenantIdClaimType = "tenant_id"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Properties

### Email \{#Kun_Identity_AspNetCore_KunCurrentUser_Email}

Email claim from the id-token, when present.

```csharp
public string? Email { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### IsAuthenticated \{#Kun_Identity_AspNetCore_KunCurrentUser_IsAuthenticated}

是否已认证。

```csharp
public bool IsAuthenticated { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Name \{#Kun_Identity_AspNetCore_KunCurrentUser_Name}

Display-name claim from the id-token, when present.

```csharp
public string? Name { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Principal \{#Kun_Identity_AspNetCore_KunCurrentUser_Principal}

The raw [`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal) the cookie middleware
installed on `HttpContext.User`. [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)
when the request is anonymous.

```csharp
public ClaimsPrincipal? Principal { get; }
```

#### Property Value

[ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)?

### PrincipalKind \{#Kun_Identity_AspNetCore_KunCurrentUser_PrincipalKind}

已认证主体类型（user / service / agent）；旧适配器默认按用户处理，身份适配器应透传真实类型。

```csharp
public string PrincipalKind { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Roles \{#Kun_Identity_AspNetCore_KunCurrentUser_Roles}

用户当前持有的角色集合。

```csharp
public IReadOnlyList<string> Roles { get; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### TenantId \{#Kun_Identity_AspNetCore_KunCurrentUser_TenantId}

Active tenant identifier, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) in single-tenant deployments.

```csharp
public string? TenantId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### UserId \{#Kun_Identity_AspNetCore_KunCurrentUser_UserId}

用户唯一标识（通常是 JWT 的 `sub` claim）。未登录时为 [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)。

```csharp
public string? UserId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
