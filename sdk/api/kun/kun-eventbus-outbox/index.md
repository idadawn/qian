> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.EventBus.Outbox

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EventBus)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[IntegrationEventBuffer](/sdk/api/kun/kun-eventbus-outbox/integrationeventbuffer.md)

In-memory accumulator for outbox messages within a single unit-of-work scope.

[OutboxMessage](/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)

Persisted Outbox message row. Created when an integration event is enqueued inside a
`SaveChangesAsync` transaction; published asynchronously by the background publisher.

### Interfaces

[IIntegrationEventOutbox](/sdk/api/kun/kun-eventbus-outbox/iintegrationeventoutbox.md)

Appends outbox messages to the current unit-of-work boundary.

[IOutboxWriter](/sdk/api/kun/kun-eventbus-outbox/ioutboxwriter.md)

Writer primitive for enqueuing integration events from non-aggregate collaborators
(e.g. audit interceptors, storage-event adapters) inside the current DbContext
`SaveChangesAsync` transaction, preserving outbox atomicity.

### Enums

[OutboxStatus](/sdk/api/kun/kun-eventbus-outbox/outboxstatus.md)

Lifecycle state of a persisted Outbox message.
