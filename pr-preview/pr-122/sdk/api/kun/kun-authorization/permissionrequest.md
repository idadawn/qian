> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class PermissionRequest

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Authorization` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Authorization`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Authorization)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Authorization](/pr-preview/pr-122/sdk/api/kun/kun-authorization/index.md)\
Assembly: Kun.Authorization.dll

权限点与可选资源实例；Permission 使用稳定的资源动作编码。

```csharp
public sealed record PermissionRequest : IEquatable<PermissionRequest>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PermissionRequest](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissionrequest.md)

#### Implements

[IEquatable\<PermissionRequest>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PermissionRequest(AuthorizationSubject, string, string?) \{#Kun_Authorization_PermissionRequest__ctor_Kun_Authorization_AuthorizationSubject_System_String_System_String\_}

权限点与可选资源实例；Permission 使用稳定的资源动作编码。

```csharp
public PermissionRequest(AuthorizationSubject Subject, string Permission, string? ResourceId = null)
```

#### Parameters

`Subject` [AuthorizationSubject](/pr-preview/pr-122/sdk/api/kun/kun-authorization/authorizationsubject.md)

`Permission` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ResourceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

## Properties

### Permission \{#Kun_Authorization_PermissionRequest_Permission}

```csharp
public string Permission { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ResourceId \{#Kun_Authorization_PermissionRequest_ResourceId}

```csharp
public string? ResourceId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### Subject \{#Kun_Authorization_PermissionRequest_Subject}

```csharp
public AuthorizationSubject Subject { get; init; }
```

#### Property Value

[AuthorizationSubject](/pr-preview/pr-122/sdk/api/kun/kun-authorization/authorizationsubject.md)

## Methods

### \<Clone>$() \{#Kun_Authorization_PermissionRequest__Clone\_\_}

```csharp
public PermissionRequest <Clone>$()
```

#### Returns

[PermissionRequest](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissionrequest.md)

### Deconstruct(out AuthorizationSubject, out string, out string?) \{#Kun_Authorization_PermissionRequest_Deconstruct_Kun_Authorization_AuthorizationSubject__System_String__System_String\_\_}

```csharp
public void Deconstruct(out AuthorizationSubject Subject, out string Permission, out string? ResourceId)
```

#### Parameters

`Subject` [AuthorizationSubject](/pr-preview/pr-122/sdk/api/kun/kun-authorization/authorizationsubject.md)

`Permission` [string](https://learn.microsoft.com/dotnet/api/system.string)

`ResourceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

### Equals(object?) \{#Kun_Authorization_PermissionRequest_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(PermissionRequest?) \{#Kun_Authorization_PermissionRequest_Equals_Kun_Authorization_PermissionRequest\_}

```csharp
public bool Equals(PermissionRequest? other)
```

#### Parameters

`other` [PermissionRequest](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissionrequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Authorization_PermissionRequest_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Authorization_PermissionRequest_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(PermissionRequest?, PermissionRequest?) \{#Kun_Authorization_PermissionRequest_op_Equality_Kun_Authorization_PermissionRequest_Kun_Authorization_PermissionRequest\_}

```csharp
public static bool operator ==(PermissionRequest? left, PermissionRequest? right)
```

#### Parameters

`left` [PermissionRequest](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissionrequest.md)?

`right` [PermissionRequest](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissionrequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(PermissionRequest?, PermissionRequest?) \{#Kun_Authorization_PermissionRequest_op_Inequality_Kun_Authorization_PermissionRequest_Kun_Authorization_PermissionRequest\_}

```csharp
public static bool operator !=(PermissionRequest? left, PermissionRequest? right)
```

#### Parameters

`left` [PermissionRequest](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissionrequest.md)?

`right` [PermissionRequest](/pr-preview/pr-122/sdk/api/kun/kun-authorization/permissionrequest.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
