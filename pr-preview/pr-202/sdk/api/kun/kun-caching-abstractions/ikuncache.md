> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface IKunCache

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Caching` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Caching`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Caching)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Caching.Abstractions](/pr-preview/pr-202/sdk/api/kun/kun-caching-abstractions/index.md)\
Assembly: Kun.Caching.dll

Unified cache abstraction for Kun Framework consumers. Implementations
back to an in-process [`IMemoryCache`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.caching.memory.imemorycache)
or to a distributed [`IDistributedCache`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache)
(Redis via StackExchange) — see
`Kun.Caching.Implementations.MemoryKunCache` and
`Kun.Caching.Implementations.RedisKunCache`.

```csharp
public interface IKunCache
```

## Remarks

The generic methods are annotated with
[`RequiresUnreferencedCodeAttribute`](https://learn.microsoft.com/dotnet/api/system.diagnostics.codeanalysis.requiresunreferencedcodeattribute) and
[`RequiresDynamicCodeAttribute`](https://learn.microsoft.com/dotnet/api/system.diagnostics.codeanalysis.requiresdynamiccodeattribute) because the default
`System.Text.Json`-backed serializer walks the type argument
by reflection. AOT-strict consumers can replace the default
serializer with a source-generated implementation; see
[IKunCacheSerializer](/pr-preview/pr-202/sdk/api/kun/kun-caching-abstractions/ikuncacheserializer.md).

## Methods

### GetAsync\<T>(string, CancellationToken) \{#Kun_Caching_Abstractions_IKunCache_GetAsync__1_System_String_System_Threading_CancellationToken\_}

Reads the cached value for `key`; returns [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) on miss.

```csharp
[RequiresUnreferencedCode("Default serializer uses reflection. Pass a source-generated IKunCacheSerializer for AOT safety.")]
[RequiresDynamicCode("Default serializer uses reflection. Pass a source-generated IKunCacheSerializer for AOT safety.")]
Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
```

#### Parameters

`key` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<T?>

#### Type Parameters

`T`

### GetBytesAsync(string, CancellationToken) \{#Kun_Caching_Abstractions_IKunCache_GetBytesAsync_System_String_System_Threading_CancellationToken\_}

Reads the cached byte payload for `key`; returns
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) on miss. Reflection-free path intended for
AOT-strict callers (e.g. `Kun.Identity`'s Redis session
store) that already own their serialization — typically via a
source-generated [`JsonSerializerContext`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonserializercontext).

```csharp
Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
```

#### Parameters

`key` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[byte](https://learn.microsoft.com/dotnet/api/system.byte)\[]?>

### GetOrSetAsync\<T>(string, Func\<CancellationToken, Task\<T>>, TimeSpan?, CancellationToken) \{#Kun_Caching_Abstractions_IKunCache_GetOrSetAsync__1_System_String_System_Func_System_Threading_CancellationToken_System_Threading_Tasks_Task___0___System_Nullable_System_TimeSpan__System_Threading_CancellationToken\_}

Returns the cached value, invoking `factory` exactly
once under concurrent access on the same process when the key is
absent. The produced value is written to the cache under
`key` before being returned.

```csharp
[RequiresUnreferencedCode("Default serializer uses reflection. Pass a source-generated IKunCacheSerializer for AOT safety.")]
[RequiresDynamicCode("Default serializer uses reflection. Pass a source-generated IKunCacheSerializer for AOT safety.")]
Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
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

### RemoveAsync(string, CancellationToken) \{#Kun_Caching_Abstractions_IKunCache_RemoveAsync_System_String_System_Threading_CancellationToken\_}

Removes the entry for `key`. No-op when the key is absent.

```csharp
Task RemoveAsync(string key, CancellationToken cancellationToken = default)
```

#### Parameters

`key` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### SetAsync\<T>(string, T, TimeSpan?, CancellationToken) \{#Kun_Caching_Abstractions_IKunCache_SetAsync__1_System_String___0_System_Nullable_System_TimeSpan__System_Threading_CancellationToken\_}

Writes `value` under `key` with an
optional TTL. When `ttl` is [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null),
`KunCachingOptions.DefaultTtl` applies; when that is also
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null), the entry never expires.

```csharp
[RequiresUnreferencedCode("Default serializer uses reflection. Pass a source-generated IKunCacheSerializer for AOT safety.")]
[RequiresDynamicCode("Default serializer uses reflection. Pass a source-generated IKunCacheSerializer for AOT safety.")]
Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
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

### SetBytesAsync(string, byte\[], TimeSpan?, CancellationToken) \{#Kun_Caching_Abstractions_IKunCache_SetBytesAsync_System_String_System_Byte___System_Nullable_System_TimeSpan__System_Threading_CancellationToken\_}

Writes `value` byte-for-byte under
`key`. When `ttl` is
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null), `KunCachingOptions.DefaultTtl` applies;
when both are [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null), the entry never expires.
Reflection-free path; see [IKunCache.GetBytesAsync](/pr-preview/pr-202/sdk/api/kun/kun-caching-abstractions/ikuncache.md#Kun_Caching_Abstractions_IKunCache_GetBytesAsync_System_String_System_Threading_CancellationToken_).

```csharp
Task SetBytesAsync(string key, byte[] value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
```

#### Parameters

`key` [string](https://learn.microsoft.com/dotnet/api/system.string)

`value` [byte](https://learn.microsoft.com/dotnet/api/system.byte)\[]

`ttl` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)?

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
