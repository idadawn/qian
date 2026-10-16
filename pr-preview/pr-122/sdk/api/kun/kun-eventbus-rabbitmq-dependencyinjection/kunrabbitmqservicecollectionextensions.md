> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunRabbitMqServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus.RabbitMQ` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus.RabbitMQ`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus.RabbitMQ)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.RabbitMQ.DependencyInjection](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq-dependencyinjection/index.md)\
Assembly: Kun.EventBus.RabbitMQ.dll

Extension methods for registering the RabbitMQ broker adapter.

```csharp
public static class KunRabbitMqServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunRabbitMqServiceCollectionExtensions](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq-dependencyinjection/kunrabbitmqservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddRabbitMq(IServiceCollection, Action\<RabbitMqOptions>, Action\<KunRabbitMqBuilder>?) \{#Kun_EventBus_RabbitMQ_DependencyInjection_KunRabbitMqServiceCollectionExtensions_AddRabbitMq_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_EventBus_RabbitMQ_Options_RabbitMqOptions__System_Action_Kun_EventBus_RabbitMQ_DependencyInjection_KunRabbitMqBuilder\_\_}

Registers the RabbitMQ transport for [IIntegrationEventPublisher](/pr-preview/pr-122/sdk/api/kun/kun-eventbus/iintegrationeventpublisher.md) and
starts the consumer background service for any subscriptions configured via `configure`.

```csharp
public static IServiceCollection AddRabbitMq(this IServiceCollection services, Action<RabbitMqOptions> configureOptions, Action<KunRabbitMqBuilder>? configure = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The service collection.

`configureOptions` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[RabbitMqOptions](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq-options/rabbitmqoptions.md)>

Configures [RabbitMqOptions](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq-options/rabbitmqoptions.md).

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunRabbitMqBuilder](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq-dependencyinjection/kunrabbitmqbuilder.md)>?

Optional builder for registering event subscriptions.

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)
