> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class NullOutboxLockProvider

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EventBus)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Background](/sdk/api/kun/kun-eventbus-background/index.md)\
Assembly: Kun.EventBus.dll

No-op [IOutboxLockProvider](/sdk/api/kun/kun-eventbus-background/ioutboxlockprovider.md) for single-instance deployments.
Always succeeds and returns a disposable that does nothing.
This is the default provider registered by `AddKunOutboxPublisher`.

```csharp
public sealed class NullOutboxLockProvider : IOutboxLockProvider
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[NullOutboxLockProvider](/sdk/api/kun/kun-eventbus-background/nulloutboxlockprovider.md)

#### Implements

[IOutboxLockProvider](/sdk/api/kun/kun-eventbus-background/ioutboxlockprovider.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### NullOutboxLockProvider() \{#Kun_EventBus_Background_NullOutboxLockProvider__ctor}

```csharp
public NullOutboxLockProvider()
```

## Fields

### Instance \{#Kun_EventBus_Background_NullOutboxLockProvider_Instance}

A shared singleton instance of [NullOutboxLockProvider](/sdk/api/kun/kun-eventbus-background/nulloutboxlockprovider.md).

```csharp
public static readonly NullOutboxLockProvider Instance
```

#### Field Value

[NullOutboxLockProvider](/sdk/api/kun/kun-eventbus-background/nulloutboxlockprovider.md)

## Methods

### TryAcquireAsync(string, TimeSpan, CancellationToken) \{#Kun_EventBus_Background_NullOutboxLockProvider_TryAcquireAsync_System_String_System_TimeSpan_System_Threading_CancellationToken\_}

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
