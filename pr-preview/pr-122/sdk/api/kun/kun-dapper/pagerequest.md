> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class PageRequest

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Dapper` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Dapper`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Dapper)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Dapper](/pr-preview/pr-122/sdk/api/kun/kun-dapper/index.md)\
Assembly: Kun.Dapper.dll

Pagination parameters for a read query.

```csharp
public sealed record PageRequest : IEquatable<PageRequest>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PageRequest](/pr-preview/pr-122/sdk/api/kun/kun-dapper/pagerequest.md)

#### Implements

[IEquatable\<PageRequest>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PageRequest(int, int) \{#Kun_Dapper_PageRequest__ctor_System_Int32_System_Int32\_}

Pagination parameters for a read query.

```csharp
public PageRequest(int Page = 1, int PageSize = 20)
```

#### Parameters

`Page` [int](https://learn.microsoft.com/dotnet/api/system.int32)

1-based page number.

`PageSize` [int](https://learn.microsoft.com/dotnet/api/system.int32)

Number of items per page.

## Properties

### Offset \{#Kun_Dapper_PageRequest_Offset}

Zero-based row offset for SQL `OFFSET … FETCH`.

```csharp
public int Offset { get; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### Page \{#Kun_Dapper_PageRequest_Page}

1-based page number.

```csharp
public int Page { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### PageSize \{#Kun_Dapper_PageRequest_PageSize}

Number of items per page.

```csharp
public int PageSize { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

## Methods

### \<Clone>$() \{#Kun_Dapper_PageRequest__Clone\_\_}

```csharp
public PageRequest <Clone>$()
```

#### Returns

[PageRequest](/pr-preview/pr-122/sdk/api/kun/kun-dapper/pagerequest.md)

### Deconstruct(out int, out int) \{#Kun_Dapper_PageRequest_Deconstruct_System_Int32__System_Int32\_\_}

```csharp
public void Deconstruct(out int Page, out int PageSize)
```

#### Parameters

`Page` [int](https://learn.microsoft.com/dotnet/api/system.int32)

`PageSize` [int](https://learn.microsoft.com/dotnet/api/system.int32)

### Equals(object?) \{#Kun_Dapper_PageRequest_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(PageRequest?) \{#Kun_Dapper_PageRequest_Equals_Kun_Dapper_PageRequest\_}

```csharp
public bool Equals(PageRequest? other)
```

#### Parameters

`other` [PageRequest](/pr-preview/pr-122/sdk/api/kun/kun-dapper/pagerequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Dapper_PageRequest_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Dapper_PageRequest_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(PageRequest?, PageRequest?) \{#Kun_Dapper_PageRequest_op_Equality_Kun_Dapper_PageRequest_Kun_Dapper_PageRequest\_}

```csharp
public static bool operator ==(PageRequest? left, PageRequest? right)
```

#### Parameters

`left` [PageRequest](/pr-preview/pr-122/sdk/api/kun/kun-dapper/pagerequest.md)?

`right` [PageRequest](/pr-preview/pr-122/sdk/api/kun/kun-dapper/pagerequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(PageRequest?, PageRequest?) \{#Kun_Dapper_PageRequest_op_Inequality_Kun_Dapper_PageRequest_Kun_Dapper_PageRequest\_}

```csharp
public static bool operator !=(PageRequest? left, PageRequest? right)
```

#### Parameters

`left` [PageRequest](/pr-preview/pr-122/sdk/api/kun/kun-dapper/pagerequest.md)?

`right` [PageRequest](/pr-preview/pr-122/sdk/api/kun/kun-dapper/pagerequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
