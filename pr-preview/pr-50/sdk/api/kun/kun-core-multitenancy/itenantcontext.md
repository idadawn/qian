> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Interface ITenantContext

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Core)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.MultiTenancy](/pr-preview/pr-50/sdk/api/kun/kun-core-multitenancy/index.md)\
Assembly: Kun.Core.dll

租户上下文，从当前请求的认证信息（S2S JWT 的 tenant_id claim）中解析。

框架内所有需要按租户隔离的组件（EF Core 全局过滤器、缓存 Key、消息路由）
都应依赖此接口而非具体实现，以便在不同宿主（HTTP、gRPC、后台任务）中替换。

```csharp
public interface ITenantContext
```

## Properties

### IsMultiTenant \{#Kun_Core_MultiTenancy_ITenantContext_IsMultiTenant}

当前应用是否启用了多租户。

```csharp
bool IsMultiTenant { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### TenantId \{#Kun_Core_MultiTenancy_ITenantContext_TenantId}

当前请求的租户 ID；后台任务中可能为 [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)。

```csharp
string? TenantId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

## Methods

### BeginScope(string?) \{#Kun_Core_MultiTenancy_ITenantContext_BeginScope_System_String\_}

打开租户作用域；释放后恢复原上下文。旧实现须显式支持后台回灌。

```csharp
IDisposable BeginScope(string? tenantId)
```

#### Parameters

`tenantId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

#### Returns

[IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)
