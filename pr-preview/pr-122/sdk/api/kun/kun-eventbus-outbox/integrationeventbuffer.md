> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class IntegrationEventBuffer

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Outbox](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/index.md)\
Assembly: Kun.EventBus.dll

In-memory accumulator for outbox messages within a single unit-of-work scope.

```csharp
public sealed class IntegrationEventBuffer : IIntegrationEventOutbox
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[IntegrationEventBuffer](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/integrationeventbuffer.md)

#### Implements

[IIntegrationEventOutbox](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/iintegrationeventoutbox.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### IntegrationEventBuffer() \{#Kun_EventBus_Outbox_IntegrationEventBuffer__ctor}

```csharp
public IntegrationEventBuffer()
```

## Methods

### Add(OutboxMessage) \{#Kun_EventBus_Outbox_IntegrationEventBuffer_Add_Kun_EventBus_Outbox_OutboxMessage\_}

Stages `message` for atomic persistence with the business transaction.

```csharp
public void Add(OutboxMessage message)
```

#### Parameters

`message` [OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)

### Clear() \{#Kun_EventBus_Outbox_IntegrationEventBuffer_Clear}

Clears staged messages after they have been flushed to the store.

```csharp
public void Clear()
```

### GetPendingMessages() \{#Kun_EventBus_Outbox_IntegrationEventBuffer_GetPendingMessages}

Returns all staged messages pending flush.

```csharp
public IReadOnlyList<OutboxMessage> GetPendingMessages()
```

#### Returns

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[OutboxMessage](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md)>
