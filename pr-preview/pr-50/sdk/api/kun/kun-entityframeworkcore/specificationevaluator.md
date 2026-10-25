> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class SpecificationEvaluator

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.EntityFrameworkCore)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

Applies an [ISpecification\<T>](/pr-preview/pr-50/sdk/api/kun/kun-infrastructure-abstractions/ispecification-1.md) to an [`IQueryable`](https://learn.microsoft.com/dotnet/api/system.linq.iqueryable-1),
translating criteria, ordering, includes, and paging into EF Core LINQ operators.

```csharp
public static class SpecificationEvaluator
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SpecificationEvaluator](/pr-preview/pr-50/sdk/api/kun/kun-entityframeworkcore/specificationevaluator.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### Apply\<T>(IQueryable\<T>, ISpecification\<T>) \{#Kun_EntityFrameworkCore_SpecificationEvaluator_Apply__1_System_Linq_IQueryable___0__Kun_Infrastructure_Abstractions_ISpecification___0\_\_}

Applies all clauses of `spec` to `query` and
returns the resulting [`IQueryable`](https://learn.microsoft.com/dotnet/api/system.linq.iqueryable-1).

```csharp
public static IQueryable<T> Apply<T>(IQueryable<T> query, ISpecification<T> spec) where T : class
```

#### Parameters

`query` [IQueryable](https://learn.microsoft.com/dotnet/api/system.linq.iqueryable-1)\<T>

The base queryable to augment.

`spec` ISpecification\<T>

The specification containing the filtering / sorting / paging rules.

#### Returns

[IQueryable](https://learn.microsoft.com/dotnet/api/system.linq.iqueryable-1)\<T>

A new [`IQueryable`](https://learn.microsoft.com/dotnet/api/system.linq.iqueryable-1) with all specification clauses applied.

#### Type Parameters

`T`

The entity type.
