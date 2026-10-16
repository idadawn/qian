> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunTokenBucketOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.RateLimiting](/sdk/api/kun/kun-gateway-ratelimiting/index.md)\
Assembly: Kun.Gateway.dll

Tunable parameters for `AddKunTokenBucketRateLimiter`. Token-bucket smooths bursty
traffic by accumulating tokens at a steady rate up to a cap; each request consumes one
token and is rejected (or queued) when the bucket is empty.

```csharp
public sealed class KunTokenBucketOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunTokenBucketOptions](/sdk/api/kun/kun-gateway-ratelimiting/kuntokenbucketoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunTokenBucketOptions() \{#Kun_Gateway_RateLimiting_KunTokenBucketOptions__ctor}

```csharp
public KunTokenBucketOptions()
```

## Properties

### AutoReplenishment \{#Kun_Gateway_RateLimiting_KunTokenBucketOptions_AutoReplenishment}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), tokens refill automatically on a background timer.
When [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), the limiter only refills on incoming requests (cheaper
but with coarser smoothing). Default [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool).

```csharp
public bool AutoReplenishment { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### QueueLimit \{#Kun_Gateway_RateLimiting_KunTokenBucketOptions_QueueLimit}

Queue capacity for requests that arrive with an empty bucket. Default 0 (immediate rejection).

```csharp
public int QueueLimit { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ReplenishmentPeriod \{#Kun_Gateway_RateLimiting_KunTokenBucketOptions_ReplenishmentPeriod}

Interval between token refills. Default 10 seconds (so 20 tokens/10s = 120 tokens/minute).

```csharp
public TimeSpan ReplenishmentPeriod { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### TokenLimit \{#Kun_Gateway_RateLimiting_KunTokenBucketOptions_TokenLimit}

Maximum tokens the bucket holds. Effectively the burst budget. Default 100.

```csharp
public int TokenLimit { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### TokensPerPeriod \{#Kun_Gateway_RateLimiting_KunTokenBucketOptions_TokensPerPeriod}

How many tokens refill each [KunTokenBucketOptions.ReplenishmentPeriod](/sdk/api/kun/kun-gateway-ratelimiting/kuntokenbucketoptions.md#Kun_Gateway_RateLimiting_KunTokenBucketOptions_ReplenishmentPeriod). Default 20.

```csharp
public int TokensPerPeriod { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)
