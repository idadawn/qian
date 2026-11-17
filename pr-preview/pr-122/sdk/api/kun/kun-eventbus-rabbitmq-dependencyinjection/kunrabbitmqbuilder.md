> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class KunRabbitMqBuilder

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus.RabbitMQ` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus.RabbitMQ`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus.RabbitMQ)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.RabbitMQ.DependencyInjection](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq-dependencyinjection/index.md)\
Assembly: Kun.EventBus.RabbitMQ.dll

Fluent builder for configuring RabbitMQ subscriptions and handlers.

```csharp
public sealed class KunRabbitMqBuilder
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunRabbitMqBuilder](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq-dependencyinjection/kunrabbitmqbuilder.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### Subscribe\<TEvent, THandler>(string, JsonTypeInfo\<TEvent>) \{#Kun_EventBus_RabbitMQ_DependencyInjection_KunRabbitMqBuilder_Subscribe__2_System_String_System_Text_Json_Serialization_Metadata_JsonTypeInfo___0\_\_}

Registers `THandler` as the handler for `TEvent`
and records the subscription so the background subscriber declares the queue at startup.

```csharp
public KunRabbitMqBuilder Subscribe<TEvent, THandler>(string eventTypeId, JsonTypeInfo<TEvent> typeInfo) where TEvent : class where THandler : class, IIntegrationEventHandler<TEvent>
```

#### Parameters

`eventTypeId` [string](https://learn.microsoft.com/dotnet/api/system.string)

Stable event type ID, e.g. `order.placed.v1`.

`typeInfo` [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)\<TEvent>

Source-generated [`JsonTypeInfo`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1) for AOT-safe deserialization.
Obtain from your `JsonSerializerContext`, e.g. `MyJsonContext.Default.OrderPlacedIntegrationEvent`.

#### Returns

[KunRabbitMqBuilder](/pr-preview/pr-122/sdk/api/kun/kun-eventbus-rabbitmq-dependencyinjection/kunrabbitmqbuilder.md)

#### Type Parameters

`TEvent`

The integration event type.

`THandler`

The handler implementation.
