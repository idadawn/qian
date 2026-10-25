> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class SagaTimeoutDetector

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Orchestration](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/index.md)\
Assembly: Kun.Saga.dll

Background service that periodically scans for timed-out saga instances and triggers
compensation. An instance is considered timed-out when its `TimeoutAt` is in the past
and its state is [SagaState.Running](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence/sagastate.md#Kun_Saga_Persistence_SagaState_Running).

```csharp
public sealed class SagaTimeoutDetector : BackgroundService, IHostedService, IDisposable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[BackgroundService](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.backgroundservice) ←
[SagaTimeoutDetector](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/sagatimeoutdetector.md)

#### Implements

[IHostedService](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostedservice),
[IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

#### Inherited Members

[BackgroundService.StartAsync(CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.backgroundservice.startasync),
[BackgroundService.StopAsync(CancellationToken)](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.backgroundservice.stopasync),
[BackgroundService.Dispose()](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.backgroundservice.dispose),
[BackgroundService.ExecuteTask](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.backgroundservice.executetask),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SagaTimeoutDetector(IServiceScopeFactory, ILogger\<SagaTimeoutDetector>, SagaTimeoutDetectorOptions) \{#Kun_Saga_Orchestration_SagaTimeoutDetector__ctor_Microsoft_Extensions_DependencyInjection_IServiceScopeFactory_Microsoft_Extensions_Logging_ILogger_Kun_Saga_Orchestration_SagaTimeoutDetector__Kun_Saga_Orchestration_SagaTimeoutDetectorOptions\_}

Creates a new [SagaTimeoutDetector](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/sagatimeoutdetector.md).

```csharp
public SagaTimeoutDetector(IServiceScopeFactory scopeFactory, ILogger<SagaTimeoutDetector> logger, SagaTimeoutDetectorOptions options)
```

#### Parameters

`scopeFactory` [IServiceScopeFactory](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicescopefactory)

Factory for creating DI scopes per scan cycle.

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[SagaTimeoutDetector](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/sagatimeoutdetector.md)>

Logger for timeout events.

`options` [SagaTimeoutDetectorOptions](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/sagatimeoutdetectoroptions.md)

Detector configuration options.

## Methods

### ExecuteAsync(CancellationToken) \{#Kun_Saga_Orchestration_SagaTimeoutDetector_ExecuteAsync_System_Threading_CancellationToken\_}

```csharp
protected override Task ExecuteAsync(CancellationToken stoppingToken)
```

#### Parameters

`stoppingToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
