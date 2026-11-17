> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class WindowRule

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.RateLimit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.RateLimit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.RateLimit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.RateLimit.Abstractions](/pr-preview/pr-122/sdk/api/kun/kun-ratelimit-abstractions/index.md)\
Assembly: Kun.RateLimit.dll

Configuration for a single named rate-limit window.

```csharp
public sealed class WindowRule
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[WindowRule](/pr-preview/pr-122/sdk/api/kun/kun-ratelimit-abstractions/windowrule.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### WindowRule() \{#Kun_RateLimit_Abstractions_WindowRule__ctor}

```csharp
public WindowRule()
```

## Properties

### AutoReplenishment \{#Kun_RateLimit_Abstractions_WindowRule_AutoReplenishment}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (default), the limiter auto-replenishes on
a timer; when [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), the caller is responsible for
driving replenishment. Leave on for HTTP traffic.

```csharp
public bool AutoReplenishment { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### PartitionKey \{#Kun_RateLimit_Abstractions_WindowRule_PartitionKey}

Dimension the limiter partitions on (tenant / user / IP).

```csharp
public RateLimitPartitionKey PartitionKey { get; set; }
```

#### Property Value

[RateLimitPartitionKey](/pr-preview/pr-122/sdk/api/kun/kun-ratelimit-abstractions/ratelimitpartitionkey.md)

### Period \{#Kun_RateLimit_Abstractions_WindowRule_Period}

Duration of each fixed window (default 1 minute).

```csharp
public TimeSpan Period { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### PermitLimit \{#Kun_RateLimit_Abstractions_WindowRule_PermitLimit}

Requests permitted per partition per window. Must be > 0 for the window to have effect.

```csharp
public int PermitLimit { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### QueueLimit \{#Kun_RateLimit_Abstractions_WindowRule_QueueLimit}

Number of requests that can queue when the permit pool is exhausted (default 0 — reject immediately).

```csharp
public int QueueLimit { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)
