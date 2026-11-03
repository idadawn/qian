> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class RedisRateLimitStoreOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Gateway)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.RateLimiting](/pr-preview/pr-50/sdk/api/kun/kun-gateway-ratelimiting/index.md)\
Assembly: Kun.Gateway.dll

Configuration for [RedisFixedWindowRateLimitStore](/pr-preview/pr-50/sdk/api/kun/kun-gateway-ratelimiting/redisfixedwindowratelimitstore.md).

```csharp
public sealed class RedisRateLimitStoreOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[RedisRateLimitStoreOptions](/pr-preview/pr-50/sdk/api/kun/kun-gateway-ratelimiting/redisratelimitstoreoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### RedisRateLimitStoreOptions() \{#Kun_Gateway_RateLimiting_RedisRateLimitStoreOptions__ctor}

```csharp
public RedisRateLimitStoreOptions()
```

## Fields

### SectionName \{#Kun_Gateway_RateLimiting_RedisRateLimitStoreOptions_SectionName}

Config section name for binding via `Configure<...>`.

```csharp
public const string SectionName = "Kun:Gateway:RateLimit:Redis"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Properties

### KeyPrefix \{#Kun_Gateway_RateLimiting_RedisRateLimitStoreOptions_KeyPrefix}

Redis key prefix. Keys take the form `{KeyPrefix}:{partitionKey}`. Change this
when the same Redis instance is shared across multiple Kun gateways that must not
collide (e.g. staging vs prod, tenant-A vs tenant-B clusters). Default `"kun:rl"`.

```csharp
public string KeyPrefix { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)
