> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class NoOpDomainEventDispatcher

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EntityFrameworkCore)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore](/sdk/api/kun/kun-entityframeworkcore/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Default no-op domain event dispatcher. Users replace this with a real implementation
(e.g. one backed by `Kun.Mediator.IPublisher`) by registering their own
[IDomainEventDispatcher](/sdk/api/kun/kun-core-domain/idomaineventdispatcher.md) before calling `AddKunEntityFrameworkCore`.

```csharp
public sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[NoOpDomainEventDispatcher](/sdk/api/kun/kun-entityframeworkcore/noopdomaineventdispatcher.md)

#### Implements

IDomainEventDispatcher

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### NoOpDomainEventDispatcher() \{#Kun_EntityFrameworkCore_NoOpDomainEventDispatcher__ctor}

```csharp
public NoOpDomainEventDispatcher()
```

## Methods

### DispatchAsync(IReadOnlyCollection\<IDomainEvent>, CancellationToken) \{#Kun_EntityFrameworkCore_NoOpDomainEventDispatcher_DispatchAsync_System_Collections_Generic_IReadOnlyCollection_Kun_Core_Domain_IDomainEvent__System_Threading_CancellationToken\_}

Dispatch the given domain events to all registered handlers, in insertion order.

```csharp
public ValueTask DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
```

#### Parameters

`domainEvents` [IReadOnlyCollection](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlycollection-1)\<IDomainEvent>

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
