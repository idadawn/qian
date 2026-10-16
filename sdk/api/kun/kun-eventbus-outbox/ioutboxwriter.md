> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IOutboxWriter

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Outbox](/sdk/api/kun/kun-eventbus-outbox/index.md)\
Assembly: Kun.EventBus.dll

Writer primitive for enqueuing integration events from non-aggregate collaborators
(e.g. audit interceptors, storage-event adapters) inside the current DbContext
`SaveChangesAsync` transaction, preserving outbox atomicity.

```csharp
public interface IOutboxWriter
```

## Remarks

The DI container resolves [IOutboxWriter](/sdk/api/kun/kun-eventbus-outbox/ioutboxwriter.md) to the same scoped instance as
[IIntegrationEventBus](/sdk/api/kun/kun-eventbus/iintegrationeventbus.md) — both interfaces name the same accumulator. Prefer
[IOutboxWriter](/sdk/api/kun/kun-eventbus-outbox/ioutboxwriter.md) when the caller is an infrastructure collaborator (interceptor,
adapter) rather than a domain aggregate or command handler; the narrower contract
communicates intent and decouples from the full bus surface.

## Methods

### Enqueue\<TEvent>(TEvent) \{#Kun_EventBus_Outbox_IOutboxWriter_Enqueue__1___0\_}

Enqueues an integration event for atomic commit with the pending DbContext change set.
Produces the same [OutboxMessage](/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md) shape as
[IIntegrationEventBus.Publish](/sdk/api/kun/kun-eventbus/iintegrationeventbus.md#Kun_EventBus_IIntegrationEventBus_Publish__1___0_): stable event id via
[IEventTypeRegistry](/sdk/api/kun/kun-eventbus/ieventtyperegistry.md), current tenant and trace context captured.

```csharp
void Enqueue<TEvent>(TEvent @event) where TEvent : class
```

#### Parameters

`event` TEvent

The event instance to enqueue. Must not be [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).

#### Type Parameters

`TEvent`

The integration event type. Must be registered via [IEventTypeRegistrar](/sdk/api/kun/kun-eventbus/ieventtyperegistrar.md).
