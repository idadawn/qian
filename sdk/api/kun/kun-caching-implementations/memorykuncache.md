> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class MemoryKunCache

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Caching` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Caching`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Caching)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Caching.Implementations](/sdk/api/kun/kun-caching-implementations/index.md)\
Assembly: Kun.Caching.dll

In-process [IKunCache](/sdk/api/kun/kun-caching-abstractions/ikuncache.md) backed by
[`IMemoryCache`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.caching.memory.imemorycache). Stores values directly — no
serialization round-trip — so mutations on cached reference
types are visible to other consumers. Callers that care about
isolation should cache immutable records or value types.

```csharp
public sealed class MemoryKunCache : IKunCache
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[MemoryKunCache](/sdk/api/kun/kun-caching-implementations/memorykuncache.md)

#### Implements

[IKunCache](/sdk/api/kun/kun-caching-abstractions/ikuncache.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### MemoryKunCache(IMemoryCache, IOptions\<KunCachingOptions>) \{#Kun_Caching_Implementations_MemoryKunCache__ctor_Microsoft_Extensions_Caching_Memory_IMemoryCache_Microsoft_Extensions_Options_IOptions_Kun_Caching_Configuration_KunCachingOptions\_\_}

Creates a new [MemoryKunCache](/sdk/api/kun/kun-caching-implementations/memorykuncache.md).

```csharp
public MemoryKunCache(IMemoryCache cache, IOptions<KunCachingOptions> options)
```

#### Parameters

`cache` [IMemoryCache](https://learn.microsoft.com/dotnet/api/microsoft.extensions.caching.memory.imemorycache)

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[KunCachingOptions](/sdk/api/kun/kun-caching-configuration/kuncachingoptions.md)>

## Methods

### GetAsync\<T>(string, CancellationToken) \{#Kun_Caching_Implementations_MemoryKunCache_GetAsync__1_System_String_System_Threading_CancellationToken\_}

Reads the cached value for `key`; returns [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) on miss.

```csharp
[RequiresUnreferencedCode("IKunCache.GetAsync contract.")]
[RequiresDynamicCode("IKunCache.GetAsync contract.")]
public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
```

#### Parameters

`key` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<T?>

#### Type Parameters

`T`

### GetBytesAsync(string, CancellationToken) \{#Kun_Caching_Implementations_MemoryKunCache_GetBytesAsync_System_String_System_Threading_CancellationToken\_}

Reads the cached byte payload for `key`; returns
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) on miss. Reflection-free path intended for
AOT-strict callers (e.g. `Kun.Identity`'s Redis session
store) that already own their serialization — typically via a
source-generated [`JsonSerializerContext`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonserializercontext).

```csharp
public Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
```

#### Parameters

`key` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[byte](https://learn.microsoft.com/dotnet/api/system.byte)\[]?>

### GetOrSetAsync\<T>(string, Func\<CancellationToken, Task\<T>>, TimeSpan?, CancellationToken) \{#Kun_Caching_Implementations_MemoryKunCache_GetOrSetAsync__1_System_String_System_Func_System_Threading_CancellationToken_System_Threading_Tasks_Task___0___System_Nullable_System_TimeSpan__System_Threading_CancellationToken\_}

Returns the cached value, invoking `factory` exactly
once under concurrent access on the same process when the key is
absent. The produced value is written to the cache under
`key` before being returned.

```csharp
[RequiresUnreferencedCode("IKunCache.GetOrSetAsync contract.")]
[RequiresDynamicCode("IKunCache.GetOrSetAsync contract.")]
public Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
```

#### Parameters

`key` [string](https://learn.microsoft.com/dotnet/api/system.string)

`factory` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<T>>

`ttl` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<T>

#### Type Parameters

`T`

#### Remarks

Single-flight protection is local to the process — cross-process
stampedes can still occur. Use an external distributed lock for
that scenario.

### RemoveAsync(string, CancellationToken) \{#Kun_Caching_Implementations_MemoryKunCache_RemoveAsync_System_String_System_Threading_CancellationToken\_}

Removes the entry for `key`. No-op when the key is absent.

```csharp
public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
```

#### Parameters

`key` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### SetAsync\<T>(string, T, TimeSpan?, CancellationToken) \{#Kun_Caching_Implementations_MemoryKunCache_SetAsync__1_System_String___0_System_Nullable_System_TimeSpan__System_Threading_CancellationToken\_}

Writes `value` under `key` with an
optional TTL. When `ttl` is [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null),
`KunCachingOptions.DefaultTtl` applies; when that is also
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null), the entry never expires.

```csharp
[RequiresUnreferencedCode("IKunCache.SetAsync contract.")]
[RequiresDynamicCode("IKunCache.SetAsync contract.")]
public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
```

#### Parameters

`key` [string](https://learn.microsoft.com/dotnet/api/system.string)

`value` T

`ttl` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

#### Type Parameters

`T`

### SetBytesAsync(string, byte\[], TimeSpan?, CancellationToken) \{#Kun_Caching_Implementations_MemoryKunCache_SetBytesAsync_System_String_System_Byte___System_Nullable_System_TimeSpan__System_Threading_CancellationToken\_}

Writes `value` byte-for-byte under
`key`. When `ttl` is
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null), `KunCachingOptions.DefaultTtl` applies;
when both are [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null), the entry never expires.
Reflection-free path; see [IKunCache.GetBytesAsync](/sdk/api/kun/kun-caching-abstractions/ikuncache.md#Kun_Caching_Abstractions_IKunCache_GetBytesAsync_System_String_System_Threading_CancellationToken_).

```csharp
public Task SetBytesAsync(string key, byte[] value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
```

#### Parameters

`key` [string](https://learn.microsoft.com/dotnet/api/system.string)

`value` [byte](https://learn.microsoft.com/dotnet/api/system.byte)\[]

`ttl` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
