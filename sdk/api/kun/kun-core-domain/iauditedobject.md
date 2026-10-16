> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IAuditedObject

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

通用审计接口，记录创建/修改时间。

```csharp
public interface IAuditedObject
```

## Properties

### CreatedAt \{#Kun_Core_Domain_IAuditedObject_CreatedAt}

UTC timestamp when the entity was created.

```csharp
DateTime CreatedAt { get; }
```

#### Property Value

[DateTime](https://learn.microsoft.com/dotnet/api/system.datetime)

### UpdatedAt \{#Kun_Core_Domain_IAuditedObject_UpdatedAt}

UTC timestamp of the last update; [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) if never updated.

```csharp
DateTime? UpdatedAt { get; }
```

#### Property Value

[DateTime](https://learn.microsoft.com/dotnet/api/system.datetime)?
