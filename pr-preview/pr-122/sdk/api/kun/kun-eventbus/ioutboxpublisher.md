> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Interface IOutboxPublisher

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/index.md)\
Assembly: Kun.EventBus.dll

Dispatches pending outbox messages to the underlying broker.

```csharp
public interface IOutboxPublisher
```

## Methods

### PublishPendingAsync(CancellationToken) \{#Kun_EventBus_IOutboxPublisher_PublishPendingAsync_System_Threading_CancellationToken\_}

Publishes all [OutboxStatus.Pending](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxstatus.md#Kun_EventBus_Outbox_OutboxStatus_Pending) messages to the broker.

```csharp
ValueTask PublishPendingAsync(CancellationToken cancellationToken = default)
```

#### Parameters

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
