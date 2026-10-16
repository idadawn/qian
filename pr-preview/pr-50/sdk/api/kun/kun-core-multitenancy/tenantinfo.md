> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class TenantInfo

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Core)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.MultiTenancy](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/index.md)\
Assembly: Kun.Core.dll

不可变的租户身份元信息，作为 [ITenantProvisioner](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/itenantprovisioner.md) 等生命周期 API 的入参。

```csharp
public sealed record TenantInfo : IEquatable<TenantInfo>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[TenantInfo](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/tenantinfo.md)

#### Implements

[IEquatable\<TenantInfo>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### TenantInfo(string, string?) \{#Kun_Core_MultiTenancy_TenantInfo__ctor_System_String_System_String\_}

创建一个新的 [TenantInfo](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/tenantinfo.md)。

```csharp
public TenantInfo(string tenantId, string? displayName = null)
```

#### Parameters

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)

租户 ID。必须非 null、非空、非全空白字符。

`displayName` [string](https://learn.microsoft.com/dotnet/api/system.string)?

可选的展示名；默认 [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)。

#### Exceptions

[ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

当 `tenantId` 为 null / 空字符串 / 仅空白字符时抛出。

## Properties

### DisplayName \{#Kun_Core_MultiTenancy_TenantInfo_DisplayName}

可选的展示名（UI / 日志 / 审计用途）。null 表示消费者未提供。

```csharp
public string? DisplayName { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TenantId \{#Kun_Core_MultiTenancy_TenantInfo_TenantId}

租户 ID。

```csharp
public string TenantId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### \<Clone>$() \{#Kun_Core_MultiTenancy_TenantInfo__Clone\_\_}

```csharp
public TenantInfo <Clone>$()
```

#### Returns

[TenantInfo](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/tenantinfo.md)

### Equals(object?) \{#Kun_Core_MultiTenancy_TenantInfo_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(TenantInfo?) \{#Kun_Core_MultiTenancy_TenantInfo_Equals_Kun_Core_MultiTenancy_TenantInfo\_}

```csharp
public bool Equals(TenantInfo? other)
```

#### Parameters

`other` [TenantInfo](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/tenantinfo.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Core_MultiTenancy_TenantInfo_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Core_MultiTenancy_TenantInfo_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(TenantInfo?, TenantInfo?) \{#Kun_Core_MultiTenancy_TenantInfo_op_Equality_Kun_Core_MultiTenancy_TenantInfo_Kun_Core_MultiTenancy_TenantInfo\_}

```csharp
public static bool operator ==(TenantInfo? left, TenantInfo? right)
```

#### Parameters

`left` [TenantInfo](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/tenantinfo.md)?

`right` [TenantInfo](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/tenantinfo.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(TenantInfo?, TenantInfo?) \{#Kun_Core_MultiTenancy_TenantInfo_op_Inequality_Kun_Core_MultiTenancy_TenantInfo_Kun_Core_MultiTenancy_TenantInfo\_}

```csharp
public static bool operator !=(TenantInfo? left, TenantInfo? right)
```

#### Parameters

`left` [TenantInfo](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/tenantinfo.md)?

`right` [TenantInfo](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/tenantinfo.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
