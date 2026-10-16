> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class NullTenantContext

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.MultiTenancy](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/index.md)\
Assembly: Kun.Core.dll

[ITenantContext](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/itenantcontext.md) implementation for non-multi-tenant hosts or background jobs
that operate without a tenant scope.

```csharp
public sealed class NullTenantContext : ITenantContext
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[NullTenantContext](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/nulltenantcontext.md)

#### Implements

[ITenantContext](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/itenantcontext.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Properties

### Instance \{#Kun_Core_MultiTenancy_NullTenantContext_Instance}

Singleton instance — [NullTenantContext](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/nulltenantcontext.md) has no state.

```csharp
public static NullTenantContext Instance { get; }
```

#### Property Value

[NullTenantContext](/pr-preview/pr-181/sdk/api/kun/kun-core-multitenancy/nulltenantcontext.md)

### IsMultiTenant \{#Kun_Core_MultiTenancy_NullTenantContext_IsMultiTenant}

当前应用是否启用了多租户。

```csharp
public bool IsMultiTenant { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### TenantId \{#Kun_Core_MultiTenancy_NullTenantContext_TenantId}

当前请求的租户 ID；后台任务中可能为 [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)。

```csharp
public string? TenantId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
