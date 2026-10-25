> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunMediator

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Mediator)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator](/sdk/api/kun/kun-mediator/index.md)\
Assembly: Kun.Mediator.dll

Reflection-free CQRS dispatcher. Handler lookup is O(1) via a pre-built
[`FrozenDictionary`](https://learn.microsoft.com/dotnet/api/system.collections.frozen.frozendictionary-2). All handler types are explicitly
registered at startup through [KunMediatorBuilder](/sdk/api/kun/kun-mediator/kunmediatorbuilder.md), so no runtime
scanning or generic type construction is needed — this class is fully AOT-safe.

```csharp
public sealed class KunMediator : ISender, IPublisher, IScopedPublisher
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunMediator](/sdk/api/kun/kun-mediator/kunmediator.md)

#### Implements

[ISender](/sdk/api/kun/kun-mediator/isender.md),
[IPublisher](/sdk/api/kun/kun-mediator/ipublisher.md),
[IScopedPublisher](/sdk/api/kun/kun-mediator/iscopedpublisher.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### Publish\<TNotification>(TNotification, CancellationToken) \{#Kun_Mediator_KunMediator_Publish__1___0_System_Threading_CancellationToken\_}

Publishes `notification` to all handlers registered for
`TNotification`. If no handlers are registered this is a no-op.
Handlers are invoked sequentially in registration order.

```csharp
public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
```

#### Parameters

`notification` TNotification

The notification to publish. Must not be [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

#### Type Parameters

`TNotification`

The notification type.

### PublishInScope\<TNotification>(TNotification, IServiceProvider, CancellationToken) \{#Kun_Mediator_KunMediator_PublishInScope__1___0_System_IServiceProvider_System_Threading_CancellationToken\_}

在调用方提供的作用域内顺序派发通知；不创建或释放该作用域。

```csharp
public ValueTask PublishInScope<TNotification>(TNotification notification, IServiceProvider services, CancellationToken cancellationToken = default) where TNotification : INotification
```

#### Parameters

`notification` TNotification

`services` [IServiceProvider](https://learn.microsoft.com/dotnet/api/system.iserviceprovider)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)

#### Type Parameters

`TNotification`

### Send\<TResponse>(IRequest\<TResponse>, CancellationToken) \{#Kun_Mediator_KunMediator_Send__1_Kun_Core_Cqrs_IRequest___0__System_Threading_CancellationToken\_}

Sends `request` to its registered handler, executing any
pipeline behaviors registered for `TResponse` along the way.

```csharp
public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
```

#### Parameters

`request` IRequest\<TResponse>

The request to dispatch. Must not be [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null).

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TResponse>

The response produced by the registered handler.

#### Type Parameters

`TResponse`

The expected response type.

#### Exceptions

[InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

Thrown when no handler is registered for `request`'s concrete type.
