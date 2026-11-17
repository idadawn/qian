> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class ReadDbOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Dapper` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Dapper`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Dapper)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Dapper](/pr-preview/pr-122/sdk/api/kun/kun-dapper/index.md)\
Assembly: Kun.Dapper.dll

Configuration for the Dapper read-side connection factory.

```csharp
public sealed class ReadDbOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ReadDbOptions](/pr-preview/pr-122/sdk/api/kun/kun-dapper/readdboptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ReadDbOptions() \{#Kun_Dapper_ReadDbOptions__ctor}

```csharp
public ReadDbOptions()
```

## Properties

### ConnectionString \{#Kun_Dapper_ReadDbOptions_ConnectionString}

ADO.NET connection string for the read replica.

```csharp
public required string ConnectionString { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)
