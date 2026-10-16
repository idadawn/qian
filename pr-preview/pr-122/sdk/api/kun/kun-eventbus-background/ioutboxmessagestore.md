> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Interface IOutboxMessageStore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Background](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/index.md)\
Assembly: Kun.EventBus.dll

Abstracts access to the outbox persistence layer. The concrete implementation
lives in Kun.EntityFrameworkCore (or any other persistence module).

```csharp
public interface IOutboxMessageStore
```

## Methods

### FetchPendingAsync(int, CancellationToken) \{#Kun_EventBus_Background_IOutboxMessageStore_FetchPendingAsync_System_Int32_System_Threading_CancellationToken\_}

Fetches up to `batchSize` pending messages ordered by [OutboxMessage.OccurredAt](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md#Kun_EventBus_Outbox_OutboxMessage_OccurredAt).

```csharp
Task<IReadOnlyList<OutboxMessage>> FetchPendingAsync(int batchSize, CancellationToken cancellationToken = default)
```

#### Parameters

`batchSize` [int](https://learn.microsoft.com/dotnet/api/system.int32)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)>>

### UpdateAsync(OutboxMessage, CancellationToken) \{#Kun_EventBus_Background_IOutboxMessageStore_UpdateAsync_Kun_EventBus_Outbox_OutboxMessage_System_Threading_CancellationToken\_}

Persists the updated state (Status, AttemptCount, PublishedAt, LastError) of a message.

```csharp
Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default)
```

#### Parameters

`message` [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
