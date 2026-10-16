> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class JwtTenantContext

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Security` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Security`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Security)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Security.MultiTenancy](/pr-preview/pr-181/sdk/api/kun/kun-security-multitenancy/index.md)\
Assembly: Kun.Security.dll

[ITenantContext](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/itenantcontext.md) implementation that extracts `tenant_id`
from the authenticated [`ClaimsPrincipal`](https://learn.microsoft.com/dotnet/api/system.security.claims.claimsprincipal) on the
current HTTP request. Falls back gracefully when unauthenticated.

```csharp
public sealed class JwtTenantContext : ITenantContext, ITenantIdSource
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[JwtTenantContext](/pr-preview/pr-181/sdk/api/kun/kun-security-multitenancy/jwttenantcontext.md)

#### Implements

ITenantContext,
ITenantIdSource

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### JwtTenantContext(IHttpContextAccessor) \{#Kun_Security_MultiTenancy_JwtTenantContext__ctor_Microsoft_AspNetCore_Http_IHttpContextAccessor\_}

Initializes the context with an HTTP context accessor.

```csharp
public JwtTenantContext(IHttpContextAccessor httpContextAccessor)
```

#### Parameters

`httpContextAccessor` [IHttpContextAccessor](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.ihttpcontextaccessor)

Accessor for the current HTTP context.

## Properties

### IsMultiTenant \{#Kun_Security_MultiTenancy_JwtTenantContext_IsMultiTenant}

当前应用是否启用了多租户。

```csharp
public bool IsMultiTenant { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### TenantId \{#Kun_Security_MultiTenancy_JwtTenantContext_TenantId}

当前请求的租户 ID；后台任务中可能为 [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)。

```csharp
public string? TenantId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
