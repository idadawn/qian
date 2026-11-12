> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunCachingOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Caching` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Caching`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Caching)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Caching.Configuration](/pr-preview/pr-202/sdk/api/kun/kun-caching-configuration/index.md)\
Assembly: Kun.Caching.dll

Configuration for `AddKunCaching`.

```csharp
public sealed class KunCachingOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunCachingOptions](/pr-preview/pr-202/sdk/api/kun/kun-caching-configuration/kuncachingoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunCachingOptions() \{#Kun_Caching_Configuration_KunCachingOptions__ctor}

```csharp
public KunCachingOptions()
```

## Properties

### DefaultTtl \{#Kun_Caching_Configuration_KunCachingOptions_DefaultTtl}

TTL applied to `SetAsync` / `GetOrSetAsync` calls that
omit an explicit `ttl`. [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) (default)
means entries never expire when no explicit TTL is given.

```csharp
public TimeSpan? DefaultTtl { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

### FallbackToMemoryOnRedisFailure \{#Kun_Caching_Configuration_KunCachingOptions_FallbackToMemoryOnRedisFailure}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), the Redis backend swallows
connection / socket exceptions and routes reads and writes to a
sidecar in-memory cache for the duration of the outage. The
exception is logged at `Warning` and never propagated.
Default is [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool); callers see the exception
and decide.

```csharp
public bool FallbackToMemoryOnRedisFailure { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### KeyPrefix \{#Kun_Caching_Configuration_KunCachingOptions_KeyPrefix}

Prefix prepended to every key passed to the cache. Ideal for
namespacing per-service caches on a shared Redis. Empty / null
means no prefix (default).

```csharp
public string? KeyPrefix { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
