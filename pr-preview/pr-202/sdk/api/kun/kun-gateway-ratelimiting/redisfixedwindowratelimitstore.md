> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class RedisFixedWindowRateLimitStore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.RateLimiting](/pr-preview/pr-202/sdk/api/kun/kun-gateway-ratelimiting/index.md)\
Assembly: Kun.Gateway.dll

Fixed-window distributed rate-limit counter backed by Redis. Uses an atomic
`INCR` with `NX EX`-style TTL-on-first-write semantics so the counter
self-expires once the window elapses — no cleanup job required.

```csharp
public sealed class RedisFixedWindowRateLimitStore : IRedisRateLimitStore
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[RedisFixedWindowRateLimitStore](/pr-preview/pr-202/sdk/api/kun/kun-gateway-ratelimiting/redisfixedwindowratelimitstore.md)

#### Implements

[IRedisRateLimitStore](/pr-preview/pr-202/sdk/api/kun/kun-gateway-ratelimiting/iredisratelimitstore.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

The implementation executes a two-command pipeline (INCR + TTL read) to obtain both
the post-increment count and the remaining TTL in one round-trip. On the first hit
(INCR returns 1), it follows up with EXPIRE to set the window TTL; subsequent hits
in the same window leave the TTL untouched.

Not a full [`RateLimiter`](https://learn.microsoft.com/dotnet/api/system.threading.ratelimiting.ratelimiter) subclass — this is
the low-level primitive. Full `PartitionedRateLimiter<HttpContext>`
integration with ASP.NET's rate-limit middleware is tracked as future work.

## Constructors

### RedisFixedWindowRateLimitStore(IConnectionMultiplexer, IOptions\<RedisRateLimitStoreOptions>) \{#Kun_Gateway_RateLimiting_RedisFixedWindowRateLimitStore__ctor_StackExchange_Redis_IConnectionMultiplexer_Microsoft_Extensions_Options_IOptions_Kun_Gateway_RateLimiting_RedisRateLimitStoreOptions\_\_}

Creates a new store.

```csharp
public RedisFixedWindowRateLimitStore(IConnectionMultiplexer redis, IOptions<RedisRateLimitStoreOptions> options)
```

#### Parameters

`redis` IConnectionMultiplexer

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[RedisRateLimitStoreOptions](/pr-preview/pr-202/sdk/api/kun/kun-gateway-ratelimiting/redisratelimitstoreoptions.md)>

## Methods

### HitAsync(string, TimeSpan, CancellationToken) \{#Kun_Gateway_RateLimiting_RedisFixedWindowRateLimitStore_HitAsync_System_String_System_TimeSpan_System_Threading_CancellationToken\_}

Atomically increments the count for `partitionKey` within a
rolling window of `window`, applying TTL on the first write so
stale partitions self-expire. Returns the post-increment count and the time until
the window resets.

```csharp
public Task<RateLimitHitResult> HitAsync(string partitionKey, TimeSpan window, CancellationToken cancellationToken = default)
```

#### Parameters

`partitionKey` [string](https://learn.microsoft.com/dotnet/api/system.string)

Caller's partition key (tenant, IP, sub, etc.).

`window` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

Window length; controls Redis EXPIRE TTL on first hit.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[RateLimitHitResult](/pr-preview/pr-202/sdk/api/kun/kun-gateway-ratelimiting/ratelimithitresult.md)>

`Count`: current number of hits within the active window (>= 1).
`ResetsIn`: time until the window resets. Never negative.
