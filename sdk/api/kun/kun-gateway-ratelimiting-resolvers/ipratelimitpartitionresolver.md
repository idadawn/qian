> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class IpRateLimitPartitionResolver

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.RateLimiting.Resolvers](/sdk/api/kun/kun-gateway-ratelimiting-resolvers/index.md)\
Assembly: Kun.Gateway.dll

Resolves the rate-limit partition key from the remote IP address.
Reads the peer normalized by trusted ForwardedHeadersMiddleware; never parses raw headers.

```csharp
public sealed class IpRateLimitPartitionResolver : IRateLimitPartitionResolver
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[IpRateLimitPartitionResolver](/sdk/api/kun/kun-gateway-ratelimiting-resolvers/ipratelimitpartitionresolver.md)

#### Implements

[IRateLimitPartitionResolver](/sdk/api/kun/kun-gateway-ratelimiting/iratelimitpartitionresolver.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### IpRateLimitPartitionResolver() \{#Kun_Gateway_RateLimiting_Resolvers_IpRateLimitPartitionResolver__ctor}

```csharp
public IpRateLimitPartitionResolver()
```

## Methods

### ResolvePartitionKey(HttpContext) \{#Kun_Gateway_RateLimiting_Resolvers_IpRateLimitPartitionResolver_ResolvePartitionKey_Microsoft_AspNetCore_Http_HttpContext\_}

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
