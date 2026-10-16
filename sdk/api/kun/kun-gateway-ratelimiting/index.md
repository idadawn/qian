> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Gateway.RateLimiting

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Gateway.RateLimiting.Resolvers](/sdk/api/kun/kun-gateway-ratelimiting-resolvers/index.md)

### Classes

[KunRateLimiterExtensions](/sdk/api/kun/kun-gateway-ratelimiting/kunratelimiterextensions.md)

Extension methods for registering Kun rate limiting services.

[KunSlidingWindowOptions](/sdk/api/kun/kun-gateway-ratelimiting/kunslidingwindowoptions.md)

Tunable parameters for `AddKunSlidingWindowRateLimiter`. Sliding-window divides
each window into segments and shifts one segment at a time, yielding smoother behavior
than a fixed window at the cost of proportionally higher CPU/memory.

[KunTokenBucketOptions](/sdk/api/kun/kun-gateway-ratelimiting/kuntokenbucketoptions.md)

Tunable parameters for `AddKunTokenBucketRateLimiter`. Token-bucket smooths bursty
traffic by accumulating tokens at a steady rate up to a cap; each request consumes one
token and is rejected (or queued) when the bucket is empty.

[RedisFixedWindowRateLimitStore](/sdk/api/kun/kun-gateway-ratelimiting/redisfixedwindowratelimitstore.md)

Fixed-window distributed rate-limit counter backed by Redis. Uses an atomic
`INCR` with `NX EX`-style TTL-on-first-write semantics so the counter
self-expires once the window elapses — no cleanup job required.

[RedisRateLimitStoreOptions](/sdk/api/kun/kun-gateway-ratelimiting/redisratelimitstoreoptions.md)

Configuration for [RedisFixedWindowRateLimitStore](/sdk/api/kun/kun-gateway-ratelimiting/redisfixedwindowratelimitstore.md).

### Structs

[RateLimitHitResult](/sdk/api/kun/kun-gateway-ratelimiting/ratelimithitresult.md)

Result of [IRedisRateLimitStore.HitAsync](/sdk/api/kun/kun-gateway-ratelimiting/iredisratelimitstore.md#Kun_Gateway_RateLimiting_IRedisRateLimitStore_HitAsync_System_String_System_TimeSpan_System_Threading_CancellationToken_).

### Interfaces

[IRateLimitPartitionResolver](/sdk/api/kun/kun-gateway-ratelimiting/iratelimitpartitionresolver.md)

Resolves the rate-limit partition key for an incoming HTTP request.
Implement this interface to plug in a custom partitioning strategy (by tenant, IP, API key, etc.).

[IRedisRateLimitStore](/sdk/api/kun/kun-gateway-ratelimiting/iredisratelimitstore.md)

Low-level Redis-backed counter used for distributed rate limiting across multiple
Gateway pods. Primitive surface for a future `PartitionedRateLimiter<HttpContext>`
integration; directly usable today as a building block for custom rate-limit middleware.
