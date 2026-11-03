> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class AuthorizationSubject

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Authorization` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Authorization`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Authorization)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Authorization](/pr-preview/pr-202/sdk/api/kun/kun-authorization/index.md)\
Assembly: Kun.Authorization.dll

统一授权主体；权限集合不从令牌直接推导。

```csharp
public sealed record AuthorizationSubject : IEquatable<AuthorizationSubject>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[AuthorizationSubject](/pr-preview/pr-202/sdk/api/kun/kun-authorization/authorizationsubject.md)

#### Implements

[IEquatable\<AuthorizationSubject>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### AuthorizationSubject(string, string?, string) \{#Kun_Authorization_AuthorizationSubject__ctor_System_String_System_String_System_String\_}

统一授权主体；权限集合不从令牌直接推导。

```csharp
public AuthorizationSubject(string Id, string? TenantId, string Kind = "user")
```

#### Parameters

`Id` [string](https://learn.microsoft.com/dotnet/api/system.string)

`TenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Kind` [string](https://learn.microsoft.com/dotnet/api/system.string)

## Properties

### Id \{#Kun_Authorization_AuthorizationSubject_Id}

```csharp
public string Id { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Kind \{#Kun_Authorization_AuthorizationSubject_Kind}

```csharp
public string Kind { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### TenantId \{#Kun_Authorization_AuthorizationSubject_TenantId}

```csharp
public string? TenantId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

## Methods

### \<Clone>$() \{#Kun_Authorization_AuthorizationSubject__Clone\_\_}

```csharp
public AuthorizationSubject <Clone>$()
```

#### Returns

[AuthorizationSubject](/pr-preview/pr-202/sdk/api/kun/kun-authorization/authorizationsubject.md)

### Deconstruct(out string, out string?, out string) \{#Kun_Authorization_AuthorizationSubject_Deconstruct_System_String__System_String__System_String\_\_}

```csharp
public void Deconstruct(out string Id, out string? TenantId, out string Kind)
```

#### Parameters

`Id` [string](https://learn.microsoft.com/dotnet/api/system.string)

`TenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`Kind` [string](https://learn.microsoft.com/dotnet/api/system.string)

### Equals(object?) \{#Kun_Authorization_AuthorizationSubject_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(AuthorizationSubject?) \{#Kun_Authorization_AuthorizationSubject_Equals_Kun_Authorization_AuthorizationSubject\_}

```csharp
public bool Equals(AuthorizationSubject? other)
```

#### Parameters

`other` [AuthorizationSubject](/pr-preview/pr-202/sdk/api/kun/kun-authorization/authorizationsubject.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Authorization_AuthorizationSubject_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Authorization_AuthorizationSubject_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(AuthorizationSubject?, AuthorizationSubject?) \{#Kun_Authorization_AuthorizationSubject_op_Equality_Kun_Authorization_AuthorizationSubject_Kun_Authorization_AuthorizationSubject\_}

```csharp
public static bool operator ==(AuthorizationSubject? left, AuthorizationSubject? right)
```

#### Parameters

`left` [AuthorizationSubject](/pr-preview/pr-202/sdk/api/kun/kun-authorization/authorizationsubject.md)?

`right` [AuthorizationSubject](/pr-preview/pr-202/sdk/api/kun/kun-authorization/authorizationsubject.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(AuthorizationSubject?, AuthorizationSubject?) \{#Kun_Authorization_AuthorizationSubject_op_Inequality_Kun_Authorization_AuthorizationSubject_Kun_Authorization_AuthorizationSubject\_}

```csharp
public static bool operator !=(AuthorizationSubject? left, AuthorizationSubject? right)
```

#### Parameters

`left` [AuthorizationSubject](/pr-preview/pr-202/sdk/api/kun/kun-authorization/authorizationsubject.md)?

`right` [AuthorizationSubject](/pr-preview/pr-202/sdk/api/kun/kun-authorization/authorizationsubject.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
