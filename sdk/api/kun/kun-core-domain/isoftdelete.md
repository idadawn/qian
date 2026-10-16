> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface ISoftDelete

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

标记实体支持软删除。由基础设施层在持久化时挂载全局查询过滤器。

```csharp
public interface ISoftDelete
```

## Properties

### DeletedAt \{#Kun_Core_Domain_ISoftDelete_DeletedAt}

UTC timestamp when the entity was soft-deleted; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) if not deleted.

```csharp
DateTime? DeletedAt { get; }
```

#### Property Value

[DateTime](https://learn.microsoft.com/dotnet/api/system.datetime)?

### IsDeleted \{#Kun_Core_Domain_ISoftDelete_IsDeleted}

[true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) when the entity has been soft-deleted.

```csharp
bool IsDeleted { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
