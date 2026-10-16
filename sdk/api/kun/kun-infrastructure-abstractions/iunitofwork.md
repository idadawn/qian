> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IUnitOfWork

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Infrastructure` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Infrastructure`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Infrastructure)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Infrastructure.Abstractions](/sdk/api/kun/kun-infrastructure-abstractions/index.md)\
Assembly: Kun.Infrastructure.dll

工作单元抽象。通常每个请求作用域一个实例，[IUnitOfWork.SaveChangesAsync](/sdk/api/kun/kun-infrastructure-abstractions/iunitofwork.md#Kun_Infrastructure_Abstractions_IUnitOfWork_SaveChangesAsync_System_Threading_CancellationToken_) 由 Pipeline Behavior 统一触发。

```csharp
[Obsolete("请使用 Kun.EntityFrameworkCore.IUnitOfWork。", DiagnosticId = "KUNLEGACY001")]
public interface IUnitOfWork
```

## Methods

### SaveChangesAsync(CancellationToken) \{#Kun_Infrastructure_Abstractions_IUnitOfWork_SaveChangesAsync_System_Threading_CancellationToken\_}

提交所有聚合根变更，并在同一事务内写入 Outbox 消息。

```csharp
Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
```

#### Parameters

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>
