> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class EfOutboxMessageStore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore](/sdk/api/kun/kun-entityframeworkcore/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

默认 PostgreSQL Outbox 存储：用 SKIP LOCKED 在同一事务中领取、发布并更新批次。

```csharp
public sealed class EfOutboxMessageStore : IOutboxBatchStore, IOutboxMessageStore, IAsyncDisposable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[EfOutboxMessageStore](/sdk/api/kun/kun-entityframeworkcore/efoutboxmessagestore.md)

#### Implements

IOutboxBatchStore,
IOutboxMessageStore,
[IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### EfOutboxMessageStore(KunDbContext, TimeProvider) \{#Kun_EntityFrameworkCore_EfOutboxMessageStore__ctor_Kun_EntityFrameworkCore_KunDbContext_System_TimeProvider\_}

默认 PostgreSQL Outbox 存储：用 SKIP LOCKED 在同一事务中领取、发布并更新批次。

```csharp
public EfOutboxMessageStore(KunDbContext context, TimeProvider clock)
```

#### Parameters

`context` [KunDbContext](/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md)

`clock` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

## Methods

### CompleteBatchAsync(CancellationToken) \{#Kun_EntityFrameworkCore_EfOutboxMessageStore_CompleteBatchAsync_System_Threading_CancellationToken\_}

提交本批次的发布状态并释放锁；未完成即释放作用域时回滚。

```csharp
public Task CompleteBatchAsync(CancellationToken cancellationToken = default)
```

#### Parameters

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### DisposeAsync() \{#Kun_EntityFrameworkCore_EfOutboxMessageStore_DisposeAsync}

```csharp
public ValueTask DisposeAsync()
```

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

### FetchPendingAsync(int, CancellationToken) \{#Kun_EntityFrameworkCore_EfOutboxMessageStore_FetchPendingAsync_System_Int32_System_Threading_CancellationToken\_}

Fetches up to `batchSize` pending messages ordered by [OutboxMessage.OccurredAt](/sdk/api/kun/kun-eventbus-outbox/outboxmessage.md#Kun_EventBus_Outbox_OutboxMessage_OccurredAt).

```csharp
public Task<IReadOnlyList<OutboxMessage>> FetchPendingAsync(int batchSize, CancellationToken cancellationToken = default)
```

#### Parameters

`batchSize` [int](https://learn.microsoft.com/dotnet/api/system.int32)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<OutboxMessage>>

### UpdateAsync(OutboxMessage, CancellationToken) \{#Kun_EntityFrameworkCore_EfOutboxMessageStore_UpdateAsync_Kun_EventBus_Outbox_OutboxMessage_System_Threading_CancellationToken\_}

Persists the updated state (Status, AttemptCount, PublishedAt, LastError) of a message.

```csharp
public Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default)
```

#### Parameters

`message` OutboxMessage

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
