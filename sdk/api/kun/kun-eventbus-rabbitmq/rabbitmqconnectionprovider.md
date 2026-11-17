> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class RabbitMqConnectionProvider

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus.RabbitMQ` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus.RabbitMQ`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EventBus.RabbitMQ)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.EventBus.RabbitMQ](/sdk/api/kun/kun-eventbus-rabbitmq/index.md)\
Assembly: Kun.EventBus.RabbitMQ.dll

Singleton that owns and lazily opens the `IConnection` to RabbitMQ.
Automatic recovery is enabled via `AutomaticRecoveryEnabled`.

```csharp
public sealed class RabbitMqConnectionProvider : IAsyncDisposable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[RabbitMqConnectionProvider](/sdk/api/kun/kun-eventbus-rabbitmq/rabbitmqconnectionprovider.md)

#### Implements

[IAsyncDisposable](https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### RabbitMqConnectionProvider(IOptions\<RabbitMqOptions>, ILogger\<RabbitMqConnectionProvider>) \{#Kun_EventBus_RabbitMQ_RabbitMqConnectionProvider__ctor_Microsoft_Extensions_Options_IOptions_Kun_EventBus_RabbitMQ_Options_RabbitMqOptions__Microsoft_Extensions_Logging_ILogger_Kun_EventBus_RabbitMQ_RabbitMqConnectionProvider\_\_}

Initializes the provider with the supplied options.

```csharp
public RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnectionProvider> logger)
```

#### Parameters

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[RabbitMqOptions](/sdk/api/kun/kun-eventbus-rabbitmq-options/rabbitmqoptions.md)>

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[RabbitMqConnectionProvider](/sdk/api/kun/kun-eventbus-rabbitmq/rabbitmqconnectionprovider.md)>

## Methods

### DisposeAsync() \{#Kun_EventBus_RabbitMQ_RabbitMqConnectionProvider_DisposeAsync}

```csharp
public ValueTask DisposeAsync()
```

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

### GetConnectionAsync(CancellationToken) \{#Kun_EventBus_RabbitMQ_RabbitMqConnectionProvider_GetConnectionAsync_System_Threading_CancellationToken\_}

Returns the open connection, lazily creating it on first call.

```csharp
public ValueTask<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
```

#### Parameters

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<IConnection>
