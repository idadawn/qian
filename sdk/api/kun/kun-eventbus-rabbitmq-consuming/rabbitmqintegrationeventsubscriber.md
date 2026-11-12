> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class RabbitMqIntegrationEventSubscriber

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus.RabbitMQ` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus.RabbitMQ`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EventBus.RabbitMQ)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.RabbitMQ.Consuming](/sdk/api/kun/kun-eventbus-rabbitmq-consuming/index.md)\
Assembly: Kun.EventBus.RabbitMQ.dll

Background service that subscribes to RabbitMQ topic queues and dispatches received messages
to registered [IIntegrationEventHandler\<TEvent>](/sdk/api/kun/kun-eventbus/iintegrationeventhandler-1.md) implementations.
Idempotency is enforced via [IProcessedMessageStore](/sdk/api/kun/kun-eventbus-idempotency/iprocessedmessagestore.md).
W3C Trace Context is restored from AMQP headers before handler dispatch.

```csharp
public sealed class RabbitMqIntegrationEventSubscriber : BackgroundService, IHostedService, IDisposable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[BackgroundService](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.backgroundservice) ←
[RabbitMqIntegrationEventSubscriber](/sdk/api/kun/kun-eventbus-rabbitmq-consuming/rabbitmqintegrationeventsubscriber.md)

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

## Methods

### ExecuteAsync(CancellationToken) \{#Kun_EventBus_RabbitMQ_Consuming_RabbitMqIntegrationEventSubscriber_ExecuteAsync_System_Threading_CancellationToken\_}

```csharp
protected override Task ExecuteAsync(CancellationToken stoppingToken)
```

#### Parameters

`stoppingToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)
