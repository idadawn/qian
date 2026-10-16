> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class InMemoryProcessedMessageStore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Idempotency](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-idempotency/index.md)\
Assembly: Kun.EventBus.dll

In-memory implementation of [IProcessedMessageStore](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-idempotency/iprocessedmessagestore.md) backed by a
[`ConcurrentDictionary`](https://learn.microsoft.com/dotnet/api/system.collections.concurrent.concurrentdictionary-2). Intended for testing and
single-instance scenarios only — state is lost on restart and is not shared
across multiple processes.

```csharp
public sealed class InMemoryProcessedMessageStore : IProcessedMessageStore
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[InMemoryProcessedMessageStore](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-idempotency/inmemoryprocessedmessagestore.md)

#### Implements

[IProcessedMessageStore](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-idempotency/iprocessedmessagestore.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### InMemoryProcessedMessageStore(TimeProvider) \{#Kun_EventBus_Idempotency_InMemoryProcessedMessageStore__ctor_System_TimeProvider\_}

Initialises the store using the supplied `timeProvider`.

```csharp
public InMemoryProcessedMessageStore(TimeProvider timeProvider)
```

#### Parameters

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

### InMemoryProcessedMessageStore() \{#Kun_EventBus_Idempotency_InMemoryProcessedMessageStore__ctor}

Initialises the store using [`System`](https://learn.microsoft.com/dotnet/api/system.timeprovider.system).

```csharp
public InMemoryProcessedMessageStore()
```

## Methods

### IsProcessedAsync(string, Guid, CancellationToken) \{#Kun_EventBus_Idempotency_InMemoryProcessedMessageStore_IsProcessedAsync_System_String_System_Guid_System_Threading_CancellationToken\_}

查询成功记录；旧自定义实现默认返回 false，保留至少一次语义，需升级实现以消除重复。

```csharp
public ValueTask<bool> IsProcessedAsync(string consumerName, Guid messageId, CancellationToken cancellationToken = default)
```

#### Parameters

`consumerName` [string](https://learn.microsoft.com/dotnet/api/system.string)

`messageId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

### TryMarkProcessedAsync(string, Guid, TimeSpan, CancellationToken) \{#Kun_EventBus_Idempotency_InMemoryProcessedMessageStore_TryMarkProcessedAsync_System_String_System_Guid_System_TimeSpan_System_Threading_CancellationToken\_}

Atomically records `messageId` as processed by `consumerName`.

```csharp
public ValueTask<bool> TryMarkProcessedAsync(string consumerName, Guid messageId, TimeSpan ttl, CancellationToken cancellationToken = default)
```

#### Parameters

`consumerName` [string](https://learn.microsoft.com/dotnet/api/system.string)

`messageId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

`ttl` [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

[true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) when the mark succeeded (first processing),
[false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) when the message was already processed (duplicate delivery).
