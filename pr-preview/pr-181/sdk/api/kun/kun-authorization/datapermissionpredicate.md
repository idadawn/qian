> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class DataPermissionPredicate

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Authorization` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Authorization`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Authorization)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Authorization](/pr-preview/pr-181/sdk/api/kun/kun-authorization/index.md)\
Assembly: Kun.Authorization.dll

可序列化的字段谓词 AST，SQL 编译与参数绑定由 Provider 实现。

```csharp
public sealed record DataPermissionPredicate : IEquatable<DataPermissionPredicate>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[DataPermissionPredicate](/pr-preview/pr-181/sdk/api/kun/kun-authorization/datapermissionpredicate.md)

#### Implements

[IEquatable\<DataPermissionPredicate>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### DataPermissionPredicate(string, string?, JsonElement?, IReadOnlyList\<DataPermissionPredicate>) \{#Kun_Authorization_DataPermissionPredicate__ctor_System_String_System_String_System_Nullable_System_Text_Json_JsonElement__System_Collections_Generic_IReadOnlyList_Kun_Authorization_DataPermissionPredicate\_\_}

可序列化的字段谓词 AST，SQL 编译与参数绑定由 Provider 实现。

```csharp
public DataPermissionPredicate(string Operator, string? Field, JsonElement? Value, IReadOnlyList<DataPermissionPredicate> Children)
```

#### Parameters

`Operator` [string](https://learn.microsoft.com/dotnet/api/system.string)

`Field` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Value` [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)?

`Children` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[DataPermissionPredicate](/pr-preview/pr-181/sdk/api/kun/kun-authorization/datapermissionpredicate.md)>

## Properties

### Children \{#Kun_Authorization_DataPermissionPredicate_Children}

```csharp
public IReadOnlyList<DataPermissionPredicate> Children { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[DataPermissionPredicate](/pr-preview/pr-181/sdk/api/kun/kun-authorization/datapermissionpredicate.md)>

### Field \{#Kun_Authorization_DataPermissionPredicate_Field}

```csharp
public string? Field { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Operator \{#Kun_Authorization_DataPermissionPredicate_Operator}

```csharp
public string Operator { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Value \{#Kun_Authorization_DataPermissionPredicate_Value}

```csharp
public JsonElement? Value { get; init; }
```

#### Property Value

[JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)?

## Methods

### \<Clone>$() \{#Kun_Authorization_DataPermissionPredicate__Clone\_\_}

```csharp
public DataPermissionPredicate <Clone>$()
```

#### Returns

[DataPermissionPredicate](/pr-preview/pr-181/sdk/api/kun/kun-authorization/datapermissionpredicate.md)

### Deconstruct(out string, out string?, out JsonElement?, out IReadOnlyList\<DataPermissionPredicate>) \{#Kun_Authorization_DataPermissionPredicate_Deconstruct_System_String__System_String__System_Nullable_System_Text_Json_JsonElement___System_Collections_Generic_IReadOnlyList_Kun_Authorization_DataPermissionPredicate\_\_\_}

```csharp
public void Deconstruct(out string Operator, out string? Field, out JsonElement? Value, out IReadOnlyList<DataPermissionPredicate> Children)
```

#### Parameters

`Operator` [string](https://learn.microsoft.com/dotnet/api/system.string)

`Field` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Value` [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)?

`Children` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[DataPermissionPredicate](/pr-preview/pr-181/sdk/api/kun/kun-authorization/datapermissionpredicate.md)>

### Equals(object?) \{#Kun_Authorization_DataPermissionPredicate_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(DataPermissionPredicate?) \{#Kun_Authorization_DataPermissionPredicate_Equals_Kun_Authorization_DataPermissionPredicate\_}

```csharp
public bool Equals(DataPermissionPredicate? other)
```

#### Parameters

`other` [DataPermissionPredicate](/pr-preview/pr-181/sdk/api/kun/kun-authorization/datapermissionpredicate.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Authorization_DataPermissionPredicate_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Authorization_DataPermissionPredicate_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(DataPermissionPredicate?, DataPermissionPredicate?) \{#Kun_Authorization_DataPermissionPredicate_op_Equality_Kun_Authorization_DataPermissionPredicate_Kun_Authorization_DataPermissionPredicate\_}

```csharp
public static bool operator ==(DataPermissionPredicate? left, DataPermissionPredicate? right)
```

#### Parameters

`left` [DataPermissionPredicate](/pr-preview/pr-181/sdk/api/kun/kun-authorization/datapermissionpredicate.md)?

`right` [DataPermissionPredicate](/pr-preview/pr-181/sdk/api/kun/kun-authorization/datapermissionpredicate.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(DataPermissionPredicate?, DataPermissionPredicate?) \{#Kun_Authorization_DataPermissionPredicate_op_Inequality_Kun_Authorization_DataPermissionPredicate_Kun_Authorization_DataPermissionPredicate\_}

```csharp
public static bool operator !=(DataPermissionPredicate? left, DataPermissionPredicate? right)
```

#### Parameters

`left` [DataPermissionPredicate](/pr-preview/pr-181/sdk/api/kun/kun-authorization/datapermissionpredicate.md)?

`right` [DataPermissionPredicate](/pr-preview/pr-181/sdk/api/kun/kun-authorization/datapermissionpredicate.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
