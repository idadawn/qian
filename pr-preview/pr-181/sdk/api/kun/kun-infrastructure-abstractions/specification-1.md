> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class Specification\<T>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Infrastructure` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Infrastructure`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Infrastructure)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Infrastructure.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-infrastructure-abstractions/index.md)\
Assembly: Kun.Infrastructure.dll

规约模式默认实现。业务方通过继承并在构造器中调用 `AddCriteria` / `ApplyOrderBy` /
`ApplyPaging` 等保护方法来定义查询语义。

```csharp
public abstract class Specification<T> : ISpecification<T>
```

#### Type Parameters

`T`

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[Specification\<T>](/pr-preview/pr-181/sdk/api/kun/kun-infrastructure-abstractions/specification-1.md)

#### Implements

[ISpecification\<T>](/pr-preview/pr-181/sdk/api/kun/kun-infrastructure-abstractions/ispecification-1.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### Specification() \{#Kun_Infrastructure_Abstractions_Specification_1__ctor}

```csharp
protected Specification()
```

## Properties

### Criteria \{#Kun_Infrastructure_Abstractions_Specification_1_Criteria}

查询条件谓词。

```csharp
public Expression<Func<T, bool>>? Criteria { get; }
```

#### Property Value

[Expression](https://learn.microsoft.com/dotnet/api/system.linq.expressions.expression-1)\<[Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [bool](https://learn.microsoft.com/dotnet/api/system.boolean)>>?

### IncludeStrings \{#Kun_Infrastructure_Abstractions_Specification_1_IncludeStrings}

需要 Include 的导航属性字符串形式（如 `"Lines.Product"`）。

```csharp
public IReadOnlyList<string> IncludeStrings { get; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### OrderBy \{#Kun_Infrastructure_Abstractions_Specification_1_OrderBy}

排序表达式（升序）。

```csharp
public Expression<Func<T, object>>? OrderBy { get; }
```

#### Property Value

[Expression](https://learn.microsoft.com/dotnet/api/system.linq.expressions.expression-1)\<[Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [object](https://learn.microsoft.com/dotnet/api/system.object)>>?

### OrderByDescending \{#Kun_Infrastructure_Abstractions_Specification_1_OrderByDescending}

排序表达式（降序）。

```csharp
public Expression<Func<T, object>>? OrderByDescending { get; }
```

#### Property Value

[Expression](https://learn.microsoft.com/dotnet/api/system.linq.expressions.expression-1)\<[Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [object](https://learn.microsoft.com/dotnet/api/system.object)>>?

### Skip \{#Kun_Infrastructure_Abstractions_Specification_1_Skip}

分页：Skip 数量。

```csharp
public int Skip { get; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### Take \{#Kun_Infrastructure_Abstractions_Specification_1_Take}

分页：Take 数量（0 表示不分页）。

```csharp
public int Take { get; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

## Methods

### AddCriteria(Expression\<Func\<T, bool>>) \{#Kun_Infrastructure_Abstractions_Specification_1_AddCriteria_System_Linq_Expressions_Expression_System_Func__0_System_Boolean\_\_\_}

```csharp
protected void AddCriteria(Expression<Func<T, bool>> criteria)
```

#### Parameters

`criteria` [Expression](https://learn.microsoft.com/dotnet/api/system.linq.expressions.expression-1)\<[Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [bool](https://learn.microsoft.com/dotnet/api/system.boolean)>>

### AddInclude(string) \{#Kun_Infrastructure_Abstractions_Specification_1_AddInclude_System_String\_}

```csharp
protected void AddInclude(string includeString)
```

#### Parameters

`includeString` [string](https://learn.microsoft.com/dotnet/api/system.string)

### ApplyOrderBy(Expression\<Func\<T, object>>) \{#Kun_Infrastructure_Abstractions_Specification_1_ApplyOrderBy_System_Linq_Expressions_Expression_System_Func__0_System_Object\_\_\_}

```csharp
protected void ApplyOrderBy(Expression<Func<T, object>> orderBy)
```

#### Parameters

`orderBy` [Expression](https://learn.microsoft.com/dotnet/api/system.linq.expressions.expression-1)\<[Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [object](https://learn.microsoft.com/dotnet/api/system.object)>>

### ApplyOrderByDescending(Expression\<Func\<T, object>>) \{#Kun_Infrastructure_Abstractions_Specification_1_ApplyOrderByDescending_System_Linq_Expressions_Expression_System_Func__0_System_Object\_\_\_}

```csharp
protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDesc)
```

#### Parameters

`orderByDesc` [Expression](https://learn.microsoft.com/dotnet/api/system.linq.expressions.expression-1)\<[Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [object](https://learn.microsoft.com/dotnet/api/system.object)>>

### ApplyPaging(int, int) \{#Kun_Infrastructure_Abstractions_Specification_1_ApplyPaging_System_Int32_System_Int32\_}

```csharp
protected void ApplyPaging(int skip, int take)
```

#### Parameters

`skip` [int](https://learn.microsoft.com/dotnet/api/system.int32)

`take` [int](https://learn.microsoft.com/dotnet/api/system.int32)
