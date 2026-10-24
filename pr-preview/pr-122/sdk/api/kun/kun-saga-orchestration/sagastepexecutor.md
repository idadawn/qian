> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class SagaStepExecutor

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Orchestration](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/index.md)\
Assembly: Kun.Saga.dll

Background service that polls [ISagaInstanceStore.FetchPendingStepsAsync](/pr-preview/pr-122/sdk/api/kun/kun-saga-persistence/isagainstancestore.md#Kun_Saga_Persistence_ISagaInstanceStore_FetchPendingStepsAsync_System_Int32_System_Threading_CancellationToken_) and
drives saga step execution and compensation. Creates an [`ActivitySource`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource) span
for each step to enable distributed tracing.

```csharp
public sealed class SagaStepExecutor : BackgroundService, IHostedService, IDisposable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[BackgroundService](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.backgroundservice) ←
[SagaStepExecutor](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/sagastepexecutor.md)

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

### SagaStepExecutor(IServiceScopeFactory, ILogger\<SagaStepExecutor>, SagaStepExecutorOptions) \{#Kun_Saga_Orchestration_SagaStepExecutor__ctor_Microsoft_Extensions_DependencyInjection_IServiceScopeFactory_Microsoft_Extensions_Logging_ILogger_Kun_Saga_Orchestration_SagaStepExecutor__Kun_Saga_Orchestration_SagaStepExecutorOptions\_}

Creates a new [SagaStepExecutor](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/sagastepexecutor.md).

```csharp
public SagaStepExecutor(IServiceScopeFactory scopeFactory, ILogger<SagaStepExecutor> logger, SagaStepExecutorOptions options)
```

#### Parameters

`scopeFactory` [IServiceScopeFactory](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicescopefactory)

Factory for creating DI scopes per poll cycle.

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[SagaStepExecutor](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/sagastepexecutor.md)>

Logger for step execution events.

`options` [SagaStepExecutorOptions](/pr-preview/pr-122/sdk/api/kun/kun-saga-orchestration/sagastepexecutoroptions.md)

Executor configuration options.

## Methods

### ExecuteAsync(CancellationToken) \{#Kun_Saga_Orchestration_SagaStepExecutor_ExecuteAsync_System_Threading_CancellationToken\_}

```csharp
protected override Task ExecuteAsync(CancellationToken stoppingToken)
```

#### Parameters

`stoppingToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
