> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class RedisOutboxLockProvider

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Background](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-background/index.md)\
Assembly: Kun.EventBus.dll

Redis-backed [IOutboxLockProvider](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-background/ioutboxlockprovider.md) using `SET key value NX EX` semantics.
Safe for multi-pod deployments: only one pod holds the lock per TTL window.

```csharp
public sealed class RedisOutboxLockProvider : IOutboxLockProvider
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[RedisOutboxLockProvider](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-background/redisoutboxlockprovider.md)

#### Implements

[IOutboxLockProvider](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-background/ioutboxlockprovider.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### RedisOutboxLockProvider(IDatabase) \{#Kun_EventBus_Background_RedisOutboxLockProvider__ctor_StackExchange_Redis_IDatabase\_}

Initializes the provider with an explicit Redis `database`.

```csharp
public RedisOutboxLockProvider(IDatabase database)
```

#### Parameters

`database` IDatabase

### RedisOutboxLockProvider(IConnectionMultiplexer) \{#Kun_EventBus_Background_RedisOutboxLockProvider__ctor_StackExchange_Redis_IConnectionMultiplexer\_}

Initializes the provider by resolving the default database from
`multiplexer`.

```csharp
public RedisOutboxLockProvider(IConnectionMultiplexer multiplexer)
```

#### Parameters

`multiplexer` IConnectionMultiplexer

## Methods

### TryAcquireAsync(string, TimeSpan, CancellationToken) \{#Kun_EventBus_Background_RedisOutboxLockProvider_TryAcquireAsync_System_String_System_TimeSpan_System_Threading_CancellationToken\_}

Tries to acquire a distributed lock with the given `lockName`.

```csharp
public ValueTask<IAsyncDisposable?> TryAcquireAsync(string lockName, TimeSpan ttl, CancellationToken ct = default)
```

#### Parameters

`lockName` [string](https://learn.microsoft.com/dotnet/api/system.string)

A well-known name that all competing pods use (e.g. `"outbox-publisher"`).

`ttl` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

How long the lock is held before it auto-expires.

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)?>

An [`IAsyncDisposable`](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable) that releases the lock on disposal, or
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) when the lock is already held by another instance.
