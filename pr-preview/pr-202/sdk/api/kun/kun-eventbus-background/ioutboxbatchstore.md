> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface IOutboxBatchStore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Background](/pr-preview/pr-202/sdk/api/kun/kun-eventbus-background/index.md)\
Assembly: Kun.EventBus.dll

事务内领取并持有消息行锁的 Outbox 存储；释放作用域前必须完成批次。

```csharp
public interface IOutboxBatchStore : IOutboxMessageStore
```

#### Implements

[IOutboxMessageStore](/pr-preview/pr-202/sdk/api/kun/kun-eventbus-background/ioutboxmessagestore.md)

## Methods

### CompleteBatchAsync(CancellationToken) \{#Kun_EventBus_Background_IOutboxBatchStore_CompleteBatchAsync_System_Threading_CancellationToken\_}

提交本批次的发布状态并释放锁；未完成即释放作用域时回滚。

```csharp
Task CompleteBatchAsync(CancellationToken cancellationToken = default)
```

#### Parameters

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
