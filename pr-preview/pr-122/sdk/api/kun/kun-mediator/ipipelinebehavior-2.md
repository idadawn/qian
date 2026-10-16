> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Interface IPipelineBehavior\<TRequest, TResponse>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Mediator)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Mediator](/pr-preview/pr-122/sdk/api/kun/kun-mediator/index.md)\
Assembly: Kun.Mediator.dll

Pipeline behavior wrapping a [IRequestHandler\<TRequest, TResponse>](/pr-preview/pr-122/sdk/api/kun/kun-core-cqrs/irequesthandler-2.md).
Behaviors form a nested "onion": outer behaviors see both the pre- and post-handler phases
of all inner behaviors.

```csharp
public interface IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
```

#### Type Parameters

`TRequest`

The request type handled by this behavior.

`TResponse`

The response type produced by this behavior.

## Methods

### Handle(TRequest, RequestHandlerDelegate\<TResponse>, CancellationToken) \{#Kun_Mediator_IPipelineBehavior_2_Handle__0_Kun_Mediator_RequestHandlerDelegate__1__System_Threading_CancellationToken\_}

Handles the request by optionally performing work before and/or after invoking
`next`.

```csharp
ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
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
