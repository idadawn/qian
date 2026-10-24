> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class EfUnitOfWork

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

EF Core-backed [IUnitOfWork](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore/iunitofwork.md) that atomically persists aggregate changes,
dispatches domain events in-process, and writes integration events to the Outbox.

```csharp
public sealed class EfUnitOfWork : IUnitOfWork
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[EfUnitOfWork](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore/efunitofwork.md)

#### Implements

[IUnitOfWork](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore/iunitofwork.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### EfUnitOfWork(KunDbContext, IDomainEventDispatcher, IntegrationEventBuffer) \{#Kun_EntityFrameworkCore_EfUnitOfWork__ctor_Kun_EntityFrameworkCore_KunDbContext_Kun_Core_Domain_IDomainEventDispatcher_Kun_EventBus_Outbox_IntegrationEventBuffer\_}

Creates a new [EfUnitOfWork](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore/efunitofwork.md).

```csharp
public EfUnitOfWork(KunDbContext dbContext, IDomainEventDispatcher domainEvents, IntegrationEventBuffer integrationBuffer)
```

#### Parameters

`dbContext` [KunDbContext](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md)

`domainEvents` IDomainEventDispatcher

`integrationBuffer` IntegrationEventBuffer

## Methods

### SaveChangesAsync(CancellationToken) \{#Kun_EntityFrameworkCore_EfUnitOfWork_SaveChangesAsync_System_Threading_CancellationToken\_}

Commits aggregate changes, dispatches domain events, and flushes buffered integration
events to the Outbox in a single transaction.

```csharp
public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
```

#### Parameters

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>
