> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunDataConventions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EntityFrameworkCore` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EntityFrameworkCore`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EntityFrameworkCore)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EntityFrameworkCore.Conventions](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore-conventions/index.md)\
Assembly: Kun.EntityFrameworkCore.dll

数据层的列命名、Guid v7 主键与 PostgreSQL xmin 并发约定。

```csharp
public static class KunDataConventions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunDataConventions](/pr-preview/pr-202/sdk/api/kun/kun-entityframeworkcore-conventions/kundataconventions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Fields

### RowVersionPropertyName \{#Kun_EntityFrameworkCore_Conventions_KunDataConventions_RowVersionPropertyName}

EF shadow property 名称，映射到 PostgreSQL 系统列 xmin。

```csharp
public const string RowVersionPropertyName = "KunRowVersion"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### ApplyKunDataConventions(ModelBuilder) \{#Kun_EntityFrameworkCore_Conventions_KunDataConventions_ApplyKunDataConventions_Microsoft_EntityFrameworkCore_ModelBuilder\_}

在实体映射完成后应用约定；已有非空主键保持不变。

```csharp
public static ModelBuilder ApplyKunDataConventions(this ModelBuilder modelBuilder)
```

#### Parameters

`modelBuilder` [ModelBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder)

#### Returns

[ModelBuilder](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.modelbuilder)
