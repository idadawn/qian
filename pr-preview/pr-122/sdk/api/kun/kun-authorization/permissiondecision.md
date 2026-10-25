> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class PermissionDecision

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Authorization` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Authorization`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Authorization)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Authorization](/pr-preview/pr-122/sdk/api/kun/kun-authorization/index.md)\
Assembly: Kun.Authorization.dll

可解释判定；Allow=false 是缺省语义。

```csharp
public sealed record PermissionDecision : IEquatable<PermissionDecision>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PermissionDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissiondecision.md)

#### Implements

[IEquatable\<PermissionDecision>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PermissionDecision(bool, string, IReadOnlyList\<string>) \{#Kun_Authorization_PermissionDecision__ctor_System_Boolean_System_String_System_Collections_Generic_IReadOnlyList_System_String\_\_}

可解释判定；Allow=false 是缺省语义。

```csharp
public PermissionDecision(bool Allow, string Reason, IReadOnlyList<string> MatchedPolicies)
```

#### Parameters

`Allow` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

`Reason` [string](https://learn.microsoft.com/dotnet/api/system.string)

`MatchedPolicies` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

## Properties

### Allow \{#Kun_Authorization_PermissionDecision_Allow}

```csharp
public bool Allow { get; init; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### MatchedPolicies \{#Kun_Authorization_PermissionDecision_MatchedPolicies}

```csharp
public IReadOnlyList<string> MatchedPolicies { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### Reason \{#Kun_Authorization_PermissionDecision_Reason}

```csharp
public string Reason { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### \<Clone>$() \{#Kun_Authorization_PermissionDecision__Clone\_\_}

```csharp
public PermissionDecision <Clone>$()
```

#### Returns

[PermissionDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissiondecision.md)

### Deconstruct(out bool, out string, out IReadOnlyList\<string>) \{#Kun_Authorization_PermissionDecision_Deconstruct_System_Boolean__System_String__System_Collections_Generic_IReadOnlyList_System_String\_\_\_}

```csharp
public void Deconstruct(out bool Allow, out string Reason, out IReadOnlyList<string> MatchedPolicies)
```

#### Parameters

`Allow` [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

`Reason` [string](https://learn.microsoft.com/dotnet/api/system.string)

`MatchedPolicies` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

### Equals(object?) \{#Kun_Authorization_PermissionDecision_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(PermissionDecision?) \{#Kun_Authorization_PermissionDecision_Equals_Kun_Authorization_PermissionDecision\_}

```csharp
public bool Equals(PermissionDecision? other)
```

#### Parameters

`other` [PermissionDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissiondecision.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Authorization_PermissionDecision_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Authorization_PermissionDecision_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(PermissionDecision?, PermissionDecision?) \{#Kun_Authorization_PermissionDecision_op_Equality_Kun_Authorization_PermissionDecision_Kun_Authorization_PermissionDecision\_}

```csharp
public static bool operator ==(PermissionDecision? left, PermissionDecision? right)
```

#### Parameters

`left` [PermissionDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissiondecision.md)?

`right` [PermissionDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissiondecision.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(PermissionDecision?, PermissionDecision?) \{#Kun_Authorization_PermissionDecision_op_Inequality_Kun_Authorization_PermissionDecision_Kun_Authorization_PermissionDecision\_}

```csharp
public static bool operator !=(PermissionDecision? left, PermissionDecision? right)
```

#### Parameters

`left` [PermissionDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissiondecision.md)?

`right` [PermissionDecision](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissiondecision.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
