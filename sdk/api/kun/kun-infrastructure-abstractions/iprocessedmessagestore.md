> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IProcessedMessageStore

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Infrastructure` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Infrastructure`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Infrastructure)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Infrastructure.Abstractions](/sdk/api/kun/kun-infrastructure-abstractions/index.md)\
Assembly: Kun.Infrastructure.dll

消费端已处理消息记录存储。用于保证 Outbox 至少一次投递场景下的消费幂等。

默认实现基于 Redis 的 `SETNX + EX`（单命令原子操作）。
业务可替换为基于数据库唯一索引的实现以获得跨故障域的强一致性。

```csharp
[Obsolete("请使用 Kun.EventBus.Idempotency.IProcessedMessageStore。", DiagnosticId = "KUNLEGACY001")]
public interface IProcessedMessageStore
```

## Methods

### MarkAsCompletedAsync(Guid, string, CancellationToken) \{#Kun_Infrastructure_Abstractions_IProcessedMessageStore_MarkAsCompletedAsync_System_Guid_System_String_System_Threading_CancellationToken\_}

Handler 成功完成后调用。可选地把状态从"处理中"升级为"已完成"，
便于运维排查长时间卡住的消息。

```csharp
Task MarkAsCompletedAsync(Guid messageId, string consumerName, CancellationToken cancellationToken = default)
```

#### Parameters

`messageId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

`consumerName` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### TryMarkAsProcessingAsync(Guid, string, CancellationToken) \{#Kun_Infrastructure_Abstractions_IProcessedMessageStore_TryMarkAsProcessingAsync_System_Guid_System_String_System_Threading_CancellationToken\_}

尝试把 `messageId` 标记为"正在处理"。

```csharp
Task<bool> TryMarkAsProcessingAsync(Guid messageId, string consumerName, CancellationToken cancellationToken = default)
```

#### Parameters

`messageId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

`consumerName` [string](https://learn.microsoft.com/dotnet/api/system.string)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

[true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) 表示首次处理，可以继续执行 Handler；
[false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) 表示消息已被处理过，Handler 应直接跳过。
