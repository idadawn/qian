> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IOutboxLockProvider

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Background](/sdk/api/kun/kun-eventbus-background/index.md)\
Assembly: Kun.EventBus.dll

Provides a distributed lease that prevents multiple pods from publishing
the same outbox batch simultaneously.

```csharp
public interface IOutboxLockProvider
```

## Methods

### TryAcquireAsync(string, TimeSpan, CancellationToken) \{#Kun_EventBus_Background_IOutboxLockProvider_TryAcquireAsync_System_String_System_TimeSpan_System_Threading_CancellationToken\_}

Tries to acquire a distributed lock with the given `lockName`.

```csharp
ValueTask<IAsyncDisposable?> TryAcquireAsync(string lockName, TimeSpan ttl, CancellationToken ct = default)
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
