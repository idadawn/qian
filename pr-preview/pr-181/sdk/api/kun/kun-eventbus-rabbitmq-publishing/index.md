> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Namespace Kun.EventBus.RabbitMQ.Publishing

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus.RabbitMQ` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus.RabbitMQ`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus.RabbitMQ)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

### Classes

[RabbitMqIntegrationEventPublisher](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-rabbitmq-publishing/rabbitmqintegrationeventpublisher.md)

Publishes [OutboxMessage](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) records to a RabbitMQ topic exchange.
Routing key equals [OutboxMessage.EventTypeId](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md#Kun_EventBus_Outbox_OutboxMessage_EventTypeId) (stable event ID, e.g. `order.placed.v1`).
W3C trace context headers and tenant metadata are forwarded as AMQP headers.
