> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface IRedisRateLimitStore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.RateLimiting](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/index.md)\
Assembly: Kun.Gateway.dll

Low-level Redis-backed counter used for distributed rate limiting across multiple
Gateway pods. Primitive surface for a future `PartitionedRateLimiter<HttpContext>`
integration; directly usable today as a building block for custom rate-limit middleware.

```csharp
public interface IRedisRateLimitStore
```

## Methods

### HitAsync(string, TimeSpan, CancellationToken) \{#Kun_Gateway_RateLimiting_IRedisRateLimitStore_HitAsync_System_String_System_TimeSpan_System_Threading_CancellationToken\_}

Atomically increments the count for `partitionKey` within a
rolling window of `window`, applying TTL on the first write so
stale partitions self-expire. Returns the post-increment count and the time until
the window resets.

```csharp
Task<RateLimitHitResult> HitAsync(string partitionKey, TimeSpan window, CancellationToken cancellationToken = default)
```

#### Parameters

`partitionKey` [string](https://learn.microsoft.com/dotnet/api/system.string)

Caller's partition key (tenant, IP, sub, etc.).

`window` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

Window length; controls Redis EXPIRE TTL on first hit.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[RateLimitHitResult](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/ratelimithitresult.md)>

`Count`: current number of hits within the active window (>= 1).
`ResetsIn`: time until the window resets. Never negative.
