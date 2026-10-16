> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface IIntegrationEventPublisher

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus](/pr-preview/pr-181/sdk/api/kun/kun-eventbus/index.md)\
Assembly: Kun.EventBus.dll

Broker-specific transport that publishes a single [OutboxMessage](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) to the message bus.
Implementations live in broker adapters (e.g. `Kun.EventBus.RabbitMQ`).

```csharp
public interface IIntegrationEventPublisher
```

## Methods

### PublishAsync(OutboxMessage, CancellationToken) \{#Kun_EventBus_IIntegrationEventPublisher_PublishAsync_Kun_EventBus_Outbox_OutboxMessage_System_Threading_CancellationToken\_}

Publishes `message` to the broker.

```csharp
ValueTask PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
```

#### Parameters

`message` [OutboxMessage](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)

The persisted outbox envelope to dispatch.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancels the publish operation.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

#### Exceptions

[Exception](https://learn.microsoft.com/dotnet/api/system.exception)

Thrown when publish fails; the caller should record the failure on the outbox row.
