> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IAuthenticatedUser

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Identity` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Identity`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Identity)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Identity.Abstractions](/sdk/api/kun/kun-identity-abstractions/index.md)\
Assembly: Kun.Identity.dll

Browser-flow extension of [ICurrentUser](/sdk/api/kun/kun-core-security/icurrentuser.md). Adds the
multi-tenant identifier, profile claims, and direct access to the
ambient [`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal) that the
`Kun.Identity.AspNetCore` cookie middleware populates.

```csharp
public interface IAuthenticatedUser : ICurrentUser
```

#### Implements

ICurrentUser

## Remarks

Services that resolve the user from an S2S JWT should keep
injecting [ICurrentUser](/sdk/api/kun/kun-core-security/icurrentuser.md). Endpoints that serve the
browser (cookie + OIDC code flow) can inject
[IAuthenticatedUser](/sdk/api/kun/kun-identity-abstractions/iauthenticateduser.md) to reach the cookie-only fields
without casting.

The DI registration provided by `Kun.Identity.AspNetCore`
binds both interfaces to the same request-scoped instance, so the
choice of injected interface is purely about the fields the caller
needs.

See `docs/architecture/when-to-use-currentuser.md` for the
full decision table across consumer scenarios and the anti-patterns
that should never show up in a PR (no `ICurrentUserEx`, no
per-service subinterface, no [`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)
injection as a first-class service).

## Properties

### Email \{#Kun_Identity_Abstractions_IAuthenticatedUser_Email}

Email claim from the id-token, when present.

```csharp
string? Email { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Name \{#Kun_Identity_Abstractions_IAuthenticatedUser_Name}

Display-name claim from the id-token, when present.

```csharp
string? Name { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Principal \{#Kun_Identity_Abstractions_IAuthenticatedUser_Principal}

The raw [`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal) the cookie middleware
installed on `HttpContext.User`. [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)
when the request is anonymous.

```csharp
ClaimsPrincipal? Principal { get; }
```

#### Property Value

[ClaimsPrincipal](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal)?

### TenantId \{#Kun_Identity_Abstractions_IAuthenticatedUser_TenantId}

Active tenant identifier, or [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) in single-tenant deployments.

```csharp
string? TenantId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
