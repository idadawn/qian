> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface IRateLimitPartitionResolver

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.RateLimiting](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/index.md)\
Assembly: Kun.Gateway.dll

Resolves the rate-limit partition key for an incoming HTTP request.
Implement this interface to plug in a custom partitioning strategy (by tenant, IP, API key, etc.).

```csharp
public interface IRateLimitPartitionResolver
```

## Methods

### ResolvePartitionKey(HttpContext) \{#Kun_Gateway_RateLimiting_IRateLimitPartitionResolver_ResolvePartitionKey_Microsoft_AspNetCore_Http_HttpContext\_}

Returns a string partition key that identifies the rate-limit bucket for the given request.

```csharp
string ResolvePartitionKey(HttpContext context)
```

#### Parameters

`context` [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext)

The current HTTP context.

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

A non-null partition key string.
