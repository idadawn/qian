> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class RabbitMqIntegrationEventPublisher

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus.RabbitMQ` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus.RabbitMQ`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus.RabbitMQ)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.RabbitMQ.Publishing](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq-publishing/index.md)\
Assembly: Kun.EventBus.RabbitMQ.dll

Publishes [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) records to a RabbitMQ topic exchange.
Routing key equals [OutboxMessage.EventTypeId](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md#Kun_EventBus_Outbox_OutboxMessage_EventTypeId) (stable event ID, e.g. `order.placed.v1`).
W3C trace context headers and tenant metadata are forwarded as AMQP headers.

```csharp
public sealed class RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[RabbitMqIntegrationEventPublisher](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq-publishing/rabbitmqintegrationeventpublisher.md)

#### Implements

IIntegrationEventPublisher

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### RabbitMqIntegrationEventPublisher(RabbitMqConnectionProvider, IOptions\<RabbitMqOptions>, ILogger\<RabbitMqIntegrationEventPublisher>) \{#Kun_EventBus_RabbitMQ_Publishing_RabbitMqIntegrationEventPublisher__ctor_Kun_EventBus_RabbitMQ_RabbitMqConnectionProvider_Microsoft_Extensions_Options_IOptions_Kun_EventBus_RabbitMQ_Options_RabbitMqOptions__Microsoft_Extensions_Logging_ILogger_Kun_EventBus_RabbitMQ_Publishing_RabbitMqIntegrationEventPublisher\_\_}

Initializes the publisher.

```csharp
public RabbitMqIntegrationEventPublisher(RabbitMqConnectionProvider connectionProvider, IOptions<RabbitMqOptions> options, ILogger<RabbitMqIntegrationEventPublisher> logger)
```

#### Parameters

`connectionProvider` [RabbitMqConnectionProvider](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq/rabbitmqconnectionprovider.md)

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[RabbitMqOptions](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq-options/rabbitmqoptions.md)>

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[RabbitMqIntegrationEventPublisher](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq-publishing/rabbitmqintegrationeventpublisher.md)>

## Methods

### PublishAsync(OutboxMessage, CancellationToken) \{#Kun_EventBus_RabbitMQ_Publishing_RabbitMqIntegrationEventPublisher_PublishAsync_Kun_EventBus_Outbox_OutboxMessage_System_Threading_CancellationToken\_}

Publishes `message` to the broker.

```csharp
public ValueTask PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
```

#### Parameters

`message` OutboxMessage

The persisted outbox envelope to dispatch.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancels the publish operation.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

#### Exceptions

[Exception](https://learn.microsoft.com/dotnet/api/system.exception)

Thrown when publish fails; the caller should record the failure on the outbox row.
