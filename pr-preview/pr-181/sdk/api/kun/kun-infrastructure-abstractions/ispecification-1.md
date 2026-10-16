> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface ISpecification\<T>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Infrastructure` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Infrastructure`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Infrastructure)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Infrastructure.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-infrastructure-abstractions/index.md)\
Assembly: Kun.Infrastructure.dll

规约模式（Specification Pattern）抽象。
把查询条件、排序、分页从仓储中剥离出来，以可组合的对象表达业务意图。

```csharp
public interface ISpecification<T>
```

#### Type Parameters

`T`

## Properties

### Criteria \{#Kun_Infrastructure_Abstractions_ISpecification_1_Criteria}

查询条件谓词。

```csharp
Expression<Func<T, bool>>? Criteria { get; }
```

#### Property Value

[Expression](https://learn.microsoft.com/dotnet/api/system.linq.expressions.expression-1)\<[Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [bool](https://learn.microsoft.com/dotnet/api/system.boolean)>>?

### IncludeStrings \{#Kun_Infrastructure_Abstractions_ISpecification_1_IncludeStrings}

需要 Include 的导航属性字符串形式（如 `"Lines.Product"`）。

```csharp
IReadOnlyList<string> IncludeStrings { get; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### OrderBy \{#Kun_Infrastructure_Abstractions_ISpecification_1_OrderBy}

排序表达式（升序）。

```csharp
Expression<Func<T, object>>? OrderBy { get; }
```

#### Property Value

[Expression](https://learn.microsoft.com/dotnet/api/system.linq.expressions.expression-1)\<[Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [object](https://learn.microsoft.com/dotnet/api/system.object)>>?

### OrderByDescending \{#Kun_Infrastructure_Abstractions_ISpecification_1_OrderByDescending}

排序表达式（降序）。

```csharp
Expression<Func<T, object>>? OrderByDescending { get; }
```

#### Property Value

[Expression](https://learn.microsoft.com/dotnet/api/system.linq.expressions.expression-1)\<[Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [object](https://learn.microsoft.com/dotnet/api/system.object)>>?

### Skip \{#Kun_Infrastructure_Abstractions_ISpecification_1_Skip}

分页：Skip 数量。

```csharp
int Skip { get; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### Take \{#Kun_Infrastructure_Abstractions_ISpecification_1_Take}

分页：Take 数量（0 表示不分页）。

```csharp
int Take { get; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)
