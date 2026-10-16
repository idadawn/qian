> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class SagaStepExecutorOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Orchestration](/pr-preview/pr-181/sdk/api/kun/kun-saga-orchestration/index.md)\
Assembly: Kun.Saga.dll

Configuration options for [SagaStepExecutor](/pr-preview/pr-181/sdk/api/kun/kun-saga-orchestration/sagastepexecutor.md).

```csharp
public sealed class SagaStepExecutorOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SagaStepExecutorOptions](/pr-preview/pr-181/sdk/api/kun/kun-saga-orchestration/sagastepexecutoroptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SagaStepExecutorOptions() \{#Kun_Saga_Orchestration_SagaStepExecutorOptions__ctor}

```csharp
public SagaStepExecutorOptions()
```

## Properties

### BatchSize \{#Kun_Saga_Orchestration_SagaStepExecutorOptions_BatchSize}

Gets or sets the maximum number of step entries processed per poll cycle. Defaults to 10.

```csharp
public int BatchSize { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ErrorRetryDelay \{#Kun_Saga_Orchestration_SagaStepExecutorOptions_ErrorRetryDelay}

Gets or sets the delay before retrying after an unhandled error. Defaults to 5 seconds.

```csharp
public TimeSpan ErrorRetryDelay { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### MaxAttempts \{#Kun_Saga_Orchestration_SagaStepExecutorOptions_MaxAttempts}

Maximum attempts per execute/compensate entry. Defaults to one for compatibility; retries require idempotent steps.

```csharp
public int MaxAttempts { get; set; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### PollInterval \{#Kun_Saga_Orchestration_SagaStepExecutorOptions_PollInterval}

Gets or sets the delay between polls when the step queue is empty. Defaults to 2 seconds.

```csharp
public TimeSpan PollInterval { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### RetryBaseDelay \{#Kun_Saga_Orchestration_SagaStepExecutorOptions_RetryBaseDelay}

Initial persisted exponential retry delay, capped at one minute. Defaults to one second.

```csharp
public TimeSpan RetryBaseDelay { get; set; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)
