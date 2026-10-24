> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface IProcessedMessageStore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Idempotency](/pr-preview/pr-202/sdk/api/kun/kun-eventbus-idempotency/index.md)\
Assembly: Kun.EventBus.dll

Tracks message IDs that have already been successfully processed by a given consumer,
enabling idempotent at-least-once delivery. Implementations are broker-agnostic
(Redis, in-memory, SQL).

```csharp
public interface IProcessedMessageStore
```

## Methods

### IsProcessedAsync(string, Guid, CancellationToken) \{#Kun_EventBus_Idempotency_IProcessedMessageStore_IsProcessedAsync_System_String_System_Guid_System_Threading_CancellationToken\_}

查询成功记录；旧自定义实现默认返回 false，保留至少一次语义，需升级实现以消除重复。

```csharp
ValueTask<bool> IsProcessedAsync(string consumerName, Guid messageId, CancellationToken cancellationToken = default)
```

#### Parameters

`consumerName` [string](https://learn.microsoft.com/dotnet/api/system.string)

`messageId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

### TryMarkProcessedAsync(string, Guid, TimeSpan, CancellationToken) \{#Kun_EventBus_Idempotency_IProcessedMessageStore_TryMarkProcessedAsync_System_String_System_Guid_System_TimeSpan_System_Threading_CancellationToken\_}

Atomically records `messageId` as processed by `consumerName`.

```csharp
ValueTask<bool> TryMarkProcessedAsync(string consumerName, Guid messageId, TimeSpan ttl, CancellationToken cancellationToken = default)
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
