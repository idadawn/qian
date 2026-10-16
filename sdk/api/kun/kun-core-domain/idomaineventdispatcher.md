> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IDomainEventDispatcher

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

Dispatches domain events to their in-process handlers.

Called by the Unit of Work immediately before persistence commits, so handlers run inside
the same EF Core transaction that produced the aggregate changes. For cross-service events,
handlers translate the domain event into an [IIntegrationEvent](/sdk/api/kun/kun-core-domain/iintegrationevent.md) and enqueue it
via the Outbox.

```csharp
public interface IDomainEventDispatcher
```

## Methods

### DispatchAsync(IReadOnlyCollection\<IDomainEvent>, CancellationToken) \{#Kun_Core_Domain_IDomainEventDispatcher_DispatchAsync_System_Collections_Generic_IReadOnlyCollection_Kun_Core_Domain_IDomainEvent__System_Threading_CancellationToken\_}

Dispatch the given domain events to all registered handlers, in insertion order.

```csharp
ValueTask DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
```

#### Parameters

`domainEvents` [IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)\<[IDomainEvent](/sdk/api/kun/kun-core-domain/idomainevent.md)>

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
