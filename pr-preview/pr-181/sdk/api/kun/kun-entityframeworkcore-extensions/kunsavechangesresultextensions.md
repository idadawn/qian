> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunSaveChangesResultExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.Extensions](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-extensions/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

在 Result 边界显式处理乐观并发冲突，其他系统异常仍抛出。

```csharp
public static class KunSaveChangesResultExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunSaveChangesResultExtensions](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore-extensions/kunsavechangesresultextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### SaveChangesResultAsync(DbContext, CancellationToken) \{#Kun_EntityFrameworkCore_Extensions_KunSaveChangesResultExtensions_SaveChangesResultAsync_Microsoft_EntityFrameworkCore_DbContext_System_Threading_CancellationToken\_}

xmin 冲突映射为 Conflict，不自动重试或覆盖其他写入者。

```csharp
public static Task<Result<int>> SaveChangesResultAsync(this DbContext context, CancellationToken cancellationToken = default)
```

#### Parameters

`context` [DbContext](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.dbcontext)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<Result\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>>
