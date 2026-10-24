> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.EventBus

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EventBus)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.EventBus.Background](/sdk/api/kun/kun-eventbus-background/index.md)

[Kun.EventBus.DependencyInjection](/sdk/api/kun/kun-eventbus-dependencyinjection/index.md)

[Kun.EventBus.Idempotency](/sdk/api/kun/kun-eventbus-idempotency/index.md)

[Kun.EventBus.Outbox](/sdk/api/kun/kun-eventbus-outbox/index.md)

[Kun.EventBus.RabbitMQ](/sdk/api/kun/kun-eventbus-rabbitmq/index.md)

### Classes

[EventTypeRegistry](/sdk/api/kun/kun-eventbus/eventtyperegistry.md)

Immutable, O(1) event-type registry backed by [`FrozenDictionary`](https://learn.microsoft.com/dotnet/api/system.collections.frozen.frozendictionary-2).

[IntegrationEventIdAttribute](/sdk/api/kun/kun-eventbus/integrationeventidattribute.md)

Declares the stable event-type identifier (token) for an integration event CLR type.
Use with [IEventTypeRegistrar.Register](/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md#Kun_EventBus_IEventTypeRegistrar_Register__1) so the token lives next
to the type definition rather than being repeated in DI wiring.

[MessageContext](/sdk/api/kun/kun-eventbus/messagecontext.md)

Metadata accompanying an integration event during handler dispatch.

### Interfaces

[IEventTypeRegistrar](/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md)

Fluent builder for populating the [IEventTypeRegistry](/sdk/api/kun/kun-eventbus/ieventtyperegistry.md).

[IEventTypeRegistry](/sdk/api/kun/kun-eventbus/ieventtyperegistry.md)

Maps stable event-type tokens to their CLR types at runtime.

[IIntegrationEventBus](/sdk/api/kun/kun-eventbus/iintegrationeventbus.md)

High-level façade for publishing integration events via the Outbox pattern.

[IIntegrationEventHandler\<TEvent>](/sdk/api/kun/kun-eventbus/iintegrationeventhandler-1.md)

Handles a strongly-typed integration event received from the broker.

[IIntegrationEventPublisher](/sdk/api/kun/kun-eventbus/iintegrationeventpublisher.md)

Broker-specific transport that publishes a single [OutboxMessage](/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) to the message bus.
Implementations live in broker adapters (e.g. `Kun.EventBus.RabbitMQ`).

[IOutboxPublisher](/sdk/api/kun/kun-eventbus/ioutboxpublisher.md)

Dispatches pending outbox messages to the underlying broker.
