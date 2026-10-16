> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class DapperAotExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Dapper` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Dapper`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Dapper)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Dapper.Extensions](/sdk/api/kun/kun-dapper-extensions/index.md)\
Assembly: Kun.Dapper.dll

Pagination helpers for Dapper query handlers.

```csharp
public static class DapperAotExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[DapperAotExtensions](/sdk/api/kun/kun-dapper-extensions/dapperaotextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

These helpers compose count + page queries into a single [PagedResult\<T>](/sdk/api/kun/kun-dapper/pagedresult-1.md).
Callers are expected to annotate their query methods with `[DapperAot]` so that
Dapper.AOT generates the reader code at build time. The helpers themselves only call
the standard Dapper `SqlMapper` extensions and introduce no runtime reflection.

## Methods

### QueryPageAsync\<T>(IDbConnection, string, string, PageRequest, object?, CancellationToken) \{#Kun_Dapper_Extensions_DapperAotExtensions_QueryPageAsync__1_System_Data_IDbConnection_System_String_System_String_Kun_Dapper_PageRequest_System_Object_System_Threading_CancellationToken\_}

Executes a count query and a page query in sequence and wraps the results in a
[PagedResult\<T>](/sdk/api/kun/kun-dapper/pagedresult-1.md).

```csharp
public static Task<PagedResult<T>> QueryPageAsync<T>(this IDbConnection connection, string countSql, string pageSql, PageRequest request, object? param = null, CancellationToken cancellationToken = default)
```

#### Parameters

`connection` [IDbConnection](https://learn.microsoft.com/dotnet/api/system.data.idbconnection)

An open [`IDbConnection`](https://learn.microsoft.com/dotnet/api/system.data.idbconnection).

`countSql` [string](https://learn.microsoft.com/dotnet/api/system.string)

SQL that returns a single `int` total-row count.

`pageSql` [string](https://learn.microsoft.com/dotnet/api/system.string)

SQL that returns the page of rows. May reference `@Offset` and `@PageSize`
— these are automatically merged into `param` if not already present.

`request` [PageRequest](/sdk/api/kun/kun-dapper/pagerequest.md)

The page request containing page number and size.

`param` [object](https://learn.microsoft.com/dotnet/api/system.object)?

Optional parameter bag (anonymous object or `DynamicParameters`).
`@Offset` and `@PageSize` are added automatically.

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[PagedResult](/sdk/api/kun/kun-dapper/pagedresult-1.md)\<T>>

A [PagedResult\<T>](/sdk/api/kun/kun-dapper/pagedresult-1.md) with the total count and current page of items.

#### Type Parameters

`T`

The item type returned by the page query.
