> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class TenantIdValidation

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.MultiTenancy](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/index.md)\
Assembly: Kun.Core.dll

HTTP、消息与后台任务共用的租户标识白名单。

```csharp
public static class TenantIdValidation
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[TenantIdValidation](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/tenantidvalidation.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### IsValid(string?) \{#Kun_Core_MultiTenancy_TenantIdValidation_IsValid_System_String\_}

只接受 1–128 个 ASCII 字母、数字、点、下划线或连字符。

```csharp
public static bool IsValid(string? tenantId)
```

#### Parameters

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
