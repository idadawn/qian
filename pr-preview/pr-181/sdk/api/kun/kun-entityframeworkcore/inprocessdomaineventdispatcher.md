> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class InProcessDomainEventDispatcher

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Dispatches domain events to in-process Mediator notification handlers.
Domain events that also implement [INotification](/pr-preview/pr-181/sdk/api/kun/kun-mediator/inotification.md) are routed to all
registered [INotificationHandler\<TNotification>](/pr-preview/pr-181/sdk/api/kun/kun-mediator/inotificationhandler-1.md)s via [IPublisher](/pr-preview/pr-181/sdk/api/kun/kun-mediator/ipublisher.md).
Events that do not implement [INotification](/pr-preview/pr-181/sdk/api/kun/kun-mediator/inotification.md) are silently skipped.

```csharp
public sealed class InProcessDomainEventDispatcher : IDomainEventDispatcher
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[InProcessDomainEventDispatcher](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/inprocessdomaineventdispatcher.md)

#### Implements

IDomainEventDispatcher

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

[IPublisher.Publish](/pr-preview/pr-181/sdk/api/kun/kun-mediator/ipublisher.md#Kun_Mediator_IPublisher_Publish__1___0_System_Threading_CancellationToken_) is a generic method keyed on the
concrete notification type at runtime. Because `Kun.EntityFrameworkCore` is not
an AOT candidate (`IsAotCandidate=false`), reflection is permitted here to
invoke `Publish` with the exact concrete type so the Mediator dispatcher can
resolve the correct [INotificationHandler\<TNotification>](/pr-preview/pr-181/sdk/api/kun/kun-mediator/inotificationhandler-1.md) registrations.

## Constructors

### InProcessDomainEventDispatcher(IPublisher) \{#Kun_EntityFrameworkCore_InProcessDomainEventDispatcher__ctor_Kun_Mediator_IPublisher\_}

Creates a new [InProcessDomainEventDispatcher](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/inprocessdomaineventdispatcher.md).

```csharp
public InProcessDomainEventDispatcher(IPublisher publisher)
```

#### Parameters

`publisher` IPublisher

The Mediator publisher used to broadcast notifications.

### InProcessDomainEventDispatcher(IPublisher, IServiceProvider) \{#Kun_EntityFrameworkCore_InProcessDomainEventDispatcher__ctor_Kun_Mediator_IPublisher_System_IServiceProvider\_}

事务入口：通知处理器在调用方的 DI scope 内解析。

```csharp
public InProcessDomainEventDispatcher(IPublisher publisher, IServiceProvider services)
```

#### Parameters

`publisher` IPublisher

`services` [IServiceProvider](https://learn.microsoft.com/dotnet/api/system.iserviceprovider)

## Methods

### DispatchAsync(IReadOnlyCollection\<IDomainEvent>, CancellationToken) \{#Kun_EntityFrameworkCore_InProcessDomainEventDispatcher_DispatchAsync_System_Collections_Generic_IReadOnlyCollection_Kun_Core_Domain_IDomainEvent__System_Threading_CancellationToken\_}

Dispatch the given domain events to all registered handlers, in insertion order.

```csharp
public ValueTask DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
```

#### Parameters

`domainEvents` [IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)\<IDomainEvent>

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
