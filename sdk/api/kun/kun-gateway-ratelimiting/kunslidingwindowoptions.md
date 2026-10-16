> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunSlidingWindowOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.RateLimiting](/sdk/api/kun/kun-gateway-ratelimiting/index.md)\
Assembly: Kun.Gateway.dll

Tunable parameters for `AddKunSlidingWindowRateLimiter`. Sliding-window divides
each window into segments and shifts one segment at a time, yielding smoother behavior
than a fixed window at the cost of proportionally higher CPU/memory.

```csharp
public sealed class KunSlidingWindowOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunSlidingWindowOptions](/sdk/api/kun/kun-gateway-ratelimiting/kunslidingwindowoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunSlidingWindowOptions() \{#Kun_Gateway_RateLimiting_KunSlidingWindowOptions__ctor}

```csharp
public KunSlidingWindowOptions()
```

## Properties

### PermitLimit \{#Kun_Gateway_RateLimiting_KunSlidingWindowOptions_PermitLimit}

Maximum permitted requests per sliding window. Default 100.

```csharp
public int PermitLimit { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### QueueLimit \{#Kun_Gateway_RateLimiting_KunSlidingWindowOptions_QueueLimit}

Queue capacity for requests that exceed the permit limit. Default 0 (immediate rejection).

```csharp
public int QueueLimit { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### SegmentsPerWindow \{#Kun_Gateway_RateLimiting_KunSlidingWindowOptions_SegmentsPerWindow}

Number of segments in each window. More segments = smoother enforcement but higher
memory per partition. Default 6 (10-second segments for a 1-minute window).

```csharp
public int SegmentsPerWindow { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### Window \{#Kun_Gateway_RateLimiting_KunSlidingWindowOptions_Window}

Total window duration. Default 1 minute.

```csharp
public TimeSpan Window { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)
