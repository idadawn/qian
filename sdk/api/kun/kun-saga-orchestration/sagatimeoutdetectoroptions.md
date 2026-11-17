> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class SagaTimeoutDetectorOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Saga)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Orchestration](/sdk/api/kun/kun-saga-orchestration/index.md)\
Assembly: Kun.Saga.dll

Configuration options for [SagaTimeoutDetector](/sdk/api/kun/kun-saga-orchestration/sagatimeoutdetector.md).

```csharp
public sealed class SagaTimeoutDetectorOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SagaTimeoutDetectorOptions](/sdk/api/kun/kun-saga-orchestration/sagatimeoutdetectoroptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SagaTimeoutDetectorOptions() \{#Kun_Saga_Orchestration_SagaTimeoutDetectorOptions__ctor}

```csharp
public SagaTimeoutDetectorOptions()
```

## Properties

### BatchSize \{#Kun_Saga_Orchestration_SagaTimeoutDetectorOptions_BatchSize}

Gets or sets the maximum number of timed-out instances per scan batch. Defaults to 20.

```csharp
public int BatchSize { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ScanInterval \{#Kun_Saga_Orchestration_SagaTimeoutDetectorOptions_ScanInterval}

Gets or sets how often the detector scans for timed-out sagas. Defaults to 5 seconds.

```csharp
public TimeSpan ScanInterval { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)
