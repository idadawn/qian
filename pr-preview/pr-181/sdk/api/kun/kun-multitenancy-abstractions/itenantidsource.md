> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Interface ITenantIdSource

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.MultiTenancy` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.MultiTenancy`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.MultiTenancy)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.MultiTenancy.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-multitenancy-abstractions/index.md)\
Assembly: Kun.MultiTenancy.dll

为尚未打开显式作用域的上下文提供已认证的租户标识。

```csharp
public interface ITenantIdSource
```

## Properties

### TenantId \{#Kun_MultiTenancy_Abstractions_ITenantIdSource_TenantId}

已认证且通过白名单校验的租户标识；未认证时为空。

```csharp
string? TenantId { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
