> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface IRepository\<T>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Infrastructure` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Infrastructure`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Infrastructure)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Infrastructure.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-infrastructure-abstractions/index.md)\
Assembly: Kun.Infrastructure.dll

聚合根仓储抽象。业务层依赖本接口，不依赖具体 ORM。

```csharp
[Obsolete("旧通用仓储入口不用于新代码；请为业务定义端口并由 EF Provider 实现。", DiagnosticId = "KUNLEGACY001")]
public interface IRepository<T> where T : class, IAggregateRoot
```

#### Type Parameters

`T`

## Methods

### AddAsync(T, CancellationToken) \{#Kun_Infrastructure_Abstractions_IRepository_1_AddAsync__0_System_Threading_CancellationToken\_}

```csharp
Task AddAsync(T entity, CancellationToken cancellationToken = default)
```

#### Parameters

`entity` T

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### CountAsync(ISpecification\<T>, CancellationToken) \{#Kun_Infrastructure_Abstractions_IRepository_1_CountAsync_Kun_Infrastructure_Abstractions_ISpecification__0__System_Threading_CancellationToken\_}

```csharp
Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
```

#### Parameters

`specification` [ISpecification](/pr-preview/pr-181/sdk/api/kun/kun-infrastructure-abstractions/ispecification-1.md)\<T>

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>

### GetByIdAsync(object, CancellationToken) \{#Kun_Infrastructure_Abstractions_IRepository_1_GetByIdAsync_System_Object_System_Threading_CancellationToken\_}

```csharp
Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
```

#### Parameters

`id` [object](https://learn.microsoft.com/dotnet/api/system.object)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<T?>

### ListAsync(ISpecification\<T>, CancellationToken) \{#Kun_Infrastructure_Abstractions_IRepository_1_ListAsync_Kun_Infrastructure_Abstractions_ISpecification__0__System_Threading_CancellationToken\_}

```csharp
Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
```

#### Parameters

`specification` [ISpecification](/pr-preview/pr-181/sdk/api/kun/kun-infrastructure-abstractions/ispecification-1.md)\<T>

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<T>>

### Remove(T) \{#Kun_Infrastructure_Abstractions_IRepository_1_Remove__0\_}

```csharp
void Remove(T entity)
```

#### Parameters

`entity` T

### Update(T) \{#Kun_Infrastructure_Abstractions_IRepository_1_Update__0\_}

```csharp
void Update(T entity)
```

#### Parameters

`entity` T
