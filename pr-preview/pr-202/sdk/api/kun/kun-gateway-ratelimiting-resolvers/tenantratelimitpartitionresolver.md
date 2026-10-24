> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class TenantRateLimitPartitionResolver

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.RateLimiting.Resolvers](/pr-preview/pr-202/sdk/api/kun/kun-gateway-ratelimiting-resolvers/index.md)\
Assembly: Kun.Gateway.dll

Resolves the rate-limit partition key from the `tenant_id` claim of the authenticated user.
Falls back to `"anonymous"` when the claim is absent.

```csharp
public sealed class TenantRateLimitPartitionResolver : IRateLimitPartitionResolver
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[TenantRateLimitPartitionResolver](/pr-preview/pr-202/sdk/api/kun/kun-gateway-ratelimiting-resolvers/tenantratelimitpartitionresolver.md)

#### Implements

[IRateLimitPartitionResolver](/pr-preview/pr-202/sdk/api/kun/kun-gateway-ratelimiting/iratelimitpartitionresolver.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### TenantRateLimitPartitionResolver() \{#Kun_Gateway_RateLimiting_Resolvers_TenantRateLimitPartitionResolver__ctor}

```csharp
public TenantRateLimitPartitionResolver()
```

## Methods

### ResolvePartitionKey(HttpContext) \{#Kun_Gateway_RateLimiting_Resolvers_TenantRateLimitPartitionResolver_ResolvePartitionKey_Microsoft_AspNetCore_Http_HttpContext\_}

Returns a string partition key that identifies the rate-limit bucket for the given request.

```csharp
public string ResolvePartitionKey(HttpContext context)
```

#### Parameters

`context` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

The current HTTP context.

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

A non-null partition key string.
