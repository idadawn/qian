> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class TracingBehavior\<TRequest, TResponse>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Mediator)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator.Behaviors](/sdk/api/kun/kun-mediator-behaviors/index.md)\
Assembly: Kun.Mediator.dll

Pipeline behavior that starts an [`Activity`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity) for each dispatched request
using the `"Kun.Mediator"` [`ActivitySource`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource). Compatible with
OpenTelemetry — attach a listener via `AddSource("Kun.Mediator")` in the
OpenTelemetry builder.

```csharp
public sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
```

#### Type Parameters

`TRequest`

The request type.

`TResponse`

The response type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[TracingBehavior\<TRequest, TResponse>](/sdk/api/kun/kun-mediator-behaviors/tracingbehavior-2.md)

#### Implements

[IPipelineBehavior\<TRequest, TResponse>](/sdk/api/kun/kun-mediator/ipipelinebehavior-2.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### TracingBehavior() \{#Kun_Mediator_Behaviors_TracingBehavior_2__ctor}

```csharp
public TracingBehavior()
```

## Fields

### ActivitySourceName \{#Kun_Mediator_Behaviors_TracingBehavior_2_ActivitySourceName}

The [`ActivitySource`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource) name used by all Kun.Mediator activities.

```csharp
public const string ActivitySourceName = "Kun.Mediator"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### Handle(TRequest, RequestHandlerDelegate\<TResponse>, CancellationToken) \{#Kun_Mediator_Behaviors_TracingBehavior_2_Handle__0_Kun_Mediator_RequestHandlerDelegate__1__System_Threading_CancellationToken\_}

Handles the request by optionally performing work before and/or after invoking
`next`.

```csharp
public ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
```

#### Parameters

`request` TRequest

The incoming request.

`next` [RequestHandlerDelegate](/sdk/api/kun/kun-mediator/requesthandlerdelegate-1.md)\<TResponse>

The delegate representing the next step (inner behavior or handler).

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

Cancellation token.

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TResponse>

The response from the inner pipeline.
