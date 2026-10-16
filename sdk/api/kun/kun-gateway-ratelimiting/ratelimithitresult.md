> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Struct RateLimitHitResult

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Gateway` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Gateway`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Gateway)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Gateway.RateLimiting](/sdk/api/kun/kun-gateway-ratelimiting/index.md)\
Assembly: Kun.Gateway.dll

Result of [IRedisRateLimitStore.HitAsync](/sdk/api/kun/kun-gateway-ratelimiting/iredisratelimitstore.md#Kun_Gateway_RateLimiting_IRedisRateLimitStore_HitAsync_System_String_System_TimeSpan_System_Threading_CancellationToken_).

```csharp
public readonly struct RateLimitHitResult : IEquatable<RateLimitHitResult>
```

#### Implements

[IEquatable\<RateLimitHitResult>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### RateLimitHitResult(long, TimeSpan) \{#Kun_Gateway_RateLimiting_RateLimitHitResult__ctor_System_Int64_System_TimeSpan\_}

Result of [IRedisRateLimitStore.HitAsync](/sdk/api/kun/kun-gateway-ratelimiting/iredisratelimitstore.md#Kun_Gateway_RateLimiting_IRedisRateLimitStore_HitAsync_System_String_System_TimeSpan_System_Threading_CancellationToken_).

```csharp
public RateLimitHitResult(long Count, TimeSpan ResetsIn)
```

#### Parameters

`Count` [long](https://learn.microsoft.com/dotnet/api/system.int64)

Post-increment hit count within the active window (>= 1).

`ResetsIn` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

Time until the window resets and the counter is cleared.

## Properties

### Count \{#Kun_Gateway_RateLimiting_RateLimitHitResult_Count}

Post-increment hit count within the active window (>= 1).

```csharp
public long Count { get; init; }
```

#### Property Value

[long](https://learn.microsoft.com/dotnet/api/system.int64)

### ResetsIn \{#Kun_Gateway_RateLimiting_RateLimitHitResult_ResetsIn}

Time until the window resets and the counter is cleared.

```csharp
public TimeSpan ResetsIn { get; init; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

## Methods

### Deconstruct(out long, out TimeSpan) \{#Kun_Gateway_RateLimiting_RateLimitHitResult_Deconstruct_System_Int64__System_TimeSpan\_\_}

```csharp
public void Deconstruct(out long Count, out TimeSpan ResetsIn)
```

#### Parameters

`Count` [long](https://learn.microsoft.com/dotnet/api/system.int64)

`ResetsIn` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### Equals(object) \{#Kun_Gateway_RateLimiting_RateLimitHitResult_Equals_System_Object\_}

```csharp
public override bool Equals(object obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(RateLimitHitResult) \{#Kun_Gateway_RateLimiting_RateLimitHitResult_Equals_Kun_Gateway_RateLimiting_RateLimitHitResult\_}

```csharp
public bool Equals(RateLimitHitResult other)
```

#### Parameters

`other` [RateLimitHitResult](/sdk/api/kun/kun-gateway-ratelimiting/ratelimithitresult.md)

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Gateway_RateLimiting_RateLimitHitResult_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Gateway_RateLimiting_RateLimitHitResult_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(RateLimitHitResult, RateLimitHitResult) \{#Kun_Gateway_RateLimiting_RateLimitHitResult_op_Equality_Kun_Gateway_RateLimiting_RateLimitHitResult_Kun_Gateway_RateLimiting_RateLimitHitResult\_}

```csharp
public static bool operator ==(RateLimitHitResult left, RateLimitHitResult right)
```

#### Parameters

`left` [RateLimitHitResult](/sdk/api/kun/kun-gateway-ratelimiting/ratelimithitresult.md)

`right` [RateLimitHitResult](/sdk/api/kun/kun-gateway-ratelimiting/ratelimithitresult.md)

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(RateLimitHitResult, RateLimitHitResult) \{#Kun_Gateway_RateLimiting_RateLimitHitResult_op_Inequality_Kun_Gateway_RateLimiting_RateLimitHitResult_Kun_Gateway_RateLimiting_RateLimitHitResult\_}

```csharp
public static bool operator !=(RateLimitHitResult left, RateLimitHitResult right)
```

#### Parameters

`left` [RateLimitHitResult](/sdk/api/kun/kun-gateway-ratelimiting/ratelimithitresult.md)

`right` [RateLimitHitResult](/sdk/api/kun/kun-gateway-ratelimiting/ratelimithitresult.md)

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
