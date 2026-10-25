> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class LoggingBehavior\<TRequest, TResponse>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Mediator)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator.Behaviors](/pr-preview/pr-122/sdk/api/kun/kun-mediator-behaviors/index.md)\
Assembly: Kun.Mediator.dll

Pipeline behavior that logs the request name on entry and the elapsed time on exit.
Logs at [`Debug`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel.debug) for normal completion and
[`Warning`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel.warning) when the handler throws.

```csharp
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
```

#### Type Parameters

`TRequest`

The request type.

`TResponse`

The response type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[LoggingBehavior\<TRequest, TResponse>](/pr-preview/pr-122/sdk/api/kun/kun-mediator-behaviors/loggingbehavior-2.md)

#### Implements

[IPipelineBehavior\<TRequest, TResponse>](/pr-preview/pr-122/sdk/api/kun/kun-mediator/ipipelinebehavior-2.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### LoggingBehavior(ILogger\<LoggingBehavior\<TRequest, TResponse>>) \{#Kun_Mediator_Behaviors_LoggingBehavior_2__ctor_Microsoft_Extensions_Logging_ILogger_Kun_Mediator_Behaviors_LoggingBehavior__0__1\_\_\_}

Initialises a new [LoggingBehavior\<TRequest, TResponse>](/pr-preview/pr-122/sdk/api/kun/kun-mediator-behaviors/loggingbehavior-2.md).

```csharp
public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
```

#### Parameters

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[LoggingBehavior](/pr-preview/pr-122/sdk/api/kun/kun-mediator-behaviors/loggingbehavior-2.md)\<TRequest, TResponse>>

The logger instance.

## Methods

### Handle(TRequest, RequestHandlerDelegate\<TResponse>, CancellationToken) \{#Kun_Mediator_Behaviors_LoggingBehavior_2_Handle__0_Kun_Mediator_RequestHandlerDelegate__1__System_Threading_CancellationToken\_}

Handles the request by optionally performing work before and/or after invoking
`next`.

```csharp
public ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
```

#### Parameters

`request` TRequest

The incoming request.

`next` [RequestHandlerDelegate](/pr-preview/pr-122/sdk/api/kun/kun-mediator/requesthandlerdelegate-1.md)\<TResponse>

The delegate representing the next step (inner behavior or handler).

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TResponse>

The response from the inner pipeline.
