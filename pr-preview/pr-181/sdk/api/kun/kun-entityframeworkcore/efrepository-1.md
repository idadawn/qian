> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class EfRepository\<T>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Generic EF Core implementation of [IRepository\<T>](/pr-preview/pr-181/sdk/api/kun/kun-infrastructure-abstractions/irepository-1.md) for aggregate roots.
Persistence is committed by the Unit of Work — callers must not call
`SaveChangesAsync` directly.

```csharp
public class EfRepository<T> : IRepository<T> where T : class, IAggregateRoot
```

#### Type Parameters

`T`

The aggregate root type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[EfRepository\<T>](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/efrepository-1.md)

#### Implements

IRepository\<T>

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### EfRepository(KunDbContext) \{#Kun_EntityFrameworkCore_EfRepository_1__ctor_Kun_EntityFrameworkCore_KunDbContext\_}

Creates a new [EfRepository\<T>](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/efrepository-1.md).

```csharp
public EfRepository(KunDbContext dbContext)
```

#### Parameters

`dbContext` [KunDbContext](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md)

The scoped [KunDbContext](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md) instance.

## Properties

### DbContext \{#Kun_EntityFrameworkCore_EfRepository_1_DbContext}

The underlying EF Core [KunDbContext](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md).

```csharp
protected KunDbContext DbContext { get; }
```

#### Property Value

[KunDbContext](/pr-preview/pr-181/sdk/api/kun/kun-entityframeworkcore/kundbcontext.md)

## Methods

### AddAsync(T, CancellationToken) \{#Kun_EntityFrameworkCore_EfRepository_1_AddAsync__0_System_Threading_CancellationToken\_}

```csharp
public virtual Task AddAsync(T entity, CancellationToken cancellationToken = default)
```

#### Parameters

`entity` T

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)

### CountAsync(ISpecification\<T>, CancellationToken) \{#Kun_EntityFrameworkCore_EfRepository_1_CountAsync_Kun_Infrastructure_Abstractions_ISpecification__0__System_Threading_CancellationToken\_}

```csharp
public virtual Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
```

#### Parameters

`specification` ISpecification\<T>

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[int](https://learn.microsoft.com/dotnet/api/system.int32)>

### GetByIdAsync(object, CancellationToken) \{#Kun_EntityFrameworkCore_EfRepository_1_GetByIdAsync_System_Object_System_Threading_CancellationToken\_}

```csharp
public virtual Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
```

#### Parameters

`id` [object](https://learn.microsoft.com/dotnet/api/system.object)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<T?>

### ListAsync(ISpecification\<T>, CancellationToken) \{#Kun_EntityFrameworkCore_EfRepository_1_ListAsync_Kun_Infrastructure_Abstractions_ISpecification__0__System_Threading_CancellationToken\_}

```csharp
public virtual Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
```

#### Parameters

`specification` ISpecification\<T>

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<T>>

### Remove(T) \{#Kun_EntityFrameworkCore_EfRepository_1_Remove__0\_}

```csharp
public virtual void Remove(T entity)
```

#### Parameters

`entity` T

### Update(T) \{#Kun_EntityFrameworkCore_EfRepository_1_Update__0\_}

```csharp
public virtual void Update(T entity)
```

#### Parameters

`entity` T
