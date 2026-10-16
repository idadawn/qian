> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class RabbitMqOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus.RabbitMQ` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus.RabbitMQ`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.EventBus.RabbitMQ)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.RabbitMQ.Options](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-rabbitmq-options/index.md)\
Assembly: Kun.EventBus.RabbitMQ.dll

Configures the RabbitMQ transport for Kun event bus.

```csharp
public sealed class RabbitMqOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[RabbitMqOptions](/pr-preview/pr-181/sdk/api/kun/kun-eventbus-rabbitmq-options/rabbitmqoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### RabbitMqOptions() \{#Kun_EventBus_RabbitMQ_Options_RabbitMqOptions__ctor}

```csharp
public RabbitMqOptions()
```

## Properties

### ConnectionString \{#Kun_EventBus_RabbitMQ_Options_RabbitMqOptions_ConnectionString}

AMQP URI, e.g. `amqp://guest:guest@localhost:5672/`.

```csharp
public required string ConnectionString { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ConsumerName \{#Kun_EventBus_RabbitMQ_Options_RabbitMqOptions_ConsumerName}

Logical consumer name (used as queue name prefix and idempotency scope).

```csharp
public required string ConsumerName { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ExchangeName \{#Kun_EventBus_RabbitMQ_Options_RabbitMqOptions_ExchangeName}

Topic exchange that publishers write to and consumers bind queues to.

```csharp
public string ExchangeName { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### IdempotencyTtl \{#Kun_EventBus_RabbitMQ_Options_RabbitMqOptions_IdempotencyTtl}

Idempotency key TTL. Default 30 minutes.

```csharp
public TimeSpan IdempotencyTtl { get; init; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

### MaxDeliveryAttempts \{#Kun_EventBus_RabbitMQ_Options_RabbitMqOptions_MaxDeliveryAttempts}

Max redeliveries before a message is routed to the dead-letter exchange. Default 5.

```csharp
public int MaxDeliveryAttempts { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### PrefetchCount \{#Kun_EventBus_RabbitMQ_Options_RabbitMqOptions_PrefetchCount}

Prefetch count per channel.

```csharp
public ushort PrefetchCount { get; init; }
```

#### Property Value

[ushort](https://learn.microsoft.com/dotnet/api/system.uint16)

### RetryBaseDelay \{#Kun_EventBus_RabbitMQ_Options_RabbitMqOptions_RetryBaseDelay}

失败投递在 broker 上持久化延迟重试的起始间隔，按指数增长，封顶一分钟。

```csharp
public TimeSpan RetryBaseDelay { get; init; }
```

#### Property Value

[TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)
