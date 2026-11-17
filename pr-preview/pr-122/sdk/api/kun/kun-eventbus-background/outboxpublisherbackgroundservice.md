> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class OutboxPublisherBackgroundService

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.Background](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/index.md)\
Assembly: Kun.EventBus.dll

Hosted background service that polls the outbox store for pending messages and
publishes them via [IIntegrationEventPublisher](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/iintegrationeventpublisher.md).

```csharp
public sealed class OutboxPublisherBackgroundService : BackgroundService, IHostedService, IDisposable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[BackgroundService](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.backgroundservice) ←
[OutboxPublisherBackgroundService](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/outboxpublisherbackgroundservice.md)

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

### OutboxPublisherBackgroundService(IServiceScopeFactory, IIntegrationEventPublisher, IOutboxLockProvider, IOptions\<OutboxPublisherOptions>, ILogger\<OutboxPublisherBackgroundService>, TimeProvider) \{#Kun_EventBus_Background_OutboxPublisherBackgroundService__ctor_Microsoft_Extensions_DependencyInjection_IServiceScopeFactory_Kun_EventBus_IIntegrationEventPublisher_Kun_EventBus_Background_IOutboxLockProvider_Microsoft_Extensions_Options_IOptions_Kun_EventBus_Background_OutboxPublisherOptions__Microsoft_Extensions_Logging_ILogger_Kun_EventBus_Background_OutboxPublisherBackgroundService__System_TimeProvider\_}

Initializes a new [OutboxPublisherBackgroundService](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/outboxpublisherbackgroundservice.md).

```csharp
public OutboxPublisherBackgroundService(IServiceScopeFactory scopeFactory, IIntegrationEventPublisher publisher, IOutboxLockProvider lockProvider, IOptions<OutboxPublisherOptions> options, ILogger<OutboxPublisherBackgroundService> logger, TimeProvider timeProvider)
```

#### Parameters

`scopeFactory` [IServiceScopeFactory](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicescopefactory)

`publisher` [IIntegrationEventPublisher](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/iintegrationeventpublisher.md)

`lockProvider` [IOutboxLockProvider](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/ioutboxlockprovider.md)

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[OutboxPublisherOptions](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/outboxpublisheroptions.md)>

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[OutboxPublisherBackgroundService](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-background/outboxpublisherbackgroundservice.md)>

`timeProvider` [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider)

## Methods

### ExecuteAsync(CancellationToken) \{#Kun_EventBus_Background_OutboxPublisherBackgroundService_ExecuteAsync_System_Threading_CancellationToken\_}

```csharp
protected override Task ExecuteAsync(CancellationToken stoppingToken)
```

#### Parameters

`stoppingToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
