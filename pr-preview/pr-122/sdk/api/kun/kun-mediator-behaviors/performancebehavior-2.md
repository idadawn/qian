> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class PerformanceBehavior\<TRequest, TResponse>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Mediator)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator.Behaviors](/pr-preview/pr-122/sdk/api/kun/kun-mediator-behaviors/index.md)\
Assembly: Kun.Mediator.dll

Pipeline behavior that logs a warning when the handler pipeline exceeds a duration
threshold. The threshold is currently hardcoded to 500 ms; a configurable version
backed by `IOptions<PerformanceBehaviorOptions>` is a planned follow-up.

```csharp
public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
```

#### Type Parameters

`TRequest`

The request type.

`TResponse`

The response type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[PerformanceBehavior\<TRequest, TResponse>](/pr-preview/pr-122/sdk/api/kun/kun-mediator-behaviors/performancebehavior-2.md)

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

### PerformanceBehavior(ILogger\<PerformanceBehavior\<TRequest, TResponse>>) \{#Kun_Mediator_Behaviors_PerformanceBehavior_2__ctor_Microsoft_Extensions_Logging_ILogger_Kun_Mediator_Behaviors_PerformanceBehavior__0__1\_\_\_}

Initialises a new [PerformanceBehavior\<TRequest, TResponse>](/pr-preview/pr-122/sdk/api/kun/kun-mediator-behaviors/performancebehavior-2.md).

```csharp
public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
```

#### Parameters

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[PerformanceBehavior](/pr-preview/pr-122/sdk/api/kun/kun-mediator-behaviors/performancebehavior-2.md)\<TRequest, TResponse>>

The logger instance.

### PerformanceBehavior(ILogger\<PerformanceBehavior\<TRequest, TResponse>>, IOptions\<PerformanceBehaviorOptions>) \{#Kun_Mediator_Behaviors_PerformanceBehavior_2__ctor_Microsoft_Extensions_Logging_ILogger_Kun_Mediator_Behaviors_PerformanceBehavior__0__1___Microsoft_Extensions_Options_IOptions_Kun_Mediator_Behaviors_PerformanceBehaviorOptions\_\_}

使用宿主的慢请求阈值，保留单 logger 构造器的默认行为。

```csharp
public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger, IOptions<PerformanceBehaviorOptions> options)
```

#### Parameters

`logger` [ILogger](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger-1)\<[PerformanceBehavior](/pr-preview/pr-122/sdk/api/kun/kun-mediator-behaviors/performancebehavior-2.md)\<TRequest, TResponse>>

`options` [IOptions](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptions-1)\<[PerformanceBehaviorOptions](/pr-preview/pr-122/sdk/api/kun/kun-mediator-behaviors/performancebehavioroptions.md)>

## Fields

### DefaultThresholdMs \{#Kun_Mediator_Behaviors_PerformanceBehavior_2_DefaultThresholdMs}

Default threshold in milliseconds. Requests that take longer than this will be
logged as warnings. A configurable alternative is a planned follow-up.

```csharp
public const long DefaultThresholdMs = 500
```

#### Field Value

[long](https://learn.microsoft.com/dotnet/api/system.int64)

## Methods

### Handle(TRequest, RequestHandlerDelegate\<TResponse>, CancellationToken) \{#Kun_Mediator_Behaviors_PerformanceBehavior_2_Handle__0_Kun_Mediator_RequestHandlerDelegate__1__System_Threading_CancellationToken\_}

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
