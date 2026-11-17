> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Interface IMultiTenant

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/pr-preview/pr-202/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

标记实体支持多租户隔离。由 `KunDbContext` 自动附加 `TenantId` 全局过滤器。

```csharp
public interface IMultiTenant
```

## Properties

### TenantId \{#Kun_Core_Domain_IMultiTenant_TenantId}

Tenant identifier for the current entity; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) in single-tenant hosts.

```csharp
string? TenantId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
