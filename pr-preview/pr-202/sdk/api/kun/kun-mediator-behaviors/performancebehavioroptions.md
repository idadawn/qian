> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class PerformanceBehaviorOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Mediator)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator.Behaviors](/pr-preview/pr-202/sdk/api/kun/kun-mediator-behaviors/index.md)\
Assembly: Kun.Mediator.dll

慢命令告警的可配置阈值。

```csharp
public sealed class PerformanceBehaviorOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PerformanceBehaviorOptions](/pr-preview/pr-202/sdk/api/kun/kun-mediator-behaviors/performancebehavioroptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### PerformanceBehaviorOptions() \{#Kun_Mediator_Behaviors_PerformanceBehaviorOptions__ctor}

```csharp
public PerformanceBehaviorOptions()
```

## Properties

### ThresholdMs \{#Kun_Mediator_Behaviors_PerformanceBehaviorOptions_ThresholdMs}

告警阈值毫秒数，默认 500；0 表示记录任何耗时为正的调用。

```csharp
public long ThresholdMs { get; set; }
```

#### Property Value

[long](https://learn.microsoft.com/dotnet/api/system.int64)
