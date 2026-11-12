> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface IIntegrationEventOutbox

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Outbox](/pr-preview/pr-202/sdk/api/kun/kun-eventbus-outbox/index.md)\
Assembly: Kun.EventBus.dll

Appends outbox messages to the current unit-of-work boundary.

```csharp
public interface IIntegrationEventOutbox
```

## Methods

### Add(OutboxMessage) \{#Kun_EventBus_Outbox_IIntegrationEventOutbox_Add_Kun_EventBus_Outbox_OutboxMessage\_}

Stages `message` for atomic persistence with the business transaction.

```csharp
void Add(OutboxMessage message)
```

#### Parameters

`message` [OutboxMessage](/pr-preview/pr-202/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)

### GetPendingMessages() \{#Kun_EventBus_Outbox_IIntegrationEventOutbox_GetPendingMessages}

Returns all staged messages pending flush.

```csharp
IReadOnlyList<OutboxMessage> GetPendingMessages()
```

#### Returns

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[OutboxMessage](/pr-preview/pr-202/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)>
