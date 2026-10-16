> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class PagedResult\<T>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Dapper` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Dapper`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Dapper)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Dapper](/pr-preview/pr-50/sdk/api/kun/kun-dapper/index.md)\
Assembly: Kun.Dapper.dll

A single page of query results with total-count metadata.

```csharp
public sealed class PagedResult<T>
```

#### Type Parameters

`T`

The item type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PagedResult\<T>](/pr-preview/pr-50/sdk/api/kun/kun-dapper/pagedresult-1.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PagedResult() \{#Kun_Dapper_PagedResult_1__ctor}

```csharp
public PagedResult()
```

## Properties

### HasNextPage \{#Kun_Dapper_PagedResult_1_HasNextPage}

Whether a next page exists.

```csharp
public bool HasNextPage { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### HasPreviousPage \{#Kun_Dapper_PagedResult_1_HasPreviousPage}

Whether a previous page exists.

```csharp
public bool HasPreviousPage { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Items \{#Kun_Dapper_PagedResult_1_Items}

Items on this page.

```csharp
public IReadOnlyList<T> Items { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<T>

### Request \{#Kun_Dapper_PagedResult_1_Request}

The page request that produced this result.

```csharp
public required PageRequest Request { get; init; }
```

#### Property Value

[PageRequest](/pr-preview/pr-50/sdk/api/kun/kun-dapper/pagerequest.md)

### TotalCount \{#Kun_Dapper_PagedResult_1_TotalCount}

Total number of items across all pages.

```csharp
public int TotalCount { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### TotalPages \{#Kun_Dapper_PagedResult_1_TotalPages}

Total number of pages.

```csharp
public int TotalPages { get; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

## Methods

### Empty(PageRequest) \{#Kun_Dapper_PagedResult_1_Empty_Kun_Dapper_PageRequest\_}

Creates an empty result for the given request.

```csharp
public static PagedResult<T> Empty(PageRequest request)
```

#### Parameters

`request` [PageRequest](/pr-preview/pr-50/sdk/api/kun/kun-dapper/pagerequest.md)

#### Returns

[PagedResult](/pr-preview/pr-50/sdk/api/kun/kun-dapper/pagedresult-1.md)\<T>
