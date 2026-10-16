> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunRateLimiterExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.RateLimiting](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/index.md)\
Assembly: Kun.Gateway.dll

Extension methods for registering Kun rate limiting services.

```csharp
public static class KunRateLimiterExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunRateLimiterExtensions](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/kunratelimiterextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Fields

### SlidingWindowPolicyName \{#Kun_Gateway_RateLimiting_KunRateLimiterExtensions_SlidingWindowPolicyName}

Name of the named policy registered by `AddKunSlidingWindowRateLimiter`.

```csharp
public const string SlidingWindowPolicyName = "kun-sliding-window"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### TokenBucketPolicyName \{#Kun_Gateway_RateLimiting_KunRateLimiterExtensions_TokenBucketPolicyName}

Name of the named policy registered by `AddKunTokenBucketRateLimiter`.

```csharp
public const string TokenBucketPolicyName = "kun-token-bucket"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### AddKunRateLimiter(IServiceCollection, Action\<RateLimiterOptions>?) \{#Kun_Gateway_RateLimiting_KunRateLimiterExtensions_AddKunRateLimiter_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Microsoft_AspNetCore_RateLimiting_RateLimiterOptions\_\_}

Registers Kun rate limiting with a tenant-partitioned fixed-window policy (100 req/min by default).
Requires an [IRateLimitPartitionResolver](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/iratelimitpartitionresolver.md) to be registered; if none is present,
defaults to [TenantRateLimitPartitionResolver](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting-resolvers/tenantratelimitpartitionresolver.md).

```csharp
public static IServiceCollection AddKunRateLimiter(this IServiceCollection services, Action<RateLimiterOptions>? configure = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[RateLimiterOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.ratelimiting.ratelimiteroptions)>?

Optional delegate to further configure rate limiter options.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection) for chaining.

### AddKunRedisRateLimitStore(IServiceCollection, Action\<RedisRateLimitStoreOptions>?) \{#Kun_Gateway_RateLimiting_KunRateLimiterExtensions_AddKunRedisRateLimitStore_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Gateway_RateLimiting_RedisRateLimitStoreOptions\_\_}

Registers a token-bucket rate limiter policy partitioned by the resolved
[IRateLimitPartitionResolver](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/iratelimitpartitionresolver.md) key. Defaults: 100-token burst budget,
refilled at 20 tokens / 10 seconds (≈120 tokens/minute steady state).
Absorbs bursty traffic better than fixed-window; use for APIs where clients
naturally come in bursts (webhook fanout, batch imports).

```csharp
public static IServiceCollection AddKunRedisRateLimitStore(this IServiceCollection services, Action<RedisRateLimitStoreOptions>? configure = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[RedisRateLimitStoreOptions](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/redisratelimitstoreoptions.md)>?

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

### AddKunSlidingWindowRateLimiter(IServiceCollection, Action\<KunSlidingWindowOptions>?, Action\<RateLimiterOptions>?) \{#Kun_Gateway_RateLimiting_KunRateLimiterExtensions_AddKunSlidingWindowRateLimiter_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Gateway_RateLimiting_KunSlidingWindowOptions__System_Action_Microsoft_AspNetCore_RateLimiting_RateLimiterOptions\_\_}

Registers a sliding-window rate limiter policy partitioned by the resolved
[IRateLimitPartitionResolver](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/iratelimitpartitionresolver.md) key. Call
`app.MapXxx().RequireRateLimiting(KunRateLimiterExtensions.SlidingWindowPolicyName)`
on endpoints that should enforce it. Defaults: 100 permits per 1-minute window with
6 segments. Defence-in-depth sibling to the fixed-window path; both may coexist.

```csharp
public static IServiceCollection AddKunSlidingWindowRateLimiter(this IServiceCollection services, Action<KunSlidingWindowOptions>? configureKun = null, Action<RateLimiterOptions>? configureHost = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`configureKun` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunSlidingWindowOptions](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/kunslidingwindowoptions.md)>?

`configureHost` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[RateLimiterOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.ratelimiting.ratelimiteroptions)>?

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

### AddKunTokenBucketRateLimiter(IServiceCollection, Action\<KunTokenBucketOptions>?, Action\<RateLimiterOptions>?) \{#Kun_Gateway_RateLimiting_KunRateLimiterExtensions_AddKunTokenBucketRateLimiter_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Gateway_RateLimiting_KunTokenBucketOptions__System_Action_Microsoft_AspNetCore_RateLimiting_RateLimiterOptions\_\_}

Registers a token-bucket rate limiter policy partitioned by the resolved
[IRateLimitPartitionResolver](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/iratelimitpartitionresolver.md) key. Defaults: 100-token burst budget,
refilled at 20 tokens / 10 seconds (≈120 tokens/minute steady state).
Absorbs bursty traffic better than fixed-window; use for APIs where clients
naturally come in bursts (webhook fanout, batch imports).

```csharp
public static IServiceCollection AddKunTokenBucketRateLimiter(this IServiceCollection services, Action<KunTokenBucketOptions>? configureKun = null, Action<RateLimiterOptions>? configureHost = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

`configureKun` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunTokenBucketOptions](/pr-preview/pr-181/sdk/api/kun/kun-gateway-ratelimiting/kuntokenbucketoptions.md)>?

`configureHost` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[RateLimiterOptions](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.ratelimiting.ratelimiteroptions)>?

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
